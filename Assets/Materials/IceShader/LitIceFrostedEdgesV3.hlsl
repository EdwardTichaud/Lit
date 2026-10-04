#ifndef LIT_ICE_FROSTED_EDGES_V3_INCLUDED
#define LIT_ICE_FROSTED_EDGES_V3_INCLUDED

#include "LitIceFrostedEdges.hlsl"

// Per-renderer influence data supplied by FlameInfluenceMaterialRuntime.
// The legacy single center remains available for inspector preview and for
// compatibility, while V3 combines every supplied sphere with a max/union.
#define LIT_ICE_MAX_FLAME_INFLUENCES 4
int _LitIceFlameInfluenceCount;
float4 _LitIceFlameCentersAndRadii[LIT_ICE_MAX_FLAME_INFLUENCES];
float4 _LitIceFlameTransitionData[LIT_ICE_MAX_FLAME_INFLUENCES];

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

float LitIceCombinedFlameMask(
    float3 positionWS,
    float3 legacyFlameCenter,
    float legacyFlameInfluenceRadius,
    float transitionSoftness,
    float legacyTransitionProgress)
{
    float combinedMask = LitIceFlameMask(
        positionWS,
        legacyFlameCenter,
        legacyFlameInfluenceRadius,
        transitionSoftness) * saturate(legacyTransitionProgress);

    int influenceCount = clamp(
        _LitIceFlameInfluenceCount, 0, LIT_ICE_MAX_FLAME_INFLUENCES);
    [loop]
    for (int i = 0; i < influenceCount; i++)
    {
        float4 centerAndRadius = _LitIceFlameCentersAndRadii[i];
        float transitionProgress = saturate(_LitIceFlameTransitionData[i].x);
        float influenceMask = LitIceFlameMask(
            positionWS,
            centerAndRadius.xyz,
            centerAndRadius.w,
            transitionSoftness) * transitionProgress;
        combinedMask = max(combinedMask, influenceMask);
    }

    return saturate(combinedMask);
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

float LitIceBakedEdgeMask(float4 vertexEdgeData, float frostWidth, float edgeBakedBoost)
{
    // Mirror the V3 edge-bake convention used by the shared core. Keeping this
    // mask here lets the Lit outputs distinguish a true ice ridge from the
    // broad body of a frozen surface: ridges stay crisp and reflective while
    // the unlit body remains dark and matte.
    float normalizedFrostWidth = saturate(frostWidth * 0.1);
    float edgePixels = lerp(0.75, 16.0, pow(normalizedFrostWidth, 1.35));
    float frostEnabled = step(0.0001, normalizedFrostWidth);
    float3 signedBarycentrics = vertexEdgeData.rgb;
    float bakedFormatV2 = 1.0 - step(0.01, abs(vertexEdgeData.a - 0.25));
    float3 barycentricDistance = abs(signedBarycentrics);
    float3 barycentricWidth = max(fwidth(barycentricDistance), 0.00001);
    float3 selectedEdges = step(0.0, signedBarycentrics);
    float3 edgeLines = selectedEdges * (1.0 - smoothstep(
        barycentricWidth * 0.25,
        barycentricWidth * max(edgePixels, 0.251),
        barycentricDistance));
    return saturate(max(edgeLines.x, max(edgeLines.y, edgeLines.z))
        * saturate(edgeBakedBoost) * bakedFormatV2 * frostEnabled);
}

float LitIceMeltBoundaryMask(float flameMask, float3 positionWS, float iceScale)
{
    // The transition mask is one inside the heat influence and fades toward
    // zero through Transition Softness. Its middle becomes a deliberately
    // narrow ring of melting ice. Moving world noise breaks the perfect sphere
    // into droplets and small gaps without requiring another texture sample.
    float inner = smoothstep(0.20, 0.38, flameMask);
    float outer = 1.0 - smoothstep(0.56, 0.76, flameMask);
    float animatedNoise = LitIceFBM(
        positionWS * max(0.35, iceScale * 0.7)
        + float3(_Time.y * 0.16, -_Time.y * 0.09, _Time.y * 0.12));
    float brokenRing = lerp(0.62, 1.0, animatedNoise);
    return saturate(inner * outer * brokenRing);
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
    float EnableEmission,
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
    UnityTexture2D CrackTexture,
    float CrackTextureStrength,
    float CrackTextureScale,
    float CrackTextureInvert,
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
    float ReflectionStrength,
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
        EdgeSensitivity, NoiseOffset, MicroScale, CrackWidth,
        1.0 - saturate(CrackTextureStrength), 1.0,
        0.0, EmissionIntensity, EdgeBakedBoost,
        bakedEdgeWidthPixels, frostEnabled,
        iceBaseColor, iceAlpha, iceNormalTS, iceEmission);

    float2 baseUV = LitIceReplacementUV(
        PositionWS, NormalWS, BoundsSize, UV0, UseScaleTiling, TilingMultiplier);
    float2 crackUV = baseUV * max(0.001, CrackTextureScale);
    float4 crackSample = CrackTexture.Sample(BaseSampler, crackUV);
    float crackLuminance = dot(crackSample.rgb, float3(0.2126, 0.7152, 0.0722));
    float textureCracks = lerp(
        crackLuminance, 1.0 - crackLuminance, saturate(CrackTextureInvert));
    // Multiplying by alpha lets transparent sprites work without halos, while
    // ordinary opaque black/white textures remain unchanged.
    textureCracks = saturate(textureCracks * crackSample.a)
                  * saturate(CrackTextureStrength);
    iceBaseColor = lerp(
        iceBaseColor, saturate(CrackColor.rgb), textureCracks * 0.55);
    iceEmission += CrackColor.rgb * textureCracks
                 * max(0.0, EmissionIntensity) * 0.42;
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
    float flameMask = LitIceCombinedFlameMask(
        PositionWS, FlameCenter, FlameInfluenceRadius,
        TransitionSoftness, TransitionProgress);

    // This is deliberately applied after the ice/normal blend: a narrative
    // dissolve therefore removes the same material in either visual state.
    // A stable world-space noise produces a true dissolve rather than a flat
    // alpha fade, while 0 is fully visible and 1 fully clipped.
    float dissolveStrength = saturate(_DissolveStrength);
    // DissolveScale is an authored world-space *size*, not a frequency:
    // 10 must therefore create visibly larger islands than 1.
    float dissolveScale = max(0.01, _DissolveScale);
    float3 dissolvePosition = PositionWS / dissolveScale;
    float proceduralDissolve = frac(sin(dot(dissolvePosition, float3(12.9898, 78.233, 37.719))) * 43758.5453);
    // Project the authored Noise on the three world planes. Sampling only XZ
    // makes a vertical wall look almost uniform; this triplanar blend keeps
    // the pattern readable on walls, floors and props alike.
    float3 dissolveNormal = abs(normalize(NormalWS));
    dissolveNormal /= max(0.0001, dissolveNormal.x + dissolveNormal.y + dissolveNormal.z);
    float shapedDissolve =
        SAMPLE_TEXTURE2D(_DissolveShape, sampler_DissolveShape, dissolvePosition.zy).r * dissolveNormal.x +
        SAMPLE_TEXTURE2D(_DissolveShape, sampler_DissolveShape, dissolvePosition.xz).r * dissolveNormal.y +
        SAMPLE_TEXTURE2D(_DissolveShape, sampler_DissolveShape, dissolvePosition.xy).r * dissolveNormal.z;
    float dissolveNoise = lerp(proceduralDissolve, shapedDissolve, saturate(_DissolveShapeBlend));
    clip((1.0 - dissolveStrength) - dissolveNoise + 0.0001);

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

    // Ice only becomes optically sharp on its actual ridges. The body retains
    // the normal/roughness relief and therefore stays matte in dark areas;
    // this avoids the uniform chrome appearance caused by a single global
    // smoothness value. Texture and baked geometry both contribute to a cold,
    // blue ridge highlight.
    float bakedEdgeMask = LitIceBakedEdgeMask(
        VertexEdgeData, FrostWidth, EdgeBakedBoost);
    float coldEdgeMask = saturate(max(textureEdgeMask, bakedEdgeMask));
    float3 coldEdgeTint = lerp(
        saturate(FrostColor.rgb),
        float3(0.18, 0.56, 1.0),
        0.38);
    iceBaseColor = lerp(iceBaseColor, coldEdgeTint, coldEdgeMask * 0.32);

    // A heated object reveals its authored normal material. The modest warm
    // tint and extra smoothness make that normal state read as damp stone or
    // wood without replacing its base texture, normal map, roughness map or
    // metallic/occlusion behaviour.
    float3 warmWetTint = float3(1.045, 0.955, 0.84);
    revealedBaseColor *= lerp(float3(1.0, 1.0, 1.0), warmWetTint, flameMask * 0.42);
    float meltBoundary = LitIceMeltBoundaryMask(flameMask, PositionWS, IceScale);
    float dropletNoise = LitIceFBM(
        PositionWS * max(1.0, IceScale * 3.8)
        + float3(0.0, -_Time.y * 0.33, _Time.y * 0.11));
    float droplets = meltBoundary * smoothstep(0.58, 0.82, dropletNoise);
    float wetness = saturate(flameMask * 0.16 + meltBoundary * 0.18 + droplets * 0.12);

    // A detailed normal map scatters an IBL/reflection probe. This is
    // physically correct, but it makes a strongly relieved ice wall lose its
    // mirror appearance. HDRP Lit exposes one NormalTS output for both surface
    // lighting and probe reflections, so ReflectionStrength also enables a
    // controlled optical top layer. The texture-edge frost above is still
    // derived from the original normal map, keeping the perceived brick/stone
    // relief visible while the reflection becomes calmer.
    float mirrorLayer = smoothstep(0.25, 1.0, saturate(ReflectionStrength));
    float3 calmIceNormalTS = normalize(lerp(
        float3(0.0, 0.0, 1.0),
        iceNormalTS,
        0.18));
    float3 frostNormalForReflection = normalize(lerp(
        iceNormalWithRelief, calmIceNormalTS, mirrorLayer));

    OutBaseColor = lerp(iceBaseColor, revealedBaseColor, flameMask);
    // The boundary is visible even with emission disabled: tiny warm droplets
    // break the cold/normal line and make a static heat radius feel alive.
    OutBaseColor = lerp(
        OutBaseColor,
        OutBaseColor * float3(1.09, 0.91, 0.72),
        meltBoundary * (0.24 + droplets * 0.18));
    // ShaderGraph_MasterShader is opaque when its dissolve is inactive.
    OutAlpha = lerp(iceAlpha, 1.0, flameMask);
    OutNormalTS = normalize(lerp(
        frostNormalForReflection, baseNormalTS, flameMask));
    // Emission is a material-wide V3 choice. The ice state keeps its procedural
    // frost/crack emission, while the revealed state emits its textured base
    // color. Interpolating with the same flame mask preserves a continuous
    // result throughout the transition. OFF is guaranteed black in both states.
    float emissionEnabled = step(0.5, EnableEmission);
    float3 revealedEmission = revealedBaseColor * max(0.0, EmissionIntensity);
    float3 meltEmission = float3(1.0, 0.16, 0.025)
        * meltBoundary * (0.10 + droplets * 0.08)
        * max(0.0, EmissionIntensity);
    OutEmission = (lerp(iceEmission, revealedEmission, flameMask) + meltEmission)
        * emissionEnabled;
    float stateSmoothness = lerp(
        iceSmoothnessWithRelief, revealedSmoothness, flameMask);
    // HDRP Reflection Probes are sampled by the Lit specular response. The
    // optical layer above calms the Frost normal; this value additionally
    // sharpens the probe itself into a stronger mirror reflection.
    float coldEdgeReflection = coldEdgeMask * (1.0 - flameMask);
    float iceSmoothness = lerp(
        saturate(stateSmoothness), 0.995,
        saturate(ReflectionStrength) * (1.0 - flameMask));
    iceSmoothness = lerp(
        iceSmoothness, 0.985,
        coldEdgeReflection * (0.72 + saturate(ReflectionStrength) * 0.28));
    OutSmoothness = saturate(iceSmoothness + wetness);
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
    half EnableEmission,
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
    UnityTexture2D CrackTexture,
    half CrackTextureStrength,
    half CrackTextureScale,
    half CrackTextureInvert,
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
    half ReflectionStrength,
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
        EnableEmission, EmissionIntensity, EdgeBakedBoost,
        BoundsSize, UV0, BaseTexture, BaseSampler, NormalTexture,
        BaseRoughnessTexture, BaseMetallicTexture, BaseOcclusionTexture,
        CrackTexture, CrackTextureStrength, CrackTextureScale, CrackTextureInvert,
        UseBaseRoughnessTexture, UseBaseMetallicTexture, UseBaseOcclusionTexture,
        BaseColor, BaseNormalStrength, UseScaleTiling,
        TilingMultiplier, FlameCenter, FlameInfluenceRadius, TransitionSoftness,
        TransitionProgress, IceSmoothness, IceMetallic, BaseSmoothness, BaseMetallic,
        IceReliefNormalStrength, IceReliefRoughnessInfluence,
        TextureEdgeStrength, TextureEdgeWidth, TextureEdgeThreshold,
        TextureEdgeNormalInfluence, TextureEdgeRoughnessInfluence, ReflectionStrength,
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
