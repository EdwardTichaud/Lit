# ScreenWave

`ScreenWaveController` drives a pre-existing HDRP `CustomPassVolume` and does
not create scene objects at runtime. In `Maison`, select `BattleManager`, then
use the `ScreenWaveController` component button named `PlayScreenWave` to
preview one wave in Edit Mode.

Scene requirement:

- a child `CustomPassVolume` configured as global;
- one `FullScreenCustomPass` named `Screen Wave`;
- `MAT_ScreenWave` assigned to the pass and to `ScreenWaveController`.

Other scripts can trigger the effect with `PlayScreenWave()`,
`PlayScreenWave(Vector2 viewportOrigin)` or `TryPlayScreenWave(Vector3 worldOrigin)`.
`StopScreenWave()` starts the configured fade-out instead of cutting the custom
pass immediately.
Use `PlayInverseScreenWave()` or `PlayScreenWave(origin, true)` for the reverse
wave used to return from the distorted transition state to the normal camera
image.
