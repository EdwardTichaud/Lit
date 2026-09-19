Shader "Hidden/HDRP/RuntimeOutlineMask"
{
    Properties
    {
        _RuntimeOutlineTexture("Opacity texture", 2D) = "white" {}
        _RuntimeOutlineAlphaEnabled("Use opacity", Float) = 0
        _RuntimeOutlineOpacity("Material opacity", Float) = 1
        _RuntimeOutlineRedChannel("Use red channel", Float) = 0
        _RuntimeOutlineVertexAlpha("Use particle alpha", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="HDRenderPipeline"
            "RenderType"="Opaque"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }

            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            sampler2D _RuntimeOutlineTexture;
            float4 _RuntimeOutlineTexture_ST;
            float _RuntimeOutlineAlphaEnabled, _RuntimeOutlineOpacity;
            float _RuntimeOutlineRedChannel, _RuntimeOutlineVertexAlpha;
            float _RuntimeOutlineAlphaThreshold;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _RuntimeOutlineTexture);
                output.alpha = lerp(1, input.color.a, _RuntimeOutlineVertexAlpha);

                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                if (_RuntimeOutlineAlphaEnabled > 0.5)
                {
                    float4 texel = tex2D(_RuntimeOutlineTexture, input.uv);
                    float opacity = lerp(texel.a, texel.r, _RuntimeOutlineRedChannel);
                    clip(opacity * _RuntimeOutlineOpacity * input.alpha - max(0.001, _RuntimeOutlineAlphaThreshold));
                }
                return float4(1,1,1,1);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
