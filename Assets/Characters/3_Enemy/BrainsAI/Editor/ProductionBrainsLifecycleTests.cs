#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Ultrabolt.BrainsAI;
using Object = UnityEngine.Object;

public sealed class ProductionBrainsLifecycleTests
{
    private readonly List<GameObject> roots = new List<GameObject>();
    private Flame[] savedFlames;
    private HashSet<Flame> Flames => (HashSet<Flame>)typeof(Flame)
        .GetField("activeInfluenceFlames", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

    [SetUp] public void SetUp() { savedFlames = Flames.ToArray(); Flames.Clear(); }
    [TearDown] public void TearDown()
    {
        foreach (var root in roots) if (root != null) Object.DestroyImmediate(root);
        roots.Clear(); Flames.Clear();
        foreach (var flame in savedFlames) if (flame != null) Flames.Add(flame);
    }
    private GameObject Root(string name)
    {
        var root = new GameObject(name); root.SetActive(false); roots.Add(root); return root;
    }
    private ProductionBrainsFlamePolicy Policy()
    {
        var root = Root("Production flame policy");
        root.AddComponent<CapsuleCollider>();
        root.AddComponent<CharacterInfo>().SetHealth(30, 30);
        return root.AddComponent<ProductionBrainsFlamePolicy>();
    }
    private Flame Flame()
    {
        var flame = Root("Fixed activation flame").AddComponent<Flame>();
        typeof(Flame).GetField("isLit", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flame, true);
        flame.gameObject.SetActive(true); Flames.Add(flame); return flame;
    }

    [Test]
    public void FixedFlameLossPausesClockWithoutMutatingHealthAndDeathReleasesPause()
    {
        var policy = Policy();
        policy.Refresh();
        Assert.IsTrue(policy.IsFlameDormant);
        Assert.IsFalse(policy.FilterIncomingDamage(10, null, out int damage));
        Assert.Zero(damage);
        Assert.Zero(policy.GetComponent<CombatTimeDomain>().Scale);
        Assert.AreEqual(30, policy.GetComponent<CharacterInfo>().CurrentHp);
        policy.GetComponent<CharacterInfo>().SetHealth(0, 30);
        policy.Refresh();
        Assert.IsFalse(policy.IsFlameDormant);
        Assert.AreEqual(1f, policy.GetComponent<CombatTimeDomain>().Scale);
    }

    [Test]
    public void OverlappingFixedFlamesUseSameContactGateAndResumeOnlyWhileOneRemains()
    {
        var policy = Policy();
        policy.gameObject.SetActive(true);
        var first = Flame(); var second = Flame();
        Physics.SyncTransforms(); policy.Refresh();
        Assert.IsFalse(policy.IsFlameDormant);
        first.gameObject.SetActive(false); Flames.Remove(first); policy.Refresh();
        Assert.IsFalse(policy.IsFlameDormant);
        second.gameObject.SetActive(false); Flames.Remove(second); policy.Refresh();
        Assert.IsTrue(policy.IsFlameDormant);
        policy.enabled = false;
        typeof(ProductionBrainsFlamePolicy).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(policy, null);
        Assert.AreEqual(1f, policy.GetComponent<CombatTimeDomain>().Scale, "Disable must release this pause owner.");
    }

    [Test]
    public void AuthoredFlameExemptionAndIndependentPauseOwnersRemainIntact()
    {
        var policy = Policy();
        var clock = policy.GetComponent<CombatTimeDomain>();
        var other = new object(); clock.SetIntrinsicPause(other, true);
        policy.requiresFlameInfluence = false; policy.Refresh();
        Assert.IsFalse(policy.IsFlameDormant);
        Assert.Zero(clock.Scale, "Releasing the flame owner must not release another owner's pause.");
        clock.SetIntrinsicPause(other, false);
        Assert.AreEqual(1f, clock.Scale);
    }

    [Test]
    public void DormantBrainsUpdatePreservesCommittedActionGenerationTargetAndMotionProgress()
    {
        var policy = Policy();
        var root = policy.gameObject;
        var fov = root.AddComponent<FOV>();
        var brain = root.AddComponent<LitBrainsEnemy>(); brain.fov = fov;
        brain.ConfigureCharacterData(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset"));
        brain.RefreshEncounterPolicies(); policy.Refresh();
        var target = Root("Committed target").transform;
        typeof(LitBrainsEnemy).GetProperty("AttackPhase").SetValue(brain, LitBrainsAttackPhase.Strike);
        typeof(LitBrainsEnemy).GetProperty("CommittedAttackTarget").SetValue(brain, target);
        typeof(LitBrainsEnemy).GetProperty("ActionSequenceId").SetValue(brain, 7);
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(LitBrainsEnemy).GetField("productionAdvanceProgress", fields).SetValue(brain, .4f);
        typeof(LitBrainsEnemy).GetMethod("Update", fields).Invoke(brain, null);
        Assert.AreEqual(LitBrainsAttackPhase.Strike, brain.AttackPhase);
        Assert.AreSame(target, brain.CommittedAttackTarget);
        Assert.AreEqual(7, brain.ActionSequenceId);
        Assert.AreEqual(.4f, typeof(LitBrainsEnemy).GetField("productionAdvanceProgress", fields).GetValue(brain));
        policy.requiresFlameInfluence = false; policy.Refresh();
        Assert.AreEqual(1f, policy.GetComponent<CombatTimeDomain>().Scale);
    }

    [Test]
    public void DeathPoseDefersToAirborneAndCinematicOwners()
    {
        var root = Root("Pending authored death");
        var fov = root.AddComponent<FOV>();
        var brain = root.AddComponent<LitBrainsEnemy>(); brain.fov = fov;
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(LitBrainsEnemy).GetField("productionAirborne", fields).SetValue(brain, true);
        Assert.IsTrue(brain.IsDeathPosePending);
        brain.EnsureDeathPose();
        Assert.IsFalse((bool)typeof(LitBrainsEnemy).GetField("productionDeathPoseStarted", fields).GetValue(brain));
        typeof(LitBrainsEnemy).GetField("productionAirborne", fields).SetValue(brain, false);
        typeof(LitBrainsEnemy).GetField("cinematicSuspended", fields).SetValue(brain, true);
        Assert.IsTrue(brain.IsDeathPosePending);
        typeof(LitBrainsEnemy).GetField("cinematicSuspended", fields).SetValue(brain, false);
        Assert.IsFalse(brain.IsDeathPosePending);
    }

    [Test]
    public void FinalDeathCannotDeactivateBeforePresentationHasCompleted()
    {
        var root = Root("Unfinished corpse");
        root.AddComponent<CharacterInfo>().SetHealth(0, 30);
        var lifecycle = root.AddComponent<ProductionBrainsDeathLifecycle>();
        Assert.IsFalse(root.GetComponent<EnemyDeathPresentation>().HasCompletedPresentation);
        Assert.IsFalse(lifecycle.TryCompleteDeath());
    }

    [Test]
    public void CompletedOfflineDeathRetainsTheSameHealthAndPersistentRegistryIdentity()
    {
        Assert.IsNull(NetworkObjectRegistry.Instance, "No live persistence registry may be replaced by a fixture.");
        var registry = Root("Retained corpse registry").AddComponent<NetworkObjectRegistry>();
        typeof(NetworkObjectRegistry).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(registry, null);
        var root = Root("Retained persistent corpse");
        var health = root.AddComponent<CharacterInfo>(); health.SetHealth(0, 30);
        var persistent = root.AddComponent<PersistentNetworkObject>(); persistent.AssignSceneIdentity("test.retained.corpse");
        var lifecycle = root.AddComponent<ProductionBrainsDeathLifecycle>();
        typeof(EnemyDeathPresentation).GetProperty("HasCompletedPresentation").SetValue(root.GetComponent<EnemyDeathPresentation>(), true);
        Assert.IsTrue(lifecycle.TryCompleteDeath());
        Assert.IsFalse(root.activeSelf);
        Assert.AreEqual(0, health.CurrentHp);
        Assert.IsTrue(registry.TryGet("test.retained.corpse", out var retained));
        Assert.AreSame(persistent, retained);
    }
}
#endif
