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
    [Header("QTE Presentation")]
    [Tooltip("Clip QTE de Lucian. L'etat Animator du meme nom est resolu automatiquement et doit utiliser ce clip.")]
    public AnimationClip playerQteAnimationClip;
    [Tooltip("Fallback lorsque le nom de l'etat Animator differe du nom du clip QTE.")]
    public string playerQteAnimatorState;
    [Range(0f, 0.25f)] public float playerQteEntryBlendSeconds = 0.08f;

    [Tooltip("Fondu utilise lorsque l'un des QTE echoue avant la fin de la sequence.")]
    [Range(0.02f, 0.25f)] public float failureIdleBlendSeconds = 0.10f;

    [Header("Success")]
    [Tooltip("Clip de reussite de Lucian. L'etat Animator du meme nom est resolu automatiquement et doit utiliser ce clip.")]
    public AnimationClip successPlayerAnimationClip;
    [Tooltip("Fallback lorsque le nom de l'etat Animator differe du nom du clip de reussite.")]
    public string successPlayerAnimatorState;
    [Range(0f, 0.25f)] public float successEntryBlendSeconds = 0.08f;
    [Min(0f), Tooltip("Delai depuis le debut de l'animation de reussite avant de resoudre le palier.")]
    public float successResolutionDelaySeconds = 0.5f;
    public CombatHealthThresholdSuccessResult successResult = CombatHealthThresholdSuccessResult.ResumeCombat;

    [Header("Failure")]
    public ThresholdSequenceFailureResult failureResult = ThresholdSequenceFailureResult.ResumeCombat;
    [Tooltip("Skill ennemi joue seulement lorsque Failure Result vaut Enemy Skill.")]
    public SkillSO failureRetaliationSkill;

    public string PlayerQteStateName => playerQteAnimationClip != null
        ? playerQteAnimationClip.name
        : playerQteAnimatorState;

    public string SuccessPlayerStateName => successPlayerAnimationClip != null
        ? successPlayerAnimationClip.name
        : successPlayerAnimatorState;

    public bool HasValidRuntimeStates =>
        !string.IsNullOrWhiteSpace(PlayerQteStateName) &&
        !string.IsNullOrWhiteSpace(SuccessPlayerStateName);

    public bool IsComplete => HasValidRuntimeStates &&
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
