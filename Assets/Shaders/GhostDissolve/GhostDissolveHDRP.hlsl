#ifndef LIT_GHOST_DISSOLVE_HDRP_INCLUDED
#define LIT_GHOST_DISSOLVE_HDRP_INCLUDED

float LitGhostHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float LitGhostValueNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);

    float n000 = LitGhostHash31(i + float3(0.0, 0.0, 0.0));
    float n100 = LitGhostHash31(i + float3(1.0, 0.0, 0.0));
    float n010 = LitGhostHash31(i + float3(0.0, 1.0, 0.0));
    float n110 = LitGhostHash31(i + float3(1.0, 1.0, 0.0));
    float n001 = LitGhostHash31(i + float3(0.0, 0.0, 1.0));
    float n101 = LitGhostHash31(i + float3(1.0, 0.0, 1.0));
    float n011 = LitGhostHash31(i + float3(0.0, 1.0, 1.0));
    float n111 = LitGhostHash31(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    float nxy0 = lerp(nx00, nx10, u.y);
    float nxy1 = lerp(nx01, nx11, u.y);
    return lerp(nxy0, nxy1, u.z);
}

void GhostDissolveHDRP_float(
    float3 PositionWS,
    float3 NormalWS,
    float4 BaseColor,
    float4 GhostTint,
    float DissolveAmount,
    float DissolveNoiseScale,
    float DissolveEdgeWidth,
    float4 DissolveEdgeColor,
    float GhostAlpha,
    float DissolveWorldMinY,
    float DissolveWorldHeight,
    float3 DissolveDirection,
    float FineNoiseMultiplier,
    float NoiseInfluence,
    float FresnelPower,
    float FresnelIntensity,
    float DissolveEdgeIntensity,
    float AlphaClipThreshold,
    out float3 OutBaseColor,
    out float OutAlpha,
    out float OutAlphaClipThreshold,
    out float3 OutEmission)
{
    float safeHeight = max(DissolveWorldHeight, 0.0001);
    float safeScale = max(DissolveNoiseScale, 0.0001);
    float safeEdge = max(DissolveEdgeWidth, 0.0001);
    float directionLength = dot(abs(DissolveDirection), float3(1.0, 1.0, 1.0));
    float3 direction = directionLength > 0.0001 ? normalize(DissolveDirection) : float3(0.0, 1.0, 0.0);

    float projected = dot(PositionWS, direction);
    float gradient = saturate((projected - DissolveWorldMinY) / safeHeight);

    float time = _Time.y;
    float3 animatedPosition = PositionWS + float3(time * 0.025, time * 0.045, -time * 0.018);
    float largeNoise = LitGhostValueNoise3D(animatedPosition * safeScale);
    float fineNoise = LitGhostValueNoise3D(animatedPosition * safeScale * max(FineNoiseMultiplier, 1.0) + 17.23);
    float layeredNoise = lerp(largeNoise, fineNoise, 0.38);

    float visibility = saturate(DissolveAmount);

    // DissolveAmount is now a direct visibility factor: 0 invisible, 1 visible.
    // The noisy field only drives the emissive edge so alpha stays linear.
    float dissolveField = saturate(gradient + (layeredNoise - 0.5) * NoiseInfluence);
    float edgeCenter = 1.0 - visibility;
    float edgeMask = 1.0 - saturate(abs(dissolveField - edgeCenter) / safeEdge);
    edgeMask *= visibility * (1.0 - visibility);

    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - PositionWS);
    float fresnel = pow(saturate(1.0 - dot(normalize(NormalWS), viewDir)), max(FresnelPower, 0.0001)) * FresnelIntensity;

    float ghostMix = saturate(GhostTint.a);
    float3 ghostColor = lerp(BaseColor.rgb, GhostTint.rgb, ghostMix);
    OutBaseColor = ghostColor * (0.45 + fresnel * 0.35);
    OutAlpha = saturate(BaseColor.a * GhostAlpha * visibility);
    OutAlphaClipThreshold = 0.0001;
    OutEmission = GhostTint.rgb * fresnel + DissolveEdgeColor.rgb * edgeMask * DissolveEdgeIntensity;
}

void GhostDissolveHDRP_half(
    half3 PositionWS,
    half3 NormalWS,
    half4 BaseColor,
    half4 GhostTint,
    half DissolveAmount,
    half DissolveNoiseScale,
    half DissolveEdgeWidth,
    half4 DissolveEdgeColor,
    half GhostAlpha,
    half DissolveWorldMinY,
    half DissolveWorldHeight,
    half3 DissolveDirection,
    half FineNoiseMultiplier,
    half NoiseInfluence,
    half FresnelPower,
    half FresnelIntensity,
    half DissolveEdgeIntensity,
    half AlphaClipThreshold,
    out half3 OutBaseColor,
    out half OutAlpha,
    out half OutAlphaClipThreshold,
    out half3 OutEmission)
{
    float3 baseColor;
    float alpha;
    float clipThreshold;
    float3 emission;

    GhostDissolveHDRP_float(
        PositionWS,
        NormalWS,
        BaseColor,
        GhostTint,
        DissolveAmount,
        DissolveNoiseScale,
        DissolveEdgeWidth,
        DissolveEdgeColor,
        GhostAlpha,
        DissolveWorldMinY,
        DissolveWorldHeight,
        DissolveDirection,
        FineNoiseMultiplier,
        NoiseInfluence,
        FresnelPower,
        FresnelIntensity,
        DissolveEdgeIntensity,
        AlphaClipThreshold,
        baseColor,
        alpha,
        clipThreshold,
        emission);

    OutBaseColor = half3(baseColor);
    OutAlpha = half(alpha);
    OutAlphaClipThreshold = half(clipThreshold);
    OutEmission = half3(emission);
}

#endif
