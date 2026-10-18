using UnityEngine;

[CreateAssetMenu(fileName = "ChargedSkill", menuName = "Scriptable Objects/Combat/Charged Skill")]
public sealed class ChargedSkillSO : SkillSO
{
    public AnimationClip preparationClip;
    public string preparationState = "Base Layer.ChargedPreparation";
    [Min(.1f)] public float chargeSeconds = 2f;
    [Header("Charge Camera")]
    [Range(0f, 30f)] public float chargeCameraFovReduction = 10f;
    [Min(0f)] public float chargeCameraBehindOffset = .6f;
    [Min(.01f)] public float chargeCameraBlendSeconds = .25f;
    [Header("Airborne Presentation")]
    public AnimationClip playerFloatClip;
    public string playerFloatState = "Base Layer.ChargedFloat";
    public AnimationClip enemyFloatClip;
    public string enemyFloatState = "Base Layer.ChargedFloatHit";
    [Min(.1f)] public float launchHeight = 3f;
    [Min(.1f)] public float ascentSeconds = .55f;
    [Min(0)] public float airborneHoldSeconds = 1.2f;
    [Min(.1f)] public float maximumVerticalHitDistance = 1.5f;
}
