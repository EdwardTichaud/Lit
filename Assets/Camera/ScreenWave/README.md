# ScreenWave

`ScreenWaveController` drives a pre-existing HDRP `CustomPassVolume` and does
not create scene objects at runtime. In `Maison`, select `BattleManager`, then
use the `ScreenWaveController` component button named `PlayScreenWave` to
preview the full forward-then-reverse wave cycle in Edit Mode.

Scene requirement:

- a child `CustomPassVolume` configured as global;
- one `FullScreenCustomPass` named `Screen Wave`;
- `MAT_ScreenWave` assigned to the pass and to `ScreenWaveController`.

Other scripts can trigger the full cycle with `PlayScreenWave()`,
`PlayScreenWave(Vector2 viewportOrigin)`,
`PlayScreenWaveCycle(Vector2 viewportOrigin)` or
`TryPlayScreenWave(Vector3 worldOrigin)`. The Custom Pass stays active between
the forward phase (`reverse = false`) and the reverse phase (`reverse = true`),
then fades out once the reverse phase is complete.
Use `PlayScreenWavePhase(...)` or `PlayInverseScreenWave(...)` only when a script
needs to play a single phase manually.
