#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatEngagementLedgerTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private Transform Actor(string name)
    {
        var value = new GameObject(name);
        objects.Add(value);
        return value.transform;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var value in objects) if (value != null) Object.DestroyImmediate(value);
        objects.Clear();
    }

    [Test]
    public void ActorThreatsAreIndependentAndRepeatedContactIsIdempotent()
    {
        var ledger = new CombatEngagementLedger();
        var first = Actor("First player");
        var second = Actor("Second player");
        var enemy = new Target(Actor("Shared enemy"));
        Assert.That(ledger.Add(first, enemy), Is.True);
        Assert.That(ledger.Add(first, enemy), Is.False);
        Assert.That(ledger.Count(first), Is.EqualTo(1));
        Assert.That(ledger.Count(second), Is.Zero);
        Assert.That(ledger.Add(second, enemy), Is.True);
        Assert.That(ledger.Count(second), Is.EqualTo(1));
        Assert.That(ledger.Prune((actor, _) => actor != first), Is.EqualTo(new[] { first }));
        Assert.That(ledger.Count(first), Is.Zero);
        Assert.That(ledger.Count(second), Is.EqualTo(1));
    }

    [Test]
    public void LastThreatEndsOnlyItsParticipantsAndSnapshotsCannotMutateLedger()
    {
        var ledger = new CombatEngagementLedger();
        var first = Actor("First player");
        var second = Actor("Second player");
        var shared = new Target(Actor("Shared enemy"));
        var extra = new Target(Actor("Extra enemy"));
        ledger.Add(first, shared);
        ledger.Add(first, extra);
        ledger.Add(second, shared);
        ledger.Snapshot(first).Clear();
        Assert.That(ledger.Count(first), Is.EqualTo(2));
        shared.IsDead = true;
        Assert.That(ledger.Prune((_, __) => true), Is.EquivalentTo(new[] { first, second }));
        Assert.That(ledger.Count(first), Is.EqualTo(1));
        Assert.That(ledger.Count(second), Is.Zero);
        Assert.That(ledger.Clear(), Is.EqualTo(new[] { first }));
        Assert.That(ledger.Clear(), Is.Empty);
    }

    [Test]
    public void DestroyedActorOrEnemyLeavesNoThreatInNextSession()
    {
        var ledger = new CombatEngagementLedger();
        var actor = Actor("Retiring player");
        ledger.Add(actor, new Target(Actor("Enemy")));
        Object.DestroyImmediate(actor.gameObject);
        Assert.That(ledger.Prune((_, __) => true).Count, Is.EqualTo(1));
        Assert.That(ledger.Clear(), Is.Empty);
        var next = Actor("New player");
        var target = new Target(Actor("Despawned enemy"));
        ledger.Add(next, target);
        Object.DestroyImmediate(target.Root.gameObject);
        ledger.Prune((_, __) => true);
        Assert.That(ledger.Count(next), Is.Zero);
    }

    [Test]
    public void ImpactRetainsActorIdentityAcrossDamageModifierCopy()
    {
        var root = Actor("Impact source").gameObject;
        root.SetActive(false);
        var actor = root.AddComponent<SquadCharacterController>();
        var impact = new CombatImpact(10, 5, CombatImpactOrigin.Basic, source: actor);
        var modified = new CombatImpact(12, impact.Interruption, impact.Origin, true, impact.Source);
        Assert.That(modified.Source, Is.SameAs(actor));
        Assert.That(modified.DamageModifiersApplied, Is.True);
        Assert.That(modified.Damage, Is.EqualTo(12));
    }

    [Test]
    public void ExternalAttackContextBelongsToExactlyOnePresentationGeneration()
    {
        var root = Actor("External action context").gameObject;
        root.SetActive(false);
        var presentation = root.AddComponent<PlayerActionPresentationController>();
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            int first = presentation.BeginExternalPresentation(skill);
            var context = new PlayerAttackContext(first, skill, Vector3.zero, Vector3.right, null, false);
            Assert.That(presentation.SetExternalAttackContext(first, context), Is.True);
            Assert.That(presentation.AttackContext, Is.SameAs(context));
            presentation.EndExternalPresentation(first);
            Assert.That(presentation.AttackContext, Is.Null);
            int second = presentation.BeginExternalPresentation(skill);
            Assert.That(presentation.SetExternalAttackContext(first, context), Is.False);
            Assert.That(presentation.AttackContext, Is.Null);
            Assert.That(presentation.IsCurrentSession(second), Is.True);
            presentation.EndExternalPresentation(second);
        }
        finally { Object.DestroyImmediate(skill); }
    }

    private sealed class Target : ICombatTarget
    {
        public Transform Root { get; }
        public Transform LockPoint => Root;
        public Animator Animator => null;
        public bool IsDead { get; set; }
        public bool IsAttackCommitted => false;
        public int ActionSequenceId => 1;
        public Target(Transform root) { Root = root; }
        public int ReceiveDamage(int damage) => damage;
        public void SetCinematicSuspended(bool value) { }
        public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => false;
    }
}
#endif
