# CharacterEffect - compiled surface sampling

## Actual failure and repair

The live Lucian instance emitted particles and had a valid 12-byte GPU buffer with 10,652 samples and 3,886 distinct triangle indices. This ruled out an empty source mesh or an uninitialized sampling buffer.

The shader compiled for Character Local Connect did not consume the sampling buffer at all: it sampled one uniform vertex offset. The source subgraph already specified Surface/Barycentric sampling and had its buffer connections intact. A targeted force-import of Character Mesh Spawn and its five consumers regenerated the correct shader with particle-dependent triangle and barycentric reads. No provider graph settings or artistic prefab values were rewritten.

## Durability

CharacterEffectSamplingValidator checks the actual generated shader on Editor startup (once per session) and before build. A stale shader is rebuilt through the targeted repair command on startup; build validation rejects an effect that ignores the surface buffer. The repair and validator are also available under Lit/VFX. Six NUnit regression cases were added; they are compiled but were not executed through the Test Runner in this turn. The equivalent five live graph validation checks all passed.

## Verification

All five compiled graphs passed. A Play Mode laboratory capture used the actual Lucian model/rig, Holy prefab and HDRP renderer. The image confirms flames distributed on the body and limbs instead of one point by the head. The laboratory actor is seated in this capture; its pose/controller was not repaired or changed by this task. Enemy renderers were hidden only during the synchronous capture, then restored. The temporary camera was destroyed and the previous Play-start scene/pause setting restored.

Body-after.png, LiveReport.txt and CompiledShaderComparison.json contain the evidence. The prior lifecycle tests alone did not cover this shader-consumption failure; the new contract closes that gap. Existing runtime activation behavior and manual gameplay ownership were preserved.
