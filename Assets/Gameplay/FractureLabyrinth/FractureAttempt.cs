using System;

namespace Lit.Fractures
{
    /// <summary>Two-puzzle fixture: clues survive an in-Play retry; door and input do not.</summary>
    public sealed class FractureArchivePuzzle
    {
        private readonly bool[] clues = new bool[3];
        private readonly int[] input = new int[3];
        public bool DoorOpen { get; private set; }
        public int FoundCount => (clues[0] ? 1 : 0) + (clues[1] ? 1 : 0) + (clues[2] ? 1 : 0);
        public static int SymbolForNumber(int index) => index >= 0 && index < 3 ? (index + 1) % 3 : -1;
        public bool HasClue(int index) => index >= 0 && index < 3 && clues[index];
        public bool Reveal(int index)
        {
            if (index < 0 || index >= 3 || clues[index]) return false;
            clues[index] = true;
            return true;
        }
        public int GetInput(int slot) => slot >= 0 && slot < 3 ? input[slot] : -1;
        public bool Cycle(int slot)
        {
            if (slot < 0 || slot >= 3 || DoorOpen) return false;
            input[slot] = (input[slot] + 1) % 3;
            return true;
        }
        public bool TryOpen()
        {
            if (DoorOpen || FoundCount != 3) return false;
            for (int i = 0; i < 3; i++) if (input[i] != SymbolForNumber(i)) return false;
            DoorOpen = true;
            return true;
        }
        public void ResetAttempt() { Array.Clear(input, 0, input.Length); DoorOpen = false; }
        public void Restore(int foundMask, int code, bool open)
        {
            for (int i = 0; i < 3; i++) { clues[i] = (foundMask & (1 << i)) != 0; input[i] = code % 3; code /= 3; }
            DoorOpen = open && FoundCount == 3 && input[0] == SymbolForNumber(0) && input[1] == SymbolForNumber(1) && input[2] == SymbolForNumber(2);
        }
    }

    public enum FracturePhase : byte { Ready, Exploring, Fighting, Won, Lost, Abandoned }
    [Flags] public enum FracturePreparation : byte { None = 0, Protection = 1, Reinforcements = 2 }

    /// <summary>Authoritative attempt state, independent of presentation and campaign persistence.</summary>
    [Serializable]
    public sealed class FractureAttempt
    {
        public int Revision { get; private set; }
        public int AttemptNumber { get; private set; }
        public int ParticipantCount { get; private set; }
        public bool Discovery { get; private set; }
        public FracturePhase Phase { get; private set; }
        public double Elapsed { get; private set; }
        public double Darkness { get; private set; }
        public int PowerAtEngagement { get; private set; }
        public bool HasPart { get; private set; }
        public bool Repaired { get; private set; }
        public bool ThresholdOpen { get; private set; }
        public bool GalleryShortcut { get; private set; }
        public bool NetworkShortcut { get; private set; }
        public FracturePreparation Preparations { get; private set; }
        public bool RewardRecorded { get; private set; }
        public bool IsRunning => Phase == FracturePhase.Exploring || Phase == FracturePhase.Fighting;
        public bool CanReform => Phase == FracturePhase.Won || Phase == FracturePhase.Lost || Phase == FracturePhase.Abandoned;

        public bool Start(int participants, bool discovery)
        {
            if (Phase != FracturePhase.Ready || participants < 1 || participants > 4) return false;
            AttemptNumber++;
            ParticipantCount = participants;
            Discovery = discovery;
            Phase = FracturePhase.Exploring;
            Changed();
            return true;
        }

        public void Tick(double seconds)
        {
            if (!IsRunning || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
            Elapsed += seconds;
            if (Phase == FracturePhase.Exploring) Darkness += seconds;
            Changed();
        }

        public int GetPower(double secondsPerTier, int maximumTier)
        {
            if (Phase == FracturePhase.Fighting || Phase == FracturePhase.Won) return PowerAtEngagement;
            if (maximumTier <= 0) return 0;
            if (double.IsNaN(secondsPerTier) || double.IsInfinity(secondsPerTier) || secondsPerTier <= 0) return 0;
            return (int)Math.Min(maximumTier, Math.Floor(Darkness / secondsPerTier));
        }

        public bool CollectPart(bool galleryEncounterDefeated)
        {
            if (Phase != FracturePhase.Exploring || HasPart || !galleryEncounterDefeated) return false;
            HasPart = GalleryShortcut = true;
            Changed();
            return true;
        }

        public bool Repair()
        {
            if (Phase != FracturePhase.Exploring || !HasPart || Repaired) return false;
            Repaired = true;
            Changed();
            return true;
        }

        public bool OpenThreshold(bool knowsNetwork, bool correctLocalRouting)
        {
            if (Phase != FracturePhase.Exploring || !Repaired || ThresholdOpen || !knowsNetwork || !correctLocalRouting) return false;
            ThresholdOpen = true;
            Changed();
            return true;
        }

        public bool OpenNetworkShortcut(bool knowsNetwork, bool correctLocalRouting)
        {
            if (Phase != FracturePhase.Exploring || NetworkShortcut || !knowsNetwork || !correctLocalRouting) return false;
            NetworkShortcut = true;
            Changed();
            return true;
        }

        public bool Prepare(FracturePreparation preparation, bool encounterDefeated, bool localMechanismSolved)
        {
            if (Phase != FracturePhase.Exploring || !encounterDefeated || !localMechanismSolved ||
                (preparation != FracturePreparation.Protection && preparation != FracturePreparation.Reinforcements) ||
                (Preparations & preparation) != 0) return false;
            Preparations |= preparation;
            Changed();
            return true;
        }

        public bool Engage(bool teamApproved, bool flameActive, double secondsPerTier, int maximumTier)
        {
            if (Phase != FracturePhase.Exploring || !ThresholdOpen || !teamApproved || !flameActive) return false;
            PowerAtEngagement = GetPower(secondsPerTier, maximumTier);
            Phase = FracturePhase.Fighting;
            Changed();
            return true;
        }

        public bool Win(bool bossDefeated)
        {
            if (Phase != FracturePhase.Fighting || !bossDefeated) return false;
            Phase = FracturePhase.Won;
            Changed();
            return true;
        }

        // Records a test-ledger result only. Campaign rewards require their own
        // transactional adapter; do not treat this flag as an item/XP delivery.
        public bool RecordReward()
        {
            if (Phase != FracturePhase.Won || RewardRecorded) return false;
            RewardRecorded = true;
            Changed();
            return true;
        }

        public bool End(FracturePhase outcome)
        {
            if (!IsRunning || outcome != FracturePhase.Lost && outcome != FracturePhase.Abandoned) return false;
            Phase = outcome;
            Changed();
            return true;
        }

        public bool Reform()
        {
            if (!CanReform) return false;
            Phase = FracturePhase.Ready;
            ParticipantCount = 0;
            Discovery = false;
            Elapsed = Darkness = 0;
            PowerAtEngagement = 0;
            HasPart = Repaired = ThresholdOpen = GalleryShortcut = NetworkShortcut = RewardRecorded = false;
            Preparations = FracturePreparation.None;
            Changed();
            return true;
        }

        private void Changed() { unchecked { Revision++; } }
    }
}
