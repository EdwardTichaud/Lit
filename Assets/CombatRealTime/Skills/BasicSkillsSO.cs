using UnityEngine;

public enum BasicSkillContext
{
    Grounded,
    Airborne
}

public enum AirborneBasicSkillLandingMode
{
    StayAirborne,
    LandOnApproach
}

[CreateAssetMenu(fileName = "BasicSkillSO", menuName = "Scriptable Objects/Combat/Basic Skill SO")]
public sealed class BasicSkillsSO : SkillSO
{
    [Header("Basic Skill Context")]
    [Tooltip("La famille de combo dans laquelle ce BasicSkill peut etre equipe.")]
    [SerializeField] private BasicSkillContext context = BasicSkillContext.Grounded;
    [Header("Airborne Landing")]
    [Tooltip("StayAirborne suspend la gravite UCC jusqu'a la fin du BasicSkill. LandOnApproach restitue la gravite et utilise le contrat d'approche/contact commun.")]
    [SerializeField] private AirborneBasicSkillLandingMode airborneLandingMode = AirborneBasicSkillLandingMode.StayAirborne;
    [SerializeField, Tooltip("Contrat physique/Animator de l'atterrissage d'une BasicSkill aerienne.")]
    private MotionHandoffProfile airborneLandingHandoff = new MotionHandoffProfile {
        minimumContactSeconds = 0.15f,
        animationExitNormalizedTime = 0.82f,
        planarSettledSpeed = 0.14f,
        verticalSettledSpeed = 0.2f,
        planarDampingPerSecond = 7f,
        maximumSettleSeconds = 0.55f,
        locomotionBlendSeconds = 0.08f,
        preLandingProbeDistance = 1.2f,
        preLandingLeadSeconds = 0.14f
    };

    public BasicSkillContext Context => context;
    public bool RequestsLandingDuringAnimation =>
        context == BasicSkillContext.Airborne &&
        airborneLandingMode == AirborneBasicSkillLandingMode.LandOnApproach;
    public bool HoldsAirborneDuringAnimation =>
        context == BasicSkillContext.Airborne &&
        airborneLandingMode == AirborneBasicSkillLandingMode.StayAirborne;
    public MotionHandoffProfile AirborneLandingHandoff => airborneLandingHandoff;
}
