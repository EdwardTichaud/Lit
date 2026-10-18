using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public enum CombatNetworkSkillFamily : byte
    {
        Unsupported, GroundBasic, HealSelf, HealArea, ProtectionAura, ProtectionLink, Revelation, Entrave, Regeneration, LifeSteal,
        AirBasic, Sommation, Justice, PasDeRecul, ContactDash, DirectAttack, Retreat, Charged, GroundImpact, Cinematic
    }

    [CreateAssetMenu(menuName = "Lit/Combat/Network Skill Catalog")]
    public sealed class CombatNetworkSkillCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SkillSO skill;
            [Tooltip("Unity asset GUID, baked by OnValidate in the editor. Never generated at runtime.")]
            public string assetGuid;
            [Min(0)] public float heightTolerance = 1.8f;
            [Range(1, 180)] public float contactArc = 110f;
            public LayerMask obstructionMask = ~0;
            public int Id => CombatSkillNetworkId.TryGet(assetGuid, out int id) ? id : 0;
            public string CanonicalGuid => Guid.TryParseExact(assetGuid, "N", out var value) ? value.ToString("N") : string.Empty;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [Serializable]
        public sealed class CinematicEntry
        {
            public UnityEngine.Object definition;
            public string assetGuid;
            public int Id => CombatSkillNetworkId.TryGet(assetGuid, out int id) ? id : 0;
        }
        [SerializeField] private List<CinematicEntry> cinematics = new List<CinematicEntry>();
        private readonly Dictionary<int, UnityEngine.Object> cinematicById = new Dictionary<int, UnityEngine.Object>();
        public IReadOnlyList<CinematicEntry> Cinematics => cinematics;
        private readonly Dictionary<int, Entry> byId = new Dictionary<int, Entry>();
        private readonly Dictionary<SkillSO, Entry> byAsset = new Dictionary<SkillSO, Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        public bool Rebuild(out string diagnostic)
        {
            byId.Clear(); byAsset.Clear(); cinematicById.Clear();
            foreach (var entry in entries)
            {
                if (entry == null || entry.skill == null || entry.Id == 0 ||
                    !CombatCommandPolicy.Finite(entry.heightTolerance) || entry.heightTolerance < 0 ||
                    !CombatCommandPolicy.Finite(entry.contactArc) || entry.contactArc < 1 || entry.contactArc > 180 ||
                    byId.ContainsKey(entry.Id) || byAsset.ContainsKey(entry.skill))
                {
                    byId.Clear(); byAsset.Clear();
                    diagnostic = "Catalog contains missing assets/GUIDs, invalid geometry, duplicate assets or colliding wire IDs.";
                    return false;
                }
                byId.Add(entry.Id, entry); byAsset.Add(entry.skill, entry);
            }
            foreach (var entry in cinematics)
            {
                if (entry == null || entry.Id <= 0 || entry.definition is not CounterSkillSO && entry.definition is not LightSkillSO ||
                    byId.ContainsKey(entry.Id) || cinematicById.ContainsKey(entry.Id) || cinematicById.ContainsValue(entry.definition))
                { byId.Clear(); byAsset.Clear(); cinematicById.Clear(); diagnostic = "Invalid/duplicate authored cinematic identity."; return false; }
                cinematicById.Add(entry.Id, entry.definition);
            }
            diagnostic = null;
            return true;
        }

        public bool TryGet(int id, out Entry entry) => byId.TryGetValue(id, out entry);
        public bool TryGetCinematic(int id, out UnityEngine.Object definition) => cinematicById.TryGetValue(id, out definition);
        public bool TryGetCinematic(UnityEngine.Object definition, out int id)
        {
            foreach (var pair in cinematicById) if (pair.Value == definition) { id = pair.Key; return true; }
            id = 0; return false;
        }
        public bool TryGet(SkillSO skill, out Entry entry)
        {
            entry = null;
            return skill != null && byAsset.TryGetValue(skill, out entry);
        }

        public static CombatNetworkSkillFamily Classify(SkillSO skill, out string diagnostic)
        {
            diagnostic = null;
            if (skill == null) return Unsupported("Missing skill.", out diagnostic);
            if (skill.HasCombatCinematic)
            {
                var definition = skill.Cinematic;
                if (definition.CombatCinematicRigPrefab == null || definition.CombatCinematicRigPrefab.RuntimeTimeline == null ||
                    skill.targetingMode != SkillTargetingMode.RequiredTarget && skill.targetingMode != SkillTargetingMode.LockedTarget ||
                    string.IsNullOrWhiteSpace(definition.PlayerAnimatorTrackName) || string.IsNullOrWhiteSpace(definition.EnemyAnimatorTrackName))
                    return Unsupported("Cinematic requires a baked rig, explicit required target and actor tracks.", out diagnostic);
                return CombatNetworkSkillFamily.Cinematic;
            }
            if (skill is ChargedSkillSO charged)
            {
                if (charged.preparationClip == null || string.IsNullOrWhiteSpace(charged.preparationState) ||
                    !ValidNonnegative(charged.chargeSeconds, charged.launchHeight, charged.ascentSeconds, charged.airborneHoldSeconds) ||
                    charged.chargeSeconds <= 0 || charged.ascentSeconds <= 0 || !TryGetImpactEvent(skill, out _, out diagnostic))
                    return Unsupported("Charge requires its preparation, finite launch/hold and one native contact.", out diagnostic);
                return CombatNetworkSkillFamily.Charged;
            }
            if (skill.IsGroundImpactSkill)
            {
                var motion = skill.groundImpact;
                if (skill.targetingMode != SkillTargetingMode.CasterArea || skill.AnimationClip == null ||
                    skill.AnimationClip.isLooping || string.IsNullOrWhiteSpace(skill.AnimatorState) ||
                    !ValidNonnegative(motion.radius, motion.descentSpeed, motion.maximumDescentSeconds,
                        motion.airborneDamageMultiplier, motion.groundPushDistance, motion.airbornePushDistance) ||
                    motion.radius <= 0 || motion.descentSpeed <= 0 || motion.maximumDescentSeconds <= 0)
                    return Unsupported("Ground impact requires a caster area, clip and finite descent/contact geometry.", out diagnostic);
                return CombatNetworkSkillFamily.GroundImpact;
            }
            if (skill.RetreatImpulse.enabled)
            {
                var motion = skill.RetreatImpulse;
                if (skill.targetingMode != SkillTargetingMode.LockedTarget ||
                    !ValidNonnegative(motion.horizontalImpulse, motion.verticalImpulse, motion.maximumHorizontalImpulse,
                        motion.maximumVerticalImpulse, motion.minimumInputLockSeconds, motion.maximumInputLockSeconds,
                        motion.airborneInertiaSeconds, motion.airborneInertiaEndSpeedMultiplier) ||
                    motion.maximumInputLockSeconds < motion.minimumInputLockSeconds || !TryGetImpactEvent(skill, out _, out diagnostic))
                    return Unsupported("Retreat requires a locked target, bounded impulse and one native impact.", out diagnostic);
                return CombatNetworkSkillFamily.Retreat;
            }
            if (skill.TargetLunge.enabled || skill.TargetLunge.resolveImpactAtContact)
            {
                var motion = skill.TargetLunge;
                if (skill is BasicSkillsSO || skill.IsHealingSkill || skill.IsProtectionSkill || skill.IsControlSkill ||
                    !motion.enabled || !motion.resolveImpactAtContact || motion.contactAnimationSlot == null ||
                    !skill.useLaunchRange || !ValidNonnegative(skill.minimumLaunchDistance, skill.maximumLaunchDistance, skill.minimumHitDistance,
                        skill.maximumHitDistance, motion.passThroughDistance, motion.stoppingDistance, motion.contactTolerance,
                        motion.approachDurationSeconds, motion.maximumApproachSpeed) ||
                    skill.maximumLaunchDistance < skill.minimumLaunchDistance || skill.maximumHitDistance < skill.minimumHitDistance ||
                    !CombatCommandPolicy.Finite(skill.maximumLaunchDistance + motion.passThroughDistance) || skill.maximumLaunchDistance + motion.passThroughDistance > 128f ||
                    motion.approachDurationSeconds <= 0 || motion.maximumApproachSpeed <= 0 ||
                    motion.reboundHorizontalImpulse != 0 || motion.reboundVerticalImpulse != 0 ||
                    skill.targetingMode != SkillTargetingMode.FreeAttack && skill.targetingMode != SkillTargetingMode.OptionalTarget && skill.targetingMode != SkillTargetingMode.RequiredTarget)
                    return Unsupported("Dash requires an explicit contact motor, finite launch/hit geometry and no rebound/mixed family.", out diagnostic);
                if (!TryGetContactWindow(skill, out _, out diagnostic)) return CombatNetworkSkillFamily.Unsupported;
                return CombatNetworkSkillFamily.ContactDash;
            }
            if (skill is BasicSkillsSO basic)
            {
                if (skill.Presentation.movementPolicy == PlayerActionMovementPolicy.ExistingScripted)
                    return Unsupported("Basic scripted motion is not migrated.", out diagnostic);
                if (skill.targetingMode != SkillTargetingMode.FreeAttack && skill.targetingMode != SkillTargetingMode.OptionalTarget &&
                    skill.targetingMode != SkillTargetingMode.RequiredTarget)
                    return Unsupported("Basic requires FreeAttack, OptionalTarget or RequiredTarget; authoritative locked-target routing is not implemented.", out diagnostic);
                if (skill.IsHealingSkill || skill.IsProtectionSkill || skill.IsControlSkill)
                    return Unsupported("Mixed Basic/support semantics are not supported.", out diagnostic);
                if (!TryGetImpactEvent(skill, out _, out diagnostic)) return CombatNetworkSkillFamily.Unsupported;
                return basic.Context == BasicSkillContext.Airborne ? CombatNetworkSkillFamily.AirBasic : CombatNetworkSkillFamily.GroundBasic;
            }
            int categories = (skill.IsHealingSkill ? 1 : 0) + (skill.IsProtectionSkill ? 1 : 0) + (skill.IsControlSkill ? 1 : 0);
            if (categories == 0 && (skill.targetingMode == SkillTargetingMode.FreeAttack ||
                skill.targetingMode == SkillTargetingMode.OptionalTarget || skill.targetingMode == SkillTargetingMode.RequiredTarget) &&
                TryGetImpactEvent(skill, out _, out diagnostic)) return CombatNetworkSkillFamily.DirectAttack;
            if (categories != 1) return Unsupported("Skill needs exactly one supported family.", out diagnostic);
            if (skill.IsHealingSkill)
            {
                if (skill.healingTarget == SkillHealingTarget.AllyRegeneration || skill.healingTarget == SkillHealingTarget.EnemyLifeSteal)
                {
                    if (!ValidNonnegative(skill.healingRadius, skill.regenerationTickSeconds) || skill.regenerationTickSeconds < .1f || skill.regenerationHealthPerTick <= 0)
                        return Unsupported("Invalid passive healing values.", out diagnostic);
                    if (skill.healingTarget == SkillHealingTarget.AllyRegeneration && skill.targetingMode == SkillTargetingMode.SelectedAlly) return CombatNetworkSkillFamily.Regeneration;
                    if (skill.healingTarget == SkillHealingTarget.EnemyLifeSteal && skill.targetingMode == SkillTargetingMode.RequiredTarget) return CombatNetworkSkillFamily.LifeSteal;
                    return Unsupported("Passive requires SelectedAlly regeneration or RequiredTarget life-steal.", out diagnostic);
                }
                if (!CombatCommandPolicy.Finite(skill.healingPercent) || skill.healingPercent <= 0 || skill.healingPercent > 100 ||
                    !CombatCommandPolicy.Finite(skill.healingRadius) || skill.healingRadius < 0)
                    return Unsupported("Invalid healing values.", out diagnostic);
                if (skill.healingTarget == SkillHealingTarget.Self && skill.targetingMode == SkillTargetingMode.Self) return CombatNetworkSkillFamily.HealSelf;
                if (skill.healingTarget == SkillHealingTarget.PlayersInRadius && skill.targetingMode == SkillTargetingMode.AllyArea) return CombatNetworkSkillFamily.HealArea;
                return Unsupported("Healing targeting mode does not match its authored family.", out diagnostic);
            }
            if (skill.IsProtectionSkill)
            {
                if (!ValidNonnegative(skill.protection.radius, skill.protection.maximumLinkDistance, skill.protection.durationSeconds) ||
                    !Unit(skill.protection.reduction) || !Unit(skill.protection.transferFraction))
                    return Unsupported("Invalid protection values.", out diagnostic);
                if (skill.protection.kind == SkillProtectionKind.Rempart && skill.targetingMode == SkillTargetingMode.AllyArea) return CombatNetworkSkillFamily.ProtectionAura;
                if (skill.protection.kind == SkillProtectionKind.Intercession && skill.targetingMode == SkillTargetingMode.SelectedAlly) return CombatNetworkSkillFamily.ProtectionLink;
                if (skill.protection.kind == SkillProtectionKind.Sommation && skill.targetingMode == SkillTargetingMode.CasterArea && skill.protection.durationSeconds > 0)
                    return CombatNetworkSkillFamily.Sommation;
                if (skill.protection.kind == SkillProtectionKind.JusteRetour && skill.targetingMode == SkillTargetingMode.RequiredTarget &&
                    Unit(skill.protection.reserveMaxHealthFraction) && skill.Presentation.movementPolicy != PlayerActionMovementPolicy.ExistingScripted &&
                    TryGetImpactEvent(skill, out _, out diagnostic)) return CombatNetworkSkillFamily.Justice;
                return Unsupported("Protection targeting or native impact contract is unsupported.", out diagnostic);
            }
            if (!ValidNonnegative(skill.control.range, skill.control.radius, skill.control.durationSeconds) ||
                skill.control.durationSeconds <= 0 || !Unit(skill.control.strength))
                return Unsupported("Invalid control values.", out diagnostic);
            if (skill.control.kind == SkillControlKind.PasDeRecul && skill.targetingMode == SkillTargetingMode.FreeAttack &&
                CombatCommandPolicy.Finite(skill.control.distance) && skill.control.distance > 0 &&
                skill.AnimationClip != null && !skill.AnimationClip.isLooping && skill.AnimationClip.length > 0 && !string.IsNullOrWhiteSpace(skill.AnimatorState))
                return CombatNetworkSkillFamily.PasDeRecul;
            if (skill.targetingMode != SkillTargetingMode.RequiredTarget)
                return Unsupported("Control requires a server-validated explicit enemy target.", out diagnostic);
            if (skill.control.kind == SkillControlKind.Revelation) return CombatNetworkSkillFamily.Revelation;
            if (skill.control.kind == SkillControlKind.Entrave) return CombatNetworkSkillFamily.Entrave;
            return Unsupported("PasDeRecul movement is not migrated.", out diagnostic);
        }

        internal static bool IsBasic(CombatNetworkSkillFamily family) => family == CombatNetworkSkillFamily.GroundBasic || family == CombatNetworkSkillFamily.AirBasic;
        internal static bool IsNativeAttack(CombatNetworkSkillFamily family) => IsBasic(family) || family == CombatNetworkSkillFamily.Justice ||
            family == CombatNetworkSkillFamily.ContactDash || family == CombatNetworkSkillFamily.DirectAttack ||
            family == CombatNetworkSkillFamily.Retreat || family == CombatNetworkSkillFamily.Charged || family == CombatNetworkSkillFamily.GroundImpact;
        internal static bool HasPresentation(CombatNetworkSkillFamily family) => IsNativeAttack(family) || family == CombatNetworkSkillFamily.PasDeRecul;

        public static bool TryGetContactWindow(SkillSO skill, out float time, out string diagnostic)
        {
            time = 0; diagnostic = null;
            if (skill.AnimationClip == null || skill.AnimationClip.isLooping || skill.AnimationClip.length <= 0 || string.IsNullOrWhiteSpace(skill.AnimatorState))
            { diagnostic = "Dash requires a non-looping native contact clip and Animator state."; return false; }
            int count = 0;
            foreach (var marker in skill.AnimationClip.events)
            {
                if (marker.functionName == "HitEnemy" || marker.functionName == "ResolveSkillImpact") count += 100;
                if (marker.functionName != "OpenSkillContactWindow") continue;
                time = marker.time; count++;
                if (!CombatCommandPolicy.Finite(time) || time < 0 || time > skill.AnimationClip.length) count += 100;
            }
            if (count == 1) return true;
            diagnostic = "Dash requires exactly one OpenSkillContactWindow and no competing instantaneous damage marker.";
            return false;
        }

        public static bool TryGetImpactEvent(SkillSO skill, out float time, out string diagnostic)
        {
            time = 0; diagnostic = null;
            if (skill.AnimationClip == null || skill.AnimationClip.isLooping || skill.AnimationClip.length <= 0 || string.IsNullOrWhiteSpace(skill.AnimatorState))
            { diagnostic = "Basic requires a non-looping authored clip and an explicit Animator state."; return false; }
            int count = 0;
            foreach (var marker in skill.AnimationClip.events)
                if (marker.functionName == "HitEnemy" || marker.functionName == "ResolveSkillImpact" ||
                    marker.functionName == "ResolveSkillImpactAndRetreat" || marker.functionName == "ChargedSkillContact")
                {
                    count++; time = marker.time;
                    if (marker.functionName == "ChargedSkillContact" && (!(skill is ChargedSkillSO) || marker.intParameter != 0) ||
                        marker.functionName == "ResolveSkillImpactAndRetreat" && !skill.RetreatImpulse.enabled) count += 100;
                    if (!CombatCommandPolicy.Finite(time) || time < 0 || time > skill.AnimationClip.length) count += 100;
                }
            if (count == 1) return true;
            diagnostic = "Basic must have exactly one HitEnemy/ResolveSkillImpact marker; no timer-based damage fallback.";
            return false;
        }

        private static bool Unit(float value) => CombatCommandPolicy.Finite(value) && value >= 0 && value <= 1;
        private static bool ValidNonnegative(params float[] values)
        {
            foreach (float value in values) if (!CombatCommandPolicy.Finite(value) || value < 0) return false;
            return true;
        }
        private static CombatNetworkSkillFamily Unsupported(string reason, out string diagnostic)
        { diagnostic = reason; return CombatNetworkSkillFamily.Unsupported; }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (var entry in entries)
                if (entry != null && entry.skill != null)
                    entry.assetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(entry.skill));
            foreach (var entry in cinematics)
                if (entry != null && entry.definition != null)
                    entry.assetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(entry.definition));
        }
#endif
    }
}
