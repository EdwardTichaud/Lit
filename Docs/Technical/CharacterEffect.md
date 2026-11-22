# CharacterEffect

## Activation

CharacterEffect owns its initialization. On playable characters (SquadCharacterController) and their child equipment, activation and showing the source Renderer never start the effect automatically, even if Play On Enable is checked. Use StartEffect / PlayEffect_CharacterEffect and StopEffect / StopEffect_CharacterEffect. SpiritBond owns Holy activation during fusion. Other objects retain automatic playback when Play On Enable is checked. Runtime lifecycle repair does not require a global scene scan.

## Surface sampling after Unity upgrades

A valid CPU/GPU buffer and a positive particle count are not sufficient: the generated VFX shader must read per-particle triangle indices and barycentric coordinates. A stale compiled graph can instead sample one constant vertex and concentrate the effect near the head even though its source graph is correct.

The Editor checks the five CharacterEffect graphs once per session and repairs this stale compilation through targeted imports. The build validator rejects missing surface sampling. No graph/mesh/animation artistic settings are automatically retuned.

Available commands under Lit/VFX:

- Audit Character Effects (Read Only): live source mesh, GPU sample and generated shader reports in Library/CharacterEffectDiagnostics.
- Recompile Character Effect Graphs: targeted block/graph regeneration, without Reimport All.
- Validate Compiled Character Sampling: verifies actual generated shader consumption.
- Verify Body Effect In Laboratory: bounded Play Mode capture on the actual actor, with restoration of the previous Play-start scene and pause state. It does not save scene changes.

Evidence for the 22 November 2026 repair is in Docs/Validation/2026-11-22/CharacterEffectSampling.
