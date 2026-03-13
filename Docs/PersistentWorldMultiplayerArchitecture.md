# Persistent World Multiplayer Architecture

This layout treats the world as a persistent gameplay state graph. NGO still handles transport, ownership, and normal network object replication, but late join continuity comes from an explicit host snapshot and deterministic reconstruction pipeline.

## Core Rules

- Host authoritative only: clients send intent, host validates and mutates world state.
- Persistent identity is never `NetworkObjectId`, Unity instance ID, or runtime-generated scene GUIDs.
- Scene-placed objects use a serialized scene persistent ID stored on `PersistentNetworkObject`.
- Runtime-spawned objects use a host-assigned session ID plus a stable runtime prefab ID.
- Late joiners stay blocked until reconstruction finishes.

## Core Components

- `PersistentNetworkObject`
  Stores the stable persistent ID, object kind, runtime prefab ID, transform state, and attached state providers.
- `IPersistentStateProvider`
  Per-feature serializer for gameplay continuity: loot, puzzle progress, braziers, upgrades, dropped loot, custom state.
- `NetworkObjectRegistry`
  Runtime lookup for scene objects and runtime objects by persistent ID. This is the dedupe guardrail.
- `WorldStateManager`
  Captures a full host snapshot and applies snapshots in the required reconstruction order.
- `SnapshotSerializer`
  NGO-safe binary serializer using `FastBufferWriter` and `FastBufferReader`.
- `JoinSyncSystem`
  Uses NGO custom named messages for request, start, chunk, finish, and ready acknowledgements.
- `SpawnManager`
  Host runtime spawning plus controlled reconstruction of runtime-prefab-backed objects.
- `WorldRulesStateManager`
  Snapshot source for derived world variables such as brazier-driven year, time, atmosphere, or environment switches.
- `WorldSaveAdapter`
  Optional binary save/load bridge using the same snapshot format as late join.

## Late Join Reconstruction Order

1. Resolve scene objects by persistent ID.
2. Ensure runtime objects exist from snapshot prefab IDs.
3. Remove runtime objects not present in the snapshot.
4. Apply transforms, rotations, scales, and active state.
5. Apply gameplay state through `IPersistentStateProvider`.
6. Finalize cross-object references and rebuild derived world rules.
7. Release the joining player into gameplay and rebind local control/HUD.

## Why This Avoids The Common Failures

- Persistent ID confusion: `PersistentNetworkObject` separates scene IDs from runtime IDs.
- Transform-only sync: provider blobs carry actual gameplay progression.
- Wrong reconstruction order: `WorldStateManager.ApplySnapshot` enforces the order.
- Duplicated objects: `NetworkObjectRegistry` plus phase 3 cleanup.
- Missing puzzle or building continuity: providers persist solved state and upgrade state, not only spawned prefabs.
- Missing brazier or atmosphere continuity: brazier state and derived world variables are both in the snapshot.
- Incorrect visuals before sync: `JoinSyncSystem` keeps the local client blocked until the snapshot is applied.

## Integration Notes

- Add `PersistentNetworkObject` to players, containers, puzzle roots, braziers, buildable prefabs, dropped loot prefabs, and any persistent interactable.
- Add the feature provider component next to the gameplay component on the same object.
- Add `NetworkObjectRegistry`, `SpawnManager`, `WorldRulesStateManager`, `WorldStateManager`, `JoinSyncSystem`, and optional `WorldSaveAdapter` to the bootstrap runtime object.
- Keep client-side reconstruction for authoritative `NetworkObject`s disabled by default. Let NGO create them, then use the snapshot to validate and hydrate gameplay state.
