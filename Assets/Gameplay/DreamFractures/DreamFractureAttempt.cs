using System;
using System.Collections.Generic;

namespace Lit.DreamFractures
{
    public enum DreamFracturePhase { Idle, Preparing, Loading, Countdown, Running, Results, Returning }
    public enum DreamFractureOutcome { Success, Failed, Abandoned }

    public interface IDreamFractureChallenge
    {
        void BeginChallenge(DreamFractureRunController controller);
        void ResetChallenge();
    }

    [Serializable]
    public sealed class DreamFractureResult
    {
        public string runId, fractureId, completedUtc;
        public int rulesVersion, initialPlayers, score, targetScore;
        public double seconds, targetSeconds;
        public bool scoreReached, timeReached, hadDisconnect;
        public DreamFractureOutcome outcome;
    }

    // Host-owned rules. No Unity clock, scene or network dependency.
    public sealed class DreamFractureAttempt
    {
        private readonly HashSet<string> events = new HashSet<string>(StringComparer.Ordinal);
        public int Score { get; private set; }
        public double Seconds { get; private set; }
        public DreamFractureResult Result { get; private set; }

        public bool AddScore(string eventId, int points)
        {
            if (Result != null || string.IsNullOrWhiteSpace(eventId) || points < 0 ||
                points > int.MaxValue - Score || !events.Add(eventId)) return false;
            Score += points;
            return true;
        }

        public void Tick(double delta)
        {
            if (Result == null && delta > 0 && !double.IsNaN(delta) && !double.IsInfinity(delta))
                Seconds += delta;
        }

        public DreamFractureResult Finish(string runId, DreamFractureDefinitionSO definition,
            int players, bool disconnected, DreamFractureOutcome outcome)
        {
            if (Result != null) return Result;
            var targets = definition.TargetsFor(players);
            Result = new DreamFractureResult
            {
                runId = runId, fractureId = definition.id, rulesVersion = definition.rulesVersion,
                initialPlayers = players, score = Score, seconds = Seconds,
                targetScore = targets.score, targetSeconds = targets.seconds,
                scoreReached = Score >= targets.score, timeReached = Seconds <= targets.seconds,
                hadDisconnect = disconnected, outcome = outcome,
                completedUtc = DateTime.UtcNow.ToString("O")
            };
            return Result;
        }
    }
}
