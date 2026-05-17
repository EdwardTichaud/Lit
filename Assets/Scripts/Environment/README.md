# HDRP Environment System

This system replaces stacked local Unity Volumes with one local `EnvironmentManager`
that drives one or more Global HDRP Volumes.

## Setup

1. Create normal HDRP Volume Profile assets with the overrides you need.
2. Add one `EnvironmentManager` in the scene or on the local player prefab.
3. Leave `Use Controlled Character As Target` enabled. The manager resolves
   `LocalPlayerContext.LocalCharacterRoot` / `LocalPlayerUtils.GetControlledCharacter()`
   automatically and follows the character controlled by the local player.
4. Assign one or more Global `Volume` components to `Global Volumes`.
5. Assign a default HDRP Volume Profile.
6. Add `EnvironmentZone` components to scene objects with trigger colliders.
7. Assign a source HDRP Volume Profile, priority, optional weight, and `Blend Distance`
   to each zone.

The manager reads the source HDRP Volume Profiles but never writes into them.
Only the runtime profile instance of the assigned Global Volume is modified.
Every `VolumeComponent` and every overridden `VolumeParameter` in the source
profiles is supported. Continuous parameters fade through Unity's own Volume
interpolation; discrete parameters such as enums, booleans, textures, and object
references switch according to their parameter type.

## Multiplayer

The manager is intentionally client-side only. Enable or instantiate it only for
the local player/camera. Do not synchronize visual profile values over the
network. Remote players should not drive the local camera environment.
The target is rebound whenever `LocalPlayerContext.LocalCharacterChanged` fires,
and is also checked every frame to handle late spawns or character swaps.

## Zone Selection

The manager samples the local target position every frame, so camera targets do
not need colliders or rigidbodies. `Blend Distance` creates a soft band around
each zone collider, so zone influence fades in before the boundary and reaches
full strength after the target is deeper inside. The manager also smooths zone
weights over time, then applies active zones from lowest priority to highest
priority. Ties use the highest weight, then the most recently entered zone for
debug/current-zone reporting.

## Temporary Profiles

Use `ForceProfile(profile, duration, intensity)` for cinematics, storms, or
scripted moments. Use `ClearForcedProfile()` to return to normal zone selection.
