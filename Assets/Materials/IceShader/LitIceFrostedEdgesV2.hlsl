#ifndef LIT_ICE_FROSTED_EDGES_V2_INCLUDED
#define LIT_ICE_FROSTED_EDGES_V2_INCLUDED

#include "LitIceFrostedEdges.hlsl"

float2 LitIceReplacementUV(
    float3 positionWS,
    float3 normalWS,
    float3 boundsSize,
    float4 uv0,
    float useScaleTiling,
    float tilingMultiplier)
{
    float scale = max(0.0001, tilingMultiplier);
    // Match ShaderGraph_MasterShader exactly when scale tiling is disabled:
    // the regular material appearance uses the mesh UV0 without bounds scaling.
    float2 meshUV = uv0.xy;

    float3 axis = abs(normalize(normalWS));
    float2 projectedUV = positionWS.xy;
    if (axis.x >= axis.y && axis.x >= axis.z)
        projectedUV = positionWS.zy;
    else if (axis.y >= axis.z)
        projectedUV = positionWS.xz;

    float2 scaleTilingUV = projectedUV * scale;
    return lerp(meshUV, scaleTilingUV, step(0.5, useScaleTiling));
}

float LitIceFlameMask(
    float3 positionWS,
    float3 flameCenter,
    float flameInfluenceRadius,
    float transitionSoftness)
{
    float radius = max(0.0, flameInfluenceRadius);
    float safeSoftness = max(0.0001, transitionSoftness);
    float mask = 1.0 - saturate((distance(positionWS, flameCenter) - radius) / safeSoftness);
    return radius > 0.0 ? mask : 0.0;
}

void LitIceFrostedEdgesV2_float(
    float3 PositionWS,
    float3 NormalWS,
    float4 IceDeepColor,
    float4 FrostColor,
    float4 VertexEdgeData,
    float IceScale,
    float FrostWidth,
    float4 CrackColor,
    float Transparency,
    float NormalStrength,
    float EdgeSensitivity,
    float3 NoiseOffset,
    float MicroScale,
    float CrackWidth,
    float FresnelPower,
    float FresnelIntensity,
    float EmissionIntensity,
    float EdgeBakedBoost,
    float3 BoundsSize,
    float4 UV0,
    UnityTexture2D BaseTexture,
    UnitySamplerState BaseSampler,
    UnityTexture2D NormalTexture,
    float4 BaseColor,
    float BaseNormalStrength,
    float UseScaleTiling,
    float TilingMultiplier,
    float3 FlameCenter,
    float FlameInfluenceRadius,
    float TransitionSoftness,
    float TransitionProgress,
    float IceSmoothness,
    float IceMetallic,
    float BaseSmoothness,
    float BaseMetallic,
    out float3 OutBaseColor,
    out float OutAlpha,
    out float3 OutNormalTS,
    out float3 OutEmission,
    out float OutSmoothness,
    out float OutMetallic)
{
    float3 iceBaseColor;
    float iceAlpha;
    float3 iceNormalTS;
    float3 iceEmission;
    LitIceFrostedEdges_float(
        PositionWS, NormalWS, IceDeepColor, FrostColor, VertexEdgeData,
        IceScale, FrostWidth, CrackColor, Transparency, NormalStrength,
        EdgeSensitivity, NoiseOffset, MicroScale, CrackWidth, FresnelPower,
        FresnelIntensity, EmissionIntensity, EdgeBakedBoost,
        iceBaseColor, iceAlpha, iceNormalTS, iceEmission);

    float2 baseUV = LitIceReplacementUV(
        PositionWS, NormalWS, BoundsSize, UV0, UseScaleTiling, TilingMultiplier);
    float4 baseAppearance = BaseTexture.Sample(BaseSampler, baseUV);
    float3 sampledBaseNormalTS = UnpackNormal(NormalTexture.Sample(BaseSampler, baseUV));
    float normalStrength = saturate(BaseNormalStrength);
    float3 baseNormalTS = float3(
        sampledBaseNormalTS.rg * BaseNormalStrength,
        lerp(1.0, sampledBaseNormalTS.b, normalStrength));
    float flameMask = LitIceFlameMask(
        PositionWS, FlameCenter, FlameInfluenceRadius, TransitionSoftness);
    flameMask *= saturate(TransitionProgress);

    float3 revealedBaseColor = BaseColor.rgb * baseAppearance.rgb;
    OutBaseColor = lerp(iceBaseColor, revealedBaseColor, flameMask);
    // ShaderGraph_MasterShader is opaque when its dissolve is inactive.
    OutAlpha = lerp(iceAlpha, 1.0, flameMask);
    OutNormalTS = normalize(lerp(iceNormalTS, baseNormalTS, flameMask));
    OutEmission = lerp(iceEmission, float3(0.0, 0.0, 0.0), flameMask);
    OutSmoothness = lerp(IceSmoothness, BaseSmoothness, flameMask);
    OutMetallic = lerp(IceMetallic, BaseMetallic, flameMask);
}

void LitIceFrostedEdgesV2_half(
    half3 PositionWS,
    half3 NormalWS,
    half4 IceDeepColor,
    half4 FrostColor,
    half4 VertexEdgeData,
    half IceScale,
    half FrostWidth,
    half4 CrackColor,
    half Transparency,
    half NormalStrength,
    half EdgeSensitivity,
    half3 NoiseOffset,
    half MicroScale,
    half CrackWidth,
    half FresnelPower,
    half FresnelIntensity,
    half EmissionIntensity,
    half EdgeBakedBoost,
    half3 BoundsSize,
    half4 UV0,
    UnityTexture2D BaseTexture,
    UnitySamplerState BaseSampler,
    UnityTexture2D NormalTexture,
    half4 BaseColor,
    half BaseNormalStrength,
    half UseScaleTiling,
    half TilingMultiplier,
    half3 FlameCenter,
    half FlameInfluenceRadius,
    half TransitionSoftness,
    half TransitionProgress,
    half IceSmoothness,
    half IceMetallic,
    half BaseSmoothness,
    half BaseMetallic,
    out half3 OutBaseColor,
    out half OutAlpha,
    out half3 OutNormalTS,
    out half3 OutEmission,
    out half OutSmoothness,
    out half OutMetallic)
{
    float3 baseColor;
    float alpha;
    float3 normalTS;
    float3 emission;
    float smoothness;
    float metallic;
    LitIceFrostedEdgesV2_float(
        PositionWS, NormalWS, IceDeepColor, FrostColor, VertexEdgeData,
        IceScale, FrostWidth, CrackColor, Transparency, NormalStrength,
        EdgeSensitivity, NoiseOffset, MicroScale, CrackWidth, FresnelPower,
        FresnelIntensity, EmissionIntensity, EdgeBakedBoost,
        BoundsSize, UV0, BaseTexture, BaseSampler, NormalTexture,
        BaseColor, BaseNormalStrength, UseScaleTiling,
        TilingMultiplier, FlameCenter, FlameInfluenceRadius, TransitionSoftness,
        TransitionProgress, IceSmoothness, IceMetallic, BaseSmoothness, BaseMetallic,
        baseColor, alpha, normalTS, emission, smoothness, metallic);
    OutBaseColor = half3(baseColor);
    OutAlpha = half(alpha);
    OutNormalTS = half3(normalTS);
    OutEmission = half3(emission);
    OutSmoothness = half(smoothness);
    OutMetallic = half(metallic);
}

#endif
