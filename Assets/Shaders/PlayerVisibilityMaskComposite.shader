Shader "Hidden/HDRP/PlayerVisibilityMaskComposite"
{
    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    TEXTURE2D_X(_PlayerVisibilityTexture);

    float4 _PlayerVisibilityMaskCenter;
    float4 _PlayerVisibilityMaskParams;
    float4 _PlayerVisibilityMaskDebug;

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        if (_PlayerVisibilityMaskCenter.z <= 0.5f || _PlayerVisibilityMaskParams.w <= 0.5f || _PlayerVisibilityMaskParams.z <= 0.001f)
        {
            return 0;
        }

        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        float2 delta = uv - _PlayerVisibilityMaskCenter.xy;
        delta.x *= _ScreenSize.x / max(_ScreenSize.y, 1.0);

        float radius = max(_PlayerVisibilityMaskParams.x, 0.0001);
        float softness = max(_PlayerVisibilityMaskParams.y, 0.0001);
        float mask = 1.0 - smoothstep(radius, radius + softness, length(delta));

        float4 playerColor = SAMPLE_TEXTURE2D_X_LOD(
            _PlayerVisibilityTexture,
            s_linear_clamp_sampler,
            ClampAndScaleUVForBilinear(uv),
            0);

        float playerAlpha = saturate(mask * _PlayerVisibilityMaskParams.z * playerColor.a);
        float outputAlpha = playerAlpha;
        float3 outputColor = playerColor.rgb;

        if (_PlayerVisibilityMaskDebug.z > 0.5f)
        {
            float debugAlpha = saturate(mask * 0.75f);
            float3 debugColor = float3(0.0f, 0.9f, 1.0f);
            outputColor = playerAlpha > 0.001f ? lerp(outputColor, debugColor, saturate(mask * 0.7f)) : debugColor;
            outputAlpha = max(outputAlpha, debugAlpha);
        }

        return float4(outputColor, outputAlpha);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Player Visibility Mask Composite"

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
