using NUnit.Framework;
using UnityEngine;

public sealed class CombatLabClashTests
{
    [Test]
    public void SweepFindsCrossingEvenWhenBothEndpointsAreSeparated()
    {
        Assert.IsTrue(CombatLabClashController.SweptContact(Vector3.left * 2, Vector3.right * 2, .1f,
            Vector3.right * 2, Vector3.left * 2, .1f, out var contact));
        Assert.Less(contact.magnitude, .001f);
    }
    [Test]
    public void ParallelAndVerticallySeparatedAttacksDoNotClash()
    {
        Assert.IsFalse(CombatLabClashController.SweptContact(Vector3.zero, Vector3.forward, .1f,
            Vector3.up, Vector3.up + Vector3.forward, .1f, out _));
    }
    [Test]
    public void StationaryOverlappingVolumesClash()
    {
        Assert.IsTrue(CombatLabClashController.SweptContact(Vector3.zero, Vector3.zero, .5f,
            Vector3.right * .9f, Vector3.right * .9f, .5f, out _));
    }
    [TestCase(7, 7, 7, false)]
    [TestCase(7, 8, 7, false)]
    [TestCase(8, 8, 7, true)]
    public void NeutralizedAndStaleAttacksCannotDeliver(int queued, int current, int neutralized, bool expected)
    {
        Assert.AreEqual(expected, CombatLabClashController.ShouldDeliver(queued, current, neutralized));
    }
    [Test]
    public void SweptResultIsIndependentOfActorEvaluationOrder()
    {
        bool ab = CombatLabClashController.SweptContact(Vector3.left, Vector3.right, .2f,
            Vector3.back, Vector3.forward, .4f, out var a);
        bool ba = CombatLabClashController.SweptContact(Vector3.back, Vector3.forward, .4f,
            Vector3.left, Vector3.right, .2f, out var b);
        Assert.AreEqual(ab, ba); Assert.AreEqual(a, b);
    }
}
