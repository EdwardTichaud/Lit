using UnityEngine;

public enum ThresholdSequenceFailureResult
{
    ResumeCombat,
    EnemySkill
}

/// <summary>
/// Complete authored response for one enemy health threshold. Player states
/// are cosmetic and must be authored in place: the threshold controller owns
/// actor placement for the entire sequence.
/// </summary>
[CreateAssetMenu(fileName = "ThresholdSequence", menuName = "Scriptable Objects/Combat/Threshold Sequence")]
public sealed class ThresholdSequence : ScriptableObject
{
    public const string DefaultQteAnimatorState = "ThresholdSequence_QTE";
    public const string DefaultSuccessAnimatorState = "Threshold_Succes";

    [Header("QTE Presentation")]
    [Tooltip("Clip QTE injecte temporairement dans l'etat generique ThresholdSequence_QTE. Laissez vide pour utiliser son clip par defaut.")]
    public AnimationClip playerQteAnimationClip;
    [HideInInspector]
    public string playerQteAnimatorState;
    [Range(0f, 0.25f)] public float playerQteEntryBlendSeconds = 0.08f;

    [Tooltip("Fondu utilise lorsque l'un des QTE echoue avant la fin de la sequence.")]
    [Range(0.02f, 0.25f)] public float failureIdleBlendSeconds = 0.10f;

    [Header("Success")]
    [Tooltip("Clip de reussite injecte temporairement dans l'etat generique Threshold_Succes.")]
    public AnimationClip successPlayerAnimationClip;
    [HideInInspector]
    public string successPlayerAnimatorState;
    [Range(0f, 0.25f)] public float successEntryBlendSeconds = 0.08f;
    [Tooltip("Fondu vers CombatIdle une fois le clip de succes termine, avant la restitution du combat.")]
    [Range(0.02f, 0.35f)] public float successExitBlendSeconds = 0.16f;
    [Min(0f), Tooltip("Delai depuis le debut de l'animation de reussite avant de resoudre le palier.")]
    public float successResolutionDelaySeconds = 0.5f;
    public CombatHealthThresholdSuccessResult successResult = CombatHealthThresholdSuccessResult.ResumeCombat;

    [Header("Failure")]
    public ThresholdSequenceFailureResult failureResult = ThresholdSequenceFailureResult.ResumeCombat;
    [Tooltip("Skill ennemi joue seulement lorsque Failure Result vaut Enemy Skill.")]
    public SkillSO failureRetaliationSkill;

    public string PlayerQteStateName => DefaultQteAnimatorState;

    public string SuccessPlayerStateName => DefaultSuccessAnimatorState;

    public bool HasValidRuntimeStates =>
        !string.IsNullOrWhiteSpace(PlayerQteStateName) &&
        !string.IsNullOrWhiteSpace(SuccessPlayerStateName);

    public bool IsComplete => HasValidRuntimeStates && successPlayerAnimationClip != null &&
                              (failureResult != ThresholdSequenceFailureResult.EnemySkill || failureRetaliationSkill != null);

    /// <summary>
    /// The authored clip is the source of truth for the QTE chain. A state-only
    /// legacy sequence remains supported as one QTE, but assigning the clip is
    /// required to author more than one event deterministically.
    /// </summary>
    public int AuthoredQteCount
    {
        get
        {
            if (playerQteAnimationClip == null) return 1;

            AnimationEvent[] events = playerQteAnimationClip.events;
            int count = 0;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].functionName == "QTE") count++;
            }

            return count;
        }
    }
}
