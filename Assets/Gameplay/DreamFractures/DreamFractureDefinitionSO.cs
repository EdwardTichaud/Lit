using System;
using UnityEngine;

namespace Lit.DreamFractures
{
    [Serializable]
    public sealed class DreamFractureTargets
    {
        [Min(0)] public int score = 300;
        [Min(1)] public float seconds = 60;
    }

    [CreateAssetMenu(menuName = "Lit/Fractures oniriques/Défi")]
    public sealed class DreamFractureDefinitionSO : ScriptableObject
    {
        public string id;
        [Min(1)] public int rulesVersion = 1;
        public string displayName;
        [TextArea] public string description;
        public string challengeType;
        public ZoneManifest zone;
        public bool available = true;
        public string unavailableReason;
        public bool developmentOnly;
        public DreamFractureTargets[] targets =
        {
            new DreamFractureTargets(), new DreamFractureTargets(),
            new DreamFractureTargets(), new DreamFractureTargets()
        };

        public bool IsVisible => !developmentOnly || Application.isEditor || Debug.isDebugBuild;
        public bool IsValid => !string.IsNullOrWhiteSpace(id) && rulesVersion > 0 &&
            zone != null && zone.IsValid && targets != null && targets.Length == 4 &&
            Array.TrueForAll(targets, t => t != null && t.score >= 0 &&
                t.seconds > 0 && !float.IsNaN(t.seconds) && !float.IsInfinity(t.seconds));
        public DreamFractureTargets TargetsFor(int players) => targets[Mathf.Clamp(players, 1, 4) - 1];
    }

}
