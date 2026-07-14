# Lit Ice — Frosted Edges (HDRP)

`ShaderGraph_LitIceFrostedEdges` is an HDRP Lit transparent Shader Graph for Unity 6.4 / Shader Graph 17.4. Its optional v2 variant adds a flame-driven transition from ice to a revealed base appearance without changing the original shader.

It combines:

- camera-dependent Fresnel frost for silhouettes and curved contours;
- screen-space curvature response for bevels and smooth shape changes;
- a deterministic per-triangle geometric-edge mask stored as signed barycentrics in vertex colour RGB;
- UV-free world-space procedural cracks, clouding and micro-normal roughness;
- HDR emission for frost/cracks, plus HDRP Lit smoothness for Reflection Probes.

## Quick use

1. Assign `Material_LitIceFrostedEdges` for the original ice, or `Material_LitIceFrostedEdges_v2` for the flame transition.
2. Add or refresh an HDRP Reflection Probe around the object.
3. Enable Bloom in an HDRP Volume to see the emissive glow.
4. For exact hard-edge frost around every individual stone, select the object and run **Lit > Shadergraph > Bake Edge Mask On Selected Meshes**. The tool creates and assigns a mesh copy; the source mesh asset is not modified.
5. To process every renderer in the currently open scenes that uses either the v1 or v2 material, run **Lit > Shadergraph > Bake Edge Mask On All Material_LitIceFrostedEdges**. Shared source meshes are baked only once and reused by their instances.
6. Use **Lit > Shadergraph > Apply Recommended Ice Preset** to restore the balanced dielectric-ice values after experimentation.

Baked mesh assets use compact names such as `IceEdges_a1b2c3d4e5f6.asset` to stay safely below Windows and Git path-length limits, regardless of the source object name.

## Important modelling note

A material shader cannot use Fresnel alone to discover the boundary of every stone inside a combined mesh: Fresnel only describes the camera-facing silhouette of the rendered surface. The V2 baker therefore detects open borders, hard angles and disconnected coincident pieces, then writes signed barycentric data after duplicating triangle corners. The shader uses that data to draw thin internal stone borders without lighting the triangulation diagonals or washing an entire low-poly face white.

## Main controls

- **Ice Deep Color / Transparency**: body tint and opacity.
- **Frost Color / Frost Width / Emission Intensity**: white-blue edge frost and glow.
- **Fresnel Power / Fresnel Intensity**: optional camera-facing silhouette response; intensity is `0` in the recommended preset because the baked stone edges now provide the principal contour.
- **Edge Sensitivity / Edge Baked Boost**: automatic curvature and baked edge contribution.
- **Ice Scale / Micro Scale / Crack Width**: procedural structure.
- **Normal Strength**: micro-asperity normal intensity.
- **Smoothness**: Reflection Probe sharpness.

`IceDeepColor` also supplies a subtle internal colour fill. This prevents a dark
Reflection Probe sample from producing pure-black patches while preserving the
Lit response. Ice is dielectric, so keep **Metallic** at `0`; Reflection Probes
still work through HDRP's dielectric specular reflection.

## Flame transition (v2)

The v2 shader is available at **LIT > Ice > Lit Ice Frosted Edges V2**. Set **Base Texture** to the colour appearance that the flame must reveal and **Normal Texture** to its tangent-space normal map. Both textures use exactly the same coordinates: with **Use Scale Tiling** disabled they use the raw UV0 exactly like `ShaderGraph_MasterShader`; when enabled they use its dominant-face world projection and **Tiling Multiplier**. Import the Normal Texture as a Unity **Normal map**; leaving it empty uses a flat normal.

Every lit regular flame and AncientFlame sends its world centre and real influence radius to compatible renderers. If several flames overlap a renderer, the closest one to the renderer bounds centre is used. Extinguishing a flame or leaving its area resets the v2 radius to zero. Regular flames never write `_AgeCenter` or `_AgeAmount`; the existing MasterShader age behaviour remains exclusive to AncientFlames.

Inside **Flame Influence Radius**, the two material states are independent. The revealed state uses **Base Color**, **Base Texture**, **Normal Texture**, **Base Normal Strength**, **Base Smoothness** and **Base Metallic**. The ice state keeps its own colour, procedural normal, transparency, emission, **Smoothness** and **Metallic**. The revealed output is opaque like the MasterShader and has no frost/crack emission. **Transition Softness** fades back to complete ice outside the radius. **Transition Progress** continuously blends every Lit output: `0` keeps complete ice and `1` reveals the complete base appearance. A radius of `0` disables the transition, including when softness is `0`.

At runtime, each flame animates **Transition Progress** from `0` to `1`. The **Transition Duration** setting on its `LitInfluenceSource` is `5` seconds by default. Extinguishing the flame animates the same value back toward `0`; a duration of `0` makes both directions instantaneous.

The revealed output is fully opaque inside the influence area, although the material itself remains rendered in HDRP's transparent queue so that the ice state can still be translucent.
