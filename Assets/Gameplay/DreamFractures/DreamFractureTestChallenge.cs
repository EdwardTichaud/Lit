using System;
using UnityEngine;

namespace Lit.DreamFractures
{
    public interface IDreamFractureInteraction
    {
        bool TryInteract(string stationId, Transform player);
    }

    // A small technical fixture. Future challenges implement the same run contract.
    public sealed class DreamFractureTestChallenge : MonoBehaviour, IDreamFractureChallenge, IDreamFractureInteraction
    {
        public DreamFractureTestStation[] objectives = Array.Empty<DreamFractureTestStation>();
        public DreamFractureTestStation finish;
        private DreamFractureRunController controller;
        private readonly bool[] collected = new bool[3];
        public void BeginChallenge(DreamFractureRunController run) => controller = run;
        public void ResetChallenge()
        {
            controller = null;
            Array.Clear(collected, 0, collected.Length);
        }
        public bool TryInteract(string id, Transform player)
        {
            if (controller == null || player == null) return false;
            var actor = player.GetComponentInChildren<SquadCharacterController>();
            if (actor == null || actor.CurrentHp <= 0) return false;
            for (int i = 0; i < objectives.Length && i < collected.Length; i++)
            {
                var station = objectives[i];
                if (station == null || station.stationId != id || collected[i] || !station.InRange(player)) continue;
                if (!controller.ReportScore(this, station.stationId, 100)) return false;
                collected[i] = true;
                return true;
            }
            if (finish == null || finish.stationId != id || !finish.InRange(player)) return false;
            controller.Resolve(this, true);
            return true;
        }
    }
}
