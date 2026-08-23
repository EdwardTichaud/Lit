Shader "Hidden/Lit/CombatWarning"
{
    Properties
    {
        _WarningOrigin("Warning Origin", Vector) = (0.5, 0.5, 0, 0)
        _WarningDirection("Warning Direction", Vector) = (0, 1, 0, 0)
        _WarningColor("Warning Color", Color) = (1, 0.1, 0.42, 1)
        _WarningIntensity("Warning Intensity", Range(0, 2)) = 0
        _WarningPulse("Warning Pulse", Range(0, 1)) = 0
        _WarningVignette("Warning Vignette", Range(0, 1)) = 0
        _WarningChromatic("Warning Chromatic", Range(0, 0.1)) = 0
    }

    HLSLINCLUDE
    #pragma vertex Vert
    #pragma target 4.5
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float4 _WarningOrigin;
    float4 _WarningDirection;
    float4 _WarningColor;
    float _WarningIntensity;
    float _WarningPulse;
    float _WarningVignette;
    float _WarningChromatic;

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        float2 centered = uv - 0.5;
        float edge = saturate((length(centered * float2(_ScreenSize.x * _ScreenSize.w, 1.0)) - 0.26) / 0.48);
        float2 direction = normalize(_WarningDirection.xy + 0.00001);
        float directional = saturate(dot(normalize(centered + 0.00001), direction) * 0.5 + 0.5);
        float arc = pow(edge * directional, 1.6);
        float pulse = lerp(0.55, 1.0, _WarningPulse);
        float2 chromaOffset = direction * _WarningChromatic * arc;
        float3 baseColor = CustomPassSampleCameraColor(uv, 0);
        float red = CustomPassSampleCameraColor(saturate(uv + chromaOffset), 0).r;
        float blue = CustomPassSampleCameraColor(saturate(uv - chromaOffset), 0).b;
        float3 chromatic = float3(red, baseColor.g, blue);
        float intensity = saturate(_WarningIntensity);
        float3 color = lerp(baseColor, chromatic, arc * intensity);
        color += _WarningColor.rgb * arc * pulse * intensity;
        color *= 1.0 - edge * _WarningVignette;
        return float4(color, 1);
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
