#ifndef LIT_TACTICAL_VISIBILITY_MASK_INCLUDED
#define LIT_TACTICAL_VISIBILITY_MASK_INCLUDED

// Shared HDRP Shader Graph fragment function. The alpha input is left unchanged:
// the tactical cutout is an additional clip, independent of existing dissolve/ice alpha.
Texture2D _LitTacticalMaskShape;
SamplerState sampler_LitTacticalMaskShape;
float4 _LitTacticalMaskShapeMotion; // x: fraction of radius, y: phase in cycles.

float LitTacticalOpening(float2 uv, float depth, float4 target, float4 settings, bool useShape)
{
    if (target.w <= 0.0 || target.z <= 0.0 || depth >= target.z - settings.w) return 0.0;
    float2 delta = uv - target.xy;
    delta.x *= settings.z; // radius measured in screen-height units, not width.
    if (useShape)
    {
        // The FireBall artwork has transparent padding. Its main silhouette
        // occupies roughly 70% of the texture width at the centre.
        float2 local = delta / max(settings.x, 0.0001);
        float amount = _LitTacticalMaskShapeMotion.x;
        // The texture can only enter this screen-space box, even at full distortion.
        if (any(abs(local) > (0.5 / 0.35 + amount))) return 0.0;
        if (amount > 0.0)
        {
            // Keep the centre fixed. Waves travel upwards at different phases
            // across the edge, rather than pulsing the size of the opening.
            float edge = smoothstep(0.55, 1.0, length(local));
            float phase = _LitTacticalMaskShapeMotion.y * 6.2831853;
            float2 wave = float2(
                0.25 * sin(local.y * 8.0 + local.x * 4.0 - phase),
                0.75 * sin(local.y * 10.0 - local.x * 9.0 - phase));
            local += wave * (edge * amount);
        }
        float2 shapeUV = 0.5 + local * 0.35;
        if (any(shapeUV < 0.0) || any(shapeUV > 1.0)) return 0.0;
        return _LitTacticalMaskShape.SampleLevel(sampler_LitTacticalMaskShape, shapeUV, 0).a
            * saturate(target.w);
    }
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
    // 1/2 = circular wall/floor, 3/4 = textured wall/floor.
    bool useShape = Enabled >= 2.5;
    bool overhead = Enabled - (useShape ? 2.0 : 0.0) >= 1.5;
    // An upper floor can occlude the player,
    // but the floor supporting either protected character must never disappear.
    float opening0 = !overhead || PositionAbsolute.y > CameraPosition.w
        ? LitTacticalOpening(uv, depth, Target0, Settings, useShape) : 0;
    float opening1 = !overhead || PositionAbsolute.y > CameraForward.w
        ? LitTacticalOpening(uv, depth, Target1, Settings, useShape) : 0;
    float opening = max(opening0, opening1);
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
