#ifndef LIT_ICE_FROSTED_EDGES_V3_INCLUDED
#define LIT_ICE_FROSTED_EDGES_V3_INCLUDED

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

float3 LitIceScaleTangentNormal(float3 normalTS, float strength)
{
    float safeStrength = max(0.0, strength);
    return normalize(float3(
        normalTS.xy * safeStrength,
        lerp(1.0, normalTS.z, saturate(safeStrength))));
}

float3 LitIceBlendTangentNormals(float3 first, float3 second)
{
    // Whiteout blending retains both the broad source relief and the ice micro-facets.
    return normalize(float3(first.xy + second.xy, first.z * second.z));
}

float LitIceReliefTextureEdgeMask(
    UnityTexture2D normalTexture,
    UnityTexture2D roughnessTexture,
    UnitySamplerState samplerState,
    float2 uv,
    float3 centerNormalTS,
    float centerRoughness,
    float useRoughnessTexture,
    float sampleWidth,
    float normalInfluence,
    float roughnessInfluence,
    float threshold)
{
    // Compare the current texel with its four neighbours. Contrary to the
    // geometric edge mask, this also discovers mortar lines and relief borders
    // which exist only inside the material textures.
    float widthInTexels = max(0.25, sampleWidth);
    float2 normalTexel = max(normalTexture.texelSize.xy, float2(0.000001, 0.000001))
                       * widthInTexels;
    float3 normalRight = UnpackNormal(normalTexture.Sample(
        samplerState, uv + float2(normalTexel.x, 0.0)));
    float3 normalLeft = UnpackNormal(normalTexture.Sample(
        samplerState, uv - float2(normalTexel.x, 0.0)));
    float3 normalUp = UnpackNormal(normalTexture.Sample(
        samplerState, uv + float2(0.0, normalTexel.y)));
    float3 normalDown = UnpackNormal(normalTexture.Sample(
        samplerState, uv - float2(0.0, normalTexel.y)));
    float normalEdge = max(
        max(length(centerNormalTS - normalRight), length(centerNormalTS - normalLeft)),
        max(length(centerNormalTS - normalUp), length(centerNormalTS - normalDown)));
    normalEdge = saturate(normalEdge * 0.5) * max(0.0, normalInfluence);

    float2 roughnessTexel = max(
        roughnessTexture.texelSize.xy, float2(0.000001, 0.000001)) * widthInTexels;
    float roughnessRight = roughnessTexture.Sample(
        samplerState, uv + float2(roughnessTexel.x, 0.0)).r;
    float roughnessLeft = roughnessTexture.Sample(
        samplerState, uv - float2(roughnessTexel.x, 0.0)).r;
    float roughnessUp = roughnessTexture.Sample(
        samplerState, uv + float2(0.0, roughnessTexel.y)).r;
    float roughnessDown = roughnessTexture.Sample(
        samplerState, uv - float2(0.0, roughnessTexel.y)).r;
    float roughnessEdge = max(
        max(abs(centerRoughness - roughnessRight), abs(centerRoughness - roughnessLeft)),
        max(abs(centerRoughness - roughnessUp), abs(centerRoughness - roughnessDown)));
    roughnessEdge *= max(0.0, roughnessInfluence) * step(0.5, useRoughnessTexture);

    float edgeSignal = saturate(normalEdge + roughnessEdge);
    float edgeThreshold = saturate(threshold);
    float edgeSoftness = max(0.01, (1.0 - edgeThreshold) * 0.12);
    return smoothstep(edgeThreshold, edgeThreshold + edgeSoftness, edgeSignal);
}

void LitIceFrostedEdgesV3_float(
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
    UnityTexture2D BaseRoughnessTexture,
    UnityTexture2D BaseMetallicTexture,
    UnityTexture2D BaseOcclusionTexture,
    float UseBaseRoughnessTexture,
    float UseBaseMetallicTexture,
    float UseBaseOcclusionTexture,
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
    float IceReliefNormalStrength,
    float IceReliefRoughnessInfluence,
    float TextureEdgeStrength,
    float TextureEdgeWidth,
    float TextureEdgeThreshold,
    float TextureEdgeNormalInfluence,
    float TextureEdgeRoughnessInfluence,
    out float3 OutBaseColor,
    out float OutAlpha,
    out float3 OutNormalTS,
    out float3 OutEmission,
    out float OutSmoothness,
    out float OutMetallic,
    out float OutOcclusion)
{
    float3 iceBaseColor;
    float iceAlpha;
    float3 iceNormalTS;
    float3 iceEmission;
    // V3 treats FrostWidth as a real 0..10 artistic range. Geometry/baked
    // contours grow from sub-pixel lines to a clearly visible 16-pixel band;
    // unlike v1/v2, the last two thirds of the slider therefore stay useful.
    float normalizedFrostWidth = saturate(FrostWidth * 0.1);
    float bakedEdgeWidthPixels = lerp(
        0.75, 16.0, pow(normalizedFrostWidth, 1.35));
    float frostEnabled = step(0.0001, normalizedFrostWidth);
    LitIceFrostedEdgesCore_float(
        PositionWS, NormalWS, IceDeepColor, FrostColor, VertexEdgeData,
        IceScale, normalizedFrostWidth, CrackColor, Transparency, NormalStrength,
        EdgeSensitivity, NoiseOffset, MicroScale, CrackWidth, 1.0,
        0.0, EmissionIntensity, EdgeBakedBoost,
        bakedEdgeWidthPixels, frostEnabled,
        iceBaseColor, iceAlpha, iceNormalTS, iceEmission);

    float2 baseUV = LitIceReplacementUV(
        PositionWS, NormalWS, BoundsSize, UV0, UseScaleTiling, TilingMultiplier);
    float4 baseAppearance = BaseTexture.Sample(BaseSampler, baseUV);
    float3 sampledBaseNormalTS = UnpackNormal(NormalTexture.Sample(BaseSampler, baseUV));
    float sampledRoughness = BaseRoughnessTexture.Sample(BaseSampler, baseUV).r;
    float sampledMetallic = BaseMetallicTexture.Sample(BaseSampler, baseUV).r;
    float sampledOcclusion = BaseOcclusionTexture.Sample(BaseSampler, baseUV).r;
    float3 baseNormalTS = LitIceScaleTangentNormal(sampledBaseNormalTS, BaseNormalStrength);
    float3 iceReliefNormalTS = LitIceScaleTangentNormal(
        sampledBaseNormalTS, IceReliefNormalStrength);
    float3 iceNormalWithRelief = LitIceBlendTangentNormals(iceNormalTS, iceReliefNormalTS);
    float textureEdgeMask = LitIceReliefTextureEdgeMask(
        NormalTexture, BaseRoughnessTexture, BaseSampler, baseUV,
        sampledBaseNormalTS, sampledRoughness, UseBaseRoughnessTexture,
        TextureEdgeWidth, TextureEdgeNormalInfluence,
        TextureEdgeRoughnessInfluence, TextureEdgeThreshold);
    float textureEdgeEmission = textureEdgeMask * max(0.0, TextureEdgeStrength);
    float textureEdgeCoverage = saturate(textureEdgeEmission);
    iceBaseColor = lerp(iceBaseColor, saturate(FrostColor.rgb), textureEdgeCoverage * 0.92);
    iceEmission += FrostColor.rgb * textureEdgeEmission * max(0.0, EmissionIntensity);
    float flameMask = LitIceFlameMask(
        PositionWS, FlameCenter, FlameInfluenceRadius, TransitionSoftness);
    flameMask *= saturate(TransitionProgress);

    float3 revealedBaseColor = BaseColor.rgb * baseAppearance.rgb;
    // Autodesk Interactive converts perceptual roughness with 1 - sqrt(roughness).
    // Keep the independent scalar controls as fallbacks when a map is disabled.
    float revealedSmoothness = lerp(
        BaseSmoothness,
        1.0 - sqrt(saturate(sampledRoughness)),
        step(0.5, UseBaseRoughnessTexture));
    float revealedMetallic = lerp(
        BaseMetallic,
        saturate(sampledMetallic),
        step(0.5, UseBaseMetallicTexture));
    float revealedOcclusion = lerp(
        1.0,
        saturate(sampledOcclusion),
        step(0.5, UseBaseOcclusionTexture));
    float iceRoughnessWeight = saturate(IceReliefRoughnessInfluence)
        * step(0.5, UseBaseRoughnessTexture);
    float iceSmoothnessWithRelief = lerp(
        IceSmoothness, revealedSmoothness, iceRoughnessWeight);
    OutBaseColor = lerp(iceBaseColor, revealedBaseColor, flameMask);
    // ShaderGraph_MasterShader is opaque when its dissolve is inactive.
    OutAlpha = lerp(iceAlpha, 1.0, flameMask);
    OutNormalTS = normalize(lerp(iceNormalWithRelief, baseNormalTS, flameMask));
    OutEmission = lerp(iceEmission, float3(0.0, 0.0, 0.0), flameMask);
    OutSmoothness = lerp(iceSmoothnessWithRelief, revealedSmoothness, flameMask);
    OutMetallic = lerp(IceMetallic, revealedMetallic, flameMask);
    OutOcclusion = lerp(1.0, revealedOcclusion, flameMask);
}

void LitIceFrostedEdgesV3_half(
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
    UnityTexture2D BaseRoughnessTexture,
    UnityTexture2D BaseMetallicTexture,
    UnityTexture2D BaseOcclusionTexture,
    half UseBaseRoughnessTexture,
    half UseBaseMetallicTexture,
    half UseBaseOcclusionTexture,
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
    half IceReliefNormalStrength,
    half IceReliefRoughnessInfluence,
    half TextureEdgeStrength,
    half TextureEdgeWidth,
    half TextureEdgeThreshold,
    half TextureEdgeNormalInfluence,
    half TextureEdgeRoughnessInfluence,
    out half3 OutBaseColor,
    out half OutAlpha,
    out half3 OutNormalTS,
    out half3 OutEmission,
    out half OutSmoothness,
    out half OutMetallic,
    out half OutOcclusion)
{
    float3 baseColor;
    float alpha;
    float3 normalTS;
    float3 emission;
    float smoothness;
    float metallic;
    float occlusion;
    LitIceFrostedEdgesV3_float(
        PositionWS, NormalWS, IceDeepColor, FrostColor, VertexEdgeData,
        IceScale, FrostWidth, CrackColor, Transparency, NormalStrength,
        EdgeSensitivity, NoiseOffset, MicroScale, CrackWidth, FresnelPower,
        FresnelIntensity, EmissionIntensity, EdgeBakedBoost,
        BoundsSize, UV0, BaseTexture, BaseSampler, NormalTexture,
        BaseRoughnessTexture, BaseMetallicTexture, BaseOcclusionTexture,
        UseBaseRoughnessTexture, UseBaseMetallicTexture, UseBaseOcclusionTexture,
        BaseColor, BaseNormalStrength, UseScaleTiling,
        TilingMultiplier, FlameCenter, FlameInfluenceRadius, TransitionSoftness,
        TransitionProgress, IceSmoothness, IceMetallic, BaseSmoothness, BaseMetallic,
        IceReliefNormalStrength, IceReliefRoughnessInfluence,
        TextureEdgeStrength, TextureEdgeWidth, TextureEdgeThreshold,
        TextureEdgeNormalInfluence, TextureEdgeRoughnessInfluence,
        baseColor, alpha, normalTS, emission, smoothness, metallic, occlusion);
    OutBaseColor = half3(baseColor);
    OutAlpha = half(alpha);
    OutNormalTS = half3(normalTS);
    OutEmission = half3(emission);
    OutSmoothness = half(smoothness);
    OutMetallic = half(metallic);
    OutOcclusion = half(occlusion);
}

#endif
