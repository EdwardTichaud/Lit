using System;
using UnityEngine;

/// <summary>
/// Shared physical/presentation contract for returning an actor to locomotion.
/// UCC remains the only owner of position and vertical motion; this profile
/// merely defines when the presentation is allowed to hand control back.
/// </summary>
[Serializable]
public sealed class MotionHandoffProfile
{
    [Min(0f)] public float minimumContactSeconds = 0.15f;
    [Range(0f, 1f)] public float animationExitNormalizedTime = 0.82f;
    [Min(0f)] public float planarSettledSpeed = 0.12f;
    [Min(0f)] public float verticalSettledSpeed = 0.2f;
    [Min(0f)] public float planarDampingPerSecond = 7f;
    [Min(0f)] public float maximumSettleSeconds = 0.55f;
    [Range(0f, 0.25f)] public float locomotionBlendSeconds = 0.08f;
    [Min(0.01f)] public float preLandingProbeDistance = 1.2f;
    [Range(0f, 0.5f)] public float preLandingLeadSeconds = 0.14f;

    public static MotionHandoffProfile CreateActionDefault()
    {
        return new MotionHandoffProfile {
            minimumContactSeconds = 0f,
            animationExitNormalizedTime = 0.82f,
            planarSettledSpeed = 0.18f,
            verticalSettledSpeed = 0.25f,
            planarDampingPerSecond = 0f,
            maximumSettleSeconds = 0.2f,
            locomotionBlendSeconds = 0.08f
        };
    }
}
