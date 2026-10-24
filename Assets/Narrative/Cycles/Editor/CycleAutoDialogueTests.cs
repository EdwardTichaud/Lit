using System;
using NUnit.Framework;

public sealed class CycleAutoDialogueTests
{
    private static CycleAutoDialogueSession Session(params ulong[] participants)
        => new CycleAutoDialogueSession(7, "intro", participants, 100d, 5d, 30d);

    [Test] public void TimerAloneNeverCompletesADialogue()
    {
        var session = Session(1);
        session.Poll(new[] { 1UL }, 110d);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Waiting));
        session.Poll(new[] { 1UL }, 135d);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Failed));
        Assert.That(session.CompletedCount, Is.Zero);
    }

    [TestCase(1UL, 2UL)] [TestCase(2UL, 1UL)]
    public void EveryFrozenViewerMustFinishNaturally(ulong first, ulong second)
    {
        var session = Session(1, 2);
        Assert.That(session.Acknowledge(first, 7, true, 106d), Is.True);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Waiting));
        Assert.That(session.Acknowledge(second, 7, true, 108d), Is.True);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Completed));
        Assert.That(session.CompletedCount, Is.EqualTo(2));
    }

    [Test] public void EarlyUnknownStaleAndDuplicateAcknowledgementsCannotAdvance()
    {
        var session = Session(1, 2);
        Assert.That(session.Acknowledge(1, 7, true, 101d), Is.False);
        Assert.That(session.Acknowledge(1, 6, true, 106d), Is.False);
        Assert.That(session.Acknowledge(99, 7, true, 106d), Is.False);
        Assert.That(session.PendingCount, Is.EqualTo(2));
        Assert.That(session.Acknowledge(1, 7, true, 106d), Is.True);
        Assert.That(session.Acknowledge(1, 7, false, 107d), Is.False);
        Assert.That(session.PendingCount, Is.EqualTo(1));
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Waiting));
        Assert.That(session.Acknowledge(2, 7, true, 108d), Is.True);
        Assert.That(session.Acknowledge(2, 7, true, 109d), Is.False);
        Assert.That(session.CompletedCount, Is.EqualTo(2));
    }

    [TestCase(false)] [TestCase(true)]
    public void RefusalOrInterruptionFailsWithoutCompletingOthers(bool otherAlreadyFinished)
    {
        var session = Session(1, 2);
        if (otherAlreadyFinished) session.Acknowledge(1, 7, true, 106d);
        Assert.That(session.Acknowledge(2, 7, false, 107d), Is.True);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Failed));
        Assert.That(session.Acknowledge(1, 7, true, 108d), Is.False);
    }

    [Test] public void DisconnectedViewerDoesNotHoldUpACompletedViewer()
    {
        var session = Session(1, 2);
        session.Acknowledge(1, 7, true, 106d);
        session.Poll(new[] { 1UL }, 107d);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Completed));
        Assert.That(session.CompletedCount, Is.EqualTo(1));
    }

    [Test] public void EveryoneDisconnectingCannotValidateAnUnseenLine()
    {
        var session = Session(1, 2);
        session.Poll(Array.Empty<ulong>(), 106d);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Failed));
        Assert.That(session.CompletedCount, Is.Zero);
    }

    [Test] public void LateArrivalDoesNotJoinTheFrozenAudienceOrConfirmForIt()
    {
        var session = Session(1, 2);
        session.Poll(new[] { 1UL, 2UL, 3UL }, 106d);
        Assert.That(session.Participants, Is.EquivalentTo(new[] { 1UL, 2UL }));
        Assert.That(session.Acknowledge(3, 7, true, 107d), Is.False);
        session.Acknowledge(1, 7, true, 108d);
        session.Poll(new[] { 1UL, 3UL }, 109d);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Completed));
    }

    [Test] public void CancellationAndTimeoutRejectLateResults()
    {
        var cancelled = Session(1); cancelled.Cancel();
        Assert.That(cancelled.Acknowledge(1, 7, true, 106d), Is.False);
        Assert.That(cancelled.Status, Is.EqualTo(CycleAutoDialogueStatus.Failed));
        var expired = Session(1);
        Assert.That(expired.Acknowledge(1, 7, true, 136d), Is.False);
        Assert.That(expired.Status, Is.EqualTo(CycleAutoDialogueStatus.Failed));
    }

    [Test] public void EmptyOrDuplicateParticipantsAreHandledExplicitly()
    {
        Assert.That(Session().Status, Is.EqualTo(CycleAutoDialogueStatus.Failed));
        var session = Session(1, 1);
        Assert.That(session.PendingCount, Is.EqualTo(1));
        Assert.That(session.Acknowledge(1, 7, true, 106d), Is.True);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Completed));
    }

    [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
    public void InvalidClockCannotProduceAnAcknowledgement(double now)
    {
        var session = Session(1);
        Assert.That(session.Acknowledge(1, 7, true, now), Is.False);
        Assert.That(session.PendingCount, Is.EqualTo(1));
    }
}
