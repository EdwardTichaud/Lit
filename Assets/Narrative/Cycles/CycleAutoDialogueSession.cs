using System;
using System.Collections.Generic;

public enum CycleAutoDialogueStatus { Waiting, Completed, Failed }

/// <summary>
/// Transient host-owned acknowledgement barrier. No Unity, RPC or save state:
/// a timer alone can never complete a dialogue, and late joiners do not change
/// the frozen audience. At least one viewer must actually finish naturally.
/// </summary>
public sealed class CycleAutoDialogueSession
{
    private readonly HashSet<ulong> pending;
    private readonly double earliestFinish;
    private readonly double deadline;
    public int Token { get; }
    public string SourceId { get; }
    public IReadOnlyCollection<ulong> Participants { get; }
    public CycleAutoDialogueStatus Status { get; private set; }
    public int CompletedCount { get; private set; }
    public int PendingCount => pending.Count;

    public CycleAutoDialogueSession(int token, string sourceId, IEnumerable<ulong> participants,
        double startedAt, double duration, double timeout)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("A dialogue requires a stable source ID.", nameof(sourceId));
        if (double.IsNaN(startedAt) || double.IsInfinity(startedAt)) throw new ArgumentOutOfRangeException(nameof(startedAt));
        Token = token;
        SourceId = sourceId;
        pending = new HashSet<ulong>(participants ?? Array.Empty<ulong>());
        Participants = new List<ulong>(pending).AsReadOnly();
        duration = double.IsNaN(duration) || double.IsInfinity(duration) ? .1d : Math.Max(.1d, duration);
        timeout = double.IsNaN(timeout) || double.IsInfinity(timeout) ? 30d : Math.Max(1d, timeout);
        earliestFinish = startedAt + duration - .1d;
        deadline = startedAt + duration + timeout;
        Status = pending.Count > 0 ? CycleAutoDialogueStatus.Waiting : CycleAutoDialogueStatus.Failed;
    }

    public bool Acknowledge(ulong sender, int token, bool naturalCompletion, double now)
    {
        if (Status != CycleAutoDialogueStatus.Waiting || token != Token || !pending.Contains(sender) ||
            double.IsNaN(now) || double.IsInfinity(now)) return false;
        if (now >= deadline) { Cancel(); return false; }
        // Reject forged/early success without consuming a later legitimate result.
        if (naturalCompletion && now < earliestFinish) return false;
        pending.Remove(sender);
        if (!naturalCompletion) { Cancel(); return true; }
        CompletedCount++;
        if (pending.Count == 0) Status = CycleAutoDialogueStatus.Completed;
        return true;
    }

    public void Poll(IEnumerable<ulong> connected, double now)
    {
        if (Status != CycleAutoDialogueStatus.Waiting) return;
        if (double.IsNaN(now) || double.IsInfinity(now) || now >= deadline) { Cancel(); return; }
        var present = new HashSet<ulong>(connected ?? Array.Empty<ulong>());
        pending.RemoveWhere(id => !present.Contains(id));
        if (pending.Count == 0)
            Status = CompletedCount > 0 ? CycleAutoDialogueStatus.Completed : CycleAutoDialogueStatus.Failed;
    }

    public void Cancel()
    {
        if (Status == CycleAutoDialogueStatus.Waiting) Status = CycleAutoDialogueStatus.Failed;
    }
}
