# Performance Optimization Plan

## Immediate Changes Applied

- Added an opt-in visibility optimization architecture documented in `Docs/VisibilityOptimization.md`: global manager, per-object proxies, camera-player visibility protection, and room light zones.
- Migrated the existing decoration visibility setup to `OptimizableObject` and removed the old dedicated decor culling implementation.
- Changed `SceneLightOcclusionEnforcer` so it no longer scans every `Light` and `Renderer` continuously by default.
- Updated the existing `Maison` scene instance of `SceneLightOcclusionEnforcer` to `enforceContinuously: false`.
- Cached `AgeManager` renderers that support `_AgeAmount`, avoiding repeated material-property scans across all scene renderers on every age recalculation.
- Removed Unity recovery scenes from `Assets/_Recovery`.
- Removed unused source archive `Assets/0 - UnityPackages/Fab/OldWoodenTable/source/model.zip`.
- Moved the UCC demo animator controllers and masks used by player prefabs to `Assets/Opsive/UltimateCharacterController/RuntimeAnimator/Characters`.
- Removed imported sample/demo content that should not be part of runtime imports:
  - `Assets/Samples/Opsive Ultimate Character Controller`.
  - `Packages/com.opsive.ultimatecharactercontroller/Samples~`.
  - The removed Opsive package sample entry in `Packages/com.opsive.ultimatecharactercontroller/package.json`.
  - TextMesh Pro examples and tutorial boilerplate.
  - NVJOB, FullOpaqueSpell, Altar Ruins, and Snow Mountain demo scenes/assets.
- Added `Tools/Lit/Performance/Print Build Dependency Audit` to report which heavy asset roots are actually dependencies of enabled build scenes.

## Main Performance Risks Found

### Runtime CPU

- `SceneLightOcclusionEnforcer` was scanning all scene lights and renderers every second.
- Visibility optimization now covers many decoration roots in `Maison`, `Bridge`, and `BridgeCross`; profile thresholds carefully to avoid visible pop-in/pop-out.
- `AgeManager` scanned all renderers and all materials to apply `_AgeAmount`.
- Several systems rely on global scene searches: `CharacterStateStore`, `BuilderController`, `BuildingInfoInteractable`, `SquadManager`, `ItemPassiveEffectSystem`.
- Many interaction/world UI scripts update positions in `LateUpdate`; this should be limited to visible/active UI only.
- Legacy locomotion scripts still compile and are still referenced by ladder/combat/fallback systems.

### GPU / Rendering

- HDRP real-time shadows and contact shadows are expensive, especially on many point lights/torches/braseros.
- Many imported props and architectural prefabs likely have high material/renderer counts.
- The project contains large displacement/EXR textures that should not ship if unused.
- Outline and XRay passes add render targets and fullscreen work; keep their resolution and layer masks tight.

### Memory / Build Size / Editor Import

- `Assets/0 - UnityPackages` is about 32 GB, with `Fab` around 30 GB.
- `Assets/Samples/Opsive Ultimate Character Controller` was about 1.4 GB before the required animator assets were moved to a project-owned runtime path.
- `Assets/Audio` is about 1.7 GB, including about 1.1 GB of music.
- `Assets/Lucian_CC5_Embed` is about 887 MB.
- Several very large FBX files exist, including `ophiel.fbx`, `decimated.fbx`, and Lucian source FBX files.
- Some large imported libraries remain in `Assets`, including Starter Assets, GalaxyBox2, WorldMaterialsFree, and large source FBX/audio folders. Starter Assets and GalaxyBox2 still have GUID references, so they require a deeper dependency cleanup before deletion.

## Priority Plan

### P0: Stabilize Runtime Cost

- Keep visibility optimization profiles conservative and tune per category before broadening culling distances.
- Keep `SceneLightOcclusionEnforcer.enforceContinuously` disabled. Use `EnforceNow` manually or once on scene load.
- Profile `Maison` in a Development Build with Deep Profiling off. Capture CPU Timeline, Rendering, Memory, and GPU where possible.
- Record baselines: frame time, main thread, render thread, batches, set pass calls, shadow casters, realtime lights, texture memory.

### P1: Replace Global Searches With Registries

- Add registries for `BuildingInfoInteractable`, `Brasero`, `Torch`, `InteractableItem`, and `SquadCharacterController`.
- Replace repeated `FindObjectsByType`, `FindObjectsOfType`, `Resources.FindObjectsOfTypeAll`, and `GameObject.Find` calls in gameplay paths.
- Keep global searches only in explicit initialization, save/load, or editor tools.
- Cache `Camera.main` in UI/interaction scripts and refresh only when the active camera changes.

### P2: Interaction UI / Outline

- Pool interaction boxes instead of instantiating/destroying per object.
- Update world-space UI position only when the box is visible and either camera or target moved enough.
- Keep `RuntimeOutlineUtility.EnsureOutlineTargets` out of hot paths. Prefer preconfigured `RuntimeOutlineTarget` components on prefabs after the dependency audit.
- Avoid recursive layer changes on large object roots; outline the renderable child target rather than whole prefab roots.

### P3: HDRP Lighting And Shadows

- Reduce shadow resolution for non-hero torches/braseros from 1024 to 512 or 256.
- Disable contact shadows except for close-up hero lights.
- Limit realtime point lights; use baked lights/light probes where possible.
- Use shadow layers and culling masks so small props and UI layers do not cast unnecessary shadows.
- Audit `ForcePixel` lights and only keep it where visual quality requires it.

### P4: Asset Library Purge

- Run `Tools/Lit/Performance/Print Build Dependency Audit`.
- Move roots with `buildDeps=0` outside `Assets` or delete them after manual review.
- Likely candidates after audit:
  - TextMesh Pro examples.
  - Imported demo scenes/showcases.
  - Starter Assets demo content.
  - NVJOB demo post-processing examples.
  - Asset-store source zips and source model folders.
- Delete imported samples and demos only after preserving any referenced runtime assets in project-owned folders.

### P5: Texture, Mesh, Audio Import Settings

- Set platform max texture sizes. Large environment textures should usually be 1024 or 2048, not source size.
- Enable texture compression and mipmaps for world textures; disable mipmaps for UI.
- Remove or downscale displacement EXR files if the shaders do not use displacement at runtime.
- Enable mesh compression/read-write off on static meshes where safe.
- Stream long music tracks and compress SFX appropriately.
- Convert duplicated audio/music imports into addressable or streamed content if needed.

### P6: Legacy Locomotion Cleanup

- Remove `CharacterControllerLegacy` only after these dependencies are replaced:
  - `LadderController` use of `StarterInspiredThirdPersonMotor`.
  - `CombatSessionManager` use of `StarterMotorAnimatorDriver`.
  - `RunSpeedCameraEffect` use of `StarterInspiredThirdPersonMotor`.
  - `SquadCharacterController` partial legacy animation/jump/height-probe files.
- Once UCC fully replaces fallback locomotion, delete the legacy scripts and prefab fields together to avoid missing scripts.

## Verification Checklist

- Unity compile in batchmode.
- Enter Play Mode in `Maison`, verify Lucian movement, jump, ladder, torch/brasero, stab reading, item outline, XRay mask.
- Compare Profiler captures before/after:
  - Main thread frame time.
  - Render thread frame time.
  - GC allocation per frame.
  - Shadow caster count.
  - Realtime light count.
  - Texture memory.
- Build once after asset purge and compare build size.
