# Production Combat Migration Audit

## Production Targetless Charged Input (2026-10-19)

The existing zone-roundtrip gamepad recipe accepts -combatMigrationCharge. It
uses the real held-X route and authored cooldown/threshold/clips, without loadout,
progression, animation or time overrides. Per destination: an early release in
the slowed tail completes its Basic without charge; a second hold preserves its
Basic generation/clip, observes slow playback and charge framing, launches once
after the authored real-time threshold and consumes cooldown. Holding briefly
after launch cannot repeat the action. Final checks require Basic playback at 1,
framing/hold/action/UCC cleanup and grounding, without engagement or camera lock.

Native run dfba2a9d62c5430ca189107904d2a224 PASSES in District and Maison, with
the actual portal/return, same actor/health/progression and world snapshot. Native
compilation succeeded; 119 source/asset/metadata hashes match main and copy.
Artifacts: Library/ProductionCombatMigration/Charge (baseline and final raw JSON,
validated-source-hashes.json); Logs/CombatProductionChargeFinal.log. The base
JSON limitation text remains the generic gamepad scope; the added checks specify
the charged cases. No runtime or author asset change was necessary. Expanded
regression was not rerun in this helper-only lot; last suite remains 544 passed,
one author-owned Etienne Flame failure, one Explicit skipped, not a new result.
Targeted projection, paired airborne float/landing, damage interruptions, visual
feel and multiplayer remain unqualified by this targetless input recipe.

## Production Defense Inputs (2026-10-19)

Optional -combatMigrationDefenses extends the real Bootstrap encounter, after
two real enemy impacts. A private Input System Gamepad emits B then Y twice,
during Recovery/None and outside the counter window. No direct dodge/guard API,
AI suspension, camera lock, forced counter or runtime parameter change.
The phase observes actual dodge controller activity and planar UCC movement,
guard-held state and release, then effective action/input/ground restitution.
Runtime errors fail the phase; 45-second bound, owner-only cleanup.

Native run 5d4f296413df4de0b8e67f70d0555596 PASSES: dodge travel 2.87/3.49 m,
two guard cycles, then escape/reengagement/third impact, home return and world
snapshot. Existing authored Flame lit as an explicit fixture, not interaction
or progression proof. 83 source/metadata hashes match main and validation copy.
Raw result: Library/ProductionCombatMigration/Defenses/two-defenses-and-lifecycle.json.
This does not qualify guarded damage amounts, guaranteed attack avoidance,
cinematic counters, visual feel, other encounters or real multiplayer.
Expanded regression on the same sources: 546 total, 544 passed, one existing
Etienne Flame-coverage authoring failure, one Explicit skipped. Native compilation
succeeded; raw XML is kept beside the encounter result. No assertions weakened,
no Flame placed, no readiness gate promoted. Both owned Unity processes exited.

## Sprint, Jump And Routed Input Receivers (2026-10-19)

The existing gamepad recipe can add -combatMigrationLocomotion: RT plus stick,
release, then two fresh South presses per destination. It observes router and
motor sprint intent, the running animation parameter and physical speed increase;
then ascent, airborne state, actual ground contact, controller completion and
effective action/UCC unlock. It never clears another owner's lock or tunes jump,
gravity, bindings or speed. The unchanged five-second jump bound allows the actual
authored landing handoff rather than asserting unlock on its first grounded frame.
Unexpected runtime exceptions/errors in this phase are captured and fail the run.

Native baseline reproduces MissingMethodException NetworkCharacterInput.OnJump:
PlayerInput.SendMessages sends InputValue, but the network component had private
CallbackContext methods with the same On<Action> names. Jump, LocomotionMode,
TriggerMunin and ToggleTorch callbacks are now OnRouted*, preserving CLR router
subscriptions, all method bodies, ownership checks and RPCs. No duplicate raw
input receiver is added. Serialized UnityEvent audit finds no author references
to the renamed private methods. Four regression cases protect names/signatures.

Native run 7e1e274b5f814e30bfae8f4651a7a91f PASSES on current matched sources:
District sprint tier 3.25, average pulse speed 1.88 -> 3.88 m/s; Maison 2.52 ->
4.08 m/s. These are short-pulse measurements, not an exact sprint multiplier
calibration. Two physical jumps and full input restitution pass in each zone,
with no captured input runtime error. No manual visual feel, combat dodge/guard,
charge, damage interruption or live network qualification follows from this run.

Expanded regression: 546 total, 544 passed, one Etienne author-owned Flame
coverage failure, one Explicit skipped. All four new callback cases pass.
Seventy-five scoped source/asset/metadata/package hashes match the validation
copy. Raw baseline/final JSON, final XML and hashes are under
Library/ProductionCombatMigration/Locomotion; logs CombatProductionLocomotionNative.log,
CombatProductionLocomotionRouted.log and CombatLocomotionRoutedExpandedRegression.log.
ReadyForProduction remains false; no global readiness gate is promoted.

## Production Input After Zone Travel (2026-10-19)

The optional -combatMigrationGamepad extension uses the existing Bootstrap zone
roundtrip. A privately owned Input System Gamepad supplies a short stick pulse
and a fresh X press in each destination, with no SetMoveInput/TryUseSkill call
for these gestures, no preference change and no new gameplay driver. It validates
routed intent, actual physical displacement and camera-relative direction, then
counts real Basic-start notifications and session release. Only its own event
subscription and device are removed. A generic fixture rejection before any
input is retained separately; clearance now uses actual UCC capsule/mask/contact
ground and chooses one safe direction among eight candidates.

An expanded provenance check finds stale validation prefabs: all four copies
still serialized networkCounterPreImpactSeconds=.2, while the main source already
has the intended .5. Source-to-copy synchronization preserves main authoring and
is recorded in source-sync-manifest.json. Four new production-asset tests assert
.5 counter, .5 dodge and .4 retry; this is authoring coverage, not live network
reaction proof. No runtime or source gameplay asset changes in this lot.

Final native run 125c75c0dc9d43129a260098dd757546 PASSES on matched sources:
District movement 1.029 m, input alignment .9976/travel alignment .9991; Maison
movement .873 m, alignments >.9998. Each fresh X starts one Basic and releases it
without repeat, lingering charge or duplicate callback. Actor/health/progression,
world/navigation and return controls still satisfy the complete solo roundtrip.

Expanded regression: 542 total, 540 passed, one real Etienne Flame-coverage
failure, one Explicit skipped. All four new authored-window tests pass. Sixty-one
scoped source/asset/metadata/package hashes match the final copy. Raw earlier
results, pre-sync hashes, sync manifest and final proofs are retained under
Library/ProductionCombatMigration/Gamepad. Logs: CombatProductionGamepadCurrentSources.log
and CombatGamepadCurrentSourceRegression.log. Not a test of all eight directions,
sprint, jump/dodge/charge, manual feel, interaction-key routing or multiplayer.
ReadyForProduction remains false; no global gate is promoted.

## Real Solo Zone Roundtrip (2026-10-19)

The existing Bootstrap validator has an optional -combatMigrationZoneRoundTrip
phase, requiring the Maison manifest and a separate single-run recipe. It moves
only Lucian to clear physical ground near the authored portal using UCC, admits
the actual PortalController.ServerTryUse path and captures that portal's real
destination poses before source unload. It does not substitute a District spawn,
force aggro, pre-open the destination scenes or create another combat runtime.

Native run 6bf1904b99794059aa90056332b7b46b PASSES: all District scenes loaded,
all old Maison scenes unloaded offline, NavMesh Ready with current zone identity,
actual captured portal arrival, same actor identity/combat binding/health/existing
progression, one common world and one controlled actor, grounded exploration/input
and interaction authorization, Basic/session release, then ReturnToHub(Default)
and the same checks back in Maison with another Basic. Both Basic waits retain
the existing eight-second bound. World snapshot write/read concludes the recipe.

This proves the solo transition APIs and continuity, not manual interaction-key
routing, stick-driven locomotion, camera/audio feel, narrative gates, real save
reload or multiplayer travel. It changes no runtime or authored gameplay asset.
The test partial lives in the existing validator, not a second qualification API.

Final expanded regression: 536 passed, one genuine Etienne Flame-coverage failure,
one Explicit skipped. No assertion or author-owned Flame placement changed.
Twenty-nine scoped source/asset/metadata hashes match the native/test copy,
including portal, manifests, navigation data, session prefab and scene flow.
Evidence: Library/ProductionCombatMigration/ZoneTransfer; logs
CombatZoneRoundTripBaseline.log and CombatZoneTransferExpandedRegression.log.
ReadyForProduction remains false and no global readiness gate is promoted.

## Maison Coverage And Expanded Regression (2026-10-19)

The existing Bootstrap validator now accepts an explicit
-combatMigrationZoneManifest Assets/.../*.asset selection and records it in the
JSON. District 1 remains the default; invalid selection fails rather than
silently testing a different zone. District-specific encounter/save fixtures
reject other manifests instead of inventing substitute enemies.

Maison baseline loads all ten scenes and the common combat world, but fails
new-game placement: Lucian appears at squad origin (0,-0.71,3). Its authored
0_SpawnPoints/LucianSpawnPoint is only a Transform, with no registered arrival
component. The same object now has ZoneSpawnPoint Default/partySlot 0. No pose,
parent, scene GUID, character identity, saved position or unlock is modified.
Only Maison_Core.unity is changed; scene/metadata backups and before/after hashes
are retained in ZoneCoverage/maison-delivery-manifest.json.

Native run 7a37b21178774e04a2d2920f775a5641 PASSES: Bootstrap, all ten Maison
scenes, navigation Ready, solo Lucian at the authored spawn (-0.47,-0.56,5.99),
grounded, Basic through the common facade and session release, native cursor
disable/destroy cleanup, then world snapshot write/read. This is not a narrated
Maison playthrough, zone transfer, real save reload or multiplayer proof.

Final expanded Edit Mode suite: 538 total, 536 passed, one failed, one Explicit
skipped. The remaining failure is EtienneCycleTests.AuthoredScenesPassReadOnlyAudit:
the authored Ghost is outside potential Flame coverage. Placement remains owned
by the author and the assertion is unchanged. All seven new zone-selection and
actual Maison spawn tests pass. Nineteen scoped source/asset/metadata hashes match
the isolated validation copy. No global readiness gate is promoted.

Evidence: Library/ProductionCombatMigration/ZoneCoverage, with baseline/fixed
native JSON, before/final expanded XML, targeted authoring manifest and matched
source hashes. Logs: CombatExpandedMaisonFinal.log, CombatMaisonBootstrapBaseline.log,
CombatMaisonBootstrapSpawnFixed.log. ReadyForProduction remains false.

## Physical Landing Recovery (2026-10-19)

The landing defect is not solved by increasing NavMesh tolerance. Native contact
diagnostics prove the actor capsule was already penetrating Flame/MeshCollider
(0.026 m) and WoodFire/MeshCollider (0.237 m) after center-ray landing. A subsequent
full-capsule attempt instead exposed a steep contact that blocked descent without
being recognized as walkable; that complete native recipe still failed.

Production motion now uses full-capsule descending sweeps, including 3D rush,
without snapping to a lower central-ray point. A steep upward-facing blocking
contact is physical support for bounded egress, not admission to navigation.
Egress uses the existing Rigidbody motor, authored clearance/walk/recovery values,
gravity, capsule clearance and a destination connected to the pre-attack NavMesh
origin. Agent reactivation still requires actual local validation at 0.15 m.
Stale generations, cinematic suspension, disable and death clean their own motion;
timeout reports contacts and refuses warp/fallback damage. No authoring asset,
collider, skill timing or pattern is changed by this lot.

Compile and targeted Edit Mode regression: 483 passed, zero failed, one Explicit
not executed. Nine new physical cases cover solid-prop egress, offset supports
under normal descent/rush and two contact angles, a blocking wall, another floor,
stale generation and cinematic ownership. Nineteen source/asset/metadata hashes
match the isolated validation copy. Native lifecycle d14f503cc325488a82628d855da4fe58
PASSES: Bootstrap/District 1, two impacts, escape/control restitution, perceived
reengagement and third impact, second escape and actual home arrival with valid
navigation/authored rotation, no late impacts, then world snapshot write/read.
This does not certify all zones, manual feel or multiplayer. The authored Flame
activation remains an explicit fixture, not a successful progression interaction.

The lifecycle fixture now uses authored eye/target heights and obstruction mask,
then asserts actual return perception after UCC placement. The earlier generic
1.2 m visibility ray could select a position the real perception rejected.
Raw failed runs and final XML are retained separately under
Library/ProductionCombatMigration/LandingRecovery. No readiness gate is promoted.

## Escape, Return Facing And Reengagement Before Landing Fix (2026-10-19)

The optional existing Bootstrap encounter recipe now has a lifecycle phase:
UCC-placed escape, restored exploration/UCC/interaction authorization, approach
during return, a new perceived engagement/impact, then another escape and actual
arrival home. No explicit EndCombat, NotifyPlayerContact or enemy placement is
used; the authored Flame fixture stays lit until cleanup. This is not a physical
stick-driven escape path, narrative interaction attempt or BattleWall test.

The baseline reproduces backwards return motion (facing/home dot -0.9145).
Return now rotates toward home using the authored tracking rate. A separate
out-of-bounds guard preserves committed living-target attacks through their
normal completion instead of cancelling them on leash exit; higher priority
death/cinematic paths remain unchanged. Three phase cases cover this rule.

The next native run proves restored controls/interaction evaluation twice,
return dot 1, proximity reengagement during return, and a third real impact
(player HP 940). The WHOLE recipe nevertheless FAILS: after that Assomoir,
Recovery remains active with the NavMeshAgent disabled at (-5,-93.56,44.96), no
Flame/cinematic suspension. Final arrival home is NOT proven. Do not increase
projection tolerances, teleport, remove assertions or re-label this as a pass.
The next native diagnostic reproduces and identifies the cause: motion active,
airborne false, end requested/abort true, vertical speed zero. Support is the
Flame collider in District_1_Corridor_Flammes at (-4.47,-93.93,44.21), root skin
0.03 above it; no NavMesh within 0.15 m, nearest at (-3.80,-94.24,44.21).
The actor landed ON an unnavigable Flame and the motor has no physical egress
before navigation restitution. Do not ignore the collider or expand attachment
tolerance. A bounded collision/gravity-respecting recovery within the existing
motor remains implementation work, followed by real local ground validation.
Current compile/regression: 474 passed / zero failed / one Explicit skipped,
including three committed-phase leash cases. Eighteen source/asset/metadata,
prefab and scene hashes match the isolated copy. This does not make the failed
native lifecycle scenario a pass; no gameplay authoring asset changed here.
Evidence: Library/ProductionCombatMigration/Lifecycle and
Logs/CombatEscapeLifecycleBaseline.log, CombatEscapeLifecycleFixed.log and
CombatLandingLifecycleDiagnostics.log. Raw failures and XML are separate.
ReadyForProduction remains false; this lot changes no authored asset.

## Imported Vision And Repeated Production Attacks (2026-10-19)

The 6/30 range mismatch is reproduced with native Unity imports. Canonical data
and a temporary copy with the interior blank line both load defaults (6 m,
110 degrees, eye 1.35), even after ForceImport; a copy without blank lines loads
the authored 30/100/1.5 values. Canonical bytes stay unchanged during diagnosis.
Repair removes only the single empty line before vision in Juggernaut.asset.
Its GUID, metadata and authored field values remain unchanged; the before asset
is backed up. No analogous interior gap is found in the 19 CharacterData assets
scanned; no blanket Unity-file normalization is applied.

With real vision, the encounter first reproduces a separate one-attack stall:
Assomoir ends inside its 2.5 m minimum, while approach keeps a destination at the
player and a longer stopping distance. Production now repositions through a
complete locally validated NavMesh path only when every equipped pattern is too
close. The desired band has an interior margin bounded by its authored limits.
No direct Transform movement, forced skill, altered damage range or interruption.

Rendered run 52de58fb32b245f6b2487b975f212307 passes using the actual marker-owned
Juggernaut and its Assomoir-only kit: vision 30, aggro without manual lock,
navigation peak 3.616 m/s, 5.606 m approach, recovery/reposition, distinct impact
generations 1/2 and player HP 1000 -> 960. A covering authored Flame is explicitly
lit as an isolated fixture and restored afterward. This is NOT proof of its
interaction/progression, escape/return, all encounters, victory or multiplayer.
Evidence: Library/ProductionCombatMigration/VisionImport;
Logs/CombatVisionImportDiagnostic.log, CombatEncounterVisionFixed.log (first-hit
stall preserved) and CombatEncounterRangeRetreat.log (bounded pass).
ReadyForProduction remains false; no existing gate is silently promoted.
Final current compile/scoped regression: 471 passed / zero failed / one Explicit
not executed, including eight added import/range/retreat cases. Ten current
source/asset/metadata hashes match the compiled isolated copy. delivery.json
records the single-line asset repair, its before/after hashes and unchanged
metadata; temporary import fixtures are removed. No broad asset replacement.

## New-Game Spawn And Saved Pose (2026-10-19)

The normal no-save/no-spawnId entry now selects the existing primary-scene
ZoneSpawnPoint rather than the squad's world origin. GameFlow samples character
file existence before restoration; an existing file, including a failed restore,
does not silently opt into new-game placement. Explicit arrivals and the editor
override keep their contracts. No spawn asset or NavMesh data was modified.

Rendered recipe 39e49776f3f2450bbb3f034a7ded5ae9 passes: authored new-game arrival
(-89.88,-93.94,51.88), physical ground, Basic, a real save at a distinct pose,
menu return and reload preserving that pose (-91.87,-93.94,52.91), HP 993, one
Lucian, existing unlocks, marker-owned fixture death and navigation Ready.
The first save fixture unnecessarily demanded a player NavMesh polygon; its
failure is retained. The fixture now uses physical ground and clear capsule
space, not relaxed enemy navigation tolerances. Full encounters, other zones,
manual feel and multiplayer remain unqualified.
Evidence: Library/ProductionCombatMigration/Spawn and Logs/CombatSpawn*.log.
Current compile/scoped Edit Mode: 463 passed, zero failed, one Explicit skipped;
seven new spawn-contract cases pass. Seven source/metadata hashes match the
compiled isolated copy. No authoring asset edits, readiness receipts or gates.

After this fix, the bounded encounter still fails. The enhanced native diagnostic
shows Lucian grounded at approximately 7 m, global/local scales 1, no Brains stop,
but the loaded CharacterData vision range is 6 while canonical YAML says 30.
A fresh vision evaluation confirms the range refusal. This is not a stale reason
or an airborne player. Loaded identity and import/serialization require diagnosis;
do not force range 30 in the brain or claim the encounter passed.
Evidence: Spawn/encounter-runtime-range-six.json and
Logs/CombatEncounterGateDiagnostics.log. The save recipe remains a separate pass.

## Actual District 1 Encounter Probe (2026-10-19)

The existing Bootstrap recipe now optionally observes the real marker-owned
Juggernaut, not an arena clone. Before correction, a bounded run reproduces
vision aggro and facade engagement without camera lock, followed by 65 seconds
of zero movement/impacts with a valid enabled agent. The authored kit contains
only Assomoir. The melee preference could filter out its only airborne pattern
when the random alternative was not selected. Airborne-only kits now remain
eligible; mixed kits retain their existing preference. Approach and return also
resume the stopped agent before assigning their navigation destination.

No authored skill, prefab, identity, Flame placement or NavMesh data was replaced.
The enemy's fixed-Flame activation rule remains required. An explicitly flagged
isolated fixture illuminates one existing covering Flame and restores its unlit
state afterward; this is NOT evidence of interaction/progression. Initial fixture
position refusals are preserved separately from the reproduced idle failure.
The later farther-position run does not qualify the encounter: no aggro/impacts
and a final out-of-range diagnosis. The initial zone-control probe also shows
Lucian falling at (0,-9,3); its relationship to the later failure is unproven.
Inspect actual UCC placement, ground colliders and normal zone spawn next.
Source inspection confirms a configured District_1 ZoneSpawnPoint in Core, but
normal startup without a spawnId does not enable the primary-scene fallback;
SquadManager's legacy spawn names are absent and its origin is used. Correcting
new-game arrival must not overwrite restored save poses. The spawn/saved-pose
case is now resolved by the following lot above; the encounter remains separate.

Compilation and scoped regression on the current correction: 456 passed, zero
failed, one Explicit not executed, including three airborne/mixed preference
cases. Five source/metadata hashes match the compiled isolated copy. This does
not turn the failed encounter recipe into a pass. No authoring assets delivered.

Evidence: Library/ProductionCombatMigration/Encounter and
Logs/CombatProductionEncounter*.log. Full readiness remains false; an implemented
pattern fix is not a passing production encounter or a manual combat review.

## Inventory, Native Play Cycles And Diagnostic Cleanup (2026-10-19)

Read-only full audit before this lot's editor-only changes: zero authoring writes
proposed, 19 enemy subjects, 34 scene subjects, 8383 inventory subjects. It reports
166 blockers: 150 missing-reference findings (77 unique GUIDs), three unbounded
dynamic loads, one runtime registry review, eleven readiness gates and sandbox
revision provenance. This is the baseline, not a post-cleanup final count.
No blanket vendor/zero-prefix/reference exemptions are introduced. Three findings
are editor icons: the exact GUID d namespace is now classified as built-in only
when Unity resolves Library/unity editor resources; native/pure checks cover it.

The source and package metadata contain no matching payloads for those queried
GUIDs. The author supplied D:/LitAssetArchive; both ZIP central directories and
all 8987 valid metadata entries were examined without unpacking 28 GB. No queried
GUID, aligned Idle or locomotion authoring profile matches. No original is
invented, swapped for a placeholder, or broadly restored. Original source assets
and optional/obsolete references still need individual resolution.

The existing Bootstrap recipe now supports two native Play Mode entries/exits
with explicit Domain Reload On or Off, preserving editor options and separate
per-cycle artifacts. Two On cycles pass; the first Off run times out on a Basic
after eight seconds. Diagnostics were added without extending the deadline;
the next two Off cycles pass with full save/menu/reload and native cleanup.
The intermittent failure remains open, not silently converted into a fix.
These bounded recipes do not prove every subscription, physical input, encounter
or remote client, and no formal readiness gate is manufactured from them.

The already-deleted diagnostic LucianJumpStateTraceBehaviour was confirmed in
Git commit 0b219dcdc4b9fd67224cacda8d57ddb5c0210726. Existing cleanup code generated
and revalidated plan bb2bf4f2f43922bb387581694a5610f789364319eb82b2dd022324e8fbf437ee:
10 controllers, 70 orphan diagnostic documents, zero owner-list references.
Only these documents were removed; motions, transitions and events remain.
453 scoped tests pass / 0 fail / 1 Explicit skipped after cleanup. Ten controllers
are delivered with byte guards, backups and unchanged metadata; no diagnostic
controller documents remain. The native cycle evidence precedes this purely
orphan cleanup; it is not asserted to qualify final assets globally.
Evidence: Library/ProductionCombatMigration/SessionCycles, Logs/CombatFullInventoryCurrent.log,
Logs/CombatBootstrapRepeat*.log, Logs/CombatDiagnosticCleanupCurrent.log and
Logs/CombatSessionCyclesRegression.log. ReadyForProduction remains false.

## Shared Controls And Save Continuity (2026-10-19)

The remaining arena control overrides now live in authored shared owners:
PlayerSettings and enemy profiles for rotation, CombatWorldRuntime for free
attacks and the world cursor at +3 Y, existing mobility/reaction components for
their validated values. The arena no longer overrides these at runtime.
The two-phase authoring install is idempotent (6ac70eb06a8f447982566ddf9fb84be5).
Eleven scoped authoring files are delivered with pre-write hash guards, backups
and unchanged metadata. Twenty-six current source files match the isolated copy.

The real save/menu/reload recipe exposed and repaired four integration defects:
last-scene additive unload hung menu return; player HP was absent from save JSON;
solo actors inherited a null prefab GlobalObjectId and could be spawned again
after snapshot reconstruction; the marker provider required an empty nested
SceneMarker when installed on a baked actor. JSON version 9 adds optional health,
old saves retain their previous initialization, and only server/offline code
restores it. Solo spawn uses the existing runtime identity contract, squad
adopts the exact reconstructed actor, and marker capture/apply resolves its real
owner even before Awake on an inactive actor. No identity or marker replacement.

Final scoped regression: 451 passed / 0 failed / 1 Explicit not executed. Rendered
recipe 66701034d42a44f5b225c5bc644f902f passes Bootstrap -> all District 1 scenes ->
Basic -> save -> menu -> actual reload: one Lucian, saved HP 993 instead of the
unsaved full value, valid snapshot identities, unchanged recruitment/unlocks,
fixture-defeated enemy still dead under its original marker, navigation Ready.
Native cursor disable/destruction cleanup also passes. This is a persistence
fixture, not a played combat victory, complete zone coverage or real host/client
proof. Failed attempts remain separate. No owned validation Editor remains.
Evidence: Library/ProductionCombatMigration/Controls and Logs/CombatControls*.log.
ReadyForProduction remains false; full zones/encounters, other save cases,
Domain Reload repetitions and real multiplayer acceptance remain required.

## Validated Laboratory Feel Integration (2026-10-19, in progress)

The common authoring migration now includes the latest approved BasicTail/Impulse
charge, three aerial Basics, captured world Burst/retreat braking settings and
contact presentation. Ascension's canonical GUID is preserved rather than
replacing the production skill with its laboratory copy. A dedicated production
camera profile inherits existing exploration settings and only copies validated
action framing. No laboratory lighting or fixture enemy replaces world authoring.

Network execution now shares BasicTail's actual external Basic session, with
authority epoch/generation presentation state, owner-only charge camera and
server-observed hold. Burst and landing braking are supplied from the server's
world profile; confirmed dash contact points and action identities reach cosmetic
feedback. This code integration is not real host/client acceptance.

Isolated authoring installs remain idempotent. The first combined regression was
413 passed / 2 failed / 1 skipped: both failures asserted the former lab-only
configuration. Tests now require the promoted configuration, canonical identity,
unchanged defaults and retained installation idempotence. The next run passed
415 / 0 / 1. Final four-roster regression passes 418 / 0 / 1 (one Explicit test
not executed). Final authoring run 9a5d899bf77549a28638ede88a9d3125 is idempotent.
Ten generated asset/metadata files are delivered to the main project with SHA-256
verification, pre-write source guards and retained backups (delivery.json).
The current source Bootstrap scene was synchronized before the rendered smoke.
That smoke exposed a genuine missing authoring reference: zoneManifests was empty,
so a normal District 1 start loaded Core alone and failed navigation. The existing
migration now explicitly registers the existing District 1 manifest in Bootstrap,
preserving other registrations and refusing dirty scenes. The corrected scene is
delivered as an eleventh file, with backup/hash guard and unchanged metadata.
The new rendered smoke passes: all 21 additives plus Core, pre-baked navigation
Ready (2/2 anchors, 6646 vertices), one solo Lucian and common combat world, no
laboratory arena, actual Basic completion/control restoration and snapshot disk
write/read in a Library-only slot. This does not prove a real save reload, a zone
playthrough or manual production feel. Both the refused and passed recipes are
retained. Final regression including runtime zone resolution passes 419 / 0 / 1
(the same Explicit read-only full-inventory test was not executed). Seventeen
script hashes and all eleven delivered asset/metadata hashes match the isolated
validation. No validation Editor remains running.
Evidence: Logs/CombatProduction*.log and Library/ProductionCombatMigration/CombatFeel.
The two control gaps found at this stage (runtime-only ConfigureTurnRates and
world cursor prefab/+3 Y) are resolved by the continuation documented above.
Full zone/encounter, save and multiplayer qualification remains open. No prior
test totals or installation receipt establish ReadyForProduction for these sources.

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
