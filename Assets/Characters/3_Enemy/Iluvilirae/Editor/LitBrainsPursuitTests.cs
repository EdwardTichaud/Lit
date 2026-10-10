#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Ultrabolt.BrainsAI;

public sealed class LitBrainsPursuitTests
{
    private GameObject holder, target;
    private LitBrainsEnemy enemy;
    private CharacterData data;
    private LitBrainsCombatProfile profile;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void Setup()
    {
        holder = new GameObject("Pursuit fixture");
        var actor = new GameObject("Enemy");
        actor.transform.SetParent(holder.transform);
        actor.transform.position = new Vector3(10000, 0, 0);
        actor.SetActive(false);
        enemy = actor.AddComponent<LitBrainsEnemy>();
        enemy.fov = actor.AddComponent<FOV>();
        enemy.fov.useExternalDetection = true;
        data = ScriptableObject.CreateInstance<CharacterData>();
        data.vision.eyeHeight = data.vision.targetHeight = 0;
        data.vision.maximumDistance = 10;
        data.vision.fieldOfViewDegrees = 100;
        data.vision.obstructionMask = ~0;
        enemy.characterData = data;
        target = new GameObject("Player");
        target.transform.SetParent(holder.transform);
        target.transform.position = actor.transform.position + Vector3.forward * 2;
    }

    [TearDown]
    public void Cleanup()
    {
        Object.DestroyImmediate(holder);
        Object.DestroyImmediate(data);
        if (profile != null) Object.DestroyImmediate(profile);
    }

    private object Invoke(string method, params object[] args) => typeof(LitBrainsEnemy).GetMethod(method, Private).Invoke(enemy, args);
    private void Detect(Transform candidate = null) => Invoke("UpdatePerception", candidate != null ? candidate : target.transform);
    private GameObject Wall()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(holder.transform);
        wall.transform.position = enemy.transform.position + Vector3.forward;
        wall.transform.localScale = new Vector3(4, 4, .25f);
        Physics.SyncTransforms();
        return wall;
    }

    [Test]
    public void UnseenPlayerBehindCannotEngageButKnownPlayerIsRetainedAroundTheEnemy()
    {
        target.transform.position = enemy.transform.position + Vector3.back * 2;
        Detect();
        Assert.IsNull(enemy.fov.CurrentTarget);
        target.transform.position = enemy.transform.position + Vector3.forward * 2;
        Detect();
        Assert.AreEqual(target.transform, enemy.fov.CurrentTarget);
        for (int angle = 0; angle < 360; angle += 30)
        {
            target.transform.position = enemy.transform.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * 2;
            Detect();
            Assert.AreEqual(target.transform, enemy.fov.CurrentTarget, "angle " + angle);
        }
    }

    [Test]
    public void ObstructionPreventsAcquisitionAndAttacksButNotKnownTargetPursuit()
    {
        var wall = Wall();
        Detect();
        Assert.IsNull(enemy.fov.CurrentTarget);
        Object.DestroyImmediate(wall); Physics.SyncTransforms();
        Detect();
        Wall();
        Detect();
        Assert.AreEqual(target.transform, enemy.fov.CurrentTarget);
        Assert.IsFalse((bool)Invoke("HasAttackLineOfSight"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BlockedImpactIsConsumedOnceWithAndWithoutCombatProfile(bool fluid)
    {
        if (fluid)
        {
            profile = ScriptableObject.CreateInstance<LitBrainsCombatProfile>();
            enemy.combatProfile = profile;
            typeof(LitBrainsEnemy).GetProperty("AttackPhase").SetValue(enemy, LitBrainsAttackPhase.Strike);
        }
        Detect();
        var wall = Wall();
        Assert.IsFalse((bool)Invoke("TryConfirmAttackImpact"));
        Object.DestroyImmediate(wall); Physics.SyncTransforms();
        Assert.IsTrue((bool)Invoke("HasAttackLineOfSight"));
        Assert.IsFalse((bool)Invoke("TryConfirmAttackImpact"), "A blocked contact cannot be retried later.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ClearImpactCanBeConfirmedOnlyOnce(bool fluid)
    {
        if (fluid)
        {
            profile = ScriptableObject.CreateInstance<LitBrainsCombatProfile>();
            enemy.combatProfile = profile;
            typeof(LitBrainsEnemy).GetProperty("AttackPhase").SetValue(enemy, LitBrainsAttackPhase.Strike);
        }
        Detect();
        Assert.IsTrue((bool)Invoke("TryConfirmAttackImpact"));
        Assert.IsFalse((bool)Invoke("TryConfirmAttackImpact"));
    }

    [Test]
    public void RangeAndHeightUseTheExistingVisionDistanceAndRequireFreshAcquisitionAfterLoss()
    {
        Detect();
        target.transform.position = enemy.transform.position + Vector3.up * 10.1f;
        Detect(); Assert.IsNull(enemy.fov.CurrentTarget);
        target.transform.position = enemy.transform.position + Vector3.back * 2;
        Detect(); Assert.IsNull(enemy.fov.CurrentTarget);
    }

    [Test]
    public void InactiveDeadAndChangedTargetsCannotKeepTheOldAcquisition()
    {
        Detect(); target.SetActive(false); Detect(); Assert.IsNull(enemy.fov.CurrentTarget);
        target.SetActive(true); Detect();
        var other = new GameObject("Other player"); other.transform.SetParent(holder.transform);
        other.transform.position = enemy.transform.position + Vector3.back * 2;
        Detect(other.transform); Assert.IsNull(enemy.fov.CurrentTarget);
        Detect();
        target.AddComponent<CharacterInfo>().SetHealth(0, 100);
        Detect(); Assert.IsNull(enemy.fov.CurrentTarget);
    }

    [Test]
    public void ResetClearsTargetAndOldInvestigation()
    {
        Detect();
        Invoke("ResetPerception");
        Assert.IsNull(enemy.fov.CurrentTarget);
        Assert.AreEqual(FOV.Status.None, enemy.fov.status);
        target.transform.position = enemy.transform.position + Vector3.back * 2;
        Detect(); Assert.IsNull(enemy.fov.CurrentTarget);
    }
}
#endif
