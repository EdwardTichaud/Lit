#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatThresholdParticipantTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> objects = new List<Object>();
    private CombatHealthThresholdController controller;
    private RealTimeCombatManager manager;
    private SquadCharacterController source, otherSource;
    private CharacterInfo health;
    private object adapter;
    private object previousController;
    private FieldInfo singleton;

    private sealed class Target : ICombatTarget, ICombatHealthThresholdParticipantActor
    {
        public CharacterInfo Health;
        public Transform Root => Health != null ? Health.transform : null;
        public Transform LockPoint => Root;
        public Animator Animator => null;
        public bool IsDead => Health == null || Health.IsDead;
        public bool IsAttackCommitted => false;
        public int ActionSequenceId => 1;
        public SquadCharacterController RetaliationParticipant;
        public int ReceiveDamage(int damage) => Health.ApplyDamage(damage);
        public void SetCinematicSuspended(bool suspended) { }
        public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => true;
        public bool TryStartThresholdFailureRetaliation(SkillSO skill, SquadCharacterController participant)
        { RetaliationParticipant = participant; return true; }
    }

    private GameObject NewObject(string name, bool active = true)
    {
        var result = new GameObject(name);
        result.SetActive(active);
        objects.Add(result);
        return result;
    }

    private T NewAsset<T>() where T : ScriptableObject
    {
        var result = ScriptableObject.CreateInstance<T>();
        objects.Add(result);
        return result;
    }

    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
    private object Get(string name) => typeof(CombatHealthThresholdController).GetField(name, Fields).GetValue(controller);
    private object Invoke(string name, params object[] args) => typeof(CombatHealthThresholdController).GetMethod(name, Fields).Invoke(controller, args);

    [SetUp]
    public void SetUp()
    {
        singleton = typeof(CombatHealthThresholdController).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        previousController = singleton.GetValue(null);
        singleton.SetValue(null, null);
        manager = NewObject("Inactive threshold manager", false).AddComponent<RealTimeCombatManager>();
        controller = NewObject("Threshold participant fixture").AddComponent<CombatHealthThresholdController>();
        source = NewObject("Impact source").AddComponent<SquadCharacterController>();
        otherSource = NewObject("Other player").AddComponent<SquadCharacterController>();
        Set(manager, "playerRoot", source.transform);
        Set(controller, "combatManager", manager);

        var data = NewAsset<CharacterData>();
        data.hp = 100;
        data.isEnemy = true;
        data.enableCombatHealthThresholds = true;
        var sequence = NewAsset<ThresholdSequence>();
        var qte = new AnimationClip(); objects.Add(qte);
        UnityEditor.AnimationUtility.SetAnimationEvents(qte,
            new[] { new AnimationEvent { functionName = "QTE", stringParameter = "Y", time = 0 } });
        var success = new AnimationClip(); objects.Add(success);
        sequence.steps.Add(new ThresholdSequenceStep { qtePresentationClip = qte, successPlayerAnimationClip = success });
        data.combatHealthThresholdStages = new List<CombatHealthThresholdStage>
        { new CombatHealthThresholdStage { healthPercent = 50, sequence = sequence } };
        health = NewObject("Threshold target").AddComponent<CharacterInfo>();
        health.SetCharacterData(data);
        health.SetHealth(100, 100);
        var adapterType = typeof(CombatHealthThresholdController).Assembly.GetType("CombatHealthThresholdTarget");
        adapter = adapterType.GetConstructor(Fields, null, new[] { typeof(ICombatTarget), typeof(EnemyController) }, null)
            .Invoke(new object[] { new Target { Health = health }, null });

        Assert.That(Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening,
            Is.True, "This fixture requires offline authority; it must not inherit a live network session.");
        Assert.That(CombatProtectionDamage.HasAuthority, Is.True);
        Assert.That(controller.isActiveAndEnabled, Is.True);
        Assert.That(source.isActiveAndEnabled && otherSource.isActiveAndEnabled, Is.True);
        Assert.That(source.CurrentHp > 0 && otherSource.CurrentHp > 0, Is.True);
        Assert.That(manager.PlayerRoot, Is.SameAs(source.transform));
        Assert.That(health.gameObject.activeInHierarchy, Is.True);
        Assert.That(health.CurrentHp, Is.EqualTo(100));
        Assert.That(health.MaxHp, Is.EqualTo(100));
        Assert.That(health.CharacterData.isEnemy && health.CharacterData.enableCombatHealthThresholds, Is.True);
        Assert.That(ThresholdSequence.CountQteEvents(qte), Is.EqualTo(1));
        Assert.That(sequence.IsComplete, Is.True, "The runtime stage-validation gate must accept the authored fixture.");
        Assert.That(data.combatHealthThresholdStages[0].IsComplete, Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        if (controller != null) controller.AbortActiveSequence("test cleanup");
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        singleton.SetValue(null, previousController);
    }

    private bool Prepare(SquadCharacterController participant, int damage, out int allowed)
    {
        var method = typeof(CombatHealthThresholdController).GetMethod("TryPrepareDamage", Fields, null,
            new[] { adapter.GetType(), typeof(int), typeof(int).MakeByRefType(), typeof(SquadCharacterController) }, null);
        var args = new object[] { adapter, damage, 0, participant };
        bool result = (bool)method.Invoke(controller, args);
        allowed = (int)args[2];
        return result;
    }

    [TestCase(typeof(EnemyController))]
    [TestCase(typeof(LitBrainsEnemy))]
    public void PublicDamageAndCommitApisAcceptExplicitSource(System.Type enemyType)
    {
        Assert.That(typeof(CombatHealthThresholdController).GetMethod("TryPrepareDamage", new[]
            { enemyType, typeof(int), typeof(int).MakeByRefType(), typeof(SquadCharacterController) }), Is.Not.Null);
        Assert.That(typeof(CombatHealthThresholdController).GetMethod("NotifyThresholdDamageApplied", new[]
            { enemyType, typeof(SquadCharacterController) }), Is.Not.Null);
    }

    [Test]
    public void CrossingImpactCapturesItsExplicitParticipant()
    {
        Assert.That(Prepare(source, 70, out int allowed), Is.True);
        Assert.That(allowed, Is.EqualTo(50));
        Assert.That(controller.ThresholdParticipant, Is.SameAs(source));
        Assert.That(Get("thresholdPlayer"), Is.SameAs(source.transform));
    }

    [Test]
    public void RemoteSourceNeverFallsBackToLocalPlayerOrClampsHealth()
    {
        LogAssert.Expect(LogType.Warning, new Regex("Remote threshold participant execution is unavailable"));
        Assert.That(Prepare(otherSource, 70, out int allowed), Is.False);
        Assert.That(allowed, Is.EqualTo(70));
        Assert.That(controller.ThresholdParticipant, Is.Null);
        Assert.That(controller.IsSequenceActive, Is.False);
    }

    [Test]
    public void MissingSourceNeverSelectsManagerPlayer()
    {
        LogAssert.Expect(LogType.Warning, new Regex("requires an explicit source actor"));
        Assert.That(Prepare(null, 70, out int allowed), Is.False);
        Assert.That(allowed, Is.EqualTo(70));
        Assert.That(controller.ThresholdParticipant, Is.Null);
    }

    [Test]
    public void OtherImpactAndStaleNotificationCannotTakeOverPendingPair()
    {
        Assert.That(Prepare(source, 70, out _), Is.True);
        Assert.That(Prepare(otherSource, 70, out _), Is.False);
        var notify = typeof(CombatHealthThresholdController).GetMethod("NotifyThresholdDamageApplied", Fields,
            null, new[] { typeof(Component), typeof(SquadCharacterController) }, null);
        notify.Invoke(controller, new object[] { health, otherSource });
        notify.Invoke(controller, new object[] { otherSource, source });
        Assert.That(Get("pendingRoutine"), Is.Null);
        Assert.That(controller.ThresholdParticipant, Is.SameAs(source));
        Assert.That(Get("activeEnemy"), Is.SameAs(adapter));
    }

    [Test]
    public void StaleSequenceTokenCannotAbortOrRedirectNewPendingPair()
    {
        Assert.That(Prepare(source, 70, out _), Is.True);
        Set(controller, "sessionToken", 10);
        Invoke("StartSequence", 9, Vector3.zero, Quaternion.identity, Quaternion.identity);
        Assert.That(controller.ThresholdParticipant, Is.SameAs(source));
        Assert.That(controller.IsSequenceActive, Is.True);
        Assert.That(Get("thresholdOwnershipHeld"), Is.False);
    }

    [TestCase("disabled")]
    [TestCase("dead")]
    [TestCase("destroyed")]
    public void SourceDisappearanceReleasesPendingStageAndNextImpactCanRetry(string loss)
    {
        Assert.That(Prepare(source, 70, out int allowed), Is.True);
        health.ApplyDamage(allowed);
        if (loss == "disabled") source.gameObject.SetActive(false);
        else if (loss == "dead") Set(source, "currentHp", 0);
        else Object.DestroyImmediate(source.gameObject);
        Invoke("StartSequence", 0, Vector3.zero, Quaternion.identity, Quaternion.identity);
        Assert.That(controller.IsSequenceActive, Is.False);
        Assert.That(controller.ThresholdParticipant, Is.Null);
        Assert.That(Get("activeEnemy"), Is.Null);
        Assert.That(((IDictionary)Get("retryEligibleStages")).Count, Is.EqualTo(1));
        Set(manager, "playerRoot", otherSource.transform);
        Assert.That(Prepare(otherSource, 5, out allowed), Is.True);
        Assert.That(allowed, Is.EqualTo(5));
        Assert.That(controller.ThresholdParticipant, Is.SameAs(otherSource));
    }

    [Test]
    public void ManagerSwitchDoesNotRebindSourceOrClearReplacementCinematic()
    {
        Assert.That(Prepare(source, 70, out _), Is.True);
        Set(manager, "playerRoot", otherSource.transform);
        Set(manager, "<IsCinematicSequenceActive>k__BackingField", true);
        controller.BlocksEnemyActions((LitBrainsEnemy)null);
        Assert.That(controller.ThresholdParticipant, Is.Null);
        Assert.That(controller.IsSequenceActive, Is.False);
        Assert.That(manager.IsCinematicSequenceActive, Is.True);
    }

    [Test]
    public void OwnedCleanupDoesNotClearReplacementActorsCinematicFlag()
    {
        Assert.That(Prepare(source, 70, out _), Is.True);
        Set(controller, "thresholdOwnershipHeld", true);
        Set(manager, "playerRoot", otherSource.transform);
        Set(manager, "<IsCinematicSequenceActive>k__BackingField", true);
        controller.AbortActiveSequence("manager actor replaced");
        Assert.That(controller.ThresholdParticipant, Is.Null);
        Assert.That(Get("thresholdOwnershipHeld"), Is.False);
        Assert.That(manager.IsCinematicSequenceActive, Is.True);
    }

    [Test]
    public void AbandonedStageWithoutSourceAllowsLethalDamageInsteadOfImmortalClamp()
    {
        Assert.That(Prepare(source, 70, out _), Is.True);
        controller.AbortActiveSequence("participant gone before begin");
        health.SetHealth(1, 100);
        LogAssert.Expect(LogType.Warning, new Regex("requires an explicit source actor"));
        Assert.That(Prepare(null, 10, out int allowed), Is.False);
        Assert.That(allowed, Is.EqualTo(10));
        health.ApplyDamage(allowed);
        Assert.That(health.IsDead, Is.True);
    }

    [Test]
    public void MissingCommitNotificationCannotLeavePermanentPendingClamp()
    {
        Assert.That(Prepare(source, 70, out _), Is.True);
        Set(controller, "thresholdPreparedFrame", Time.frameCount - 1);
        Invoke("UpdateThresholdFlamePause");
        Assert.That(controller.IsSequenceActive, Is.False);
        Assert.That(controller.ThresholdParticipant, Is.Null);
    }

    [Test]
    public void RetaliationAdapterPassesCapturedSourceRatherThanPerceptionTarget()
    {
        var target = (Target)adapter.GetType().GetProperty("Target").GetValue(adapter);
        var method = adapter.GetType().GetMethod("TryStartThresholdFailureRetaliation",
            new[] { typeof(SkillSO), typeof(SquadCharacterController) });
        Assert.That(method.Invoke(adapter, new object[] { NewAsset<SkillSO>(), source }), Is.True);
        Assert.That(target.RetaliationParticipant, Is.SameAs(source));
    }
}
#endif
