# CharacterEffect - lifecycle repair

## Findings

- UniformMeshSample.OnEnable returned before mesh initialization when a property binder was absent.
- OnEnable and Start both reset the graph. A caller starting an effect immediately after spawning could lose its play event in Start. BrokenAnchorLightBall had a deferred restart to compensate for this.
- The scene-wide repair skipped effects that already had a VisualEffect reference and did not own dynamic/pool activation.
- Legacy CharacterEffect events and the base Start/Stop API maintained different states.
- Unity 6000.6 reported a non-serializable sampling structure. The GPU buffer itself must not be serialized.

## Current contract

CharacterEffect initializes its own instance, mesh, binder and GPU samples. It plays on enable and when its source Renderer is shown, in Edit Mode as well as Play Mode. Disable **Play On Enable** in the CharacterEffect Inspector for manual control. Explicit Stop stays respected until the next automatic activation/show event.

The graph is initialized once per enable/setup, before buffer binding. There is no delayed Start reset and no global scene scan. CharacterEffectRuntimeRepair remains a small compatibility entry point for existing callers. SpiritBond explicitly owns Holy playback; the projectile uses the central setup instead of a delayed restart.

Existing effect prefabs, artistic settings, source meshes and user scene/prefab changes were preserved.

## Verification

Unity 6000.6.4f1, Direct3D12: 12 Edit Mode tests passed (7 lifecycle/GPU cases and 5 existing graph binding cases). Checks include activation, pooling, component re-enable, renderer visibility, manual opt-out, legacy events, missing binder recovery, CPU/GPU sampling round-trip, serialization, and both actual Lucian body/sword source meshes. Runtime and Editor compilation passed. Final gameplay caller adjustments also passed standalone compilation with Unity's current response file.

The actual rendered effect on an animated character in the user's scene has not been visually certified by these tests. No full Play Mode gameplay sequence was run.
