#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using Lit.Combat.Migration.Network;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatCinematicParticipantLifecycleTests
{
    private sealed class Target : ICombatTarget
    {
        public Transform Root { get; set; }
        public Transform LockPoint => Root;
        public Animator Animator => null;
        public bool IsDead { get; set; }
        public bool IsAttackCommitted => false;
        public int ActionSequenceId => 1;
        public int ReceiveDamage(int damage) => 0;
        public void SetCinematicSuspended(bool suspended) { }
        public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => true;
    }

    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<CombatCinematicReservationLease> leases = new List<CombatCinematicReservationLease>();
    private SquadCharacterController source;
    private Target target;

    private GameObject NewObject(string name)
    {
        var value = new GameObject(name); objects.Add(value); return value;
    }

    [SetUp]
    public void SetUp()
    {
        Assert.That(CombatLocalCinematicPolicy.AllowsLocalExecution, Is.True,
            "Lifecycle fixture must not inherit a running network session.");
        source = NewObject("Cinematic source").AddComponent<SquadCharacterController>();
        target = new Target { Root = NewObject("Cinematic victim").transform };
        Assert.That(source.CurrentHp, Is.GreaterThan(0));
    }

    private CombatCinematicReservationLease Acquire()
    {
        Assert.That(CombatCinematicReservations.TryAcquire(this, source.transform, target, out var lease), Is.True);
        leases.Add(lease);
        return lease;
    }

    private static void Sweep() => typeof(CombatCinematicReservations)
        .GetMethod("SweepLocal", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

    [TearDown]
    public void TearDown()
    {
        foreach (var lease in leases) lease.Release();
        leases.Clear();
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }

    [Test]
    public void PairCannotBeStolenAndReleaseAllowsNextSession()
    {
        var first = Acquire();
        Assert.That(CombatCinematicReservations.TryAcquire(new object(), source.transform, target, out _), Is.False);
        first.Release();
        Assert.That(Acquire().IsActive, Is.True);
    }

    [Test]
    public void ReservationProtectsOnlyItsCapturedActorsNotAnotherEncounter()
    {
        var lease = Acquire();
        var unrelated = NewObject("Unrelated combat actor");
        var visualChild = NewObject("Reserved visual child");
        visualChild.transform.SetParent(target.Root);
        Assert.That(CombatCinematicReservations.ContainsParticipant(source.transform), Is.True);
        Assert.That(CombatCinematicReservations.ContainsParticipant(target.Root), Is.True);
        Assert.That(CombatCinematicReservations.ContainsParticipant(visualChild.transform), Is.True);
        Assert.That(CombatCinematicReservations.ContainsParticipant(unrelated.transform), Is.False);
        lease.Release();
        Assert.That(CombatCinematicReservations.ContainsParticipant(source.transform), Is.False);
        Assert.That(CombatCinematicReservations.ContainsParticipant(target.Root), Is.False);
    }

    [Test]
    public void ReleaseIsIdempotentAndCallbackCannotReleaseReplacement()
    {
        var first = Acquire(); int releases = 0;
        first.Released += () => releases++;
        first.Release();
        var replacement = Acquire();
        first.Release(); first.Dispose();
        Assert.That(releases, Is.EqualTo(1));
        Assert.That(replacement.IsActive, Is.True);
    }

    [TestCase("disabled source")]
    [TestCase("dead source")]
    [TestCase("destroyed source")]
    [TestCase("disabled target")]
    [TestCase("dead target")]
    [TestCase("destroyed target")]
    public void ParticipantLossReleasesCapturedPair(string loss)
    {
        var lease = Acquire(); int releases = 0;
        lease.Released += () => releases++;
        switch (loss)
        {
            case "disabled source": source.gameObject.SetActive(false); break;
            case "dead source": typeof(SquadCharacterController).GetField("currentHp",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(source, 0); break;
            case "destroyed source": Object.DestroyImmediate(source.gameObject); break;
            case "disabled target": target.Root.gameObject.SetActive(false); break;
            case "dead target": target.IsDead = true; break;
            case "destroyed target": Object.DestroyImmediate(target.Root.gameObject); break;
        }
        Sweep(); Sweep();
        Assert.That(lease.IsActive, Is.False);
        Assert.That(releases, Is.EqualTo(1));
    }

    [Test]
    public void MissingNetworkAuthorityCannotCreateQteOrSubstituteLocalSource()
    {
        Assert.That(CombatNetworkQteSession.TryCreate(null, null, null, target, _ => true, out var session), Is.False);
        Assert.That(session, Is.Null);
    }

    [Test]
    public void DisabledControllerOwnerReleasesCapturedPair()
    {
        var owner = NewObject("Cinematic controller owner").AddComponent<CombatSkillCinematicController>();
        Assert.That(CombatCinematicReservations.TryAcquire(owner, source.transform, target, out var lease), Is.True);
        leases.Add(lease);
        owner.enabled = false;
        Sweep();
        Assert.That(lease.IsActive, Is.False,
            "ParticipantsAlive must reject a disabled Behaviour owner, not only destroyed owners.");
    }
}
#endif
