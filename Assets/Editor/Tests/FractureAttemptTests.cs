using Lit.Fractures;
using NUnit.Framework;

public sealed class FractureAttemptTests
{
    private static FractureAttempt Exploring(int players = 1)
    {
        var attempt = new FractureAttempt();
        Assert.That(attempt.Start(players, true), Is.True);
        return attempt;
    }

    private static void Open(FractureAttempt attempt)
    {
        Assert.That(attempt.CollectPart(true), Is.True);
        Assert.That(attempt.Repair(), Is.True);
        Assert.That(attempt.OpenThreshold(true, true), Is.True);
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void CompleteAttemptAndReformWithoutAnyPreparation(int players)
    {
        var attempt = Exploring(players);
        Open(attempt);
        attempt.Tick(121);
        Assert.That(attempt.Engage(true, true, 60, 2), Is.True);
        Assert.That(attempt.PowerAtEngagement, Is.EqualTo(2));
        Assert.That(attempt.Win(true), Is.True);
        Assert.That(attempt.RecordReward(), Is.True);
        Assert.That(attempt.RecordReward(), Is.False);
        Assert.That(attempt.Reform(), Is.True);
        Assert.That(attempt.Phase, Is.EqualTo(FracturePhase.Ready));
        Assert.That(attempt.Elapsed, Is.Zero);
        Assert.That(attempt.Darkness, Is.Zero);
        Assert.That(attempt.HasPart || attempt.Repaired || attempt.ThresholdOpen || attempt.GalleryShortcut || attempt.NetworkShortcut || attempt.RewardRecorded, Is.False);
        Assert.That(attempt.Start(players, false), Is.True);
        Assert.That(attempt.AttemptNumber, Is.EqualTo(2));
    }

    [Test]
    public void ClockIncludesBossButLightStopsOnlyDarkness()
    {
        var attempt = Exploring(); Open(attempt); attempt.Tick(30);
        attempt.Engage(true, true, 10, 2); attempt.Tick(15);
        Assert.That(attempt.Elapsed, Is.EqualTo(45));
        Assert.That(attempt.Darkness, Is.EqualTo(30));
        Assert.That(attempt.GetPower(10, 2), Is.EqualTo(2));
        attempt.Win(true); attempt.Tick(100);
        Assert.That(attempt.Elapsed, Is.EqualTo(45));
    }

    [Test]
    public void InvalidTimeCannotChangeTheAttempt()
    {
        var attempt = Exploring();
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, 0d }) attempt.Tick(value);
        Assert.That(attempt.Elapsed, Is.Zero);
        Assert.That(attempt.GetPower(0, 2), Is.Zero);
        Assert.That(attempt.GetPower(double.NaN, 2), Is.Zero);
    }

    [Test]
    public void PartRoutingAndLightAreGatesNotPreparations()
    {
        var attempt = Exploring();
        Assert.That(attempt.Repair(), Is.False);
        Assert.That(attempt.CollectPart(false), Is.False);
        Assert.That(attempt.Engage(true, true, 60, 2), Is.False);
        attempt.CollectPart(true); attempt.Repair();
        Assert.That(attempt.OpenThreshold(false, true), Is.False);
        Assert.That(attempt.OpenThreshold(true, false), Is.False);
        Assert.That(attempt.OpenThreshold(true, true), Is.True);
        Assert.That(attempt.Engage(false, true, 60, 2), Is.False);
        Assert.That(attempt.Engage(true, false, 60, 2), Is.False);
        Assert.That(attempt.Engage(true, true, 60, 2), Is.True);
    }

    [Test]
    public void PreparationsAreIndependentAndIdempotent()
    {
        foreach (var first in new[] { FracturePreparation.Protection, FracturePreparation.Reinforcements })
        {
            var attempt = Exploring();
            Assert.That(attempt.Prepare(first, false, true), Is.False);
            Assert.That(attempt.Prepare(first, true, false), Is.False);
            Assert.That(attempt.Prepare(first, true, true), Is.True);
            Assert.That(attempt.Prepare(first, true, true), Is.False);
            var other = first == FracturePreparation.Protection ? FracturePreparation.Reinforcements : FracturePreparation.Protection;
            Assert.That(attempt.Prepare(other, true, true), Is.True);
            Assert.That(attempt.Preparations, Is.EqualTo(FracturePreparation.Protection | FracturePreparation.Reinforcements));
        }
    }

    [Test]
    public void ActiveAttemptCannotBeRestartedOrReformed()
    {
        var attempt = Exploring(4); attempt.Tick(42);
        Assert.That(attempt.Start(1, false), Is.False);
        Assert.That(attempt.Reform(), Is.False);
        Assert.That(attempt.Elapsed, Is.EqualTo(42));
        Assert.That(attempt.ParticipantCount, Is.EqualTo(4));
    }

    [TestCase(FracturePhase.Lost)] [TestCase(FracturePhase.Abandoned)]
    public void FailureNeverGrantsARewardAndCanBeReformed(FracturePhase outcome)
    {
        var attempt = Exploring(); attempt.CollectPart(true);
        Assert.That(attempt.End(outcome), Is.True);
        Assert.That(attempt.RecordReward(), Is.False);
        Assert.That(attempt.Reform(), Is.True);
        Assert.That(attempt.HasPart, Is.False);
    }

    [Test]
    public void LatePreparationAndFalseVictoryAreRejected()
    {
        var attempt = Exploring(); Open(attempt); attempt.Engage(true, true, 60, 2);
        Assert.That(attempt.Prepare(FracturePreparation.Protection, true, true), Is.False);
        Assert.That(attempt.Win(false), Is.False);
        Assert.That(attempt.RecordReward(), Is.False);
        Assert.That(attempt.Win(true), Is.True);
        Assert.That(attempt.Win(true), Is.False);
    }
}
