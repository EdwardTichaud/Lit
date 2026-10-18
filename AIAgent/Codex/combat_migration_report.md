# Production Combat Migration Audit

## Implementation continuation, sources combined (2026-10-17)

The remaining implementation is now connected rather than represented only by
network admission helpers. Actor-owned threshold controllers run the existing
sequence engine on the server for the actual damage source. Owner-only prompt
and input transport validate window/session generations. Observers receive the
sequence presentation; no host substitution or network slow motion is used.

Ordinary cinematics, Counters and LightSkills have actor-owned playback pools,
stable authored definition IDs, participant reservations, native impact routing
and owner-only camera/input scope. Enemy reactions are routed from both enemy
adapters to the actual victim. Timestamped B/Y presses, pre-impact counter phase,
targeted dodge protection and contact-time checks use the existing motors and
resolvers. Defensive and passive-float presentation is replicated without a
second movement motor. World retirement releases only the owning resources.

World clash now discovers compatible authored actors instead of binding the
nearest/locked pair. Network Basic impacts are deferred through the same arbiter;
accepted clashes invalidate both generations and replicate cosmetic feedback.
Attack volumes are authored before spawn, including laboratory fixtures. Only
the previously compatible ground strikes qualify; aerial, sweep, airborne rush
and cinematic actions do not acquire implicit clash eligibility.

Specialized encounter adapters retain Ghost introduction, emergency brake,
torches, relay/exposure and Veilleur gates. Two separate Giant profiles are
derived from their actual equipped Jump and Assomoir sources; identity, shared
prefab and equipped attacks are not merged or replaced by laboratory actors.
The bounded encounter tool records source provenance and planned asset files.

Combined runtime/editor compilation succeeded in
Logs/CombatMigrationFinalImplementationCompile.log (bounded catalog run
59dfca280fd24d6484451d4d80713c70): zero blockers and zero unsupported skills.
Network smoke-test sources were also built, not executed, with zero errors.
Isolated installation succeeded (Logs/CombatMigrationFinalAuthoringInstall2.log,
run 925f647c3b9347369a968b5a1b23dcd8): authoringInstalled=true,
idempotent=true, 90 watched files, preserved GUIDs, no unresolved implementation
in the bounded manifest. This includes dedicated equipped-only profiles for
DeadWeight, FalseChoir and Veilleur, whose original profiles requested unequipped
strikes. Their specialized mechanics remain untouched.
Final combined compilation also succeeds in Logs/CombatMigrationDeliveryCompile.log;
the post-install bounded catalog proposes zero writes, blockers or unsupported
skills. These are compile/authoring checks, not executed gameplay tests.

Main-project installation is now delivered: run d0aff98072784154b05309f35428062c,
Library/ProductionCombatMigration/implementation-install.json reports installed,
authoringInstalled=true, idempotent=true, 90 watched files and zero unresolved
implementation entries in the approved bounded manifest. Existing GUIDs were
preserved, backups retained and a second application was byte-stable.
Main runtime/editor compilation succeeded in Logs/Editor.log before installation.
The static CombatClashAuthoring helper now lives in ProductionCombatMigration.cs
because the main Editor's stale compilation index excluded its standalone file
even after refresh. The v2 request was consumed by the current installer.
No wholesale isolated-project copy was performed.
The first main install (8d9ffc8b6db241099e936e46f3677245) was rejected by the
unchanged-source guard while code was still being edited; it is not a successful
delivery receipt; the successful main receipt above supersedes it.
No new tests or Play Mode run have been executed. Previous
suite totals below are historical, not results of these new sources.
ReadyForProduction remains false until all eleven evidence gates are satisfied.
Etienne Flame placement remains owned by the author and was not modified.

Installation is intentionally bounded to the approved actor/session manifest.
The full inventory, dependency graph, scene-consumer and readiness audit commands
remain available and unchanged in scope; the installer does not claim those
gates. This avoids repeatedly loading every zone and all its assets during each
idempotence pass. Asset hashes, existing GUIDs and repeated-write checks remain
part of the authoring install. No missing gate is converted to a passing result.

### Deferred Gameplay Recipe

1. Start through Bootstrap into District 1: one solo Lucian, navigation ready,
   exploration attacks without lock, then vision/contact aggro and escape.
2. Check charge/air combo, both dashes, Rupture lock requirement, counter, clash,
   LightSkill, multi-QTE threshold and interactions after the last threat.
3. Save/reload, change zone and return to menu; verify persistent enemy identity
   and only genuinely unlocked companion kits.
4. Exercise each specialized encounter and each character kit.
5. Use a real host and remote client, then four players: separate fights,
   nonparticipant exploration/camera, concurrent cinematics, late join and
   disconnect. Verify no network slow motion and actor-scoped cleanup.

## Earlier checkpoint (2026-10-17)

Implementation-only continuation, with gameplay tests explicitly deferred:
the network executor now includes native direct attacks (Flèche), locked Rupture,
server-observed charge/preparation, Ascension launch/float and ground-area impact.
External presentations expose authored recovery/cancellation windows and captured
targets. Authority replacement preflights before releasing the outgoing action.
Server dodge and remote guard use the existing actor/UCC presentation/motors.
ICombatChargedTarget extends launch/float to retained EnemyController actors;
grounded production Brains actions can also be interrupted by a valid launch.
Legacy enemy perception no longer selects the host by default; confirmed enemy
feedback is replicated. Local input buffers/reconciliation are actor-scoped.
Rupture's native clip was incorrectly looping; it is now non-looping with its
original impact at 0.8333333 seconds preserved.

Intermediate authoring evidence: CombatImplementationAuthoring.log, run
faf1b21178f94d6786cfcdf2edcc637d, zero unsupported catalog skills, zero first
Apply writes on the already-staged assets, repeated Apply and dry-run zero writes.
This run precedes the latest runtime edits. Current runtime/editor compilation
succeeded in CombatImplementationCurrentCompile.log, with zero catalog blockers
and zero unsupported skills (network dry-run 675b602d24e54c9894b89b5531d01ab9).
No new EditMode suite, Play Mode or multiplayer run was executed.

CombatMigrationImplementationInstall provides an explicit source-editor install
of common services, four kits and the original production Juggernaut, with backups
and byte-stability/GUID checks. Its result does NOT mean full implementation.
The source Editor became unresponsive while a historical import-time repair
opened MainMenu; it was not terminated. That repair is now menu-only. Installation
into the main project's assets remains unconfirmed, not reported as completed.
An explicit Library-only authoring request is queued for the source Editor; it
does not start Play Mode and refuses dirty authoring assets before applying.
Remote reactions/QTE/cinematic outcomes, multi-pair clash and remaining encounter
integration are still implementation work, not manual-test items. ReadyForProduction
remains false.

Continuation evidence (supersedes the historical counts below): the expanded
suite reached 387 passed, one genuine Etienne coverage failure, one manual skip.
Evidence: Logs/CombatMigrationLotOneAndStaging.log and the isolated
ProductionCombatMigration/lot-one-etienne-authoring-pending.xml. Full additive
coverage confirmed 20 active Flames, none covering Etienne's authored Ghost.
The author approved a new local, initially unlit common Flame, then explicitly
took over its placement. Automated placement was stopped; Etienne's scene and
Ghost remain unchanged. Coverage remains an open authoring validation item.

The aerial Basic now has one native HitEnemy event at 0.53333336 seconds;
its slash VFX events remain. Nina text/renderer tests and the guarded VFX graph
property bindings pass. Ardent's local Flame uses the canonical 11.25 m light
range override, not a radius overwritten by synchronization. Conversion of the
actual production Juggernaut passed in isolation with its original identity and
four attacks; repeated generation/Apply preserves bytes and GUIDs. Seventy
proven orphan diagnostic sub-assets were removed in isolation, not unrelated
controller references or gameplay behaviours.

Latest isolated staging: Logs/CombatMigrationCurrentStaging.log, zero primary
writes, four network dependency writes, then zero writes on repeat and dry-run.
Later manifest/build-settings and validation-tool edits require fresh evidence.
District 1 has 21 required additives plus Core; the Indoor_Lights asset exists
at Assets root. Its runtime-name cache is repaired, and two unconsumed dangling
Build Settings scene entries are removed. No valid NavMesh data was changed.
Rendered Bootstrap, real save restoration, all-zone and multiplayer acceptance
remain unvalidated; no final readiness receipt or global rollout is authorized
by these authoring/EditMode results.

Migration remains incomplete; ReadyForProduction must remain false. No global
production prefab rollout has been copied back from the isolated staging project.

Source inventory correction: the earlier isolated-copy reference counts are NOT
evidence that those resources are missing from the source game. Of 905 historical
reference findings, 770 have exact source metadata/payload matches. Original
dependencies were restored into the validation copy without invented replacements.
Nina remains at Assets/Scenes/Cycles/Cycle_Nina/NinaCycle.asset; tooling and the
obsolete cycle scene-name reference now use its existing canonical scene. GUID,
cycle identity and unlocks are preserved. The Veilleur VFX binder exists in the
active VFX Graph package. The removed LucianJumpStateTraceBehaviour is instead a
proven deleted diagnostic; a narrowly scoped, reviewed cleanup tool is provided.
Unresolved authored motion and optional narrative bindings remain explicit issues.

Runtime continuation includes deferred final-threat cleanup across cinematics,
dead-lock liveness cleanup without ending other threats, participant-scoped impact
protection, production multi-victim spatial contacts and per-action damage capture.
Network execution now additionally contains aerial Basic support, contact dashes,
Sommation, Juste Retour, Pas de recul and bounded Basic input buffering. These are
implemented paths, not multiplayer acceptance evidence. The authored aerial Basic
impact marker is now repaired; cancellation/recovery parity is unfinished.
Charge/projection/ground-slam, remote reactions/QTE/cinematic outcomes, general
multiplayer clash and real host/client validation still block release.

Authoring now validates/installs existing server-authoritative NGO movement and
animation components before spawn, including the player's existing dependencies.
One isolated staging snapshot passed a first Apply (3 primary assets, 14 network
entries), second Apply with zero writes, and final dry-run with zero proposals.
Evidence: Library/ActorAnimationSandbox/Logs/CombatMigrationFinishStaging.log.
Later source changes require renewed evidence; this is not final-source readiness.

Expanded initial Unity run: 322 passed, 16 failed, 1 skipped. Several failures were
then repaired; final current-source rerun is recorded separately below. Managed
checks passed for inventory, diagnostic cleanup, authority/catalog and QTE admission;
they do not replace Unity execution or real network sessions.

Bootstrap attempt reached real Play Mode and an isolated save slot, but failed to
complete loading in 180 seconds under Null graphics: HDRP rejected that renderer
and loading presentation waited for a rendered frame. This is NOT a demonstrated
NavMesh/combat failure. The validation tool now rejects Null graphics early and
provides an isolated rendered-Editor recipe. Bootstrap/District 1, complete save
restore, all zones, Domain Reload variants and real multiplayer remain unvalidated.

Historical checkpoints below are retained as history, not current asset diagnosis.

### Final Expanded Unity Evidence

Current-source runtime/editor compilation succeeded. The expanded run completed
with 380 passed, 7 failed, 1 skipped, saved as finish-expanded-final.xml and
Logs/CombatMigrationFinishFinal.log in the isolated project. Passing fixtures
include participant lifecycle (11), diagnostic cleanup (12), network authoring
(4), inventory (10), contact dashes (11), world migration (15), production lifecycle
(7) and production impact continuity (3).

Failures: authored aerial Basic missing its required impact event; Ardent/Etienne
scene audits; Nina missing renderer lookup and two encoded-title assertions; one
death-presentation fixture omitted the Edit Mode Awake setup. That fixture was
corrected without changing its assertions. Its focused Unity rerun passed both
tests (2 passed, 0 failed), saved as death-presentation-final.xml with log
Logs/CombatMigrationDeathFixtureFinal.log. Six other expanded failures remain open.
Do not combine a later focused result into an invented all-green expanded run.

## Continuation: implementation checkpoint (2026-10-16)

Full production delivery is still blocked. The continuation adds executable
runtime paths, not permission to enable every production encounter silently.

- Actor-scoped world engagement ledger, complete owner threat snapshots, explicit
  impact source identity, and source-owned threshold staging/cleanup. A remote
  threshold participant is never replaced by the host/local actor; executing that
  participant's QTE is still unsupported and reported without clamping damage.
- Common encounter/death contracts preserve ghost introduction, flame and boss
  gates. Existing bespoke bosses retain their original implementations; this is
  not a claim that their combat brains were converted.
- Production Brains conversion tooling now translates reviewed source skills,
  clips/events and pattern definitions, including scripted motion and authored
  lock points. It rejects missing/incompatible mappings instead of substituting
  lab identities. No production enemy conversion has been copied back yet.
- Opt-in actor-owned server execution covers grounded Basics, immediate heals,
  regeneration/life steal, Rempart/Intercession and Revelation/Entrave. Stable GUID
  catalog IDs, actor/loadout validation, owned effects, HP/effect replication and
  rejection feedback are implemented. See Migration/Network/INTEGRATION.md.
- Charged/aerial skills, targeted dashes, ground slam, Juste Retour, Sommation,
  Pas de recul, network counter/QTE/cinematic execution, general multiplayer clash,
  combo buffering and full enemy animation replication remain release blockers.
- CombatNetworkSetup provides a separate bounded dry-run/apply authoring step,
  before spawn, with unsupported-skill reporting and an actual saved-file manifest.
  GameFlow explicitly readmits only destination-world actors/assigned players after
  placement. Legacy support RPCs cannot bypass an installed new actor executor.
- ReadyForProduction is now computed from source/asset-bound evidence gates,
  inventory coverage and authoring idempotence, not a constant or an Apply result.
  No unperformed scenario has been attested as passed.

Initial expanded Unity run: runtime/editor compilation succeeded; 244 tests
passed, 40 failed and one skipped. New fixture/generator failures were identified
for repair. Other failures include obsolete narrative asset paths (for example
Resources/Narrative/NinaCycle.asset is absent in the source project), authored
scene/timeline audits and existing encoded-text/renderer expectations. They are
not hidden or declared fixed by narrowing the later core regression selection.
Evidence: Logs/CombatMigrationContinuation.log and
Library/ProductionCombatMigration/continuation-full-edit-results.xml inside
Library/ActorAnimationSandbox.

Final current-source validation: actual Unity runtime/editor compilation passed;
248 selected Edit Mode cases passed, zero failed and one explicitly skipped
manual authoring check. The previously excluded held-direction test is included.
Charged preparation Play Mode passed with real input/damage, preserved pose,
ordinary cancellation rejection and release/priority/death cleanup. Evidence:
Logs/CombatMigrationContinuationFinal.log and continuation-edit-results.xml.
The intermediate single failure came from a stale sandbox Editor copy of a probe
moved into runtime; that duplicate was removed and its source GUID restored.

Read-only authoring audit completed: primary pass proposes 3 asset updates;
network authoring proposes 11, with no network preflight errors and 10 unsupported
skills. Neither pass applied production assets in this continuation. The expanded
inventory reports 949 blockers in this isolated workspace: 905 unresolved reachable
GUID references, 12 unknown dynamic references, one unknown marker, six unconverted
enemies, 13 unsupported network-family findings, 11 missing evidence gates and
one missing isolated-root Git provenance. These are findings, not 949 demonstrated
gameplay regressions. At least one sampled unresolved vendor-prefab GUID was also
absent from the main project's metadata; do not repair these with placeholder assets.
The isolated-root provenance warning is not a claim that the main checkout lacks Git.

No Bootstrap/District 1 playthrough, real save-slot roundtrip, visual controller
review or host/remote-client session has been validated in this continuation.
Production GameplaySessionRoot/roster/enemy asset rollout remains staged.

## Previous Integration Baseline

Status: shared-runtime integration implemented; full game migration NOT delivered.
Unity runtime/editor compilation and isolated authoring migration passed. Initial
apply changed 13 assets; the second pass proposed zero writes. ReadyForProduction
remains false, with 10 explicit blockers. The production session/roster prefab
bascule has NOT been copied back to the working game. Only laboratory bindings
and common-code changes are integrated while the production blockers remain.

Targeted validation: 107 Edit Mode cases passed, plus a Play Mode held-X charge
with real damage, preserved pose, ordinary cancellation refusal and release/
priority/death cleanup. Evidence: Library/ActorAnimationSandbox/Logs/
ProductionCombatRuntimeChecks.log and Library/ActorAnimationSandbox/Library/
ProductionCombatMigration/edit-results.xml. The first combined runner logged a
scene-cleanup exception after Edit Mode because Play began too early; the runner
now waits for test cleanup. Clean revalidation passed in
Library/ActorAnimationSandbox/Logs/ProductionCombatRuntimeFinal.log: 107/107
Edit Mode tests and charged preparation PLAY PASS, without the restoration
exception. Latest recipe: ProductionCombatSnapshotFinal.log, 108/108 tests plus
charged preparation PLAY PASS, including NPC-safe solo snapshot filtering.
After that recipe, common retaliation-impact notification and obsolete arena
impact-field cleanup compiled against actual Unity references; the full
retaliation sequence was not replayed. Do not infer all gameplay coverage.
The existing HeldDirectionSurvivesMapDisableButPhysicalReleaseAllowsSameDirection
test failed and is explicitly excluded from the final targeted selection, not
declared fixed or ignored silently. Initial fixture scene-creation errors were
corrected without altering runtime authorization.

No actual Bootstrap-to-zone gameplay, boss migration, host/remote-client session,
save-slot compatibility run or manual camera/rendering recipe was completed.
Managed network smoke checks are infrastructure checks, not multiplayer proof.

## Run Contract

- Menu: `Lit > Combat > Production Migration > Dry Run` or `Apply`.
- Batch: `-executeMethod ProductionCombatMigration.RunBatch` defaults to dry-run. Add `-applyProductionCombatMigration` to explicitly apply in the isolated project.
- Callable API: `ProductionCombatMigration.Run(false/true)` returns its report and does not exit Unity. `RunBatch` exits 0 on success, 1 on failure.
- Reports: `Library/ProductionCombatMigration/dry-run.json`, `apply.json`. `changedAssets` is the explicit proposed/saved copy-back manifest; entries include path, GUID and field/source details. Copy only successful apply entries, plus new assets' `.meta` files and new folder metas.
- `applied` indicates an apply pass was requested; `succeeded` describes only that pass's authoring work, never gameplay readiness. `ReadyForProduction` remains false and `blockers` is a machine-readable list of unsupported enemies and the unvalidated trusted online combat-command executor. Successful apply logs staged authoring writes, not migration completion. Online commands and unsupported encounters remain release blockers.
- Apply performs a complete read-only preflight before writes. Dirty open lab scenes and Play Mode/compilation are rejected. There is no import hook, global SaveAssets, automatic scene replacement, asset deletion or migration at runtime.
- Only changed prefabs/scenes/assets are saved in place. Subsequent dry-run must propose zero writes. Failures during saving are reported but not transactionally rolled back: discard the isolated run, not the user's checkout.
- Inspection opens the lab additively and closes it only if opened by the tool; other scenes are not saved. Dry-run writes only its Library report, not source assets. Imported dependencies are read, not regenerated by this utility.

## Explicit Write Envelope

The following is the maximum intended copy-back list, not a claim every file changes. The JSON manifest is authoritative after execution.

1. `Assets/Core/System/GameplaySessionRoot.prefab`
2. `Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test.unity`
3. `Assets/Characters/1_Squad/Lucian/Lucian.asset`
4. `Assets/Characters/1_Squad/Vesta/Vesta.asset`
5. `Assets/Characters/1_Squad/Soren/Soren.asset`
6. `Assets/Characters/1_Squad/Aurell/Aurell.asset`
7. `Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab`
8. `Assets/Characters/1_Squad/Vesta/Vesta.prefab`
9. `Assets/Characters/1_Squad/Soren/Soren.prefab`
10. `Assets/Characters/1_Squad/Aurell/Aurell.prefab`
11. `Assets/Characters/3_Enemy/Juggernaut_v2/Juggernaut_v2_CombatProfile.asset`
12. `Assets/Characters/3_Enemy/Iluvilirae/Clash/CombatLab_ClashEnemyProfile.asset`
13. `Assets/CombatRealTime/Migration/Controllers/Lucian.overrideController` (new)
14. `Assets/CombatRealTime/Migration/Controllers/Vesta.overrideController` (new)
15. `Assets/CombatRealTime/Migration/Controllers/Soren.overrideController` (new)
16. `Assets/CombatRealTime/Migration/Controllers/Aurell.overrideController` (new)

Roster prefab paths are taken from the existing CharacterData.worldPrefab; profile paths are taken from the authored lab enemy/clash references. Preflight reports actual resolved paths. A changed source reference can therefore alter this envelope: review dry-run before applying/copying. No enemy prefab/controller is a migration target. No zone scene except the lab is a write target.

## Production Versus Lab

Production BattleManager already contains RealTimeCombatManager, input, mobility, lock camera, impact feedback, LightSkill, counter, pooled cinematic playback, telegraph/warning, health thresholds and AnimationEvents. Several service bindings are null, including cinematic playback bindings. Production counter uses TemporalRiposte rather than the lab's owned pooled CombatLab_CounterSkill. The manager's skill-cinematic/threshold links are null. Production has no authored CombatWorldRuntime or common clash component in the audited prefab.

The utility explicitly ensures all shared services on BattleManager and the lab arena host, wires intra-host manager/input/mobility/cinematic references, copies the lab counter asset list, and binds the correct local camera. Production gets its own GameplayCameraModeController Camera; lab gets arena.arenaCamera. Serialized `freeAttacks` comes from the arena without copying any scene camera reference into the prefab. It installs/configures the common CombatLabClashController from the lab's authored compatible skills/states, clip mappings, profile, windows/volumes, recoil values and VFX/audio. Runtime playerVolume/enemyVolume references are never copied.

The tool uses TypeCache plus SerializedObject for the parent runtime component and attack.skill schema. Missing/ambiguous types or missing serialized properties block migration rather than silently skipping bindings. It does not invoke runtime Bind/RegisterControlledActor methods in Edit Mode.

## Roster Sources And Identity

- Lucian: lab arena.lucianPrefab is `Iluvilirae/Lucian_ImpactLab.prefab`, its data is `Lucian_ImpactLab.asset`, and base controller is `Lucian_ImpactLab.controller`. Production Lucian.asset and Player_Model_Lucian.prefab remain the production identities.
- Vesta: arena.vestaPrefab is `1_Squad/Vesta/Vesta.prefab`, controller `Vesta_Model.controller`, same production CharacterData.
- Soren: arena.sorenPrefab is `1_Squad/Soren/Soren.prefab`, controller `Soren_Model.controller`, same production CharacterData.
- Aurell: arena.aurellPrefab is `1_Squad/Aurell/Aurell.prefab`, base `4_Animations/Player_Model.controller`, same production CharacterData.
- Ground BasicSkills: exact arena.basicSkills references currently `Iluvilirae/Clash/ClashBasic_0.asset`, `_1.asset`, `_2.asset`. These are not CombatLab_BasicSkill_1..3 or generic production BasicSkill_1..3. Air basics and chargedSkill use the selected lab CharacterInfo.SourceData.
- Wheel skills: exact four slots per character in `Resources/CombatSkillKits/Default.asset`, stable slot order left/up/down/right. The catalog is read-only; unlock masks/progression are not changed. Source skill assets, clips and Animation Events are read-only.
- Four new AnimatorOverrideControllers retain the exact lab base controller and playerClips mappings, including the original clip keys needed by contact/Justice overrides. Existing controllers and state subasset IDs are not overwritten. These baked player overrides also allow free basics before any enemy pair binds.
- Prefabs ensure presentation, CombatTimeDomain, input relay, torso facing and the existing character-specific modules. UCC, jump/dodge/state motion, CharacterInfo and SquadCharacterController must already exist; missing required roots block rather than replacing the actor.
- Preserve uniqueId, characterId (including Soren/Aurell capitalization), worldPrefab, portrait, HP, stats, playerSettings, inventory, voice lines, thresholds and enemy encounter/death settings. Preflight refuses an existing roster uniqueId/GUID mismatch rather than repairing it. No HP=100, progression unlocks, ally spawning or test navigation/turn-rate overrides are transferred.
- Link, Mia and Luna currently have no authored combat skills/basic combos. They are inventory-only, not silently made Lucian clones.

## Enemy Inventory And Blockers

Production is not a uniform Brains roster. Do NOT replace any of these with Juggernaut patterns:

| CharacterData | Current contract / exact blocker |
| --- | --- |
| `Characters/3_Enemy/Juggernaut/Juggernaut.asset` | `Juggernaut_Combat.prefab`, legacy EnemyController/EnemyCombatProfileSO. No authored equivalent Brains phase profile and event/skill mapping. District_1_Enemies references this production identity, not the v2 lab enemy. |
| `Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut_CharacterData.asset` | giant_juggernaut; legacy GiantJuggernaut prefab/controller, jump and threshold/cinematic contracts must survive. No equivalent Brains attack profile. |
| `Characters/3_Enemy/Juggernaut/GiantJuggernaut.asset` | Distinct giantjuggernaut ID/GUID referencing the same giant prefab. Do not merge/deduplicate with giant_juggernaut; reconcile consumers explicitly before any migration. |
| `Narrative/NinaCycle/Data/Enemy_ScientifiqueFou.asset` | mad_scientist_nina; `Characters/9_Ghosts/Luc/Enemy_Model_MadScientist.prefab`, `MadScientist_CombatProfile.asset`. EnemyController implements IGhostInteractionHandler, startAsGhost, dormant/introduction/active gates, optional knowledge gating, and delayed death presentation. Needs explicit equivalent encounter/death adapter plus authored attack mapping before conversion. NinaCycleTests assert these contracts. |
| `Scenes/Cycles/Cycle_Etienne/Data/CharacterData_DeadWeight.asset` | dead_weight_etienne; Prefab_DeadWeight + DeadWeightBoss/BossEncounterBehaviour + DeadWeight_CombatProfile. Engagement/NavMesh/authority checks, damage rejection before engagement, health synchronization and emergency-brake owned suspension/cooldown must be retained. No Brains mapping supplied. |
| `Scenes/Cycles/Cycle_Belmont/Data/CharacterData_VeilleurDOmbre.asset` | shadow_warden_belmont; Prefab_Enemy_VeilleurDOmbre with legacy profile. Need explicit preservation of its scene/boss-gate consumers and authored phase-event/skill mapping; not a v2 Juggernaut despite shared legacy profile reference. |
| `Scenes/Cycles/Cycle_Belmont/Data/CharacterData_BrokenAnchor.asset` | broken_anchor_belmont; BrokenAnchor.prefab, null enemyCombatProfile, BrokenAnchorBoss bespoke torch/projectile/release mechanics. No generic attack pattern to translate. Preserve torch subscriptions, authoritative boss resolution and scene bindings. |
| `Scenes/Cycles/Cycle_Ardent/Data/CharacterData_FalseChoir.asset` | false_choir_ardent; Prefab_FalseChoir + FalseChoirBoss + CombatProfile_FalseChoir. Three echo-relay prerequisites, whole-party arena gate, default-brain suspension before engagement, exposed-only damage filter, relay rotation/cooldowns, visual decoys and authority must remain intact. No Brains adapter/profile supplied. |
| `Characters/3_Enemy/Iluvilirae/Iluvilirae.asset` | Brains-only lab identity iluvilirae_brainsai_test; not a production substitute. |
| `Characters/3_Enemy/Juggernaut_v2/Juggernaut_v2.asset` | Brains test identity juggernaut_v2_brainsai_test. The lab actually references a separate clash prefab; do not redirect production data to either. |

Only two existing lab Brains profiles gain explicit SkillSO links: Attack -> arena.enemyAttackSkills[0] (Juggernaut_Strike), Attack_Sweep -> [1] (Juggernaut_Sweep), Attack_Followup -> [2] (Juggernaut_Followup). Both state uniqueness and expected SkillName order are checked. Preserve profile GUID/identity, clips, marker times, advances, reach/arc/height, recovery, cadence and resistance. Authored skills currently deal 20, not the arena's old enemyDamage=10; this is an intentional runtime contract change for parent validation, not a migrated constant.

## Zone Inventory

`Scenes/Maison/ZoneManifest_Maison.asset` and `ZoneManifest_District_1.asset` stay read-only. District_1 includes District_1_Enemies plus narrative cycles; bakedNavMeshData/version/agent type, scene phases and marker identities are preserved. Zone.cs governs flame consumption, audio and Maison followers; it is not an enemy pattern definition.

The generated report inventories all imported CharacterData, all ZoneManifest assets, and enemy CharacterData/prefab dependencies of scenes under Assets/Scenes. It does not claim to validate runtime Addressables/resources lookups, marker spawning, scene-instance overrides or physical NavMesh suitability. Those remain parent Play tests.

## Parent Runtime Requirements

1. CombatWorldRuntime on the authored hosts: public FreeAttackProfile freeAttacks, public Camera gameplayCamera. Bind via LocalPlayerContext changes and RealTimeCombatManager.RegisterControlledActor, including local actor replacement/despawn. Never spawn/move/teleport actors or install components at runtime.
2. Generalize/remove the old FreeAttackProfile.AllowsScene restriction in both AllowsFreeAttacks and ConfigureFreeAttacks. Production enablement must be explicit shared runtime configuration, not another scene-name allowlist.
3. LitBrainsAttackProfile.public SkillSO skill is required. Route impacts/reactions from committed attacker/victim and ActionSequenceId; use skill.Damages once, not arena constants. Preserve authority, ground/contact geometry, obstruction, guard, dodge, health thresholds and death priority.
4. Remove duplicate lab impact/reaction subscriptions and movement ownership from the arena. Bind closest/engaged pair through common clash Bind(player, enemy, manager, hurtState), IsBound and QueueEnemyImpact(attacker, victim, apply). Honor the mapped clash profile/clip events without replacing enemy patterns. Unsubscribe and restore owned state/overrides across disable, rebind and destroyed actors.
5. Preserve legacy EnemyController encounter gates and BossEncounterBehaviour.FilterIncomingDamage/authority. A scan discovering an enemy must not activate dormant ghosts/bosses, bypass flame dormancy, or turn a bespoke boss into a generic Brains enemy.
6. Configure/inject skills when local actor changes without lab-only SetGroundBasicSkillOverride, HP reset, unlock bypass, or an arena restart. Preserve solo-only-Lucian materialization and network-owned actor bindings.

## Verification Handoff

Parent should compile runtime/editor first, run dry-run, inspect its exact manifest, then apply in the isolated project. Re-run apply and dry-run: zero changes, stable existing GUIDs/IDs and original prefab references expected. Check generated override controllers retain base clip keys while evaluating exact clash Basic clips. Confirm no HP/stats/quest/progression/input action/zone/enemy prefab writes.

Play checks: production Bootstrap -> zone -> local actor binding; attacks without lock and with lock; all four authored kits; charge/release; enemy committed-skill damage and reactions once; common clash pair and multi-enemy isolation; counter/cinematic/threshold cleanup; actor swap, pause/death/disable, domain reload on/off. Re-run NinaCycleTests and boss gate/filter tests unchanged. Multiplayer authority and physical NavMesh/rendering remain unverified until executed. This report does not claim all production enemies have migrated or that runtime extraction has passed tests.
