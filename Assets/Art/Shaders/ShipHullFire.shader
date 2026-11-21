Shader "Starfall/ShipHullFire"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Burning Effect)]
        _BurnAmount ("Burn Progression", Range(0.0, 1.0)) = 0.0
        [HDR] _BurnColor ("Burn Outer Flame Color", Color) = (1.0, 0.35, 0.05, 1.0)
        [HDR] _BurnCoreColor ("Burn Hot Core Color", Color) = (1.0, 0.95, 0.6, 1.0)
        _FlameIntensity ("Flame Glow Intensity", Range(1.0, 4.0)) = 2.2

        [Header(Fire Dynamics)]
        _NoiseScale ("Fire Noise Scale", Vector) = (12.0, 16.0, 0.0, 0.0)
        _BurnSpeed ("Flame Animation Speed", Float) = 3.5
        _EdgeWidth ("Fire Border Width", Range(0.02, 0.35)) = 0.14
        _AuraSpread ("Fire Aura Spread", Range(0.0, 0.4)) = 0.12

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;

            float _BurnAmount;
            fixed4 _BurnColor;
            fixed4 _BurnCoreColor;
            float _FlameIntensity;

            float4 _NoiseScale;
            float _BurnSpeed;
            float _EdgeWidth;
            float _AuraSpread;

            float hash(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                fixed4 baseColor = tex2D(_MainTex, uv) * IN.color;

                // When burn amount is 0, render sprite cleanly without any extra flame calculations
                if (_BurnAmount <= 0.001)
                {
                    return baseColor;
                }

                // Animated procedural fire noise
                float timeVal = _Time.y * _BurnSpeed;
                float2 noiseUv1 = uv * _NoiseScale.xy + float2(0.0, timeVal);
                float2 noiseUv2 = uv * (_NoiseScale.xy * 1.5) - float2(timeVal * 0.7, timeVal * 1.3);

                float n = lerp(noise2D(noiseUv1), noise2D(noiseUv2), 0.5);

                // Sample neighbors for silhouette aura when burn amount is active
                float alphaUp    = tex2D(_MainTex, uv + float2(0, _MainTex_TexelSize.y * 3.0)).a;
                float alphaDown  = tex2D(_MainTex, uv - float2(0, _MainTex_TexelSize.y * 3.0)).a;
                float alphaLeft  = tex2D(_MainTex, uv - float2(_MainTex_TexelSize.x * 3.0, 0)).a;
                float alphaRight = tex2D(_MainTex, uv + float2(_MainTex_TexelSize.x * 3.0, 0)).a;
                float neighborAlphaMax = max(max(alphaUp, alphaDown), max(alphaLeft, alphaRight));

                // Burn threshold across sprite
                // Noise determines which areas catch fire first
                float burnThreshold = _BurnAmount * 1.2 - 0.1;
                float firePattern = n - (1.0 - uv.y) * 0.3; // Flames naturally rise upwards

                float isBurning = smoothstep(burnThreshold - _EdgeWidth, burnThreshold, firePattern);
                float fireEdge = smoothstep(burnThreshold - _EdgeWidth, burnThreshold, firePattern) -
                                 smoothstep(burnThreshold, burnThreshold + _EdgeWidth, firePattern);
                fireEdge = saturate(fireEdge * 2.0);

                // Outer fire aura around ship edges
                float auraMask = saturate((neighborAlphaMax - baseColor.a) * (n * 1.2)) * _BurnAmount;

                // Fire color gradient (core vs edge)
                fixed3 flameCol = lerp(_BurnColor.rgb, _BurnCoreColor.rgb, pow(fireEdge, 1.8)) * _FlameIntensity;

                // Blend ship hull with fire:
                // Charred hull color where burning has consumed the ship
                fixed3 charredColor = baseColor.rgb * fixed3(0.2, 0.15, 0.12);
                fixed3 blendedHull = lerp(baseColor.rgb, charredColor, saturate(burnThreshold * 1.5));

                // Add vibrant flame glow on burning regions
                fixed3 finalRgb = blendedHull + flameCol * (fireEdge * 1.4);

                // If outside sprite but in fire aura, draw pure flame
                finalRgb = lerp(finalRgb, flameCol, auraMask * (1.0 - baseColor.a));
                float finalAlpha = saturate(baseColor.a + auraMask * _BurnAmount);

                return fixed4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
