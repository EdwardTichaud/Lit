Shader "Hidden/Lit/MuninOrbAlphaSafe"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaMultiplier ("Alpha Multiplier", Range(0, 1)) = 1
        _BlackLuminanceThreshold ("Black Luminance Threshold", Range(0, 0.25)) = 0.025
        _BlackFeather ("Black Feather", Range(0.001, 0.25)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _AlphaMultiplier;
            float _BlackLuminanceThreshold;
            float _BlackFeather;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv) * input.color * _Color;
                float luminance = dot(max(color.rgb, 0.0), float3(0.2126, 0.7152, 0.0722));
                float safeAlpha = smoothstep(_BlackLuminanceThreshold, _BlackLuminanceThreshold + _BlackFeather, luminance);
                color.a = saturate(color.a * _AlphaMultiplier * safeAlpha);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
