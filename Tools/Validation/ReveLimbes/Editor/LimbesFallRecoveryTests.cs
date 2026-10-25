#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class LimbesFallRecoveryTests
{
    private readonly List<GameObject> owned = new();
    private LimbesFallRecovery recovery;
    private BoxCollider floor;
    private Transform arrival;
    private readonly Vector3 origin = new(10000, 120, 10000);

    [SetUp]
    public void SetUp()
    {
        Assert.That(File.Exists("CycleSmokeValidationProject.marker"), Is.True, "Physics fixture belongs only in the marked disposable copy.");
        var controller = Make("Recovery fixture", origin); controller.SetActive(false); recovery = controller.AddComponent<LimbesFallRecovery>();
        floor = Make("Owned landing floor", origin - Vector3.up * .25f).AddComponent<BoxCollider>(); floor.size = new Vector3(16, .5f, 16);
        arrival = Make("Owned arrival", origin + Vector3.up * .1f).transform;
        recovery.landings = new[] { new LimbesFallRecovery.SafeLanding { id = "safe", arrival = arrival } };
        Physics.SyncTransforms();
    }
    [TearDown]
    public void TearDown()
    {
        foreach (var go in owned) if (go != null) Object.DestroyImmediate(go);
        owned.Clear(); Physics.SyncTransforms();
    }
    [Test]
    public void RecoveryRequiresAnActualFloorAtTheAuthoredArrival()
    {
        Assert.That(Arrival(0, -1, out Pose pose), Is.True);
        Assert.That(pose.position, Is.EqualTo(arrival.position));
        floor.enabled = false; Physics.SyncTransforms();
        Assert.That(Arrival(0, -1, out _), Is.False, "Never teleport back into the void when a landing has lost its collider.");
    }
    [Test]
    public void FourCoopSlotsUseDistinctGroundedArrivalPositions()
    {
        var positions = new HashSet<Vector3>();
        for (int slot = 0; slot < 4; slot++) { Assert.That(Arrival(0, slot, out Pose pose), Is.True); positions.Add(pose.position); }
        Assert.That(positions.Count, Is.EqualTo(4));
    }
    [Test]
    public void OutOfRangeOrMissingArrivalsCannotProduceATeleport()
    {
        Assert.That(Arrival(-1, -1, out _), Is.False); Assert.That(Arrival(8, -1, out _), Is.False);
        recovery.landings[0].arrival = null;
        Assert.That(Arrival(0, -1, out _), Is.False);
    }
    [Test]
    public void ParticipationUsesTheOrientedVolumeAndRejectsForeignZones()
    {
        var go = Make("Participation", new Vector3(600, 145, 40));
        var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(220, 150, 240);
        Assert.That(LimbesFallRecovery.Contains(box, new Vector3(600, 120, 0)), Is.True);
        Assert.That(LimbesFallRecovery.Contains(box, Vector3.zero), Is.False, "A late joiner in Maison is not a falling Limbes participant.");
        Assert.That(LimbesFallRecovery.Contains(box, new Vector3(-1, -94, 55)), Is.False);
        go.transform.position = origin; go.transform.rotation = Quaternion.Euler(0, 45, 0); box.size = Vector3.one * 4;
        Assert.That(LimbesFallRecovery.Contains(box, origin + new Vector3(2.5f, 0, 2.5f)), Is.False, "A point inside a world AABB can still be outside the real rotated trigger.");
    }
    [Test]
    public void DisabledAndNonFiniteVolumesAreNotEntered()
    {
        var box = Make("Trigger", origin).AddComponent<BoxCollider>();
        Assert.That(LimbesFallRecovery.Contains(box, new Vector3(float.NaN, 0, 0)), Is.False);
        box.enabled = false; Assert.That(LimbesFallRecovery.Contains(box, origin), Is.False);
    }
    private bool Arrival(int index, int slot, out Pose pose)
    {
        object[] args = { index, slot, default(Pose) };
        bool result = (bool)typeof(LimbesFallRecovery).GetMethod("TryGetArrival", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(recovery, args);
        pose = (Pose)args[2]; return result;
    }
    private GameObject Make(string name, Vector3 position) { var go = new GameObject(name); go.transform.position = position; owned.Add(go); return go; }
}
#endif
