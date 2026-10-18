using System;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        private bool basicImpactResolved;
        private int justiceDamage;
        private Action<bool> bufferedBasic;
        [SerializeField, Min(.01f)] private float basicBufferSeconds = .85f;
        private double bufferedBasicAt;
        private float previousActionProgress = float.NaN, stalledActionSeconds;
        private bool chargedAscentActive;

        internal bool TryBufferBasicCommand(ulong sender, ulong sequence, int skillId, Vector3 direction, Action<bool> dispatch)
        {
            if (!IsServer || !IsSpawned || !hasAction || ending || bufferedBasic != null || dispatch == null ||
                !CombatCommandPolicy.Finite(basicBufferSeconds) || basicBufferSeconds <= 0 ||
                !admitted || authority == null || !authority.CanDeferCommand(sender, NetworkObject, sequence, skillId) || admittedWorld != authority.WorldGeneration || sender != OwnerClientId ||
                !IsAssigned(sender) || !IsAlive() || !CombatCommandPolicy.Finite(direction) ||
                !CombatNetworkSkillCatalog.IsBasic(activeFamily) || !catalogValid || !catalog.TryGet(skillId, out var next) ||
                !Owns(next.skill) || !CombatNetworkSkillCatalog.IsBasic(CombatNetworkSkillCatalog.Classify(next.skill, out _)) ||
                ((BasicSkillsSO)next.skill).Context != ((BasicSkillsSO)activeEntry.skill).Context ||
                presentation == null || !presentation.IsCurrentSession(presentationSession) || !authority.IsCurrent(active.Ticket)) return false;
            // Intent only: no ticket, cooldown, movement or effects until the original authority gate accepts it.
            bufferedBasic = dispatch;
            bufferedBasicAt = Time.unscaledTimeAsDouble;
            return true;
        }

        public bool CanReplace(CombatCommandContext current, CombatCommandContext next)
        {
            if (!hasAction || !SameTicket(current.Ticket, active.Ticket) || presentation == null || chargeHeld ||
                !presentation.IsCurrentSession(presentationSession) || !catalog.TryGet(next.SkillId, out var entry)) return false;
            bool ruptureHandoff = entry.skill.IsGroundImpactSkill && entry.skill.groundImpact.interruptibleSkill == activeEntry.skill;
            if (!ruptureHandoff && !presentation.CanStartAction) return false;
            return CanStartAction(next, true);
        }

        public bool CanStart(CombatCommandContext context) => CanStartAction(context, false);

        private bool CanStartAction(CombatCommandContext context, bool replacing)
        {
            if (!IsServer || !IsSpawned || !admitted || Time.timeScale <= 0 || authority == null || context.Actor != NetworkObject ||
                admittedWorld != authority.WorldGeneration || !IsAssigned(OwnerClientId) || !IsAlive() ||
                !catalogValid || !catalog.TryGet(context.SkillId, out var entry) || context.Skill != entry.skill ||
                !Owns(entry.skill) || hasAction && !replacing ||
                !replacing && !SkillHealing.CanCast(actor)) return Diagnose("Action rejected: actor/source/session, loadout, assignment or actor presentation is unavailable/busy.");
            var family = CombatNetworkSkillCatalog.Classify(entry.skill, out string diagnostic);
            if (family == CombatNetworkSkillFamily.Unsupported) return Diagnose(diagnostic);
            if (family == CombatNetworkSkillFamily.Cinematic) return CanStartNetworkCinematic(context);
            if (!CombatNetworkSkillCatalog.IsBasic(family) && input != null && !input.IsLegacySupportReady(entry.skill))
                return Diagnose("Skill is still cooling down through the legacy support RPC route.");
            if (CombatNetworkSkillCatalog.HasPresentation(family))
            {
                if (presentation == null || !presentation.isActiveAndEnabled || animator == null || relay == null || !relay.IsBoundTo(this) || !presentation.CanPlayState(entry.skill.AnimatorState) ||
                    bridge == null || !bridge.IsDriving || !replacing && (actor.IsMovementInputSuppressed || presentation.IsActionActive) ||
                    replacing && bridge.IsExternalLockActive && !presentation.OwnsOnlyActionMotionLock(bridge))
                    return Diagnose("Action requires an available actor session, Animator/event relay and driving UCC.");
                if (CombatNetworkSkillCatalog.IsBasic(family) && (family == CombatNetworkSkillFamily.AirBasic) == bridge.Grounded)
                    return Diagnose("Skill context does not match the authoritative grounded/airborne actor state.");
                if (family == CombatNetworkSkillFamily.ContactDash) return CanStartContactDash(context, entry);
                if (family == CombatNetworkSkillFamily.Charged && (!bridge.Grounded || !CanCommitChargedSkill((ChargedSkillSO)entry.skill)))
                    return Diagnose("Charge requires its server-confirmed held input and a grounded actor.");
                if (family == CombatNetworkSkillFamily.GroundImpact) return presentation.CanStartGroundImpact(entry.skill, replacing);
                if (family == CombatNetworkSkillFamily.Retreat && (context.Target == null || serverManualLock != context.Target))
                    return Diagnose("Retreat requires the actor's confirmed manual lock.");
                if (family == CombatNetworkSkillFamily.PasDeRecul)
                    return context.Direction.sqrMagnitude > .0001f && presentation.GetSafePlanarTravel(context.Direction, entry.skill.control.distance) > .02f;
                if (family == CombatNetworkSkillFamily.Justice &&
                    (actor.GetComponent<PlayerProtectionEffects>() == null || Mathf.RoundToInt(actor.GetComponent<PlayerProtectionEffects>().Reserve) <= 0))
                    return Diagnose("Juste Retour requires a positive server-owned reserve.");
                if (entry.skill.Presentation.movementPolicy == PlayerActionMovementPolicy.StateTrajectory)
                {
                    var profile = stateMotion != null && stateMotion.Library != null ? stateMotion.Library.Find(Animator.StringToHash(entry.skill.AnimatorState)) : null;
                    if (profile == null || profile.initialUpwardSpeed > 0 || family == CombatNetworkSkillFamily.AirBasic && !profile.allowAirborne)
                        return Diagnose("Basic trajectory is missing or requires an unsupported airborne impulse.");
                }
                return context.Target == null || EnemyEligible(context.Target, entry, false);
            }
            if (family == CombatNetworkSkillFamily.HealSelf)
                return SkillHealing.CalculateAmount(actor.CurrentHp, actor.MaxHp, entry.skill.healingPercent) > 0 || Diagnose("Self heal has no missing health.");
            if (family == CombatNetworkSkillFamily.HealArea)
            {
                authority.CopyRegisteredActors(candidates);
                foreach (var candidate in candidates)
                    if (EligibleAlly(candidate, entry.skill.healingRadius) &&
                        SkillHealing.CalculateAmount(candidate.GetComponent<SquadCharacterController>().CurrentHp,
                            candidate.GetComponent<SquadCharacterController>().MaxHp, entry.skill.healingPercent) > 0) return true;
                return Diagnose("Area heal has no living registered ally with missing health in range.");
            }
            if (family == CombatNetworkSkillFamily.ProtectionAura) return true;
            if (family == CombatNetworkSkillFamily.Sommation)
            {
                if (controlStates.Count >= 64) return Diagnose("Actor reached the supported active-control-effect limit (64).");
                authority.CopyRegisteredActors(candidates);
                foreach (var candidate in candidates) if (TauntEligible(candidate, entry)) return true;
                return Diagnose("Sommation has no eligible registered enemy in radius.");
            }
            if (family == CombatNetworkSkillFamily.Regeneration)
                return !(passiveEntry == entry && passiveTarget == context.Target) && EligibleAlly(context.Target, entry.skill.healingRadius) && context.Target != NetworkObject &&
                    PlayerRegenerationEffect.IsValidRegenerationRecipient(actor, context.Target.GetComponent<SquadCharacterController>());
            if (family == CombatNetworkSkillFamily.LifeSteal) return !(passiveEntry == entry && passiveTarget == context.Target) && EnemyEligible(context.Target, entry, false);
            if (family == CombatNetworkSkillFamily.ProtectionLink)
                return EligibleAlly(context.Target, entry.skill.protection.radius);
            if (controlStates.Count >= 64) return Diagnose("Actor reached the supported active-control-effect limit (64).");
            if (!EnemyEligible(context.Target, entry, false)) return Diagnose("Control requires a living registered enemy with clear line of sight.");
            return family != CombatNetworkSkillFamily.Entrave || EnemyEntraveZone.TryFindGround(ResolveEnemy(context.Target).Root, out _) ||
                Diagnose("Entrave has no valid server-ground sample.");
        }

        public bool TryStart(CombatCommandContext context)
        {
            if (!CanStart(context)) return false;
            active = context; catalog.TryGet(context.SkillId, out activeEntry);
            activeFamily = CombatNetworkSkillCatalog.Classify(activeEntry.skill, out _);
            hasAction = true; basicImpactResolved = false; observedAnimatorState = false;
            previousActionProgress = float.NaN; stalledActionSeconds = 0; chargedAscentActive = false;
            dashWindowArmed = false; contactVictim = null;
            actionStartedAt = Time.unscaledTimeAsDouble;
            justiceDamage = 0;
            if (activeFamily == CombatNetworkSkillFamily.Cinematic)
            {
                if (!StartNetworkCinematic(context)) return false;
                input?.MirrorAcceptedSupportCooldown(activeEntry.skill);
                return true;
            }
            if (CombatNetworkSkillCatalog.HasPresentation(activeFamily) && !StartLocalPresentation(activeEntry, 0, true)) return false;
            if (activeFamily == CombatNetworkSkillFamily.Justice) justiceDamage = PlayerProtectionEffects.Get(actor).ConsumeReserve();
            publishPending = CombatNetworkSkillCatalog.HasPresentation(activeFamily);
            supportPending = !publishPending;
            if (!CombatNetworkSkillCatalog.IsBasic(activeFamily)) input?.MirrorAcceptedSupportCooldown(activeEntry.skill);
            return true;
        }

        public bool CanApplyImpact(CombatCommandContext context, NetworkObject target, int impactEvent)
        {
            if (!IsServer || !hasAction || activeEntry == null || context.Actor != NetworkObject || !SameTicket(context.Ticket, active.Ticket) ||
                context.Skill != activeEntry?.skill || context.SkillId != activeEntry.Id ||
                admittedWorld != authority.WorldGeneration || !admitted || !IsAlive() || !IsAssigned(OwnerClientId) ||
                !Owns(activeEntry.skill) || impactEvent != 0) return false;
            if (activeFamily == CombatNetworkSkillFamily.Cinematic) return CanApplyNetworkCinematicImpact(context, target);
            if (CombatNetworkSkillCatalog.IsNativeAttack(activeFamily))
                return serverEventActive && presentation != null && presentation.IsCurrentSession(presentationSession) &&
                    (activeFamily == CombatNetworkSkillFamily.ContactDash ? dashWindowArmed && target == contactVictim && EnemyEligible(target, activeEntry, false) :
                        activeFamily == CombatNetworkSkillFamily.GroundImpact ? GroundAreaEligible(target) : EnemyEligible(target, activeEntry, true));
            if (!applyingSupport) return false;
            if (activeFamily == CombatNetworkSkillFamily.Sommation) return TauntEligible(target, activeEntry);
            if (activeFamily == CombatNetworkSkillFamily.HealSelf) return target == NetworkObject && EligibleAlly(target, 0);
            if (activeFamily == CombatNetworkSkillFamily.HealArea) return EligibleAlly(target, activeEntry.skill.healingRadius);
            if (activeFamily == CombatNetworkSkillFamily.ProtectionAura) return target == NetworkObject;
            if (activeFamily == CombatNetworkSkillFamily.Regeneration) return target == active.Target && EligibleAlly(target, activeEntry.skill.healingRadius);
            if (activeFamily == CombatNetworkSkillFamily.LifeSteal) return target == active.Target && EnemyEligible(target, activeEntry, false);
            if (activeFamily == CombatNetworkSkillFamily.ProtectionLink) return target == active.Target && EligibleAlly(target, activeEntry.skill.protection.radius);
            return target == active.Target && EnemyEligible(target, activeEntry, false);
        }

        public void End(CombatCommandContext context, CombatActionEndReason reason)
        {
            if (!hasAction || !SameTicket(context.Ticket, active.Ticket)) return;
            if (reason != CombatActionEndReason.Completed) { StopCharge(); chargeRequiresRelease = true; }
            var buffered = bufferedBasic;
            bufferedBasic = null;
            ending = true;
            try
            {
                if (activeFamily == CombatNetworkSkillFamily.Cinematic) StopNetworkCinematic(reason == CombatActionEndReason.Completed);
                hasAction = publishPending = supportPending = serverEventActive = false;
                if (IsSpawned && IsServer)
                {
                    var current = actionState.Value;
                    current.Active = false; actionState.Value = current;
                }
                StopLocalPresentation();
                if (reason == CombatActionEndReason.Death || reason == CombatActionEndReason.SceneChanged ||
                    reason == CombatActionEndReason.Despawn || reason == CombatActionEndReason.Disconnect ||
                    reason == CombatActionEndReason.OwnershipChanged || reason == CombatActionEndReason.AuthorityDisabled)
                    ClearOwnedEffects();
                activeEntry = null;
            }
            finally { ending = false; }
            buffered?.Invoke(reason == CombatActionEndReason.Completed && BufferIsFresh());
        }

        private bool BufferIsFresh() => CombatCommandPolicy.Finite(basicBufferSeconds) && basicBufferSeconds > 0 &&
            Time.unscaledTimeAsDouble >= bufferedBasicAt && Time.unscaledTimeAsDouble - bufferedBasicAt <= basicBufferSeconds;

        internal void OnAnimationImpact(AnimationEvent source)
        {
            if (TryHandleNetworkCinematicImpact(source)) return;
            if (!IsServer || !hasAction || !CombatNetworkSkillCatalog.IsNativeAttack(activeFamily) || activeFamily == CombatNetworkSkillFamily.ContactDash || basicImpactResolved ||
                !AcceptActorEvent(source) || !authority.IsCurrent(active.Ticket) ||
                !CombatNetworkSkillCatalog.TryGetImpactEvent(activeEntry.skill, out float expected, out _) ||
                Mathf.Abs(source.time - expected) > .001f ||
                (source.functionName != "HitEnemy" && source.functionName != "ResolveSkillImpact" &&
                 source.functionName != "ResolveSkillImpactAndRetreat" && source.functionName != "ChargedSkillContact")) return;
            basicImpactResolved = true;
            serverEventActive = true;
            try
            {
                NetworkObject target = active.Target;
                bool requiresCapturedTarget = activeEntry.skill.targetingMode == SkillTargetingMode.RequiredTarget ||
                    activeEntry.skill.targetingMode == SkillTargetingMode.LockedTarget;
                if (target != null && !EnemyEligible(target, activeEntry, true) && !requiresCapturedTarget) target = null;
                if (target == null)
                {
                    if (requiresCapturedTarget) return;
                    authority.CopyRegisteredActors(candidates);
                    double bestAngle = double.PositiveInfinity, bestDistance = double.PositiveInfinity;
                    Vector3 intent = active.Direction.sqrMagnitude > .0001f ? active.Direction : transform.forward;
                    foreach (var candidate in candidates)
                    {
                        if (!EnemyEligible(candidate, activeEntry, true)) continue;
                        double distance = (candidate.transform.position - transform.position).sqrMagnitude;
                        double angle = Vector3.Angle(intent, Vector3.ProjectOnPlane(candidate.transform.position - transform.position, Vector3.up));
                        if (CombatNetworkTargetRanking.IsBetter(angle, distance, candidate.NetworkObjectId,
                            bestAngle, bestDistance, target != null ? target.NetworkObjectId : ulong.MaxValue))
                        { bestAngle = angle; bestDistance = distance; target = candidate; }
                    }
                }
                if (target != null)
                {
                    var ticket = active.Ticket;
                    var victim = target;
                    Action contact = () =>
                    {
                        if (!hasAction || !SameTicket(ticket, active.Ticket) || !authority.IsCurrent(ticket)) return;
                        bool previousEvent = serverEventActive;
                        serverEventActive = true;
                        try { if (authority.TryConsumeImpact(ticket, victim)) DeliverBasicImpact(victim); }
                        finally { serverEventActive = previousEvent; }
                    };
                    if (!CombatNetworkSkillCatalog.IsBasic(activeFamily) || combatWorld == null ||
                        !combatWorld.QueueClashImpact(transform, activeEntry.skill, contact)) contact();
                }
                if (hasAction && activeFamily == CombatNetworkSkillFamily.Retreat)
                    presentation.HandleConfirmedSkillRetreat(activeEntry.skill, ResolveEnemy(active.Target)?.Root);
            }
            finally { serverEventActive = false; }
        }

        private int DeliverBasicImpact(NetworkObject targetObject, int? damageOverride = null)
        {
            var entry = activeEntry;
            var ticket = active.Ticket;
            var target = ResolveEnemy(targetObject);
            if (target == null || target.IsDead) return 0;
            int damage = damageOverride ?? (activeFamily == CombatNetworkSkillFamily.Justice ? justiceDamage : Mathf.Max(0, Mathf.RoundToInt(entry.skill.Damages)));
            int applied;
            bool accepted;
            var brain = target.Root.GetComponent<LitBrainsEnemy>();
            if (brain != null)
                applied = brain.ReceiveImpact(new CombatImpact(damage, entry.skill.interruptionForce, CombatImpactOrigin.Basic, false, actor), out accepted);
            else
            {
                var legacy = target.Root.GetComponent<EnemyController>();
                if (legacy == null) { Diagnose("Target has no supported actor-attributed impact receiver."); return 0; }
                var defence = legacy.GetComponent<EnemyTacticalResponseController>();
                if (defence != null && defence.IsDodging) return 0;
                damage = EnemyRevelation.ModifyDamage(target.Root, damage);
                if (defence != null) damage = defence.ResolveIncomingPlayerDamage(entry.skill, damage);
                applied = legacy.ReceivePlayerContactDamage(damage, actor, out accepted);
            }
            if (accepted && combatWorld != null) combatWorld.TryEngageActor(actor, target);
            if (applied <= 0) return 0;
            GrantNetworkClarity(entry.skill.ClarityGainOnHit);
            if (activeFamily == CombatNetworkSkillFamily.Charged && entry.skill is ChargedSkillSO charged && !target.IsDead)
                chargedAscentActive = presentation.BeginExternalChargedLaunch(presentationSession, charged, CombatChargedTargets.Resolve(target));
            if (entry.skill.stunSeconds > 0 && target is ICombatStunReceiver stun) stun.TryApplySkillStun(entry.skill.stunSeconds);
            try { ServerImpactApplied?.Invoke(entry.skill, applied, target); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            if (IsClient) PlayImpactCues(entry.skill, target);
            ConfirmedImpactClientRpc(entry.Id, new Unity.Collections.FixedString64Bytes(entry.CanonicalGuid),
                ticket.AuthorityEpoch, ticket.Generation, new NetworkObjectReference(targetObject), applied);
            return applied;
        }

        private bool EnemyEligible(NetworkObject identity, CombatNetworkSkillCatalog.Entry entry, bool atImpact)
        {
            if (identity == null || identity == NetworkObject || authority == null || !authority.IsRegistered(identity)) return false;
            var target = ResolveEnemy(identity);
            if (target?.Root == null || !target.Root.gameObject.activeInHierarchy || target.IsDead) return false;
            var encounter = EnemyEncounterActors.Resolve(target.Root);
            if (!EnemyEncounterActors.IsAvailable(encounter) || encounter.IsSuspended ||
                encounter.BossEncounter != null && (!encounter.BossEncounter.IsBossEngaged || encounter.BossEncounter.IsBossResolved)) return false;
            Vector3 delta = target.Root.position - transform.position;
            if (!CombatCommandPolicy.Finite(delta) || Mathf.Abs(delta.y) > entry.heightTolerance) return false;
            float distance = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
            if (atImpact && (!entry.skill.IsWithinHitRange(distance) || Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(delta, Vector3.up)) > entry.contactArc * .5f)) return false;
            if (atImpact && !ContactVolumeContains(target, entry.skill)) return false;
            return ClearLine(target.Root, entry.obstructionMask);
        }

        private bool ContactVolumeContains(ICombatTarget target, SkillSO skill)
        {
            var profile = combatWorld != null ? combatWorld.freeAttacks : null;
            if (profile == null || !profile.enabled || target?.Root == null) return false;
            bool ranged = (skill.targetingMode == SkillTargetingMode.RequiredTarget || skill.targetingMode == SkillTargetingMode.LockedTarget) &&
                skill.MaximumHitDistance > 4;
            Vector3 direction = active.Direction.sqrMagnitude > .0001f ? active.Direction.normalized : transform.forward;
            var contacts = ranged
                ? Physics.OverlapCapsule(transform.position + Vector3.up, transform.position + Vector3.up + direction * skill.MaximumHitDistance,
                    .35f, ~0, QueryTriggerInteraction.Ignore)
                : Physics.OverlapSphere(transform.TransformPoint(profile.contactCenter), Mathf.Max(.01f, profile.contactRadius),
                    ~0, QueryTriggerInteraction.Ignore);
            foreach (var collider in contacts)
            {
                var candidate = RealTimeCombatManager.TargetFromCollider(collider);
                if (candidate?.Root == target.Root) return true;
            }
            return false;
        }

        private bool ClearLine(Transform target, LayerMask mask)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 delta = target.position + Vector3.up - origin;
            if (!CombatCommandPolicy.Finite(delta)) return false;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, mask, QueryTriggerInteraction.Ignore))
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.collider.transform != transform &&
                    !hit.collider.transform.IsChildOf(target) && hit.collider.transform != target) return false;
            return true;
        }

        private bool EligibleAlly(NetworkObject identity, float radius)
        {
            if (identity == null || authority == null || !authority.IsRegistered(identity) ||
                identity.GetComponent<CombatNetworkActorRuntime>() == null) return false;
            var ally = identity.GetComponent<SquadCharacterController>();
            return ally != null && ally.isActiveAndEnabled && ally.CharacterData != null && !ally.CharacterData.isEnemy && ally.CurrentHp > 0 &&
                CombatCommandPolicy.Finite(identity.transform.position) &&
                (identity.transform.position - transform.position).sqrMagnitude <= radius * radius;
        }

        private void MonitorServerAction()
        {
            if (bufferedBasic != null && !BufferIsFresh())
            {
                var expired = bufferedBasic; bufferedBasic = null;
                expired(false);
            }
            if (!hasAction || authority == null || !authority.IsCurrent(active.Ticket)) return;
            if (!Owns(activeEntry.skill)) { authority.Cancel(active.Ticket); return; }
            if (!CombatNetworkSkillCatalog.HasPresentation(activeFamily)) return;
            if (presentation == null || !presentation.IsCurrentSession(presentationSession)) { authority.Cancel(active.Ticket); return; }
            if (animator == null || !animator.isActiveAndEnabled) { authority.Cancel(active.Ticket); return; }
            if ((activeEntry.skill.targetingMode == SkillTargetingMode.RequiredTarget || activeEntry.skill.targetingMode == SkillTargetingMode.LockedTarget) &&
                (active.Target == null || ResolveEnemy(active.Target)?.IsDead != false))
            { authority.Cancel(active.Ticket); return; }
            if (activeFamily == CombatNetworkSkillFamily.PasDeRecul) return;
            if (activeFamily == CombatNetworkSkillFamily.AirBasic && activeEntry.skill is BasicSkillsSO basic &&
                basic.RequestsLandingDuringAnimation && bridge.ShouldBeginMotionHandoff(basic.AirborneLandingHandoff)) bridge.RequestCombatSkillLanding();
            var current = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            presentation.UpdateExternalSkillProgress(presentationSession, current.normalizedTime);
            if (presentation.IsRecoveryOpen && activeEntry.skill.Presentation.allowMoveAfterRecovery) ReleaseMovementSuppression();
            bool matching = current.fullPathHash == Animator.StringToHash(activeEntry.skill.AnimatorState);
            observedAnimatorState |= matching;
            var domain = GetComponent<CombatTimeDomain>();
            if (matching)
            {
                stalledActionSeconds = Mathf.Approximately(previousActionProgress, current.normalizedTime)
                    ? stalledActionSeconds + Mathf.Max(0, domain != null ? domain.DeltaTime : Time.deltaTime) : 0;
                previousActionProgress = current.normalizedTime;
                if (stalledActionSeconds >= 1) { Diagnose("Confirmed animation stopped progressing; only its session was released."); authority.Cancel(active.Ticket); return; }
            }
            if (activeFamily == CombatNetworkSkillFamily.GroundImpact && !basicImpactResolved || chargedAscentActive) return;
            if (matching && (current.normalizedTime >= 1 || bufferedBasic != null && basicImpactResolved &&
                CombatNetworkSkillCatalog.IsBasic(activeFamily) && CombatCommandPolicy.Finite(activeEntry.skill.Presentation.chainTransitionNormalizedTime) &&
                current.normalizedTime >= Mathf.Clamp01(activeEntry.skill.Presentation.chainTransitionNormalizedTime))) authority.Complete(active.Ticket);
            else if (observedAnimatorState && !matching || !observedAnimatorState && Time.unscaledTimeAsDouble - actionStartedAt > .8)
            {
                Diagnose("Animator left or failed to enter the confirmed Basic state; action cancelled without timer damage.");
                authority.Cancel(active.Ticket);
            }
        }

        private static bool SameTicket(CombatActionTicket a, CombatActionTicket b) => a.AuthorityEpoch == b.AuthorityEpoch &&
            a.ActorId == b.ActorId && a.Generation == b.Generation;
    }
}
