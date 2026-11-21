Shader "Starfall/SpriteAdditive"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseMap ("Base Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _Color;
            float4 _BaseColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color * _BaseColor;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 texMain = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 texBase = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 tex = (texMain.a < 0.999 || texMain.r < 0.999 || texMain.g < 0.999 || texMain.b < 0.999) ? texMain : texBase;
                return tex * input.color;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _Color;
            float4 _BaseColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color * _BaseColor;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 texMain = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 texBase = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 tex = (texMain.a < 0.999 || texMain.r < 0.999 || texMain.g < 0.999 || texMain.b < 0.999) ? texMain : texBase;
                return tex * input.color;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _Color;
            float4 _BaseColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color * _BaseColor;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 texMain = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 texBase = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 tex = (texMain.a < 0.999 || texMain.r < 0.999 || texMain.g < 0.999 || texMain.b < 0.999) ? texMain : texBase;
                return tex * input.color;
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

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
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _BaseColor;
            sampler2D _MainTex;
            sampler2D _BaseMap;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _BaseColor;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 texMain = tex2D(_MainTex, IN.texcoord);
                fixed4 texBase = tex2D(_BaseMap, IN.texcoord);
                fixed4 tex = (texMain.a < 0.999 || texMain.r < 0.999 || texMain.g < 0.999 || texMain.b < 0.999) ? texMain : texBase;
                return tex * IN.color;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
