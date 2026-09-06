using System;
using UnityEngine;

/// <summary>Editor-measured, actor-local trajectories. No source animation is needed at runtime.</summary>
[CreateAssetMenu(menuName = "Lit/Animation/Player State Motion Library")]
public sealed class PlayerStateMotionLibrary : ScriptableObject
{
    [Serializable]
    public sealed class Profile
    {
        public string statePath;
        public float duration;
        public AnimationCurve localX = AnimationCurve.Constant(0, 1, 0);
        public AnimationCurve localZ = AnimationCurve.Constant(0, 1, 0);
        public AnimationCurve yaw = AnimationCurve.Constant(0, 1, 0);
        public bool allowAirborne;
        public float initialUpwardSpeed;
        public Vector3 Position(float time) => new Vector3(localX.Evaluate(time), 0, localZ.Evaluate(time));
    }
    public Profile[] profiles = Array.Empty<Profile>();

    public Profile Find(int stateHash)
    {
        foreach (var profile in profiles)
            if (Animator.StringToHash(profile.statePath) == stateHash) return profile;
        return null;
    }
}
