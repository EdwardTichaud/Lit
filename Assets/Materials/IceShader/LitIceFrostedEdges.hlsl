#ifndef LIT_ICE_FROSTED_EDGES_INCLUDED
#define LIT_ICE_FROSTED_EDGES_INCLUDED

float LitIceHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float LitIceNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);

    float n000 = LitIceHash31(i + float3(0, 0, 0));
    float n100 = LitIceHash31(i + float3(1, 0, 0));
    float n010 = LitIceHash31(i + float3(0, 1, 0));
    float n110 = LitIceHash31(i + float3(1, 1, 0));
    float n001 = LitIceHash31(i + float3(0, 0, 1));
    float n101 = LitIceHash31(i + float3(1, 0, 1));
    float n011 = LitIceHash31(i + float3(0, 1, 1));
    float n111 = LitIceHash31(i + float3(1, 1, 1));

    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    return lerp(lerp(nx00, nx10, u.y), lerp(nx01, nx11, u.y), u.z);
}

float LitIceFBM(float3 p)
{
    float value = 0.0;
    float weight = 0.55;
    value += LitIceNoise3D(p) * weight;
    p = p * 2.03 + 11.7;
    weight *= 0.5;
    value += LitIceNoise3D(p) * weight;
    p = p * 2.01 + 7.9;
    weight *= 0.5;
    value += LitIceNoise3D(p) * weight;
    return value / 0.9625;
}

float LitIceCracks(float3 p, float width)
{
    float coarse = 1.0 - abs(LitIceFBM(p) * 2.0 - 1.0);
    float fine = 1.0 - abs(LitIceFBM(p * 2.73 + 19.1) * 2.0 - 1.0);
    float ridge = max(coarse, fine * 0.88);
    return smoothstep(1.0 - saturate(width), 1.0, ridge);
}

void LitIceFrostedEdges_float(
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
    out float3 OutBaseColor,
    out float OutAlpha,
    out float3 OutNormalTS,
    out float3 OutEmission)
{
    float safeScale = max(IceScale, 0.001);
    float3 p = PositionWS * safeScale + NoiseOffset;
    float3 n = normalize(NormalWS);
    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - PositionWS);

    float cloud = LitIceFBM(p * 0.42);
    float detail = LitIceFBM(p * max(MicroScale, 1.0));
    float cracks = LitIceCracks(p * 0.73, max(CrackWidth, 0.001));

    float fresnel = pow(saturate(1.0 - dot(n, viewDir)), max(FresnelPower, 0.001));
    fresnel = saturate(fresnel * FresnelIntensity);

    // This catches bevels and smoothly varying curvature. Fresnel remains a
    // camera-facing silhouette effect; it cannot discover borders between the
    // disconnected stones contained in a single renderer.
    float curvature = saturate(length(fwidth(n)) * max(EdgeSensitivity, 0.0));
    float automaticFrost = smoothstep(
        1.0 - saturate(FrostWidth),
        1.0,
        max(fresnel, curvature));

    // The edge baker duplicates triangle corners and stores signed barycentrics
    // in vertex colour RGB. Positive channels identify only selected geometric
    // edges; negative channels suppress internal triangulation diagonals. Using
    // screen-space derivatives keeps the line thin even on a four-vertex face.
    float3 signedBarycentrics = VertexEdgeData.rgb;
    float bakedFormatV2 = 1.0 - step(0.01, abs(VertexEdgeData.a - 0.25));
    float3 barycentricDistance = abs(signedBarycentrics);
    float3 barycentricWidth = max(fwidth(barycentricDistance), 0.00001);
    float edgePixels = lerp(0.9, 5.0, saturate(FrostWidth));
    float3 selectedEdges = step(0.0, signedBarycentrics);
    float3 edgeLines = selectedEdges * (1.0 - smoothstep(
        barycentricWidth * 0.25,
        barycentricWidth * edgePixels,
        barycentricDistance));
    float bakedFrost = max(edgeLines.x, max(edgeLines.y, edgeLines.z))
                     * saturate(EdgeBakedBoost) * bakedFormatV2;
    float frost = saturate(max(automaticFrost, bakedFrost));

    // Keep a real deep-ice colour floor. Previously the procedural modulation
    // could make the Lit base extremely dark; with a strong specular response
    // that looked like black holes and made IceDeepColor appear ineffective.
    float3 body = IceDeepColor.rgb * lerp(0.68, 1.22, cloud);
    body += IceDeepColor.rgb * detail * 0.12;
    body = max(body, IceDeepColor.rgb * 0.62);
    body = lerp(body, saturate(FrostColor.rgb), frost * 0.92);
    body = lerp(body, CrackColor.rgb, cracks * 0.55);

    float epsilon = 0.035 / safeScale;
    float height0 = LitIceFBM(p * max(MicroScale, 1.0));
    float heightX = LitIceFBM((p + float3(epsilon, 0, 0)) * max(MicroScale, 1.0));
    float heightY = LitIceFBM((p + float3(0, epsilon, 0)) * max(MicroScale, 1.0));
    // Retain visible asperities without sending half of the reflected rays into
    // black parts of the environment probe.
    float2 slope = float2(height0 - heightX, height0 - heightY)
                 * min(max(NormalStrength, 0.0), 2.0) * 4.25;

    // A subtle coloured internal fill guarantees that a dark Reflection Probe
    // sample remains deep blue instead of becoming pure black. Because this uses
    // the HDR IceDeepColor directly, changing that property now has an immediate
    // and clearly visible influence on the body of the ice.
    float deepFillMask = saturate(1.0 - frost * 0.72);
    float3 deepColorFill = IceDeepColor.rgb
                         * lerp(0.38, 0.72, cloud)
                         * deepFillMask;

    OutBaseColor = saturate(body);
    OutAlpha = saturate(Transparency + fresnel * (1.0 - Transparency) * 0.4);
    OutNormalTS = normalize(float3(slope, 1.0));
    OutEmission = deepColorFill
                + FrostColor.rgb * frost * EmissionIntensity
                + CrackColor.rgb * cracks * EmissionIntensity * 0.42;
}

void LitIceFrostedEdges_half(
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
    out half3 OutBaseColor,
    out half OutAlpha,
    out half3 OutNormalTS,
    out half3 OutEmission)
{
    float3 baseColor;
    float alpha;
    float3 normalTS;
    float3 emission;
    LitIceFrostedEdges_float(
        PositionWS, NormalWS, IceDeepColor, FrostColor, VertexEdgeData,
        IceScale, FrostWidth, CrackColor, Transparency, NormalStrength,
        EdgeSensitivity, NoiseOffset, MicroScale, CrackWidth, FresnelPower,
        FresnelIntensity, EmissionIntensity, EdgeBakedBoost,
        baseColor, alpha, normalTS, emission);
    OutBaseColor = half3(baseColor);
    OutAlpha = half(alpha);
    OutNormalTS = half3(normalTS);
    OutEmission = half3(emission);
}

#endif
