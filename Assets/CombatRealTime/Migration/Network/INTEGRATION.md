# Migration Network Integration

## 2026-10-17 Execution Delta And Release Limits

The actor executor additionally implements aerial Basic admission, Sommation,
Juste Retour, Pas de recul, targeted/free contact dashes, ordinary native-event
attacks, locked retreat, server-observed charge, charged launch/float and grounded
area impact, using existing presentation sessions and movement adapters. Contact
events remain authoritative, generation-bound and unique; clients cannot apply
damage or install temporary target collision ignores. Free dash distance comes
from the shared world profile, not a second network tuning constant.

The authored aerial Basic has one native impact marker. Ordinary projectile VFX
use the captured actor/target context; damage and stun still occur at the native
impact, not on a guessed projectile timer. External presentation windows follow
the skill profile; replacement validates the next action before cancelling the old.
Charge is admitted from the actual initial Basic and held input, with a distinct
server-confirmed preparation and an authority continuation that preserves RPC
sequence numbering. The charge camera belongs only to its local owner.
Actor-scoped guard and dodge reuse existing presentation/UCC motors, with targeted
invulnerability and world-exit cleanup. Legacy enemy vision selects registered,
assigned server players; actual enemy impacts replicate their presentation cues.

This is not complete multiplayer delivery: general clash and remote
cinematic/reaction/QTE execution still require implementation.
CombatNetworkQteSession is admission infrastructure only, not an operational remote
QTE flow. No true host/client or four-player acceptance run has been performed.

Enemy NetworkAnimator/NetworkTransform authoring is now explicit before spawn.
This does not prove all boss state/death/impact feedback replication. Earlier
subset descriptions below remain historical details where superseded here.

Namespace: `Lit.Combat.Migration.Network`. This continuation owns this folder.
The sole explicitly authorized outside edit moved the authority installation
below the null guard in NetcodeBootstrap.EnsureNetworkConfig.

## Concrete Actor Executor Checkpoint

`CombatNetworkActorRuntime` is now the concrete per-actor trusted executor. Author
it on the existing root NetworkObject before spawn, assign `catalog`, and author
one `CombatNetworkAnimationEvents` on the player's actual Animator. No runtime
NetworkBehaviour or relay installation is performed. Parent owns prefab/catalog
installation in `Assets/Editor/CombatNetworkSetup.cs`.

`CombatNetworkSkillCatalog` entries contain `skill` and the authored Unity
`assetGuid`. Wire IDs are deterministic positive hashes of the GUID; duplicate
assets, missing GUIDs and hash collisions reject the entire catalog. Runtime
registers only skills equipped in the actor's current CharacterData, not every
catalog asset. Unsupported entries report an explicit diagnostic.

Parent-facing APIs:

```csharp
bool TryRequestSkill(SkillSO skill, NetworkObject target, Vector3 direction);
bool TryGetSkillId(SkillSO skill, out int id);
float GetSkillCooldownRemaining(SkillSO skill);
string LastDiagnostic { get; }
bool AdmitToCurrentWorld();
event Action<SkillSO, CombatActionTicket> ConfirmedAction;
event Action<SkillSO, int, ICombatTarget> ServerImpactApplied;
```

Cooldown getter reads the authority's game-time clock on the server and
owner-replicated, server-timestamped samples on the owner. No speculative UI
cooldown on queued input. `ConfirmedAction` fires once after accepted Basic start,
or after actual support commit. Instant supports use a reliable confirmation RPC
because start/end may coalesce in one NGO tick. Samples are available before that
event. Gate rejection reports an owner diagnostic without confirmation; a later
support commit failure also reports explicitly and retains its admitted cooldown.

The local owner facade's confirmation owns local start SFX; runtime supplies one
spatial authored start cue for other observers. Parent should not play a second
local cue outside that confirmation. Confirmed impacts call the common feedback
controller with the actual caster transform, not a rebound local player root.

Implemented concrete subsets:

- Grounded BasicSkills: FreeAttack/OptionalTarget/RequiredTarget, exactly one native
  HitEnemy/ResolveSkillImpact marker, no lunge/retreat/cinematic/airborne impulse.
  StateTrajectory needs an authored actor-local trajectory. No timer damage fallback.
  Contact selection is angle, distance, stable network ID; an explicit captured
  target remains bound and is never replaced mid-action. Server validates source
  ticket/session, clip/state/event, live loadout, encounter/boss gates, range,
  vertical separation, line of sight and duplicate contact. Existing impact
  receivers retain caster attribution and their defence/threshold policies.
- Immediate self/radius heals, using only living registered ally actors.
- Rempart and Intercession, including explicit Intercession self-targeting.
- Selected-other-ally regeneration and explicit-enemy life-steal using the existing
  server-only PlayerRegenerationEffect, with replicated passive indicators and
  health. Online self regeneration remains rejected.
- Revelation and grounded target-origin Entrave, with replicated effect records.

Supports do not begin external presentation, suppress movement, replace locomotion
or play fake action animations. Only Basics use an actor-owned external session;
the original AnimationEvents receiver rejects this owner type, while the new relay
routes the real native event only to this actor's server executor.

Root actors self-register after spawn/initial world readiness. World invalidation
clears admission synchronously; persistent old-world actors do NOT auto-register.
Parent GameFlowService explicitly calls AdmitToCurrentWorld only for admitted zone
actors/assigned persistent players. Assignment remains separate from NGO ownership.
An existing target-only registry entry may attach this executor without resetting
cooldown. Cooldowns survive unregister/re-register of the same object in one world.

Player and enemy CharacterInfo health replicate as revisioned snapshots, including
zero, on health changes and before despawn. Client SetHealth preserves native death
listeners; cross-actor heal RPCs are presentation-only so stale feedback cannot
resurrect an actor. An immediate NetworkObject despawn can still truncate its
death animation; complete enemy Animator replication is NOT implemented here.

Accepted contacts call CombatWorldRuntime.TryEngageActor with the real caster.
Full owner-only threat snapshots apply atomically through
RealTimeCombatManager.ApplyConfirmedWorldThreats; unresolved identities retain the
previous complete snapshot, never a partial disengaged frame. Clients do not write
the authoritative encounter ledger.

## Cinematic controllers (available now)

```csharp
CombatCinematicReservationLease lease;
if (!CombatCinematicReservations.TryAcquire(this, playerTransform, contextTarget, out lease))
    return false;
// Store the lease before starting playback. Every failed start/exit must release it.
// Subscribe to Released to abort playback, or stop when !lease.IsActive.
lease.Release(); // Idempotent; Dispose() is equivalent.
```

`contextTarget` is an `ICombatTarget`; it need not equal the local manager's manual
lock. Reservation does NOT change a lock, actor binding, positions or target state.
Controllers must stop/release their own cinematic suspension, UCC handles and
presentation on invalidation. Acquire before any of those side effects.

Offline: pair overlap is checked across player/target roots; live player controller
and live target are required. Cleanup polls death/disabled/destroyed participants,
world invalidation, participant scene unload and transition into networking.
Domain reload is handled. Unrelated additive scene churn does not release pairs.

Online: only the listening server may acquire, and both NetworkObjects must already
be registered with the authority below. Clients, missing authority, unknown actors,
missing business assignment, shutdown and overlapping actions/pairs fail closed.
Do not treat a client's local rejection as permission to run the cinematic anyway.
Online leases have a 60-second safety timeout. Pair release does not apply damage.

## Server Registration

Add one `CombatCommandAuthority` to the SAME GameObject as the `NetworkManager`.
After network spawn, the world/server registers each participant in the CURRENT
world/session (not every loaded legacy or previous-world actor):

```csharp
bool RegisterActor(NetworkObject actor, Func<bool> isAlive,
    ICombatCommandExecutor executor = null, Func<ulong, bool> isAssigned = null);
bool RegisterSkill(NetworkObject actor, int skillId, ScriptableObject skill,
    CombatCommandPolicy policy);
void UnregisterActor(NetworkObject actor,
    CombatActionEndReason reason = CombatActionEndReason.Despawn);
```

Targets need only identity and a reliable live-health predicate. Command sources
also require an executor and business-assignment predicate, separate from NGO
ownership. Registrations are per-actor equipped allowlists. Use stable positive
skill IDs agreed by host/client build, not asset names or Resources paths.
Re-registration is explicit after world invalidation, death/respawn or ownership change.
Duplicate registration is rejected to prevent accidentally resetting cooldowns.

## Commands And Impacts

`NetworkCharacterInput.TryRequestCombatAction(int skillId, NetworkObject target,
Vector3 direction)` is supplied by a new partial under this directory. It sends
an ownership-required ServerRpc and an owner-only result acknowledgement.
`CombatCommandResult` is acknowledgement only, not client damage authority.

The authority validates sender ownership, business assignment, replay sequence,
rate, live actors, allowlisted skill, target identity/loaded scene, horizontal/vertical
range, cooldown and pair contention. `CombatCommandPolicy` is server-authored,
immutable and requires finite ranges/cooldown/duration. Supply the real authored
launch/impact limits. It deliberately does not infer those for every skill family.

Implement `ICombatCommandExecutor.CanStart`, `TryStart`, `CanApplyImpact`, `End`.
`CanStart` must check live loadout, actor-state/resources, enemy/ally relationship,
LOS and authoritative locked-target requirements. Unsupported paths return false.
Never temporarily rebind `RealTimeCombatManager` to a remote actor. `TryStart`
must not apply a synchronous impact; the ticket is usable after it returns true.
Failed or throwing starts receive `End` and retain their cooldown conservatively.

Server contact/timeline code calls `TryConsumeImpact(ticket, targetNetworkObject,
impactEventIndex)` BEFORE invoking existing authoritative impact receivers.
It checks the current server ticket, live registered target, range, loaded
participant scenes and duplicate event/target. Executor `CanApplyImpact` MUST verify real
server contact/timing/shape and target eligibility; there is no client impact RPC.
Event indices are bounded by policy; a free attack may affect multiple valid
targets once per index. A targeted action may only impact its captured target.
Call `Complete(ticket)` or `Cancel(ticket)` at the actual end. Cleanup invalidates
the ticket before `End`, and only releases locks owned by that ticket.

Commands with `ReserveCinematicPair=true` already hold their pair while executing.
Their executor must NOT also call standalone `TryAcquire` for the same pair:
pass the command context/ticket to the presentation path and use command lifetime
instead. Standalone leases and command reservations share contention checks.

`ClearForSceneTransition()` MUST be called for explicit world/session transitions
that do not go through NavMeshWorldService. The authority also observes
`NavMeshWorldService.StateChanged` (Invalidating/Unloaded) and service replacement
to clear all registrations/actions. A scene unload retires only its participants.
Loaded distinct additive scenes and persistent roots can share the registered
world after range checks; scene equality is NOT a world membership check.
Active-scene changes and unrelated additive unloads preserve registrations.
Disconnect, despawn, health death, ownership/assignment loss, expiry, disabled
authority and server shutdown are cleaned automatically (polling within a frame).

## Actual Gaps

This is an executable opt-in vertical slice, NOT completed multiplayer migration.
Charged/airborne/lunge/retreat Basics, ordinary cinematics, LightSkills, CounterSkills,
QTE authority, JusteRetour, Sommation/taunt and PasDeRecul remain unavailable through
this executor. There is no Basic combo buffering, lag compensation, authoritative
lock replication, full enemy Animator replication or frame-exact time-domain/pause
synchronization for remote effect visuals. Unsupported skills fail closed.
Parent owns input routing, support-RPC delegation/rejection when runtime exists,
prefab/catalog authoring and world admission integration. Native Unity/NGO
postprocessing and actual host/remote-client gameplay remain required.

Validation for this continuation: 31 managed smoke scenarios passed using actual
authority/catalog sources and explicitly limited Unity/NGO doubles. Six NUnit
catalog/marker/ranking/enemy-health tests are visible under Network/Editor. Runtime source
compilation against installed Unity/NGO assemblies passed, without launching Unity.
These checks do NOT execute native Animator, physics, effect ticking or NGO RPC
postprocessing. Earlier parent offline results in ProductionCombatRuntimeFinal.log
predate this concrete network executor and are not evidence for this continuation
or an actual host/remote-client session.
