Shader "Hidden/HDRP/RuntimeOutlineFullscreen"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.35, 0.65, 1, 1)
        _Thickness("Thickness", Float) = 2
    }

    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float4 _OutlineColor;
    float _Thickness;

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(
            varyings.positionCS.xy,
            _ScreenSize.zw,
            depth,
            UNITY_MATRIX_I_VP,
            UNITY_MATRIX_V
        );

        float2 uv = posInput.positionNDC;
        float2 pixel = _ScreenSize.zw * _Thickness;

        float center = SampleCustomColor(uv).r;

        float around = 0;
        around = max(around, SampleCustomColor(uv + float2( pixel.x, 0)).r);
        around = max(around, SampleCustomColor(uv + float2(-pixel.x, 0)).r);
        around = max(around, SampleCustomColor(uv + float2(0,  pixel.y)).r);
        around = max(around, SampleCustomColor(uv + float2(0, -pixel.y)).r);
        around = max(around, SampleCustomColor(uv + float2( pixel.x,  pixel.y)).r);
        around = max(around, SampleCustomColor(uv + float2(-pixel.x,  pixel.y)).r);
        around = max(around, SampleCustomColor(uv + float2( pixel.x, -pixel.y)).r);
        around = max(around, SampleCustomColor(uv + float2(-pixel.x, -pixel.y)).r);

        float edge = saturate(around - center);

        return float4(_OutlineColor.rgb, edge * _OutlineColor.a);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Custom Pass 0"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma fragment FullScreenPass
            ENDHLSL
        }
    }

    Fallback Off
}