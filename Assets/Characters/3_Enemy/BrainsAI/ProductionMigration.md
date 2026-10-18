# Production Enemy Conversion

The executable production Juggernaut vertical slice is implemented, but its
authoring commands and Play/network tests have not been run in this checkout.
No production prefab, CharacterData, SkillSO, controller or clip has been changed
by this task. Unity has not been invoked.

## Runtime Contract

`LitBrainsEnemy` uses `CharacterInfo` for source identity, instance data and health.
External `DataChanged` updates the brain's source without resetting restored HP.
`CombatImpact.Source` is passed unchanged through encounter filtering and
`TryPrepareDamage(enemy, damage, out allowedDamage, source)`; null remains null.
The non-fluid lab damage path uses the same pipeline rather than losing Source.

`ILitBrainsEncounterPolicy` supplies independent decision/damage gates, returning,
real dormancy state and source-aware damage filtering. Encounter owners call
`RefreshEncounterPolicies()` after adding/removing an adapter at runtime.
`LitBrainsEncounterGate` is an explicit gate, not a ghost introduction, return
motor, boss puzzle, or automatic Flame influence receiver. Existing
`IBossEncounterBehaviour` suppression and filtering are respected, not replaced.

Network simulation requires server authority and a spawned NetworkObject.
Foreign-player perception uses `CombatWorldRuntime.TryEngageActor(actor, target)`;
the local manager is notified only for its own PlayerRoot.
Presentation states and hurt blend are owned by `LitBrainsCombatProfile`; lab
defaults are retained. The original production profile reference is provenance
only and does not imply its AI features are executed.

## Editor Workflow

`Lit > Combat > Production Enemies > Audit Selected Character` reads the exact
selected production CharacterData/prefab/profile. It records unique source
SkillSO/clip GUIDs, exact Animator state paths and authored reaction, lock, impact
and end times. No prototype, name similarity, replacement skill, identity repair,
partial pattern conversion or asset overwrite is inferred.

`Generate Reviewed Juggernaut Assets` creates a dedicated BrainsProduction folder
beside the existing production prefab. It rebuilds the proven controller states
and clones each proven source clip, translating every known event at its original time and
with its original payload. The generated profile keeps the original SkillSO
references and original EnemyCombatProfileSO; it does not import Juggernaut_v2 or
Iluvilirae attacks. All source animation curves must match the generated clips.
A second generation validates and reuses those exact assets without changing
their GUIDs. Unowned/partial output is never overwritten.

`CreateInspectionCandidate(report)` returns an unsaved inspection-only profile
containing only proven source mappings. It must not be attached as a playable
production profile. It retains source SkillSO and clip references, including a
240-degree sweep without silently narrowing it to the lab's default arc.

`Apply Selected Reviewed Request` / `Convert Production Juggernaut In Place`
preflight the entire source before loading a
prefab for editing. It requires explicit reviewed destination assets with exact
animation curves, event times, states and full skill coverage. Unsupported
authoring produces an exception before any write. Apply saves at the original
prefab path and checks GUID preservation; it never edits source skills/characters.

## Executable Juggernaut Path

- Production mode reads the original weighted Frappe/Combo/Balayage/Assomoir
  patterns, combo order, cooldown, recovery and repetition metadata at runtime.
  It executes exact mapped states rather than randomly choosing flattened skills.
- Guard uses the source probability, duration, cooldown and reduction; incoming
  direction uses the explicit impact source, never LocalPlayerContext.
- Dedicated fixed-step production motion consumes the source advance curve and
  start/stop events, airborne launch/gravity/fall speed/landing request, and the
  bounded fixed-direction XYZ rush metadata. It does not use lab advance easing.
- Production impacts expose the original sphere/offset/mask, grounded constraint,
  skill range/arc and enemy multiplier through shared-resolver hooks below.
- Home/pursuit bounds use the source radius (35 m for production Juggernaut), not
  vision range or the enemy's current position. Observation, return pause,
  return re-engagement distance and facing rate come from the source profile.
- Any source controller/clip mismatch or unit-speed violation is reported per
  skill. No mapping is fabricated to make the audit pass.

Cinematic placement uses local typed sampling and world validation; there is no
Warp, 2 m recovery search or restoration of a historical transform. An invalid
current pose keeps navigation disabled until physical ground and local NavMesh
are valid. Production action recovery follows the same rule.

## Parent-Owned Dependencies

- Generated clip core events use existing Brains routing; production motion
  events use LitBrainsAnimationEvents on the same Animator object, forwarding
  to distinct actor handlers to prevent double delivery. Conversion validates
  exactly one receiver for every generated event. Original clips/events remain intact.
- Conversion captures the original EnemyController.LockPoint before removal
  and preserves that exact Transform in the serialized Brains lockPoint field.
  No offset, transform reparenting or synthetic anchor is introduced; standalone
  lab actors with no explicit anchor continue to use their root.
- `CombatWorldRuntime.cs`: when `enemy.UsesAuthoredProductionCombat`, use
  `CollectAuthoredImpactVictims(list)`, `HasAuthoredImpactContact(victim)` and
  `AuthoredAttackDamage`, preserving per-victim guard/dodge/clash and hit dedupe.
  This is required for authored sweep/multiple victims and grounded constraints.
- Ledger pruning uses `IsThreatFor(actor)` / `IsEncounterEngaged` and
  `IsWithinPursuitBounds(actor)`, not only `fov.CurrentTarget` or IsEngagedWith.
- `BossEncounterBehaviour.cs`, concrete cycle bosses, scientific ghost/last-words
  encounter presentation and their scene bindings still require dedicated
  IEnemyEncounterActor/policy migrations. Never substitute Juggernaut behavior.
- Threshold controller owns source-specific player selection; network directory
  owns registration/assignment. Neither is modified by this scoped change.

## Validation

The generator builds a fresh direct-play controller containing only the mapped
attack and presentation states. State machines/states belong to the new asset;
BlendTrees are recursively cloned, preserving authored child settings and clips.
Legacy transitions are not imported: the production executor owns state changes.
The source controller contains dangling destination IDs
`-234594715202117747`, `812330941234567102`, `812330941234567105`;
copying its serialized graph reproduced broken PPtr import errors. The source
asset remains untouched. Tests force controller reimport and validate graph
ownership, motions, entry state and idempotent controller/profile GUID reuse.

New Edit Mode tests cover full Juggernaut mapping, source identity, blocked writes,
generation/idempotent GUID reuse, exact event timing/payload, source-only
candidates, encounter gates, restored health, explicit source propagation and
home-relative pursuit bounds and rejection of a nearby different cinematic
floor. The parent's first Unity run exposed the copied-controller PPtr issue and
a source-policy test probe incorrectly located in an Editor assembly. The probe
now lives outside Editor under UNITY_INCLUDE_TESTS, retaining its meta GUID.
Runtime and full editor C# reference compilation, including these fixes/tests,
passed without launching Unity. The fixes still require a Unity test rerun;
reference compilation is not an asset import, Play or network result.
Required next: rerun these Edit Mode tests, then
production Juggernaut/threshold/persistence and host/client acceptance tests.
