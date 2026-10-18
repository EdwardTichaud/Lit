using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        private bool applyingSupport, ownsProtection;
        private readonly Dictionary<ulong, Component> serverControlEffects = new Dictionary<ulong, Component>();
        private readonly Dictionary<ulong, Component> clientControlEffects = new Dictionary<ulong, Component>();
        private PlayerRegenerationEffect regeneration;
        private NetworkObject passiveTarget;
        private CombatNetworkSkillCatalog.Entry passiveEntry;
        private CombatDamageWorldFeedback passiveIndicator;
        private CombatNetworkPassiveSnapshot appliedPassive;
        private CombatCommandAuthority boundAuthority;

        private void ExecuteSupport()
        {
            var entry = activeEntry;
            var context = active;
            var family = activeFamily;
            bool applied = false;
            applyingSupport = true;
            try
            {
                if (family == CombatNetworkSkillFamily.HealSelf || family == CombatNetworkSkillFamily.HealArea)
                {
                    authority.CopyRegisteredActors(candidates);
                    foreach (var recipient in candidates)
                    {
                        if (family == CombatNetworkSkillFamily.HealSelf && recipient != NetworkObject) continue;
                        if (!EligibleAlly(recipient, family == CombatNetworkSkillFamily.HealSelf ? 0 : entry.skill.healingRadius)) continue;
                        var ally = recipient.GetComponent<SquadCharacterController>();
                        int amount = SkillHealing.CalculateAmount(ally.CurrentHp, ally.MaxHp, entry.skill.healingPercent);
                        if (amount <= 0 || !authority.TryConsumeImpact(context.Ticket, recipient)) continue;
                        int before = ally.CurrentHp;
                        ally.SetCurrentHp(before + amount);
                        int healed = Mathf.Max(0, ally.CurrentHp - before);
                        if (healed <= 0) continue;
                        applied = true;
                        if (IsClient) CombatDamageWorldFeedback.ShowMessage(ally.transform, "+" + healed + " PV", new Color(.3f, 1, .6f), 2.1f);
                        ConfirmedHealingClientRpc(new NetworkObjectReference(recipient), ally.CurrentHp, ally.MaxHp, healed);
                    }
                }
                else if (family == CombatNetworkSkillFamily.Sommation)
                {
                    authority.CopyRegisteredActors(candidates);
                    foreach (var recipient in candidates)
                    {
                        if (controlStates.Count >= 64) break;
                        if (!TauntEligible(recipient, entry) || !authority.TryConsumeImpact(context.Ticket, recipient)) continue;
                        var enemy = ResolveEnemy(recipient);
                        var brain = enemy.Root.GetComponent<LitBrainsEnemy>();
                        var legacy = enemy.Root.GetComponent<EnemyController>();
                        bool accepted = brain != null ? brain.TryReceiveTaunt(actor, entry.skill.protection.durationSeconds, entry.skill.protection.radius) :
                            legacy != null && legacy.TryReceiveTaunt(actor, entry.skill.protection.durationSeconds, entry.skill.protection.radius);
                        if (!accepted) continue;
                        var taunt = enemy.Root.GetComponent<EnemyTaunt>();
                        ulong generation = ControlEffectGeneration.Next();
                        taunt.Set(actor, entry.skill.protection.durationSeconds, generation);
                        serverControlEffects[generation] = taunt;
                        controlStates.Add(new CombatNetworkControlSnapshot
                        {
                            SkillId = entry.Id, Guid = new FixedString64Bytes(entry.CanonicalGuid), Generation = generation,
                            Target = new NetworkObjectReference(recipient), Position = enemy.Root.position,
                            Until = NetworkManager.ServerTime.Time + entry.skill.protection.durationSeconds
                        });
                        combatWorld?.TryEngageActor(actor, enemy);
                        applied = true;
                    }
                }
                else
                {
                    NetworkObject target = family == CombatNetworkSkillFamily.ProtectionAura ? NetworkObject : context.Target;
                    if (target != null && authority.TryConsumeImpact(context.Ticket, target))
                    {
                        if (family == CombatNetworkSkillFamily.ProtectionAura)
                        { applied = PlayerProtectionEffects.Get(actor).ToggleAura(entry.skill); ownsProtection |= applied; }
                        else if (family == CombatNetworkSkillFamily.ProtectionLink)
                        { applied = PlayerProtectionEffects.Get(actor).BeginLink(entry.skill, target.GetComponent<SquadCharacterController>()); ownsProtection |= applied; }
                        else if (family == CombatNetworkSkillFamily.Regeneration || family == CombatNetworkSkillFamily.LifeSteal)
                        {
                            EnsureRegeneration();
                            applied = family == CombatNetworkSkillFamily.Regeneration
                                ? regeneration.TryAssign(actor, target.GetComponent<SquadCharacterController>(), entry.skill)
                                : regeneration.TryAssignLifeSteal(actor, ResolveEnemy(target), entry.skill);
                            if (applied)
                            {
                                passiveTarget = target; passiveEntry = entry;
                                passiveState.Value = new CombatNetworkPassiveSnapshot
                                {
                                    SkillId = entry.Id, Guid = new FixedString64Bytes(entry.CanonicalGuid),
                                    Target = new NetworkObjectReference(target), Drain = family == CombatNetworkSkillFamily.LifeSteal
                                };
                            }
                        }
                        else
                        {
                            var enemy = ResolveEnemy(target);
                            ulong generation = ControlEffectGeneration.Next();
                            Vector3 point = enemy.Root.position;
                            if (family == CombatNetworkSkillFamily.Revelation)
                            {
                                var mark = enemy.Root.GetComponent<EnemyRevelation>() ?? enemy.Root.gameObject.AddComponent<EnemyRevelation>();
                                mark.Apply(actor, entry.skill.control.durationSeconds, entry.skill.control.strength, generation);
                                serverControlEffects[generation] = mark; applied = true;
                            }
                            else if (family == CombatNetworkSkillFamily.Entrave && EnemyEntraveZone.TryFindGround(enemy.Root, out point))
                            { serverControlEffects[generation] = EnemyEntraveZone.Create(actor, point, entry.skill, generation); applied = true; }
                            if (applied) controlStates.Add(new CombatNetworkControlSnapshot
                            {
                                SkillId = entry.Id, Guid = new FixedString64Bytes(entry.CanonicalGuid), Generation = generation,
                                Target = new NetworkObjectReference(target), Position = point, Until = NetworkManager.ServerTime.Time + entry.skill.control.durationSeconds
                            });
                        }
                    }
                }
            }
            catch (Exception exception) { Debug.LogException(exception, this); }
            finally { applyingSupport = false; }
            if (!applied)
            {
                string reason = entry.skill.name + ": accepted command could not commit its effect; no client-side fallback was run.";
                Diagnose(reason);
                SupportCommitFailedClientRpc(reason, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } });
            }
            else
            {
                PublishCooldowns();
                RaiseConfirmedAction(entry.skill, context.Ticket);
                // Reliable confirmation survives instant support start/end coalescing. Supports never own locomotion/presentation.
                ConfirmedSupportClientRpc(entry.Id, new FixedString64Bytes(entry.CanonicalGuid), context.Ticket.AuthorityEpoch,
                    context.Ticket.Generation, GetSkillCooldownRemaining(entry.skill), NetworkManager.ServerTime.Time, Time.timeScale);
            }
            PublishPersistentState();
            if (applied) authority.Complete(context.Ticket); else authority.Cancel(context.Ticket);
        }

        private void PublishPersistentState()
        {
            if (!IsServer || !IsSpawned) return;
            PublishHealthSnapshot();
            if (actor != null)
            {
                if (!IsAlive()) { ClearOwnedEffects(); return; }
            }
            for (int index = controlStates.Count - 1; index >= 0; index--)
            {
                var snapshot = controlStates[index];
                float remaining = 0;
                if (serverControlEffects.TryGetValue(snapshot.Generation, out var effect) && effect != null)
                {
                    if (effect is EnemyRevelation mark && mark.Generation == snapshot.Generation && mark.Owner == actor) remaining = mark.Remaining;
                    else if (effect is EnemyEntraveZone zone && zone.Owner == actor) remaining = zone.Remaining;
                    else if (effect is EnemyTaunt taunt && taunt.Target == actor && taunt.Generation == snapshot.Generation) remaining = taunt.Remaining;
                }
                if (remaining <= 0) { serverControlEffects.Remove(snapshot.Generation); controlStates.RemoveAt(index); }
                else
                {
                    // Native effects use game time. Never retire their cleanup ownership from a wall-clock estimate.
                    double until = NetworkManager.ServerTime.Time + remaining;
                    if (Math.Abs(snapshot.Until - until) > .05) { snapshot.Until = until; controlStates[index] = snapshot; }
                }
            }
            var effects = actor != null ? actor.GetComponent<PlayerProtectionEffects>() : null;
            if (effects != null && catalogValid)
            {
                int aura = effects.AuraSkill != null && catalog.TryGet(effects.AuraSkill, out var auraEntry) ? auraEntry.Id : 0;
                int link = effects.HasLink && catalog.TryGet(effects.LinkSkill, out var linkEntry) ? linkEntry.Id : 0;
                var allyIdentity = effects.LinkedAlly != null ? effects.LinkedAlly.GetComponentInParent<NetworkObject>() : null;
                protectionState.Value = new CombatNetworkProtectionSnapshot
                {
                    AuraId = aura, LinkId = link,
                    Ally = allyIdentity != null && allyIdentity.IsSpawned ? new NetworkObjectReference(allyIdentity) : default,
                    LinkUntil = link != 0 ? Math.Round(NetworkManager.ServerTime.Time + effects.LinkRemaining, 2) : 0,
                    Reserve = effects.Reserve
                };
            }
            PublishCooldowns(); PublishThreatSnapshot();
        }

        private void ValidatePersistentEffects()
        {
            if (!IsServer || !IsSpawned) return;
            for (int index = controlStates.Count - 1; index >= 0; index--)
            {
                var snapshot = controlStates[index];
                if (admitted && authority != null && admittedWorld == authority.WorldGeneration && IsAssigned(OwnerClientId) &&
                    snapshot.Target.TryGet(out var target, NetworkManager) && authority.IsRegistered(target) &&
                    catalogValid && catalog.TryGet(snapshot.SkillId, out var entry) && Owns(entry.skill)) continue;
                if (serverControlEffects.TryGetValue(snapshot.Generation, out var effect)) ReleaseControl(effect, snapshot.Generation);
                serverControlEffects.Remove(snapshot.Generation);
                controlStates.RemoveAt(index);
            }
            if (regeneration != null && passiveTarget != null &&
                (!admitted || authority == null || admittedWorld != authority.WorldGeneration ||
                 !authority.IsRegistered(passiveTarget) || !IsAssigned(OwnerClientId) || !IsAlive() || passiveEntry == null || !Owns(passiveEntry.skill)))
                regeneration.Clear();
            if (!IsAlive()) ClearOwnedEffects();
        }

        private void EnsureRegeneration()
        {
            if (regeneration != null) return;
            regeneration = GetComponent<PlayerRegenerationEffect>() ?? gameObject.AddComponent<PlayerRegenerationEffect>();
            regeneration.TickApplied += OnRegenerationTick;
            regeneration.LifeStealTickApplied += OnLifeStealTick;
            regeneration.Ended += OnPassiveEnded;
        }

        private void OnRegenerationTick(SquadCharacterController recipient, int amount)
        {
            if (!IsSpawned || !IsServer || recipient == null || amount <= 0) return;
            var identity = recipient.GetComponentInParent<NetworkObject>();
            if (identity != null && identity.IsSpawned) ConfirmedHealingClientRpc(new NetworkObjectReference(identity), recipient.CurrentHp, recipient.MaxHp, amount);
        }

        private void OnLifeStealTick(ICombatTarget recipient, int stolen, int healed)
        {
            if (!IsSpawned || !IsServer || stolen <= 0) return;
            combatWorld?.TryEngageActor(actor, recipient);
            if (healed > 0) ConfirmedHealingClientRpc(new NetworkObjectReference(NetworkObject), actor.CurrentHp, actor.MaxHp, healed);
            var identity = recipient?.Root != null ? recipient.Root.GetComponentInParent<NetworkObject>() : null;
            if (identity != null && identity.IsSpawned && passiveEntry != null)
                ConfirmedImpactClientRpc(passiveEntry.Id, new FixedString64Bytes(passiveEntry.CanonicalGuid),
                    0, 0, new NetworkObjectReference(identity), stolen);
        }

        private void OnPassiveEnded()
        {
            passiveTarget = null; passiveEntry = null;
            if (IsServer && IsSpawned) passiveState.Value = default;
        }

        private void OnProtectionChanged(CombatNetworkProtectionSnapshot before, CombatNetworkProtectionSnapshot after)
        { if (!IsServer) ApplyClientProtection(); }

        private void ApplyClientProtection()
        {
            if (IsServer || !catalogValid || actor == null) return;
            var snapshot = protectionState.Value;
            SkillSO aura = snapshot.AuraId != 0 && catalog.TryGet(snapshot.AuraId, out var auraEntry) ? auraEntry.skill : null;
            SkillSO link = snapshot.LinkId != 0 && catalog.TryGet(snapshot.LinkId, out var linkEntry) ? linkEntry.skill : null;
            SquadCharacterController ally = null;
            if (snapshot.LinkId != 0)
            {
                if (!snapshot.Ally.TryGet(out var identity, NetworkManager)) return;
                ally = identity.GetComponent<SquadCharacterController>();
            }
            var effects = actor.GetComponent<PlayerProtectionEffects>();
            if (effects == null && (aura != null || link != null)) effects = PlayerProtectionEffects.Get(actor);
            effects?.ApplyRemoteState(aura, link, ally, (float)Math.Max(0, snapshot.LinkUntil - NetworkManager.ServerTime.Time), snapshot.Reserve);
        }

        private void OnPassiveChanged(CombatNetworkPassiveSnapshot before, CombatNetworkPassiveSnapshot after)
        { if (!IsServer) ApplyClientPassive(); }

        private void ApplyClientPassive()
        {
            if (IsServer || !catalogValid || actor == null) return;
            var snapshot = passiveState.Value;
            if (snapshot.Equals(appliedPassive)) return;
            if (snapshot.SkillId == 0) { DestroyPassiveIndicator(); appliedPassive = snapshot; return; }
            if (!catalog.TryGet(snapshot.SkillId, out var entry) || entry.CanonicalGuid != snapshot.Guid.ToString() ||
                !snapshot.Target.TryGet(out var identity, NetworkManager)) return;
            if (!snapshot.Drain && identity.GetComponent<SquadCharacterController>() == null) return;
            DestroyPassiveIndicator();
            passiveIndicator = snapshot.Drain
                ? PlayerRegenerationEffect.CreateLifeStealIndicator(identity.transform, entry.skill.regenerationHealthPerTick, entry.skill.regenerationTickSeconds)
                : PlayerRegenerationEffect.CreateIndicator(identity.GetComponent<SquadCharacterController>(), entry.skill.regenerationHealthPerTick, entry.skill.regenerationTickSeconds);
            appliedPassive = snapshot;
        }

        private void OnControlStatesChanged(NetworkListEvent<CombatNetworkControlSnapshot> change)
        { if (!IsServer) ReconcileClientControls(); }

        private void ReconcileClientControls()
        {
            if (IsServer || actor == null || !catalogValid) return;
            var retained = new HashSet<ulong>();
            foreach (var snapshot in controlStates)
            {
                if (snapshot.Until <= NetworkManager.ServerTime.Time) continue;
                retained.Add(snapshot.Generation);
                if (clientControlEffects.ContainsKey(snapshot.Generation)) continue;
                if (!catalog.TryGet(snapshot.SkillId, out var entry) || entry.CanonicalGuid != snapshot.Guid.ToString() ||
                    !snapshot.Target.TryGet(out var target, NetworkManager)) continue;
                var enemy = ResolveEnemy(target);
                if (enemy?.Root == null) continue;
                float remaining = (float)(snapshot.Until - NetworkManager.ServerTime.Time);
                if (entry.skill.protection.kind == SkillProtectionKind.Sommation)
                {
                    var taunt = enemy.Root.GetComponent<EnemyTaunt>() ?? enemy.Root.gameObject.AddComponent<EnemyTaunt>();
                    taunt.Set(actor, remaining, snapshot.Generation);
                    clientControlEffects.Add(snapshot.Generation, taunt);
                }
                else if (entry.skill.control.kind == SkillControlKind.Revelation)
                {
                    var mark = enemy.Root.GetComponent<EnemyRevelation>() ?? enemy.Root.gameObject.AddComponent<EnemyRevelation>();
                    mark.Apply(actor, remaining, entry.skill.control.strength, snapshot.Generation);
                    clientControlEffects.Add(snapshot.Generation, mark);
                }
                else if (entry.skill.control.kind == SkillControlKind.Entrave)
                {
                    var zone = EnemyEntraveZone.Create(actor, snapshot.Position, entry.skill, snapshot.Generation);
                    clientControlEffects.Add(snapshot.Generation, zone);
                }
            }
            foreach (ulong generation in new List<ulong>(clientControlEffects.Keys))
                if (!retained.Contains(generation))
                { ReleaseControl(clientControlEffects[generation], generation); clientControlEffects.Remove(generation); }
        }

        private void ClearOwnedEffects()
        {
            if (IsServer)
            {
                if (regeneration != null && passiveTarget != null) regeneration.Clear();
                foreach (var pair in serverControlEffects) ReleaseControl(pair.Value, pair.Key);
                serverControlEffects.Clear();
                if (IsSpawned) { controlStates?.Clear(); passiveState.Value = default; protectionState.Value = default; }
                if (ownsProtection && actor != null)
                {
                    var effects = actor.GetComponent<PlayerProtectionEffects>();
                    if (effects != null && effects.enabled) { effects.enabled = false; effects.enabled = true; }
                }
                ownsProtection = false;
            }
            foreach (var pair in clientControlEffects) ReleaseControl(pair.Value, pair.Key);
            clientControlEffects.Clear(); DestroyPassiveIndicator(); appliedPassive = default;
        }

        private void DestroyPassiveIndicator()
        {
            if (passiveIndicator != null) { passiveIndicator.gameObject.SetActive(false); Destroy(passiveIndicator.gameObject); }
            passiveIndicator = null;
        }

        private void ReleaseControl(Component effect, ulong generation)
        {
            if (effect is EnemyRevelation mark && mark.Generation == generation) mark.ClearIfOwner(actor);
            else if (effect is EnemyEntraveZone zone) zone.ClearIfOwner(actor);
            else if (effect is EnemyTaunt taunt) taunt.ClearIfOwner(actor, generation);
        }

        private bool TauntEligible(NetworkObject target, CombatNetworkSkillCatalog.Entry entry) =>
            EnemyEligible(target, entry, false) &&
            Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).sqrMagnitude <= entry.skill.protection.radius * entry.skill.protection.radius;

        [ClientRpc]
        private void ConfirmedSupportClientRpc(int skillId, FixedString64Bytes guid, ulong epoch, ulong generation,
            float remaining, double sampledAt, float timeScale)
        {
            if (IsServer || !catalogValid || !catalog.TryGet(skillId, out var entry) || entry.CanonicalGuid != guid.ToString()) return;
            acceptedCooldowns[skillId] = new CombatNetworkCooldownSnapshot { SkillId = skillId, Remaining = remaining, SampledAt = sampledAt, TimeScale = timeScale };
            RaiseConfirmedAction(entry.skill, new CombatActionTicket(epoch, NetworkObjectId, generation));
        }

        [ClientRpc]
        private void ConfirmedHealingClientRpc(NetworkObjectReference target, int current, int maximum, int amount)
        {
            if (!target.TryGet(out var identity, NetworkManager)) return;
            var recipient = identity.GetComponent<SquadCharacterController>();
            if (recipient == null || current < 0 || current > maximum || maximum <= 0) return;
            // Health comes from the recipient's revisioned actor snapshot. This cross-actor RPC is presentation only.
            if (!IsServer && amount > 0) CombatDamageWorldFeedback.ShowMessage(recipient.transform, "+" + amount + " PV", new Color(.3f, 1, .6f), 2.1f);
        }

        private void RaiseConfirmedAction(SkillSO skill, CombatActionTicket ticket)
        {
            LastDiagnostic = null;
            // The local owner's facade confirmation owns its start SFX. Other observers get one spatial cue here.
            var facade = RealTimeCombatManager.Instance;
            bool localFacade = IsOwner && facade != null && facade.PlayerRoot == transform;
            if (IsClient && !localFacade && skill.PlayerAttackSfx != null) AudioManager.PlayClipAtPoint(skill.PlayerAttackSfx, transform.position);
            try { ConfirmedAction?.Invoke(skill, ticket); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        public float GetSkillCooldownRemaining(SkillSO skill)
        {
            if (!IsSpawned || !catalogValid || !catalog.TryGet(skill, out var entry)) return 0;
            if (IsServer) return authority != null ? authority.GetCooldownRemaining(NetworkObject, skill) : 0;
            if (!IsOwner) return 0;
            CombatNetworkCooldownSnapshot sample = default;
            foreach (var current in cooldownStates) if (current.SkillId == entry.Id) { sample = current; break; }
            if (acceptedCooldowns.TryGetValue(entry.Id, out var accepted) && accepted.SampledAt >= sample.SampledAt) sample = accepted;
            return Mathf.Max(0, sample.Remaining - (float)Math.Max(0, NetworkManager.ServerTime.Time - sample.SampledAt) * sample.TimeScale);
        }

        private void PublishCooldowns()
        {
            if (!IsServer || !IsSpawned || authority == null || !catalogValid) return;
            foreach (int id in registeredSkills)
            {
                if (!catalog.TryGet(id, out var entry)) continue;
                var sample = new CombatNetworkCooldownSnapshot
                { SkillId = id, Remaining = authority.GetCooldownRemaining(NetworkObject, entry.skill), SampledAt = NetworkManager.ServerTime.Time, TimeScale = Time.timeScale };
                int index = -1;
                for (int item = 0; item < cooldownStates.Count; item++) if (cooldownStates[item].SkillId == id) { index = item; break; }
                if (index < 0) cooldownStates.Add(sample); else cooldownStates[index] = sample;
            }
        }

        internal void ReportCommandRejection(CombatCommandRejection rejection, string serverDiagnostic)
        { Diagnose("Server rejected combat command (" + rejection + "): " + serverDiagnostic); }

        [ClientRpc]
        private void SupportCommitFailedClientRpc(string reason, ClientRpcParams parameters = default) => Diagnose(reason);

        private void BindAuthorityEvents()
        {
            if (boundAuthority == authority) return;
            UnbindAuthorityEvents(); boundAuthority = authority;
            if (boundAuthority == null) return;
            boundAuthority.WorldInvalidated += OnAuthorityInvalidated;
            boundAuthority.ActorUnregistered += OnActorUnregistered;
        }

        private void UnbindAuthorityEvents()
        {
            if (boundAuthority == null) return;
            boundAuthority.WorldInvalidated -= OnAuthorityInvalidated;
            boundAuthority.ActorUnregistered -= OnActorUnregistered;
            boundAuthority = null;
        }

        private void OnAuthorityInvalidated()
        {
            ReleaseParticipantFlows();
            StopOwnedTransientMotion();
            StopCharge(); chargeRequiresRelease = true;
            admitted = false; spawnAdmissionPending = false; registeredSkills.Clear();
            ClearOwnedEffects(); StopLocalPresentation();
            if (IsServer && IsSpawned)
            {
                engagementState.Value = new CombatNetworkThreatSnapshot
                { Revision = engagementState.Value.Revision + 1, Threats = Array.Empty<NetworkObjectReference>() };
            }
        }

        private void OnActorUnregistered(NetworkObject identity, CombatActionEndReason reason)
        {
            if (identity == NetworkObject) { ReleaseParticipantFlows(); StopOwnedTransientMotion(); StopCharge(); chargeRequiresRelease = true; registeredSkills.Clear(); ClearOwnedEffects(); StopLocalPresentation(); }
            else if (identity == passiveTarget && regeneration != null) regeneration.Clear();
        }
    }
}
