# Ghost Dissolve Into Dust Effect

Target: Unity 6, HDRP 17.x, Shader Graph, VFX Graph, animated SkinnedMeshRenderer characters.

## Generated Runtime Script

Script path:

- `Assets/Scripts/VisualEffects/GhostDissolveController.cs`

Main behavior:

- Drives `_DissolveAmount`, `_DissolveNoiseScale`, `_DissolveEdgeWidth`, `_DissolveEdgeColor`, `_GhostAlpha` through `MaterialPropertyBlock`.
- Applies properties to every material slot on every collected `SkinnedMeshRenderer` and `MeshRenderer`.
- Computes character world bounds and sends `_DissolveWorldMinY`, `_DissolveWorldHeight`, `_DissolveDirection`.
- Supports multiple VFX Graph instances through `DustVfxBinding`.
- Sends `OnDissolveStarted`, `OnDissolveFinished`, `OnAppearStarted`, and `OnAppearFinished` UnityEvents.
- Provides `TriggerDissolve()`, `TriggerDissolve(float)`, `TriggerAppear()`, `TriggerAppear(float)`, `StartGhostDissolve()`, `StartGhostAppear()`, `HideInstant()`, `ResetDissolve()`, and `SetDissolveAmount(float)`.

## HDRP Shader Graph Asset

Created assets:

- `Assets/Shaders/GhostDissolve/SG_GhostDissolve_HDRP.shadergraph`
- `Assets/Shaders/GhostDissolve/Material_GhostDissolve.mat`
- `Assets/Shaders/GhostDissolve/GhostDissolveHDRP.hlsl`
- Graph type: HDRP Lit Shader Graph
- Graph Inspector / Target Settings:
  - Active Target: HDRP
  - Material: Lit
  - Surface Type: Transparent
  - Blending Mode: Alpha for a soft ghost, Premultiply for stronger edge glow
  - Preserve Specular Lighting: Off for an ethereal look
  - Sorting Priority: 0 by default, increase only to solve character-specific overlap
  - Transparent Depth Prepass: On
  - Transparent Depth Postpass: On
  - Transparent Writes Motion Vectors: Off unless this effect must contribute to TAA/motion blur
  - Support Decals: Off
  - Receive SSR Transparent: Off
- Alpha Clipping: On
- Render Face: Both if clothing/hair needs it, otherwise Front
- Cast Shadows: Off for pure ghost; On only if the ghost must still shadow
- Receive Shadows: Off or On by art direction

HDRP-specific material notes:

- Keep the master stack Lit, not Unlit, if you want scene fog, exposure, and lighting to affect the ghost.
- Use Emission for the dissolve edge and Fresnel rim; HDRP bloom will carry the cinematic glow.
- If the character becomes too opaque in fog, lower `_GhostAlpha` rather than changing material blending per instance.
- If you see self-sorting artifacts, first enable depth pre/postpass, then split problematic submeshes. Avoid forcing huge Sorting Priority values globally.

### Shader Properties

Required by script:

| Reference | Type | Default | Notes |
| --- | --- | ---: | --- |
| `_DissolveAmount` | Float | -0.08 | Animated to 1.12 |
| `_DissolveNoiseScale` | Float | 2.75 | Large world noise scale |
| `_DissolveEdgeWidth` | Float | 0.055 | Width of glowing ash edge |
| `_DissolveEdgeColor` | HDR Color | (0.35, 0.95, 1, 1) | Cyan edge glow |
| `_GhostAlpha` | Float | 0.68 | Overall transparent character alpha |

Recommended extra properties:

| Reference | Type | Default |
| --- | --- | ---: |
| `_BaseColor` | Color | (0.72, 0.92, 1, 1) |
| `_GhostTint` | Color | (0.25, 0.9, 1, 0.78) |
| `_FresnelPower` | Float | 2.2 |
| `_FresnelIntensity` | Float | 1.9 |
| `_DissolveEdgeIntensity` | Float | 5.5 |
| `_FineNoiseMultiplier` | Float | 5.5 |
| `_NoiseInfluence` | Float | 0.32 |
| `_AlphaClipThreshold` | Float | 0.08 |
| `_DissolveWorldMinY` | Float | set by script |
| `_DissolveWorldHeight` | Float | set by script |
| `_DissolveDirection` | Vector3 | (0, 1, 0) |

### Exact Node Structure

Implemented graph layout:

1. `Position` node set to World -> `GhostDissolveHDRP` custom function `PositionWS`.
2. `Normal Vector` node set to World -> `GhostDissolveHDRP` custom function `NormalWS`.
3. `_BaseColor`, `_GhostTint`, `_DissolveAmount`, `_DissolveNoiseScale`, `_DissolveEdgeWidth`, `_DissolveEdgeColor`, `_GhostAlpha`, `_DissolveWorldMinY`, `_DissolveWorldHeight`, `_DissolveDirection`, `_FineNoiseMultiplier`, `_NoiseInfluence`, `_FresnelPower`, `_FresnelIntensity`, `_DissolveEdgeIntensity`, `_AlphaClipThreshold` -> matching `GhostDissolveHDRP` inputs.
4. `GhostDissolveHDRP.OutBaseColor` -> Lit Master Stack `Base Color`.
5. `GhostDissolveHDRP.OutEmission` -> Lit Master Stack `Emission`.
6. `GhostDissolveHDRP.OutAlpha` -> Lit Master Stack `Alpha`.
7. `GhostDissolveHDRP.OutAlphaClipThreshold` -> Lit Master Stack `Alpha Clip Threshold`.
8. `_Smoothness` -> Lit Master Stack `Smoothness`.
9. `_Metallic` -> Lit Master Stack `Metallic`.

The custom function file contains the world-space layered noise, directional bottom-to-top gradient, Fresnel rim, edge glow, and animated noise drift. This keeps the graph readable while still being a real Shader Graph asset.

Equivalent logic inside `GhostDissolveHDRP.hlsl`:

1. Use World Position from the graph.
2. Project it on `_DissolveDirection`.
3. Subtract `_DissolveWorldMinY`.
4. Divide by `Max(_DissolveWorldHeight, 0.001)`.
5. `Saturate`: this is `height01`.
6. `Split` Position and use XZ as a `Vector2`.
7. Large world-space value noise:
   - UV = World XZ
   - Scale = `_DissolveNoiseScale`
8. Fine world-space value noise:
   - UV = World XZ
   - Scale = `_DissolveNoiseScale * _FineNoiseMultiplier`
9. `Multiply` large by 0.7.
10. `Multiply` fine by 0.3.
11. `Add` large and fine.
12. `Subtract` 0.5 to center noise.
13. `Multiply` by `_NoiseInfluence`.
14. `Add` to `height01`. This is `dissolveField`.

Visibility and edge:

1. `Step`:
   - Edge = `_DissolveAmount`
   - In = `dissolveField`
   - Output = `visibleMask`
2. `Subtract`: `dissolveField - _DissolveAmount`.
3. `Absolute`.
4. `Divide` by `Max(_DissolveEdgeWidth, 0.001)`.
5. `Saturate`.
6. `One Minus`.
7. `Multiply` by `visibleMask`. This is `edgeMask`.

Alpha and clip:

1. `Sampled Alpha` * `_GhostAlpha`.
2. Multiply by `visibleMask`.
3. Connect to Alpha.
4. `_AlphaClipThreshold` connect to Alpha Clip Threshold.

Fresnel and emission:

1. `Fresnel Effect` node:
   - Normal = World Normal
   - View Dir = World View Direction
   - Power = `_FresnelPower`
2. Fresnel * `_GhostTint` * `_FresnelIntensity`.
3. `edgeMask` * `_DissolveEdgeColor` * `_DissolveEdgeIntensity`.
4. Add Fresnel emission and edge emission.
5. Connect to Emission.
6. In HDRP material settings, enable emission contribution to bloom through the color intensity, not by adding point lights.

Optimization:

- Use `Simple Noise`, not high-octave procedural custom noise, for character materials.
- Avoid per-pixel triplanar noise for this effect unless used on hero shots only.
- Keep alpha clip threshold very low so `_GhostAlpha` can fade below 0.5 without prematurely clipping the whole mesh.
- Enable GPU instancing on materials where possible; `MaterialPropertyBlock` keeps material instances shared.
- HDRP transparent depth pre/postpass adds extra passes. Use it for hero characters; disable it for distant crowds.
- In HDRP, transparent overdraw plus bloom is usually the main cost. Keep edge width narrow and dust sprites small.

## VFX Graph Setup

Create:

- `Assets/VFX/VFX_GhostDust_HDRP.vfx`

Exposed properties:

| Name | Type | Default |
| --- | --- | ---: |
| `SourceSkinnedMesh` | Skinned Mesh Renderer | assigned by script |
| `DissolveAmount` | Float | 0 |
| `DissolveWorldMinY` | Float | 0 |
| `DissolveWorldHeight` | Float | 2 |
| `DissolveDirection` | Vector3 | (0, 1, 0) |
| `DissolveNormalizedTime` | Float | 0 |
| `SpawnRateMultiplier` | Float | 1 |
| `DissolveEdgeColor` | Vector4/Color | cyan |
| `BandWidth` | Float | 0.09 |
| `BaseSpawnRate` | Float | 2200 |
| `DustSpeed` | Float | 0.75 |
| `DissolveNoiseScale` | Float | 2.75 |

### Contexts and Blocks

Spawn context:

- Event: `OnDissolveStart`
- Constant Spawn Rate:
  - `BaseSpawnRate * SpawnRateMultiplier`
- Stop event: `OnDissolveFinish`
- Optional: multiply spawn rate by `smoothstep(0.05, 0.85, DissolveNormalizedTime) * (1 - smoothstep(0.9, 1, DissolveNormalizedTime))`

Initialize Particle:

- Capacity: 16000 for hero characters, 6000 for NPCs
- Set Lifetime: Random 1.3 to 3.2
- Set Size: Random 0.012 to 0.055
- Set Position (Skinned Mesh):
  - Source = `SourceSkinnedMesh`
  - Mode = Surface
- Sample Skinned Mesh normal.
- Compute same dissolve band:
  - `height01 = saturate((position.y - DissolveWorldMinY) / DissolveWorldHeight)`
  - Add large/fine noise approximation.
  - `band = abs((height01 + noise) - DissolveAmount) < BandWidth`
- Kill or Set Alive false when outside band.
- Set Velocity:
  - normal * random(0.05, 0.32)
  - plus up * random(0.25, 0.9)
  - plus random tangent drift * 0.2
- Set Color:
  - lerp dark ash `(0.22, 0.22, 0.2)` to `DissolveEdgeColor.rgb`, random 0 to 0.35.

Update Particle:

- Turbulence / Curl Noise Force: 0.35 to 0.75
- Drag: 1.2 to 2.0
- Gravity: (0, -0.04, 0)
- Age over Lifetime -> alpha fade curve:
  - 0: 0
  - 0.1: 0.85
  - 0.75: 0.35
  - 1: 0
- Optional slight size over lifetime: 1.0 to 0.35.

Output Particle Quad:

- HDRP Output Particle Quad, preferably Unlit for ash readability
- Blend: Alpha for ash, Additive Alpha only for a secondary glowing ember/dust output
- Sort: Youngest in front or by distance
- Soft Particles: On if using depth texture
- Texture: small soft ash/smoke sprite
- HDRP lighting:
  - Use Unlit for dark ash and color it manually.
  - Use Lit only if dust must catch strong local lights; it is more expensive and can look noisy.
- Optional second output:
  - Additive tiny cyan motes, lifetime 0.4 to 1.1, spawn rate 10% of ash, color from `DissolveEdgeColor`.

For multiple SkinnedMeshRenderers, add one child VFX object per sampled renderer and create one `DustVfxBinding` for each.

## Unity Hierarchy

Recommended structure:

```text
Character_GhostTarget
  Animator
  GhostDissolveController
  Armature
  Renderers
    Body_SkinnedMeshRenderer
    Clothes_SkinnedMeshRenderer
    Hair_SkinnedMeshRenderer
  VFX
    GhostDust_Body     (VisualEffect, SourceSkinnedMesh = Body)
    GhostDust_Clothes  (VisualEffect, SourceSkinnedMesh = Clothes)
  Audio
    DissolveAudioSource
```

## Inspector Setup

On `GhostDissolveController`:

- Renderer Root: `Renderers` or the character root.
- Explicit Renderers: optional; leave empty if auto collect is enabled.
- Noise Scale: 2.75.
- Edge Width: 0.055.
- Edge Color: HDR cyan.
- Start Ghost Alpha: 0.68.
- Duration: 2.8.
- Disable Renderers On Finish: enabled.
- Deactivate GameObject On Finish: optional.
- Destroy GameObject On Finish: optional.
- Dust VFX Bindings:
  - Body VFX -> Body SkinnedMeshRenderer.
  - Clothes VFX -> Clothes SkinnedMeshRenderer.

On each character material:

- Use `Material_GhostDissolve` or a material using `SG_GhostDissolve_HDRP`.
- Tune `_BaseColor` and `_GhostTint` per character/material slot.
- Do not instantiate materials in code.
- If the character has several material slots, assign a compatible material to every slot that must dissolve.

## Triggering

Proximity reveal:

- `GhostController` drives proximity dissolve by default.
- When no controlled character is in range, targets stay at dissolve amount max
  (`1.12` by default).
- When the character enters the proximity range, targets lerp toward dissolve
  amount `0`.
- The lerp duration is `Proximity Dissolve Lerp Duration`, default `1`.
- If `Proximity Dissolve Targets` is empty, the `GhostController` GameObject is
  used as the target. The controller can add `GhostDissolveController` at runtime
  if the target does not already have one.

Gameplay:

```csharp
GhostDissolveController dissolve = target.GetComponent<GhostDissolveController>();
dissolve.TriggerDissolve();
```

Knowledge-driven:

- Add an ID in `GhostKnowledgeReaction.triggerEffectIds`, for example
  `luc_dissolve`.
- On the scene `GhostController`, add a `dissolveEffectRules` entry with the same
  `effectId`.
- Fill `targetObjects` with the GameObjects whose renderers should dissolve.
- Optional: add a `KnowledgeRequirement` on the rule if the scene effect should
  double-check the player's knowledge.

Current content example: `GhostData_Luc` requires `Knowledge_JonLocation` and
triggers `luc_dissolve` when the player talks to Luc with that knowledge.

With custom duration:

```csharp
dissolve.TriggerDissolve(3.5f);
```

Appearance:

```csharp
GhostDissolveController dissolve = target.GetComponent<GhostDissolveController>();
dissolve.HideInstant();
dissolve.TriggerAppear();
```

Appearance with custom duration:

```csharp
dissolve.TriggerAppear(2.2f);
```

Animation Event:

- Add an Animation Event on the desired frame.
- Function name for disappearance: `StartGhostDissolve`.
- Function name for appearance: `StartGhostAppear`.

Syncing VFX:

- The controller sends `DissolveAmount`, bounds, direction, alpha, normalized time, and `SourceSkinnedMesh`.
- The VFX Graph should emit only where its sampled mesh band matches `DissolveAmount`.

Collider trigger:

- Add a Collider on the same GameObject as `GhostDissolveController`.
- Enable `Is Trigger` on that Collider.
- Enable `Trigger With Collider` in the controller.
- By default, only colliders belonging to a `SquadCharacterController` trigger the effect.
- On enter: the ghost calls `TriggerAppear()`.
- On exit of the last character collider: the ghost calls `TriggerDissolve()`.
- Keep `Deactivate GameObject On Finish` and `Destroy GameObject On Finish` disabled for trigger-driven ghosts, otherwise the trigger cannot detect a later re-entry.

## Common Pitfalls

- Transparent sorting: HDRP transparent meshes still sort per renderer, not per triangle. Use Transparent Depth Prepass/Postpass and split huge overlapping character meshes when needed.
- HDRP bloom/exposure: high `_DissolveEdgeIntensity` can blow out under automatic exposure. Clamp edge intensity per scene lighting setup.
- Alpha clip threshold: keep it near 0.001 so fading does not clip the whole ghost early.
- Skinned mesh bounds: enable `Update When Offscreen` on important SkinnedMeshRenderers if the dissolve can happen off camera or during large poses.
- VFX Graph source: each VFX Graph Skinned Mesh property samples one renderer. Multi-renderer characters need multiple VFX bindings.
- Property names must match exactly. Shader Graph uses underscore names; VFX Graph properties in this setup do not.
- Mobile: GPU VFX Graph and transparent overdraw are expensive. Reduce particles, avoid HDR edge intensity, and prefer baked flipbook dust on low-end devices.
- Depth prepass cost: leave it on for cinematic closeups; consider disabling it for low-importance enemies.

## Final Recommended Cinematic Setup

- Duration: 2.8 seconds.
- `_DissolveAmount`: -0.08 to 1.12.
- `_GhostAlpha`: 0.68 to 0.0.
- `_DissolveNoiseScale`: 2.75.
- `_FineNoiseMultiplier`: 5.7.
- `_NoiseInfluence`: 0.28.
- `_DissolveEdgeWidth`: 0.055.
- `_DissolveEdgeIntensity`: 4.0.
- `_FresnelPower`: 4.5.
- `_FresnelIntensity`: 1.4.
- Edge color: HDR `(0.35, 0.95, 1.0, 1.0)`.
- HDRP material: Transparent, Alpha, Alpha Clip on, Depth Prepass on, Depth Postpass on, Preserve Specular off.
- VFX base spawn rate: 2200 for hero, 900 for NPC.
- Particle lifetime: 1.3 to 3.2 seconds.
- Turbulence: 0.55.
- Drag: 1.6.
- Disable renderers after finish, but delay object deactivation by at least 0.75 seconds if VFX is a child.
