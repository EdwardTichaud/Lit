using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class LitEnemyFlameInfluenceTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private Flame[] previousRegistry;
    private HashSet<Flame> Registry => (HashSet<Flame>)typeof(Flame)
        .GetField("activeInfluenceFlames", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    [SetUp] public void Setup() { previousRegistry = Registry.ToArray(); Registry.Clear(); }
    [TearDown] public void Cleanup()
    {
        foreach (GameObject root in created) Object.DestroyImmediate(root);
        created.Clear(); Registry.Clear();
        foreach (Flame flame in previousRegistry) if (flame != null) Registry.Add(flame);
    }
    private T Create<T>() where T : Component
    {
        var root = new GameObject(typeof(T).Name); created.Add(root);
        root.SetActive(false);
        return root.AddComponent<T>();
    }
    private Flame CreateFlame(bool ancient = false)
    {
        Flame flame = Create<Flame>();
        SetBool(flame, "isLit", true); SetBool(flame, "ancientFlame", ancient);
        flame.gameObject.SetActive(true); Registry.Add(flame);
        return flame;
    }
    private static void SetBool(Object target, string field, bool value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    [TestCase(false)] [TestCase(true)] public void OnlyRealActiveLitFlamesAreAccepted(bool ancient)
    {
        Flame flame = CreateFlame(ancient);
        var info = new LitInfluenceInfo(flame, ancient ? LitInfluenceSourceKind.AncientFlame : LitInfluenceSourceKind.Flame, Vector3.zero, 6f);
        Assert.That(EnemyController.IsActivationFlame(info), Is.True);
        SetBool(flame, "isLit", false);
        Assert.That(EnemyController.IsActivationFlame(info), Is.False);
        SetBool(flame, "isLit", true); flame.enabled = false;
        Assert.That(EnemyController.IsActivationFlame(info), Is.False);
    }
    [Test] public void PlayerTorchCannotActivateAnEnemyEvenWithFlameKind()
    {
        PlayerTorchInfluence torch = Create<PlayerTorchInfluence>();
        Assert.That(EnemyController.IsActivationFlame(new LitInfluenceInfo(torch, LitInfluenceSourceKind.Flame, Vector3.zero, 6f)), Is.False);
        Assert.That(EnemyController.IsActivationFlame(default), Is.False);
    }
    [Test] public void OverlapRemainsActiveUntilLastSourceDisappears()
    {
        EnemyController enemy = Create<EnemyController>();
        enemy.gameObject.SetActive(true);
        Flame first = CreateFlame(), second = CreateFlame(true);
        Assert.That(Sample(enemy), Is.True);
        Assert.That(SourceCount(enemy), Is.EqualTo(2));
        SetBool(first, "isLit", false);
        Assert.That(Sample(enemy), Is.True);
        Assert.That(SourceCount(enemy), Is.EqualTo(1));
        second.gameObject.SetActive(false); Registry.Remove(second);
        Assert.That(Sample(enemy), Is.False);
        Assert.That(SourceCount(enemy), Is.Zero);
    }
    [Test] public void EnemyRequiresFlameByDefaultAndDeathWinsOverDormancy()
    {
        EnemyController enemy = Create<EnemyController>();
        enemy.Health.SetHealth(10, 10);
        Assert.That(enemy.RequiresFlameInfluence, Is.True);
        Assert.That(enemy.IsFlameDormant, Is.True);
        Assert.That(enemy.ReceiveDamage(5), Is.Zero);
        Assert.That(enemy.Health.CurrentHp, Is.EqualTo(10));
        enemy.Health.SetHealth(0, 10);
        Assert.That(enemy.IsFlameDormant, Is.False);
    }
    [Test] public void ExplicitExemptionDoesNotRequireAnActiveSource()
    {
        EnemyController enemy = Create<EnemyController>();
        enemy.Health.SetHealth(10, 10);
        SetBool(enemy, "requiresFlameInfluence", false);
        Assert.That(enemy.IsFlameDormant, Is.False);
    }
    [Test] public void ActivationRespectsInfluenceLayersAndTriggerRules()
    {
        Flame flame = CreateFlame();
        BoxCollider contact = Create<BoxCollider>(); contact.gameObject.SetActive(true);
        var serialized = new SerializedObject(flame);
        var influence = serialized.FindProperty("litInfluence");
        influence.FindPropertyRelative("layerMask").intValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(flame.ProvidesEnemyActivationTo(contact), Is.False);
        influence.FindPropertyRelative("layerMask").intValue = -1;
        influence.FindPropertyRelative("queryTriggerInteraction").enumValueIndex = (int)QueryTriggerInteraction.Ignore;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        contact.isTrigger = true;
        Assert.That(flame.ProvidesEnemyActivationTo(contact), Is.False);
        contact.isTrigger = false;
        Assert.That(flame.ProvidesEnemyActivationTo(contact), Is.True);
    }
    [Test] public void SuppressedFlameAndOutOfRangeContactDoNotActivate()
    {
        EnemyController enemy = Create<EnemyController>(); enemy.gameObject.SetActive(true);
        Flame flame = CreateFlame();
        typeof(Flame).GetField("externalSuppression", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flame, true);
        Assert.That(Sample(enemy), Is.False);
        typeof(Flame).GetField("externalSuppression", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flame, false);
        flame.transform.position = Vector3.one * 100f;
        Physics.SyncTransforms();
        Assert.That(Sample(enemy), Is.False);
    }
    private static bool Sample(EnemyController enemy) => (bool)typeof(EnemyController)
        .GetMethod("SampleActiveFlameInfluence", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
    private static int SourceCount(EnemyController enemy) => ((HashSet<EntityId>)typeof(EnemyController)
        .GetField("flameSourceIds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy)).Count;
}
