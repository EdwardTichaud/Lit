using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        private CombatNetworkSkillCatalog.Entry clientEntry;
        private ulong clientEpoch, clientGeneration, appliedThreatRevision;
        private bool clientActionConsumed;
        private ulong appliedHealthRevision;
        private NetworkObject clientCapturedTarget;
        private Vector3 clientCapturedDirection;

        private bool StartLocalPresentation(CombatNetworkSkillCatalog.Entry entry, float normalizedTime, bool server)
        {
            if (presentation == null || actor == null) return Diagnose("Actor has no external presentation session API.");
            if (presentation.IsActionActive) return Diagnose("An existing actor presentation owns this actor; it was not replaced.");
            presentation.ResolveReferences(animator, bridge);
            var family = CombatNetworkSkillCatalog.Classify(entry.skill, out _);
            int session = presentation.BeginExternalPresentation(this, preserveAirborneHold: family == CombatNetworkSkillFamily.AirBasic);
            if (session == 0) return false;
            presentationSession = session;
            if (!presentation.ConfigureExternalSkill(session, entry.skill)) { StopLocalPresentation(); return false; }
            if (server && family == CombatNetworkSkillFamily.Retreat &&
                (!IsServer || !presentation.ConfigureExternalRetreatBraking(session,
                    combatWorld != null && combatWorld.isActiveAndEnabled && combatWorld.freeAttacks != null && combatWorld.freeAttacks.enabled
                        ? combatWorld.freeAttacks.retreatLandingDeceleration : 0)))
            { StopLocalPresentation(); return Diagnose("Server retreat braking profile is invalid."); }
            if (!presentation.RegisterActionTermination(session, reason => OnPresentationTerminated(session, reason)))
            { StopLocalPresentation(); return false; }
            actor.PushScriptedMovementSuppression(); movementSuppressed = true;
            if (!CombatNetworkSkillCatalog.HasPresentation(family)) return true;
            int stateHash = Animator.StringToHash(entry.skill.AnimatorState);
            if (!presentation.CanPlayState(entry.skill.AnimatorState)) { StopLocalPresentation(); return false; }
            stateMotion?.SetActionPolicy(stateHash, server && family != CombatNetworkSkillFamily.PasDeRecul && family != CombatNetworkSkillFamily.ContactDash ? entry.skill.Presentation.movementPolicy : PlayerActionMovementPolicy.VisualOnly);
            if (CombatNetworkSkillCatalog.IsNativeAttack(family))
            {
                var context = server && family == CombatNetworkSkillFamily.ContactDash ? CaptureDashContext(active, entry.skill, session) :
                    new PlayerAttackContext(session, entry.skill, transform.position, server ? active.Direction : clientCapturedDirection,
                        ResolveEnemy(server ? active.Target : clientCapturedTarget), entry.skill.targetingMode == SkillTargetingMode.LockedTarget);
                if (!presentation.SetExternalAttackContext(session, context))
                { StopLocalPresentation(); return Diagnose("Actor rejected the server session's external attack context."); }
            }
            if (family == CombatNetworkSkillFamily.ContactDash && !presentation.TryPrepareExternalContactSkill(session, entry.skill))
            { StopLocalPresentation(); return Diagnose("Dash Animator is missing its authored contact-animation slot."); }
            animator.CrossFadeInFixedTime(stateHash, Mathf.Clamp(entry.skill.Presentation.entryBlendSeconds, 0, .25f), 0,
                Mathf.Clamp01(normalizedTime) * entry.skill.AnimationClip.length);
            if (server && family == CombatNetworkSkillFamily.ContactDash)
            {
                if (!IsServer || !TryGetWorldContactDashDistance(out float freeDashDistance))
                { StopLocalPresentation(); return Diagnose("Server dash world profile is no longer available or valid."); }
                var ticket = active.Ticket;
                if (!presentation.TryBeginExternalContactLunge(session, entry.skill,
                    (target, point, direction) => ResolveDashContact(ticket, session, target, point, direction), freeDashDistance,
                    combatWorld.freeAttacks.contactDashMotion))
                { StopLocalPresentation(); return Diagnose("Dash could not acquire the existing actor contact motor."); }
            }
            if (server && family == CombatNetworkSkillFamily.AirBasic && entry.skill is BasicSkillsSO basic && basic.HoldsAirborneDuringAnimation)
            {
                airborneHandle = bridge.AcquireCombatAirborneHold(this, entry.skill.AnimationClip.length * 3 + 2);
                if (airborneHandle == 0) { StopLocalPresentation(); return Diagnose("UCC rejected the authoritative aerial hold."); }
            }
            if (server && family == CombatNetworkSkillFamily.PasDeRecul && !StartPlanarMove(entry.skill, session))
            { StopLocalPresentation(); return false; }
            if (server && family == CombatNetworkSkillFamily.GroundImpact &&
                !presentation.BeginExternalGroundImpact(session, entry.skill, ResolveServerGroundImpact))
            { StopLocalPresentation(); return false; }
            return true;
        }

        private void OnPresentationTerminated(int session, PlayerActionPresentationController.ActionEndReason reason)
        {
            if (presentationSession != session) return;
            presentationSession = 0;
            ReleaseExecutionMotion();
            ReleaseMovementSuppression();
            if (!IsServer || !hasAction || ending || authority == null) return;
            if (reason == PlayerActionPresentationController.ActionEndReason.Completed) authority.Complete(active.Ticket);
            else authority.Cancel(active.Ticket);
        }

        private void StopLocalPresentation()
        {
            ReleaseExecutionMotion();
            int session = presentationSession; presentationSession = 0;
            if (presentation != null && session != 0 && presentation.IsCurrentSession(session)) presentation.EndExternalPresentation(session);
            ReleaseMovementSuppression();
        }

        private void ReleaseMovementSuppression()
        {
            if (!movementSuppressed) return;
            movementSuppressed = false;
            if (actor != null) actor.PopScriptedMovementSuppression();
        }

        private bool AcceptActorEvent(AnimationEvent source)
        {
            var entry = IsServer ? activeEntry : clientEntry;
            if (!IsSpawned || entry == null || presentationSession == 0 || presentation == null ||
                !presentation.IsCurrentSession(presentationSession) || source == null || !source.isFiredByAnimator ||
                animator == null || !animator.isActiveAndEnabled || source.animatorClipInfo.clip != entry.skill.AnimationClip ||
                source.animatorClipInfo.weight <= 0 || source.animatorStateInfo.fullPathHash != Animator.StringToHash(entry.skill.AnimatorState)) return false;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : current;
            int expected = Animator.StringToHash(entry.skill.AnimatorState);
            // A self-transition has two indistinguishable clip/state sources. Neither may commit an impact during that blend.
            if (animator.IsInTransition(0) && current.fullPathHash == expected && next.fullPathHash == expected) return false;
            return current.fullPathHash == expected || next.fullPathHash == expected;
        }

        internal void OnWeaponEvent(AnimationEvent source, bool visible)
        {
            if (!AcceptActorEvent(source) && !AcceptNetworkTimelineEvent(source)) return;
            if (visible) presentation.HandleShowSword(); else presentation.HandleHideSword();
        }

        internal void OnBowEvent(AnimationEvent source, bool visible)
        {
            if (!AcceptActorEvent(source) && !AcceptNetworkTimelineEvent(source)) return;
            if (visible) presentation.HandleShowBow(); else presentation.HandleHideBow();
        }

        internal void OnVfxEvent(AnimationEvent source, int index)
        {
            if (IsClient && AcceptNetworkTimelineEvent(source) && cinematicDefinition is SkillSO cinematicSkill)
            {
                presentation.PlayExternalSkillVfx(cinematicPresentationGeneration, cinematicSkill, index);
                return;
            }
            if (!IsClient || !AcceptActorEvent(source)) return;
            presentation.PlayExternalSkillVfx(presentationSession, (IsServer ? activeEntry : clientEntry).skill, index);
        }

        private bool AcceptNetworkTimelineEvent(AnimationEvent source) => source != null && source.isFiredByAnimator &&
            HasNetworkCinematic && actorCinematicPlayback?.ActiveRig != null && animator != null &&
            CombatCinematicPlaybackService.IsActivePlayerClip(actorCinematicPlayback.ActiveRig.Director, animator, source.animatorClipInfo.clip);

        private void OnActionStateChanged(CombatNetworkActionSnapshot before, CombatNetworkActionSnapshot after)
        { if (!IsServer) ApplyClientAction(after); }

        private void ApplyClientAction(CombatNetworkActionSnapshot snapshot)
        {
            if (IsServer || snapshot.Generation == 0 || !catalogValid) return;
            if (snapshot.Epoch == clientEpoch && snapshot.Generation < clientGeneration) return;
            acceptedCooldowns[snapshot.SkillId] = new CombatNetworkCooldownSnapshot
            {
                SkillId = snapshot.SkillId, Remaining = snapshot.CooldownRemaining,
                SampledAt = snapshot.StartedAt, TimeScale = snapshot.CooldownTimeScale
            };
            bool changed = snapshot.Epoch != clientEpoch || snapshot.Generation != clientGeneration;
            if (changed)
            {
                StopLocalPresentation(); clientActionConsumed = false;
                clientEpoch = snapshot.Epoch; clientGeneration = snapshot.Generation;
            }
            if (!snapshot.Active) { StopLocalPresentation(); clientActionConsumed = true; return; }
            if (clientActionConsumed || presentationSession != 0) return;
            if (!catalog.TryGet(snapshot.SkillId, out var entry) || entry.CanonicalGuid != snapshot.Guid.ToString())
            { Diagnose("Confirmed action has an unknown/mismatched authored GUID; no local animation fallback."); clientActionConsumed = true; return; }
            var family = CombatNetworkSkillCatalog.Classify(entry.skill, out string reason);
            if (family == CombatNetworkSkillFamily.Unsupported) { Diagnose(reason); clientActionConsumed = true; return; }
            if (!CombatNetworkSkillCatalog.HasPresentation(family)) return;
            clientCapturedTarget = null;
            if (snapshot.HasTarget && !snapshot.Target.TryGet(out clientCapturedTarget, NetworkManager)) return;
            clientCapturedDirection = snapshot.Direction;
            float elapsed = (float)Math.Max(0, NetworkManager.ServerTime.Time - snapshot.StartedAt);
            float normalized = elapsed / entry.skill.AnimationClip.length;
            if (normalized >= 1) { clientActionConsumed = true; return; }
            clientEntry = entry;
            if (!StartLocalPresentation(entry, normalized, false)) return;
            clientActionConsumed = true;
            RaiseConfirmedAction(entry.skill, new CombatActionTicket(snapshot.Epoch, NetworkObjectId, snapshot.Generation));
        }

        [ClientRpc]
        private void ConfirmedImpactClientRpc(int skillId, FixedString64Bytes guid, ulong epoch, ulong generation,
            NetworkObjectReference targetReference, int amount, bool hasContactPosition, Vector3 contactPosition)
        {
            if (IsServer || amount <= 0 || !catalogValid || !catalog.TryGet(skillId, out var entry) || entry.CanonicalGuid != guid.ToString() ||
                hasContactPosition && !CombatCommandPolicy.Finite(contactPosition) ||
                !targetReference.TryGet(out var identity, NetworkManager)) return;
            var target = ResolveEnemy(identity);
            if (target?.Root == null) return;
            CombatDamageWorldFeedback.Show(target.Root, amount, Color.white, 2f);
            PlayImpactCues(entry.skill, target, generation, hasContactPosition ? contactPosition : (Vector3?)null);
        }

        private void PlayImpactCues(SkillSO skill, ICombatTarget target, ulong generation = 0, Vector3? contactPosition = null)
        {
            if (target?.Root == null) return;
            var point = target.LockPoint != null ? target.LockPoint : target.Root;
            if (generation != 0 && target.Root.TryGetComponent<CombatContactPose>(out var contactPose))
                contactPose.PlayContact(transform.position, Mathf.InverseLerp(10, 35, skill.Damages));
            CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(skill, point, transform, contactPosition);
            presentation?.PlayConfirmedSkillContactVfx(skill, point, contactPosition, unchecked((int)generation));
        }

        private void OnHealthChanged(CombatNetworkHealthSnapshot before, CombatNetworkHealthSnapshot after) { if (!IsServer) ApplyClientHealth(); }
        private void ApplyClientHealth()
        {
            ApplyHealthSnapshot(healthState.Value);
        }

        private void ApplyHealthSnapshot(CombatNetworkHealthSnapshot snapshot)
        {
            if (IsServer || snapshot.Revision == 0 || snapshot.Revision <= appliedHealthRevision ||
                snapshot.Maximum <= 0 || snapshot.Current < 0 || snapshot.Current > snapshot.Maximum) return;
            if (actor != null) actor.SetHealth(snapshot.Current, snapshot.Maximum);
            else if (characterHealth != null) characterHealth.SetHealth(snapshot.Current, snapshot.Maximum);
            else { Diagnose("Confirmed health has no authored actor/CharacterInfo receiver."); return; }
            appliedHealthRevision = snapshot.Revision;
        }

        private void OnActorHealthChanged(SquadCharacterController changed) => PublishHealthSnapshot(true);
        private void OnCharacterHealthChanged(CharacterInfo changed) => PublishHealthSnapshot(true);

        private void PublishHealthSnapshot(bool reliable = false)
        {
            if (!IsServer || !IsSpawned) return;
            int maximum = IsPlayer ? actor.MaxHp : characterHealth != null ? characterHealth.MaxHp : 0;
            int current = IsPlayer ? actor.CurrentHp : characterHealth != null ? characterHealth.CurrentHp : 0;
            if (maximum <= 0) return;
            var snapshot = healthState.Value;
            if (snapshot.Revision == 0 || snapshot.Current != current || snapshot.Maximum != maximum)
            {
                snapshot = new CombatNetworkHealthSnapshot { Revision = snapshot.Revision + 1, Current = current, Maximum = maximum };
                healthState.Value = snapshot;
            }
            if (reliable) ConfirmedHealthClientRpc(snapshot);
        }

        [ClientRpc]
        private void ConfirmedHealthClientRpc(CombatNetworkHealthSnapshot snapshot) => ApplyHealthSnapshot(snapshot);

        private void OnWorldThreatsChanged(SquadCharacterController changed)
        { if (IsServer && changed == actor) PublishThreatSnapshot(); }

        private void PublishThreatSnapshot()
        {
            if (!IsServer || !IsSpawned || combatWorld == null || actor == null) return;
            var threats = combatWorld.GetThreats(actor);
            if (threats.Count == 0 && !HasNetworkCinematic && serverThreshold == null) ClearNetworkCombatClarity();
            if (threats.Count > 64) { Diagnose("Owner threat snapshot exceeds the supported limit (64); previous complete snapshot retained."); return; }
            var identities = new List<NetworkObject>();
            foreach (var threat in threats)
            {
                var identity = threat?.Root != null ? threat.Root.GetComponentInParent<NetworkObject>() : null;
                if (identity == null || !identity.IsSpawned || identity.NetworkManager != NetworkManager)
                { Diagnose("Owner threat has no spawned identity; previous complete snapshot retained."); return; }
                identities.Add(identity);
            }
            identities.Sort((a, b) => a.NetworkObjectId.CompareTo(b.NetworkObjectId));
            var previous = engagementState.Value;
            bool same = previous.Threats != null && previous.Threats.Length == identities.Count;
            for (int index = 0; same && index < identities.Count; index++) same &= previous.Threats[index].Equals(new NetworkObjectReference(identities[index]));
            if (same) return;
            var snapshot = new CombatNetworkThreatSnapshot { Revision = previous.Revision + 1, Threats = new NetworkObjectReference[identities.Count] };
            for (int index = 0; index < identities.Count; index++) snapshot.Threats[index] = new NetworkObjectReference(identities[index]);
            engagementState.Value = snapshot;
        }

        private void OnEngagementChanged(CombatNetworkThreatSnapshot before, CombatNetworkThreatSnapshot after) => ApplyOwnerEngagement();

        private void ApplyOwnerEngagement()
        {
            if (!IsSpawned || !IsOwner || !IsAssigned(OwnerClientId) || actor == null) return;
            var manager = RealTimeCombatManager.Instance;
            if (manager == null || manager.PlayerRoot != actor.transform) { appliedThreatRevision = 0; return; }
            var snapshot = engagementState.Value;
            if (snapshot.Revision == 0 || snapshot.Revision == appliedThreatRevision) return;
            var resolved = new List<ICombatTarget>();
            foreach (var reference in snapshot.Threats ?? Array.Empty<NetworkObjectReference>())
            {
                if (!reference.TryGet(out var identity, NetworkManager)) return; // Keep prior complete snapshot until identities resolve.
                var target = ResolveEnemy(identity);
                if (target == null) return;
                resolved.Add(target);
            }
            manager.ApplyConfirmedWorldThreats(resolved);
            appliedThreatRevision = snapshot.Revision;
        }
    }
}
