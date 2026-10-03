#ifndef LIT_TACTICAL_VISIBILITY_MASK_INCLUDED
#define LIT_TACTICAL_VISIBILITY_MASK_INCLUDED

// Shared HDRP Shader Graph fragment function. The alpha input is left unchanged:
// the tactical cutout is an additional clip, independent of existing dissolve/ice alpha.
float LitTacticalOpening(float2 uv, float depth, float4 target, float4 settings)
{
    if (target.w <= 0.0 || target.z <= 0.0 || depth >= target.z - settings.w) return 0.0;
    float2 delta = uv - target.xy;
    delta.x *= settings.z; // radius measured in screen-height units, not width.
    float feather = max(settings.y, 0.0001);
    return (1.0 - smoothstep(max(0.0, settings.x - feather * 0.5),
        settings.x + feather * 0.5, length(delta))) * saturate(target.w);
}

void LitTacticalMask_float(float3 PositionAbsolute, float OriginalAlpha, float Enabled,
    float4 Target0, float4 Target1, float4 Settings, float4 CameraPosition,
    float4 CameraForward, float Version, out float Alpha)
{
    Alpha = OriginalAlpha;
#if !defined(SHADERGRAPH_PREVIEW) && defined(SHADERPASS)
    // Keep the original geometry/alpha in shadow maps and baked lighting.
    // Ray tracing cannot use a view-dependent opening either.
    #if SHADERPASS != SHADERPASS_SHADOWS && SHADERPASS != SHADERPASS_LIGHT_TRANSPORT && !defined(SHADER_STAGE_RAY_TRACING)
    if (Enabled < 0.5 || Version < 1.0) return;
    float3 viewPosition = GetAbsolutePositionWS(GetCurrentViewPosition());
    if (distance(viewPosition, CameraPosition.xyz) > 0.01 ||
        dot(GetViewForwardDir(), CameraForward.xyz) < 0.999) return;
    float4 projected = TransformWorldToHClip(GetCameraRelativePositionWS(PositionAbsolute));
    if (projected.w <= 0.0) return;
    float2 uv = projected.xy / projected.w;
    uv.y *= _ProjectionParams.x;
    uv = uv * 0.5 + 0.5;
    float depth = dot(PositionAbsolute - CameraPosition.xyz, CameraForward.xyz);
    float opening = max(LitTacticalOpening(uv, depth, Target0, Settings),
        LitTacticalOpening(uv, depth, Target1, Settings));
    // Stable ordered dither: no frame-dependent noise fighting temporal AA.
    uint2 pixel = (uint2)floor(uv * _ScreenParams.xy);
    uint low = ((pixel.x ^ pixel.y) & 1u) * 2u + (pixel.y & 1u);
    uint high = (((pixel.x >> 1u) ^ (pixel.y >> 1u)) & 1u) * 2u + ((pixel.y >> 1u) & 1u);
    float threshold = (4u * low + high + 0.5) / 16.0;
    clip(1.0 - opening - threshold);
    #endif
#endif
}
#endif
