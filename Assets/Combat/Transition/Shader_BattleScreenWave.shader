Shader "Hidden/Lit/BattleScreenWave"
{
    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _Progress;
    float _Intensity;
    float2 _WaveCenter;
    float _RingWidth;
    float _Frequency;
    float _ChromaticAberration;
    float _Vignette;
    float _Fade;

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        float2 center = _WaveCenter;
        float aspect = _ScreenSize.x * _ScreenSize.w;
        float2 delta = float2((uv.x - center.x) * aspect, uv.y - center.y);
        float distanceToCenter = length(delta);
        float ringDistance = abs(distanceToCenter - _Progress);
        float ringMask = 1.0f - smoothstep(0.0f, max(_RingWidth, 0.0001f), ringDistance);
        float ripple = sin((distanceToCenter - _Progress) * _Frequency);
        float2 direction = normalize(delta + 0.00001f);
        float2 distortion = float2(direction.x / max(aspect, 0.0001f), direction.y) * ripple * ringMask * _Intensity;
        float2 distortedUv = saturate(uv + distortion);

        float3 color = CustomPassSampleCameraColor(distortedUv, 0);
        if (_ChromaticAberration > 0.0001f)
        {
            float2 chroma = distortion * _ChromaticAberration * 12.0f;
            color.r = CustomPassSampleCameraColor(saturate(distortedUv + chroma), 0).r;
            color.b = CustomPassSampleCameraColor(saturate(distortedUv - chroma), 0).b;
        }

        float2 vignetteDelta = uv - 0.5f;
        float vignetteMask = smoothstep(0.28f, 0.82f, length(vignetteDelta));
        color *= 1.0f - vignetteMask * _Vignette;
        color = lerp(color, color * 0.18f, _Fade);

        return float4(color, 1.0f);
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
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment FullScreenPass
            ENDHLSL
        }
    }

    Fallback Off
}
