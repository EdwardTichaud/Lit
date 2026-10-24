using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CycleGroupSequenceGateTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject gateObject, playerObject;
    private CycleGroupSequenceGate gate;
    private SquadCharacterController character;
    [SetUp]
    public void SetUp()
    {
        gateObject = new GameObject("Isolated group gate");
        gate = gateObject.AddComponent<CycleGroupSequenceGate>();
        playerObject = new GameObject("Isolated living participant"); playerObject.SetActive(false);
        character = playerObject.AddComponent<SquadCharacterController>();
        character.SetHealth(10, 10);
        Physics.SyncTransforms();
    }
    [TearDown]
    public void TearDown() { Object.DestroyImmediate(playerObject); Object.DestroyImmediate(gateObject); }
    private void Pending()
    {
        typeof(CycleGroupSequenceGate).GetField("offlineExpires", Private).SetValue(gate, Time.unscaledTimeAsDouble + 30);
        typeof(CycleGroupSequenceGate).GetField("offlineStarts", Private).SetValue(gate, Time.unscaledTimeAsDouble + 3);
    }
    private bool Call(string method) => (bool)typeof(CycleGroupSequenceGate).GetMethod(method, Private).Invoke(gate, new object[] { playerObject.transform });

    [TestCase(false, 1f, true)]
    [TestCase(false, 5f, false)]
    [TestCase(true, 5f, true)]
    [TestCase(true, 9f, false)]
    public void HostRequiresInteractionRangeToOpenButAllowsGatheringRangeToConfirm(bool pending, float distance, bool expected)
    {
        if (pending) Pending();
        playerObject.transform.position = Vector3.right * distance;
        Physics.SyncTransforms();
        Assert.That(Call("CanConfirmLocation"), Is.EqualTo(expected));
    }
    [TestCase(false, 1f, false)]
    [TestCase(true, 1f, true)]
    [TestCase(true, 9f, false)]
    public void CancellationCannotBeSentFromOutsideTheGathering(bool pending, float distance, bool expected)
    {
        if (pending) Pending();
        playerObject.transform.position = Vector3.right * distance;
        Assert.That(Call("CanCancelRequest"), Is.EqualTo(expected));
    }
    [Test]
    public void DeadParticipantCannotConfirmOrCancel()
    {
        Pending(); character.SetHealth(0, 10);
        Assert.That(Call("CanConfirmLocation"), Is.False);
        Assert.That(Call("CanCancelRequest"), Is.False);
    }
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-8f)]
    public void InvalidGatheringRadiusCannotAcceptAPlayer(float radius)
    {
        Pending(); gate.gatheringRadius = radius;
        Assert.That(Call("CanConfirmLocation"), Is.False);
    }
    [TestCase(true)]
    [TestCase(false)]
    public void RosterChangeResetsCountdownAndOnlyJoinsInvalidateSurvivingConfirmations(bool joins)
    {
        Pending();
        typeof(CycleGroupSequenceGate).GetMethod("CaptureRoster", Private).Invoke(gate, new object[] { new ulong[] { 0, 1 } });
        var votes = (HashSet<ulong>)typeof(CycleGroupSequenceGate).GetField("offlineConfirmations", Private).GetValue(gate);
        votes.Add(0); votes.Add(1);
        typeof(CycleGroupSequenceGate).GetMethod("ReconcileRoster", Private).Invoke(gate,
            new object[] { joins ? new ulong[] { 0, 1, 2 } : new ulong[] { 1 } });
        Assert.That((double)typeof(CycleGroupSequenceGate).GetField("offlineStarts", Private).GetValue(gate), Is.Zero);
        Assert.That(gate.Pending, Is.True);
        Assert.That(votes.Contains(1), Is.EqualTo(!joins));
        if (joins) Assert.That(votes, Is.Empty);
    }
    [Test]
    public void StableRosterOrderDoesNotRestartTheCountdown()
    {
        Pending();
        typeof(CycleGroupSequenceGate).GetMethod("CaptureRoster", Private).Invoke(gate, new object[] { new ulong[] { 0, 1 } });
        double before = (double)typeof(CycleGroupSequenceGate).GetField("offlineStarts", Private).GetValue(gate);
        typeof(CycleGroupSequenceGate).GetMethod("ReconcileRoster", Private).Invoke(gate, new object[] { new ulong[] { 1, 0, 1 } });
        Assert.That((double)typeof(CycleGroupSequenceGate).GetField("offlineStarts", Private).GetValue(gate), Is.EqualTo(before));
    }
}
