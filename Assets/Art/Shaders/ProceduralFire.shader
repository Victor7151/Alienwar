Shader "Starfall/ProceduralFire"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture (Optional)", 2D) = "white" {}
        [Header(Colors)]
        [HDR] _FlameColor ("Outer Flame Color", Color) = (1.0, 0.4, 0.1, 1.0)
        [HDR] _CoreColor ("Inner Core Color", Color) = (1.0, 0.95, 0.7, 1.0)
        _Intensity ("Color Boost / HDR Intensity", Range(1.0, 5.0)) = 2.0
        
        [Header(Flame Motion)]
        _ScrollSpeed ("Vertical Flow Speed", Float) = 4.5
        _NoiseScale ("Turbulence Scale", Vector) = (4.0, 8.0, 0.0, 0.0)
        _Distortion ("Distortion Power", Range(0.0, 0.5)) = 0.15
        _FlickerSpeed ("Flicker Speed", Float) = 14.0
        _FlickerAmount ("Flicker Amount", Range(0.0, 0.4)) = 0.12
        
        [Header(Flame Shape)]
        _Taper ("Bottom Taper Power", Range(0.5, 4.0)) = 1.8
        _CoreSize ("Inner Core Size", Range(0.1, 0.9)) = 0.45
        _TipSoftness ("Tip Softness", Range(0.01, 1.0)) = 0.25
        
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
        Blend SrcAlpha One // Additive Glow Blend for hyper-vibrant energy thrusters

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
            
            fixed4 _FlameColor;
            fixed4 _CoreColor;
            float _Intensity;
            
            float _ScrollSpeed;
            float4 _NoiseScale;
            float _Distortion;
            float _FlickerSpeed;
            float _FlickerAmount;
            
            float _Taper;
            float _CoreSize;
            float _TipSoftness;

            // Simple fast procedural 2D pseudo-noise
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // Smoothstep

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
                OUT.color = IN.color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Center coordinates horizontally around 0 (-0.5 to 0.5)
                float centeredX = uv.x - 0.5;
                float flameDistX = abs(centeredX) * 2.0; // 0 at center, 1 at edges

                // Animated time offsets
                float timeVal = _Time.y;
                float flicker = 1.0 - sin(timeVal * _FlickerSpeed) * _FlickerAmount;

                // Flowing noise UV coordinates (flowing downward along flame)
                float2 noiseCoord = uv * _NoiseScale.xy + float2(0.0, timeVal * _ScrollSpeed);
                float n1 = noise2D(noiseCoord);
                float n2 = noise2D(noiseCoord * 1.8 - float2(timeVal * 1.2, timeVal * (_ScrollSpeed * 1.4)));
                float combinedNoise = lerp(n1, n2, 0.5);

                // Distort flame silhouette horizontally with noise
                float distortedDistX = flameDistX + (combinedNoise - 0.5) * _Distortion * (1.0 - uv.y);

                // Tear-drop / conical flame profile
                // uv.y = 1.0 is root (engine nozzle), uv.y = 0.0 is flame tip
                float flameProfile = pow(saturate(uv.y), _Taper);
                float flameShape = 1.0 - smoothstep(flameProfile * (1.0 - _TipSoftness), flameProfile, distortedDistX);

                // Vertical tip fade
                float tipFade = smoothstep(0.0, _TipSoftness, uv.y);
                float flameAlpha = flameShape * tipFade * flicker;

                // Core mask (hottest white/light inner column)
                float coreProfile = pow(saturate(uv.y), _Taper * 1.3) * _CoreSize;
                float coreShape = 1.0 - smoothstep(coreProfile * 0.4, coreProfile, flameDistX + (combinedNoise - 0.5) * (_Distortion * 0.5));
                float coreAlpha = saturate(coreShape * tipFade);

                // Sample texture if a custom sprite/particle texture is assigned
                fixed4 texColor = tex2D(_MainTex, uv);
                flameAlpha *= texColor.a;

                // Dynamic coloring: Blend Core -> Outer Flame Color, modulated by Vertex/Sprite tint
                fixed4 shipTint = IN.color;
                fixed3 finalOuterColor = _FlameColor.rgb * shipTint.rgb;
                fixed3 finalCoreColor = _CoreColor.rgb * lerp(fixed3(1.0, 1.0, 1.0), shipTint.rgb, 0.35);

                fixed3 color = lerp(finalOuterColor, finalCoreColor, coreAlpha);
                color *= _Intensity;

                float finalAlpha = saturate(flameAlpha * _FlameColor.a * shipTint.a);

                return fixed4(color * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
