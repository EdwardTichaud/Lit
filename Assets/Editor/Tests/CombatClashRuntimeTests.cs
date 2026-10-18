using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatClashRuntimeTests
{
    private GameObject managerObject, firstObject, secondObject;
    private RealTimeCombatManager manager;
    private CombatLabClashController first, second;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        managerObject = new GameObject("Clash manager");
        managerObject.SetActive(false);
        manager = managerObject.AddComponent<RealTimeCombatManager>();
        firstObject = new GameObject("First pair");
        secondObject = new GameObject("Second pair");
        firstObject.SetActive(false); secondObject.SetActive(false);
        first = firstObject.AddComponent<CombatLabClashController>();
        second = secondObject.AddComponent<CombatLabClashController>();
        SetManager(first); SetManager(second);
    }

    [TearDown]
    public void TearDown()
    {
        first.Unbind(); second.Unbind();
        UnityEngine.Object.DestroyImmediate(firstObject);
        UnityEngine.Object.DestroyImmediate(secondObject);
        UnityEngine.Object.DestroyImmediate(managerObject);
    }

    private void SetManager(CombatLabClashController pair) =>
        typeof(CombatLabClashController).GetField("combat", PrivateInstance).SetValue(pair, manager);
    private static bool Register(CombatLabClashController pair) =>
        (bool)typeof(CombatLabClashController).GetMethod("RegisterArbitrator", PrivateInstance).Invoke(pair, null);

    [Test]
    public void PairRemovalKeepsOtherPairRouterUntilLastUnbind()
    {
        Assert.IsTrue(Register(first));
        var router = manager.ExternalImpactArbitrator;
        Assert.IsTrue(Register(second));
        Assert.AreEqual(router, manager.ExternalImpactArbitrator);
        first.Unbind();
        Assert.AreEqual(router, manager.ExternalImpactArbitrator);
        Assert.IsFalse(router(null, () => Assert.Fail("Unbound pair intercepted damage")));
        second.Unbind();
        Assert.IsNull(manager.ExternalImpactArbitrator);
    }

    [Test]
    public void ForeignArbitratorIsNeitherOverwrittenNorCleared()
    {
        Func<SkillSO, Action, bool> foreign = (skill, apply) => false;
        manager.ExternalImpactArbitrator = foreign;
        LogAssert.Expect(LogType.Warning, "[CombatClash] Pair binding refused: manager arbitration is owned by another system.");
        Assert.IsFalse(Register(first));
        first.Unbind();
        Assert.AreSame(foreign, manager.ExternalImpactArbitrator);
    }

    [Test]
    public void UnbindingDoesNotClearReplacementDelegate()
    {
        Assert.IsTrue(Register(first));
        Func<SkillSO, Action, bool> replacement = (skill, apply) => true;
        manager.ExternalImpactArbitrator = replacement;
        first.Unbind();
        Assert.AreSame(replacement, manager.ExternalImpactArbitrator);
    }

    [Test]
    public void CancelInvalidatesBindingGenerationAndClearsQueuedImpacts()
    {
        var type = typeof(CombatLabClashController);
        var generation = type.GetField("bindingGeneration", PrivateInstance);
        var pending = (IList)type.GetField("pending", PrivateInstance).GetValue(first);
        pending.Add(Activator.CreateInstance(type.GetNestedType("PendingImpact", BindingFlags.NonPublic)));
        int before = (int)generation.GetValue(first);
        first.Cancel();
        Assert.AreNotEqual(before, generation.GetValue(first));
        Assert.IsEmpty(pending);
    }

    [Test]
    public void OrphanActorHasNoActionGenerationAndStartsClosed()
    {
        var actor = firstObject.AddComponent<CombatLabClashActor>();
        Assert.AreEqual(-1, actor.Generation);
        Assert.AreEqual(-1, actor.WindowGeneration);
        Assert.IsFalse(actor.Open);
    }

    [Test]
    public void SharedContactRejectsMissingActors()
    {
        Assert.IsFalse(CombatContactGeometry.HasContact(null, firstObject.transform, 3, 180));
        Assert.IsFalse(CombatContactGeometry.HasContact(firstObject.transform, null, 3, 180));
    }
}
