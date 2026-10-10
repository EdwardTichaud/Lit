using System;
using UnityEngine;

[Serializable]
public sealed class VisualActionHandoff
{
    [Tooltip("Enable only after comparing the exit poses on the actual actor.")]
    public bool validated;
    [Tooltip("Source normalized interval that contains the visual recovery. Gameplay markers remain unchanged.")]
    public Vector2 sourceExitWindow = new Vector2(.8f, 1f);
    [Range(0, .25f)] public float blendSeconds = .08f;
    [Range(0,1)] public float destinationPhase;
    [Min(0)] public float destinationCycleSeconds;
    public float RecoveryRate(AnimationClip clip, float seconds) =>
        clip != null && seconds > 0 ? Mathf.Max(.01f, (sourceExitWindow.y - sourceExitWindow.x) * clip.length / seconds) : 1;
}
