Shader "Hidden/Lit/ScreenWave"
{
    Properties
    {
        _Origin("Origin", Vector) = (0.5, 0.5, 0, 0)
        _Direction("Direction", Vector) = (0, 0, 0, 0)
        _Elapsed("Elapsed", Float) = 0
        _Duration("Duration", Float) = 0.9
        _Reverse("Reverse", Range(0, 1)) = 0
        _Frequency("Frequency", Range(0.1, 64)) = 14
        _PropagationSpeed("Propagation Speed", Range(0.01, 4)) = 1.45
        _Amplitude("Amplitude", Range(0, 0.25)) = 0.08
        _Falloff("Falloff", Range(0.01, 16)) = 6
        _WaveFade("Wave Fade", Range(0, 1)) = 0
        _HighlightColor("Highlight Color", Color) = (0.72, 0.9, 1, 1)
        _HighlightIntensity("Highlight Intensity", Range(0, 2)) = 0.38
        _EdgeContrast("Edge Contrast", Range(0.1, 8)) = 2.2
    }

    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float4 _Origin;
    float4 _Direction;
    float _Elapsed;
    float _Duration;
    float _Reverse;
    float _Frequency;
    float _PropagationSpeed;
    float _Amplitude;
    float _Falloff;
    float _WaveFade;
    float4 _HighlightColor;
    float _HighlightIntensity;
    float _EdgeContrast;

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        float aspect = _ScreenSize.x * _ScreenSize.w;
        float2 origin = saturate(_Origin.xy);
        float2 viewportDelta = uv - origin;
        float2 aspectDelta = float2(viewportDelta.x * aspect, viewportDelta.y);
        float distanceToOrigin = length(aspectDelta);
        float directionAmount = saturate(length(_Direction.xy));
        float2 requestedDirection = normalize(_Direction.xy + 0.00001f);
        float directionalBias = dot(viewportDelta, requestedDirection) * directionAmount * 0.35f;
        float waveDistance = max(distanceToOrigin - directionalBias, 0.0f);
        float waveTime = lerp(_Elapsed, max(_Duration - _Elapsed, 0.0f), saturate(_Reverse));
        float travel = max(waveTime * _PropagationSpeed, 0.0f);
        float ringDistance = abs(waveDistance - travel);
        float falloff = max(_Falloff, 0.01f);
        float frontBand = exp(-ringDistance * falloff);
        float revealSoftness = max(0.02f, 1.0f / falloff);
        float revealedArea = 1.0f - smoothstep(travel - revealSoftness, travel + revealSoftness, waveDistance);
        float settledFalloff = rcp(1.0f + distanceToOrigin * falloff * 0.08f);
        float settledWave = revealedArea * settledFalloff * 0.45f;
        float waveMask = max(frontBand, settledWave);
        float ripple = sin((waveDistance - travel) * _Frequency * 6.28318530718f);

        float2 radialDirection = normalize(float2(aspectDelta.x / max(aspect, 0.0001f), aspectDelta.y) + 0.00001f);
        float2 pushDirection = normalize(lerp(radialDirection, requestedDirection, directionAmount) + 0.00001f);

        float visible = saturate(_WaveFade);
        float2 distortion = pushDirection * ripple * waveMask * _Amplitude;
        float2 distortedUv = saturate(uv + distortion);
        float3 normalColor = CustomPassSampleCameraColor(uv, 0);
        float3 waveColor = CustomPassSampleCameraColor(distortedUv, 0);
        float3 color = lerp(normalColor, waveColor, visible);
        float edge = pow(saturate(frontBand), max(_EdgeContrast, 0.1f));
        float rippleLift = saturate(abs(ripple) * 0.7f + 0.3f);
        color += _HighlightColor.rgb * edge * rippleLift * _HighlightIntensity * visible;

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
