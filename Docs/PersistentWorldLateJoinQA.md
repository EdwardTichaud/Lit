# Persistent World Late-Join QA

Use this checklist against the real NGO host/client flow after scene load, host save/reload, and client late join. Keep the Unity console open and filter on `[PersistentWorld]`.

## Core Pass Criteria

- The joining client stays behind the sync blackout until reconstruction completes.
- The console shows the full phase sequence in order:
  `client connected` -> `snapshot requested` -> `snapshot sent` -> `snapshot received` -> `resolve scene objects` -> `spawn missing runtime objects` -> `remove invalid objects` -> `apply transforms and active states` -> `apply gameplay state` -> `finalize references` -> `release gameplay`
- No `persistent ID collision`, `snapshot resolve failed`, `provider payload invalid`, `restore order issue`, `failed to reconstruct runtime object`, or `duplicated ... reconstruction` errors appear in a passing run.
- `snapshot apply summary success=true` is logged on the joining client.

## Scenario Checklist

### Fresh host session -> modify world -> client joins

1. Host starts a fresh session and modifies at least two persistent systems before any remote client joins.
2. Client joins after those world changes.
3. Verify the host logs include `fresh-session late-join synchronization request`, `fresh-session late-join synchronization preparing snapshot`, and `fresh-session late-join synchronization snapshot sent`.
4. Verify the client remains behind the blackout until `release gameplay` is logged.
5. Verify the client sees the modified world state immediately on release, not default scene state.

### Multiple clients join at different times after world changes

1. Host changes persistent world state.
2. Client A joins and completes synchronization.
3. Host changes the world again.
4. Client B joins after the second set of changes.
5. Verify Client A keeps the correct live world through normal host-authoritative gameplay replication.
6. Verify Client B receives a new snapshot that includes the later world changes.
7. Verify the host logs one completed transfer per joining client and no `late-join snapshot ready ack transfer mismatch`, `late-join snapshot transfer replaced`, or unresolved snapshot errors during the passing run.

### Container partially looted -> join

1. Host opens a scene container and removes only part of its contents.
2. Client joins after the loot change.
3. Verify the client sees the reduced loot contents, not the original defaults.
4. Verify the console includes `validation scenario=container_partial_loot success=true`.

### Puzzle partially solved -> join

1. Host toggles one or more levers on an integrated puzzle without resetting it.
2. Client joins mid-progress.
3. Verify lever states and solved/unsolved state match the host exactly.
4. Verify the console includes `validation scenario=puzzle_progress success=true`.

### Brazier lit -> join

1. Host lights a brazier and lets the linked world rules update.
2. Client joins after the brazier/world change.
3. Verify the brazier lit state, year/time progression, stage roots, and active volume profiles match the host.
4. Verify the console includes `validation scenario=brazier_world_rules success=true` and `validation scenario=world_rules_extended success=true`.

### Building placed -> join

1. Host places one integrated runtime building.
2. Client joins after placement.
3. Verify the building exists exactly once, has the correct transform, and is registered in the builder state.
4. Verify the console includes `validation scenario=building_placement success=true`.

### Building upgraded -> join

1. Host upgrades an integrated runtime building.
2. Client joins after the upgrade.
3. Verify the client sees the correct level, building identity, and any container state under that building.
4. Verify the console includes `validation scenario=building_upgrade success=true`.

### Treasure discovered -> join

1. Host discovers one treasure/knowledge entry.
2. Client joins after discovery.
3. Verify the knowledge/treasure state is already unlocked on join.
4. Verify the console includes `validation scenario=treasure_found success=true`.

### Dropped loot spawned -> join

1. Host drops an item into the world so it becomes runtime persistent dropped loot.
2. Client joins while the drop still exists.
3. Verify the dropped loot appears exactly once with the correct item payload.
4. Verify the console includes `validation scenario=dropped_loot success=true`.
5. Verify there is no `duplicate dropped-loot reconstruction avoided` warning unless a deliberate duplicate-protection path is being exercised.

### TrouEtroit detected -> join

1. Host detects an integrated `TrouEtroit` / secret passage.
2. Client joins after detection.
3. Verify the secret passage is already in the detected state on the client.
4. Verify the console includes `validation scenario=interactable_custom_state success=true`.

### Save host -> reload host -> client join

1. Host changes multiple persistent systems: loot, puzzle, brazier, building, treasure, dropped loot.
2. Host saves, leaves, reloads from the real save path, and becomes host again.
3. Client joins after the reload.
4. Verify the restored host world and the late-joining client world match.
5. Verify the host logs include `host world restore` and `post-load late-join synchronization` messages before the client is released.
6. Verify the console includes `snapshot apply summary success=true` after host snapshot load and after client late join.

## Most Likely Runtime-Only Failure Cases

- A stale `client ready` acknowledgement arrives after the host already replaced that client's transfer, producing `late-join snapshot ready ack transfer mismatch`.
- A runtime object is spawned through a gameplay path that bypasses the persistent allocator before the registry and spawn manager are fully ready, causing fallback identity assignment or ID reuse pressure.
- A runtime snapshot entry references a prefab mapping that is valid in editor data but not registered in the active host session, producing `missing runtime prefab`.
- A provider restores its own payload successfully but a dependency is still unresolved in that phase, producing `restore order issue` or `provider state application failed`.
- A building or dropped-loot object is reconstructed from snapshot and then reintroduced by another gameplay path, producing `duplicated building reconstruction detected` or `duplicate dropped-loot reconstruction avoided`.
- The host finishes loading a save and accepts joins while late initialization work is still catching up, producing post-load validation warnings even though the session appears online.
- Multiple staggered joins during heavy world churn can expose slow or stuck transfers, producing `late-join snapshot transfer still pending`.

## Failure Signatures To Watch

- `persistent object missing persistent ID`
- `persistent ID collision`
- `snapshot resolve failed`
- `snapshot resolve kind mismatch`
- `snapshot resolve prefab mismatch`
- `provider payload invalid`
- `provider state application failed`
- `restore order issue`
- `runtime reconstruction failed`
- `late-join snapshot ready ack transfer mismatch`
- `late-join snapshot transfer still pending`
- `late-join snapshot transfer replaced`
- `duplicated building reconstruction detected`
- `duplicate dropped-loot reconstruction avoided`
