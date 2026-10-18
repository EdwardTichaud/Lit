using System;
using System.Collections.Generic;
using System.Reflection;
using Lit.Combat.Migration.Network;
using UnityEngine;

internal static class CatalogSmokeTests
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    private static BasicSkillsSO Basic() => new BasicSkillsSO
    {
        targetingMode = SkillTargetingMode.FreeAttack,
        AnimatorState = "Base Layer.Basic",
        AnimationClip = new AnimationClip { events = new[] { new AnimationEvent { functionName = "HitEnemy", time = .5f } } }
    };
    private static CombatNetworkSkillCatalog Catalog(params CombatNetworkSkillCatalog.Entry[] entries)
    {
        var catalog = new CombatNetworkSkillCatalog();
        typeof(CombatNetworkSkillCatalog).GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(catalog, new List<CombatNetworkSkillCatalog.Entry>(entries));
        return catalog;
    }
    private static CombatNetworkSkillCatalog.Entry Entry(SkillSO skill, string guid) => new CombatNetworkSkillCatalog.Entry { skill = skill, assetGuid = guid };
    private static void Family(SkillSO skill, CombatNetworkSkillFamily expected)
    {
        var actual = CombatNetworkSkillCatalog.Classify(skill, out string diagnostic);
        Check(actual == expected, $"Expected {expected}, got {actual}: {diagnostic}");
        if (expected == CombatNetworkSkillFamily.Unsupported) Check(!string.IsNullOrEmpty(diagnostic), "missing unsupported diagnostic");
    }
    public static void Run(Action<string, Action> test)
    {
        const string guid = "0123456789abcdef0123456789abcdef";
        test("GUID wire identity is deterministic and rejects names/empty GUIDs", () =>
        {
            Check(CombatSkillNetworkId.TryGet(guid, out int lower) && CombatSkillNetworkId.TryGet(guid.ToUpperInvariant(), out int upper) && lower == upper, "canonical GUID");
            Check(!CombatSkillNetworkId.TryGet("BasicSkill_1", out _) && !CombatSkillNetworkId.TryGet(new string('0', 32), out _), "invalid GUID admission");
        });
        test("catalog is stable across renaming and list order", () =>
        {
            var first = Basic(); var second = Basic();
            var a = Entry(first, guid); var b = Entry(second, "11111111111111111111111111111111");
            var catalog = Catalog(a, b); Check(catalog.Rebuild(out _), "initial catalog");
            int id = a.Id; first.name = "Renamed Skill";
            catalog = Catalog(b, a); Check(catalog.Rebuild(out _) && catalog.TryGet(id, out var same) && same.skill == first, "renaming/order changed ID");
        });
        test("catalog collision and missing GUID fail closed as a whole", () =>
        {
            var a = Entry(Basic(), guid); var b = Entry(Basic(), guid);
            var catalog = Catalog(a, b); Check(!catalog.Rebuild(out string reason) && !catalog.TryGet(a.Id, out _) && reason != null, "collision partial catalog");
            Check(!Catalog(Entry(Basic(), null)).Rebuild(out _), "missing GUID");
            Check(!Catalog(a, Entry(a.skill, "22222222222222222222222222222222")).Rebuild(out _), "asset alias");
        });
        test("Basic needs one real marker and refuses timer/multihit fallback", () =>
        {
            var skill = Basic(); Family(skill, CombatNetworkSkillFamily.GroundBasic);
            skill.AnimationClip.events = Array.Empty<AnimationEvent>(); Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.AnimationClip.events = new[] { new AnimationEvent { functionName = "HitEnemy", time = .4f }, new AnimationEvent { functionName = "ResolveSkillImpact", time = .6f } };
            Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.AnimationClip.events = new[] { new AnimationEvent { functionName = "HitEnemy", time = float.NaN } }; Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("airborne Basics require native markers; incomplete specialized contracts remain unsupported", () =>
        {
            var skill = Basic(); skill.Context = BasicSkillContext.Airborne; Family(skill, CombatNetworkSkillFamily.AirBasic);
            skill.AnimationClip.events = Array.Empty<AnimationEvent>(); Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill = Basic(); skill.targetingMode = SkillTargetingMode.LockedTarget; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill = Basic(); skill.TargetLunge.enabled = true; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill = Basic(); skill.RetreatImpulse.enabled = true; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill = Basic(); skill.HasCombatCinematic = true; Family(skill, CombatNetworkSkillFamily.Unsupported);
            Family(new ChargedSkillSO(), CombatNetworkSkillFamily.Unsupported);
        });
        test("charged skill requires preparation, finite ascent and one first-contact marker", () =>
        {
            var skill = new ChargedSkillSO { targetingMode = SkillTargetingMode.FreeAttack, AnimatorState = "Charged",
                preparationClip = new AnimationClip(), preparationState = "Preparation", AnimationClip = new AnimationClip {
                    events = new[] { new AnimationEvent { functionName = "ChargedSkillContact", time = .4f, intParameter = 0 } } } };
            Family(skill, CombatNetworkSkillFamily.Charged);
            Check(CombatNetworkSkillCatalog.TryGetImpactEvent(skill, out float time, out _) && time == .4f, "charged native timing");
            skill.preparationClip = null; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.preparationClip = new AnimationClip(); skill.ascentSeconds = 0; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.ascentSeconds = .3f; skill.airborneHoldSeconds = float.NaN; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.airborneHoldSeconds = 1.2f; skill.AnimationClip.events[0].intParameter = 1; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.AnimationClip.events[0].intParameter = 0; skill.AnimationClip.events = Array.Empty<AnimationEvent>(); Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.AnimationClip = Basic().AnimationClip; skill.AnimationClip.isLooping = true; Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("retreat requires actual lock, bounded motion and its native impact", () =>
        {
            var skill = new SkillSO { targetingMode = SkillTargetingMode.LockedTarget, AnimatorState = "Rupture",
                AnimationClip = new AnimationClip { events = new[] { new AnimationEvent { functionName = "ResolveSkillImpactAndRetreat", time = .6f } } } };
            skill.RetreatImpulse.enabled = true; Family(skill, CombatNetworkSkillFamily.Retreat);
            Check(CombatNetworkSkillCatalog.TryGetImpactEvent(skill, out float time, out _) && time == .6f, "retreat native timing");
            skill.targetingMode = SkillTargetingMode.RequiredTarget; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.targetingMode = SkillTargetingMode.LockedTarget;
            skill.RetreatImpulse.maximumInputLockSeconds = .01f; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.RetreatImpulse.maximumInputLockSeconds = .4f; skill.RetreatImpulse.horizontalImpulse = float.PositiveInfinity;
            Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.RetreatImpulse.horizontalImpulse = 5; skill.AnimationClip.events = Array.Empty<AnimationEvent>(); Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.AnimationClip = new AnimationClip { events = new[] { new AnimationEvent { functionName = "ResolveSkillImpactAndRetreat", time = .6f } } };
            skill.RetreatImpulse.enabled = false; skill.targetingMode = SkillTargetingMode.FreeAttack; Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("ground impact accepts caster-area descent but rejects incomplete or nonfinite geometry", () =>
        {
            var skill = new SkillSO { IsGroundImpactSkill = true, targetingMode = SkillTargetingMode.CasterArea,
                AnimatorState = "GroundImpact", AnimationClip = new AnimationClip() };
            Family(skill, CombatNetworkSkillFamily.GroundImpact);
            skill.targetingMode = SkillTargetingMode.FreeAttack; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.targetingMode = SkillTargetingMode.CasterArea; skill.groundImpact.radius = 0; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.groundImpact.radius = 5; skill.groundImpact.maximumDescentSeconds = float.NaN; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.groundImpact.maximumDescentSeconds = 2; skill.AnimationClip.isLooping = true; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.AnimationClip.isLooping = false; skill.AnimationClip = null; Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("direct attacks require an actual unique impact; specialized contact markers cannot masquerade as direct damage", () =>
        {
            var skill = new SkillSO { targetingMode = SkillTargetingMode.RequiredTarget, AnimatorState = "Arrow", AnimationClip = Basic().AnimationClip };
            Family(skill, CombatNetworkSkillFamily.DirectAttack);
            skill.AnimationClip.events = new[] { new AnimationEvent { functionName = "ChargedSkillContact", time = .4f } };
            Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.AnimationClip.events = Array.Empty<AnimationEvent>(); Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("cinematic skills require a baked timeline, required victim and both actor tracks", () =>
        {
            var skill = new SkillSO { HasCombatCinematic = true, targetingMode = SkillTargetingMode.RequiredTarget };
            Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.Cinematic.CombatCinematicRigPrefab = new CombatCinematicRig { RuntimeTimeline = new UnityEngine.Object() };
            Family(skill, CombatNetworkSkillFamily.Cinematic);
            skill.targetingMode = SkillTargetingMode.LockedTarget; Family(skill, CombatNetworkSkillFamily.Cinematic);
            skill.targetingMode = SkillTargetingMode.OptionalTarget; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.targetingMode = SkillTargetingMode.RequiredTarget; skill.Cinematic.EnemyAnimatorTrackName = " "; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.Cinematic.EnemyAnimatorTrackName = "Enemy.Animator"; skill.Cinematic.CombatCinematicRigPrefab.RuntimeTimeline = null;
            Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("cinematic IDs resolve authored counter/light assets and reject wrong types and cross-catalog aliases", () =>
        {
            var counter = new CounterSkillSO(); var light = new LightSkillSO();
            var first = new CombatNetworkSkillCatalog.CinematicEntry { definition = counter, assetGuid = guid };
            var second = new CombatNetworkSkillCatalog.CinematicEntry { definition = light, assetGuid = "11111111111111111111111111111111" };
            var catalog = Catalog();
            var field = typeof(CombatNetworkSkillCatalog).GetField("cinematics", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(catalog, new List<CombatNetworkSkillCatalog.CinematicEntry> { first, second });
            Check(catalog.Rebuild(out _), "valid cinematic IDs");
            Check(catalog.TryGetCinematic(first.Id, out var resolved) && resolved == counter && catalog.TryGetCinematic(light, out int id) && id == second.Id, "cinematic round trip");
            first.definition = Basic(); Check(!catalog.Rebuild(out _) && !catalog.TryGetCinematic(second.Id, out _), "wrong cinematic type fails closed");
            first.definition = counter; second.definition = counter; Check(!catalog.Rebuild(out _), "duplicate cinematic asset");
            second.definition = light; second.assetGuid = guid; Check(!catalog.Rebuild(out _), "colliding cinematic IDs");
            catalog = Catalog(Entry(Basic(), guid)); field.SetValue(catalog, new List<CombatNetworkSkillCatalog.CinematicEntry> { first });
            Check(!catalog.Rebuild(out _) && !catalog.TryGet(first.Id, out _), "skill/cinematic ID collision");
        });
        test("concrete contact dashes require native window, authored launch geometry and no competing impact", () =>
        {
            var skill = new SkillSO { targetingMode = SkillTargetingMode.OptionalTarget, useLaunchRange = true,
                AnimatorState = "Base Layer.Eclair", AnimationClip = new AnimationClip {
                    events = new[] { new AnimationEvent { functionName = "OpenSkillContactWindow", time = .08f } } } };
            skill.TargetLunge.enabled = skill.TargetLunge.resolveImpactAtContact = true;
            skill.TargetLunge.contactAnimationSlot = new AnimationClip();
            Family(skill, CombatNetworkSkillFamily.ContactDash);
            skill.TargetLunge.passThroughTarget = true; Family(skill, CombatNetworkSkillFamily.ContactDash);
            skill.TargetLunge.maximumApproachSpeed = float.NaN; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.TargetLunge.maximumApproachSpeed = 65;
            skill.useLaunchRange = false; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill.useLaunchRange = true;
            skill.AnimationClip.events = new[] { new AnimationEvent { functionName = "OpenSkillContactWindow", time = .08f },
                new AnimationEvent { functionName = "HitEnemy", time = .5f } };
            Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("Sommation Justice and PasDeRecul classify only their concrete source contracts", () =>
        {
            var skill = new SkillSO { targetingMode = SkillTargetingMode.CasterArea };
            skill.protection.kind = SkillProtectionKind.Sommation; Family(skill, CombatNetworkSkillFamily.Sommation);
            skill.protection.kind = SkillProtectionKind.JusteRetour; skill.targetingMode = SkillTargetingMode.RequiredTarget;
            skill.AnimatorState = "Base Layer.Justice"; skill.AnimationClip = Basic().AnimationClip;
            Family(skill, CombatNetworkSkillFamily.Justice);
            skill.protection.kind = SkillProtectionKind.None; skill.control.kind = SkillControlKind.PasDeRecul;
            skill.targetingMode = SkillTargetingMode.FreeAttack; Family(skill, CombatNetworkSkillFamily.PasDeRecul);
            skill.control.distance = float.NaN; Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("heals and persistent passives classify only their authored target families", () =>
        {
            var skill = new SkillSO { healingTarget = SkillHealingTarget.Self, healingPercent = 20, targetingMode = SkillTargetingMode.Self };
            Family(skill, CombatNetworkSkillFamily.HealSelf);
            skill.healingTarget = SkillHealingTarget.PlayersInRadius; skill.targetingMode = SkillTargetingMode.AllyArea; Family(skill, CombatNetworkSkillFamily.HealArea);
            skill.healingTarget = SkillHealingTarget.AllyRegeneration; skill.targetingMode = SkillTargetingMode.SelectedAlly; Family(skill, CombatNetworkSkillFamily.Regeneration);
            skill.healingTarget = SkillHealingTarget.EnemyLifeSteal; skill.targetingMode = SkillTargetingMode.RequiredTarget; Family(skill, CombatNetworkSkillFamily.LifeSteal);
            skill.regenerationTickSeconds = float.NaN; Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("protection/control supported subsets are explicit", () =>
        {
            var skill = new SkillSO { targetingMode = SkillTargetingMode.AllyArea }; skill.protection.kind = SkillProtectionKind.Rempart;
            Family(skill, CombatNetworkSkillFamily.ProtectionAura);
            skill.protection.kind = SkillProtectionKind.Intercession; skill.targetingMode = SkillTargetingMode.SelectedAlly; Family(skill, CombatNetworkSkillFamily.ProtectionLink);
            skill.protection.kind = SkillProtectionKind.JusteRetour; Family(skill, CombatNetworkSkillFamily.Unsupported);
            skill = new SkillSO { targetingMode = SkillTargetingMode.RequiredTarget }; skill.control.kind = SkillControlKind.Revelation; Family(skill, CombatNetworkSkillFamily.Revelation);
            skill.control.kind = SkillControlKind.Entrave; Family(skill, CombatNetworkSkillFamily.Entrave);
            skill.control.kind = SkillControlKind.PasDeRecul; Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("mixed-family skills fail closed", () =>
        {
            var skill = new SkillSO { healingTarget = SkillHealingTarget.Self, healingPercent = 20, targetingMode = SkillTargetingMode.Self };
            skill.control.kind = SkillControlKind.Revelation; Family(skill, CombatNetworkSkillFamily.Unsupported);
        });
        test("contact ranking is alignment then distance then stable ID", () =>
        {
            Check(CombatNetworkTargetRanking.IsBetter(1, 20, 9, 20, 1, 1), "alignment must beat nearer off-axis target");
            Check(CombatNetworkTargetRanking.IsBetter(10, 2, 9, 10, 3, 1), "distance tie breaker");
            Check(CombatNetworkTargetRanking.IsBetter(10, 2, 1, 10, 2, 9), "identity tie breaker");
            Check(!CombatNetworkTargetRanking.IsBetter(20, 1, 1, 1, 20, 9), "distance must not beat alignment");
        });
    }
}
