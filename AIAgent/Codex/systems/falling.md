# Falling (Legacy)

## Status

The Falling prototype is archived under `Assets/FallingPhase_Legacy/`. It is no
longer an active gameplay system and its scene is not in the Build Settings.
The shared `Falling` Action Map remains only so the archived scene can still be
opened and tested manually.

## Role

Provide a standalone Traversal prototype for Ancient Flame breaches: Lucian moves
through a high-speed falling corridor, avoids geometry and accumulates a rank.

## Runtime

- `FallingPlayerController`: enables only the `Falling` Action Map, moves Lucian
  on two screen-facing axes with inertia. Holding `Accelerate` slows Lucian,
  moves his visual backward and pushes the camera in; releasing it triggers a
  temporary, non-linear boost whose strength depends on charge. Charge is hard
  capped at two seconds and auto-releases at full charge; boost duration stays
  fixed. The Animator enters `BoostCharge` while the input is held, using the
  backward-movement fallback clip until a dedicated clip is assigned. The model leans
  into steering and acceleration. `movementBounds`
  provides hard left/right and up/down limits around Lucian's starting point.
- `FallingObstacleSpawner`: instantiates primitives ahead of the camera and
  destroys them once they are behind the player.
- `FallingRotator`: animates an orbiting visual and its own rotation.
- `FallingPlayerController` converts collision speed into a temporary speed
  reduction; impacts do not end the run. A valid collision keeps the hitbox
  active for 0.5 seconds to sell the impact, then disables it for 0.5 seconds
  so an obstacle cannot block the run.
- `FallingGrapplePoint` marks reusable traversal anchors. `FallingGrappleController`
  selects the valid point closest to the screen centre, only between its minimum
  and maximum distance, then shows its world-space input prompt and glow. The
  `Grapple` action uses gamepad North Button or keyboard `E`, shows a short
  `LineRenderer` tether, and launches Lucian strictly along the falling axis.
  Grapple impulse, cooldown, camera movement, SFX and voice all have dedicated
  serialized settings. `FallingObstacleSpawner` converts 10% of its spawned
  obstacles into grapple points and applies `Material_Grapple` to them.
- `FallingRunScore`: combines distance, sustained acceleration and impact
  penalties into ranks `F` through `SSS`.
- `FallingCameraRig`: makes speed readable through progressive FOV, pull-back,
  roll, acceleration pitch, restrained movement and a short impact shake. Its
  `oppositeMovementAmplitude` offsets camera X/Y against Lucian's steering.
  Charge and boost each expose independent camera-distance and FOV contrast
  multipliers. Charge quickly converges on the configurable `leftShoulderOffset`
  relative to Lucian; it no longer uses a charge push distance. Its
  `chargeApproachLerp` ranges from instant (`0`) to very slow (`1`). During the
  charge, `boostChargeCameraMoveSpeed` moves this shoulder target in local
  units per second. The charge shoulder, follow distance and FOV each blend
  continuously, while speed breathing is suppressed during charge and boost.

## Assets

`FallingPhaseSceneBuilder` creates `Assets/FallingPhase_Legacy/FallingPhase.unity`, the two
primitive obstacle prefabs and `Player_Model_Falling.controller`. The controller
is copied from `Player_Model` and adds `Falling_Loop` (`Mixamo_Flying`),
`Falling_Boost` (`Anim_SF_Strike_Fly`) and `Falling_Impact`
(`Anim_SF_Get_Hit_Hard`) from the Raise Creation pack. `BoostCharge` initially
uses `Anim_SF_Moving_Backward` as a replaceable fallback.
Use `Lit/Legacy/Falling/Refresh Falling Animator` after importing the scripts to add the
replaceable `Falling_Grapple` state and its `FallingGrapple` trigger.

## Notes

The scene is a direct test scene, not yet an Ancient Flame transition. It uses
the `Falling` map in `PlayerInputs.inputactions`: left stick/WASD to move and
right trigger/left shift to charge a temporary boost, then release it.
