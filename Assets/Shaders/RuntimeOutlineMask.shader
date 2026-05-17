Shader "Hidden/HDRP/RuntimeOutlineMask"
{
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
            Cull Back

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS = UnityObjectToClipPos(input.vertex);

                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return float4(1,1,1,1);
            }

            ENDHLSL
        }
    }

    Fallback Off
}