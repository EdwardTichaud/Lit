using UnityEngine;

[CreateAssetMenu(menuName = "Symphonie/Animation Trigger")]
public class AnimationTriggerSO : ScriptableObject
{
    [Header("Target Animator ID (name)")]
    public string animatorID;

    [Header("Animation to play")]
    public string animationName;

    [Header("Optional CrossFade")]
    public float crossFadeDuration = 0f;
}
