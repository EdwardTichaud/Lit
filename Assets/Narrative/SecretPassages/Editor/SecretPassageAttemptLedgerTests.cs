using NUnit.Framework;
using UnityEngine;

public sealed class SecretPassageAttemptLedgerTests
{
    private GameObject root;
    private WorldRulesStateManager world;
    [SetUp] public void Setup()
    {
        root = new GameObject("Owned secret passage attempt fixture"); root.SetActive(false);
        world = root.AddComponent<WorldRulesStateManager>();
    }
    [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

    [TestCase(1, SecretPassageAttemptLedger.Result.Failed)]
    [TestCase(2, SecretPassageAttemptLedger.Result.Succeeded)]
    [TestCase(3, SecretPassageAttemptLedger.Result.Succeeded)]
    [TestCase(4, SecretPassageAttemptLedger.Result.Succeeded)]
    [TestCase(5, SecretPassageAttemptLedger.Result.Succeeded)]
    [TestCase(6, SecretPassageAttemptLedger.Result.Succeeded)]
    public void ExactD6ThresholdAndOutcomeArePersistent(int roll, SecretPassageAttemptLedger.Result expected)
    {
        Assert.That(SecretPassageAttemptLedger.Observe(world, "passage", "lucian", true, false, () => roll), Is.EqualTo(expected));
        Assert.That(SecretPassageAttemptLedger.HasAttempted(world, "passage", "lucian"), Is.True);
        Assert.That(world.CaptureVariables().Count, Is.EqualTo(1));
    }

    [TestCase(1)] [TestCase(6)]
    public void RepeatedApproachesNeverRollAgain(int initialRoll)
    {
        int rolls = 0;
        var first = SecretPassageAttemptLedger.Observe(world, "p", "lucian", true, false, () => { rolls++; return initialRoll; });
        for (int i = 0; i < 20; i++)
            Assert.That(SecretPassageAttemptLedger.Observe(world, "p", "lucian", true, false, () => { rolls++; return 6; }), Is.EqualTo(first));
        Assert.That(rolls, Is.EqualTo(1));
    }

    [Test] public void DifferentCharactersAndPassagesHaveIndependentAttempts()
    {
        Assert.That(SecretPassageAttemptLedger.Observe(world, "p1", "lucian", true, false, () => 1), Is.EqualTo(SecretPassageAttemptLedger.Result.Failed));
        Assert.That(SecretPassageAttemptLedger.Observe(world, "p1", "vesta", true, false, () => 2), Is.EqualTo(SecretPassageAttemptLedger.Result.Succeeded));
        Assert.That(SecretPassageAttemptLedger.Observe(world, "p2", "lucian", true, false, () => 6), Is.EqualTo(SecretPassageAttemptLedger.Result.Succeeded));
        Assert.That(world.CaptureVariables().Count, Is.EqualTo(3));
    }

    [Test] public void RestoringTheWorldSnapshotRetainsAnUnsuccessfulAttempt()
    {
        SecretPassageAttemptLedger.Observe(world, "p", "lucian", true, false, () => 1);
        var saved = world.CaptureVariables();
        world.ResetRuntimeState(); world.ApplyVariables(saved);
        Assert.That(SecretPassageAttemptLedger.Observe(world, "p", "lucian", true, false, () => { Assert.Fail("A restored attempt must not reroll."); return 6; }), Is.EqualTo(SecretPassageAttemptLedger.Result.Failed));
    }

    [Test] public void ReadingTheNoteOrSharedKnowledgeBypassesAnEarlierFailureWithoutAnotherAttempt()
    {
        SecretPassageAttemptLedger.Observe(world, "p", "lucian", true, false, () => 1);
        Assert.That(SecretPassageAttemptLedger.Observe(world, "p", "lucian", false, true, null), Is.EqualTo(SecretPassageAttemptLedger.Result.AlreadyKnown));
        Assert.That(SecretPassageAttemptLedger.HasAttempted(world, "p", "vesta"), Is.False);
    }

    [Test] public void MissingAbilityIdentityOrWorldDoesNotSpendAnAttempt()
    {
        Assert.That(SecretPassageAttemptLedger.Observe(world, "p", "lucian", false, false, () => 6), Is.EqualTo(SecretPassageAttemptLedger.Result.Unavailable));
        Assert.That(SecretPassageAttemptLedger.Observe(world, "", "lucian", true, false, () => 6), Is.EqualTo(SecretPassageAttemptLedger.Result.Unavailable));
        Assert.That(SecretPassageAttemptLedger.Observe(world, "p", "", true, false, () => 6), Is.EqualTo(SecretPassageAttemptLedger.Result.Unavailable));
        Assert.That(SecretPassageAttemptLedger.Observe(null, "p", "lucian", true, false, () => 6), Is.EqualTo(SecretPassageAttemptLedger.Result.Unavailable));
        Assert.That(world.CaptureVariables(), Is.Empty);
    }

    [Test] public void EquippingObserverAgainDoesNotResetAnAttempt()
    {
        SecretPassageAttemptLedger.Observe(world, "p", "lucian", true, false, () => 1);
        SecretPassageAttemptLedger.Observe(world, "p", "lucian", false, false, null);
        Assert.That(SecretPassageAttemptLedger.Observe(world, "p", "lucian", true, false, () => 6), Is.EqualTo(SecretPassageAttemptLedger.Result.Failed));
    }

    [Test] public void SeparatorCharactersCannotCollideBetweenIdentities()
    {
        Assert.That(SecretPassageAttemptLedger.AttemptKey("a|b", "c"), Is.Not.EqualTo(SecretPassageAttemptLedger.AttemptKey("a", "b|c")));
        Assert.That(SecretPassageAttemptLedger.AttemptKey("a%7Cb", "c"), Is.Not.EqualTo(SecretPassageAttemptLedger.AttemptKey("a|b", "c")));
    }

    [TestCase(0)] [TestCase(7)]
    public void InvalidDieDoesNotCommitAPersistentResult(int roll)
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => SecretPassageAttemptLedger.Observe(world, "p", "lucian", true, false, () => roll));
        Assert.That(world.CaptureVariables(), Is.Empty);
    }
}
