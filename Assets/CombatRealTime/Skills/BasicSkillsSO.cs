using UnityEngine;

public enum BasicSkillContext
{
    Grounded,
    Airborne
}

public enum AirborneBasicSkillLandingMode
{
    StayAirborne,
    LandAtAnimationTime
}

[CreateAssetMenu(fileName = "BasicSkillSO", menuName = "Scriptable Objects/Combat/Basic Skill SO")]
public sealed class BasicSkillsSO : SkillSO
{
    [Header("Basic Skill Context")]
    [Tooltip("La famille de combo dans laquelle ce BasicSkill peut etre equipe.")]
    [SerializeField] private BasicSkillContext context = BasicSkillContext.Grounded;
    [Header("Airborne Landing")]
    [Tooltip("StayAirborne suspend la gravite UCC jusqu'a la fin du BasicSkill. LandAtAnimationTime demande une descente physique au temps configure.")]
    [SerializeField] private AirborneBasicSkillLandingMode airborneLandingMode = AirborneBasicSkillLandingMode.StayAirborne;
    [SerializeField, Min(0f), Tooltip("Seconde du clip a laquelle la descente vers le sol commence. Utilise uniquement pour un BasicSkill Airborne configure sur LandAtAnimationTime.")]
    private float landingAtAnimationSeconds;

    public BasicSkillContext Context => context;
    public bool RequestsLandingDuringAnimation =>
        context == BasicSkillContext.Airborne &&
        airborneLandingMode == AirborneBasicSkillLandingMode.LandAtAnimationTime;
    public bool HoldsAirborneDuringAnimation =>
        context == BasicSkillContext.Airborne &&
        airborneLandingMode == AirborneBasicSkillLandingMode.StayAirborne;
    public float LandingAtAnimationSeconds => landingAtAnimationSeconds;
}
