Shader "Hidden/Lit/RunSpeedPeripheralBlur"
{
    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    TEXTURE2D_X(_MainTex);

    float _Intensity;
    float _CenterRadius;
    float _EdgeStart;
    float _SampleStep;
    int _Samples;

    float4 SampleSource(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X_LOD(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinear(uv), 0);
    }

    float4 Frag(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = input.texcoord;
        float2 centered = uv - 0.5f;
        float aspect = _ScreenSize.x * _ScreenSize.w;
        float2 aspectCentered = float2(centered.x * aspect, centered.y);
        float maxRadius = length(float2(0.5f * aspect, 0.5f));
        float radius = length(aspectCentered) / max(maxRadius, 0.0001f);
        float peripheralMask = smoothstep(_CenterRadius, _EdgeStart, radius);
        float blurWeight = saturate(peripheralMask * _Intensity);

        float4 original = SampleSource(uv);
        if (blurWeight <= 0.0001f)
        {
            return original;
        }

        float2 direction = normalize(centered + 0.00001f);
        float4 sum = original;
        float totalWeight = 1.0f;

        [loop]
        for (int i = 1; i <= 12; i++)
        {
            if (i > _Samples)
            {
                break;
            }

            float sampleWeight = 1.0f - (float)i / 13.0f;
            float2 offset = direction * (_SampleStep * i * blurWeight);
            sum += SampleSource(uv + offset) * sampleWeight;
            sum += SampleSource(uv - offset) * sampleWeight;
            totalWeight += sampleWeight * 2.0f;
        }

        float4 blurred = sum / totalWeight;
        return lerp(original, blurred, blurWeight);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "RunSpeedPeripheralBlur"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag
            ENDHLSL
        }
    }

    Fallback Off
}
