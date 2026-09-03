using System;
using System.Collections.Generic;
using UnityEngine;

public enum ThresholdSequenceFailureResult
{
    ResumeCombat,
    EnemySkill
}

/// <summary>One authored QTE clip beat in a health-threshold sequence. A clip can carry several QTE events.</summary>
[Serializable]
public sealed class ThresholdSequenceStep
{
    [Header("QTE Presentation")]
    [Tooltip("Clip injecte dans ThresholdSequence_QTE. Il doit contenir un ou plusieurs Animation Events QTE(string), joues dans leur ordre auteur.")]
    public AnimationClip qtePresentationClip;
    [Range(0f, 0.25f)] public float playerQteEntryBlendSeconds = 0.08f;
    [Min(0.01f), Tooltip("Duree reelle de la fenetre de saisie.")]
    public float qteDurationSeconds = 0.5f;
    [Range(0.01f, 1f), Tooltip("Ralentissement global applique uniquement pendant la saisie.")]
    public float qteGlobalTimeScale = 0.4f;
    [Min(0.1f), Tooltip("Temps reel maximal avant l'Animation Event QTE attendue.")]
    public float qteEventTimeoutSeconds = 3f;

    [Header("Success")]
    [Tooltip("Clip injecte dans Threshold_Succes apres la reussite.")]
    public AnimationClip successPlayerAnimationClip;
    [Range(0f, 0.25f)] public float successEntryBlendSeconds = 0.08f;
    [Range(0.02f, 0.35f)] public float successExitBlendSeconds = 0.16f;
    [Min(0f), Tooltip("Delai depuis le debut du clip avant la resolution du dernier step.")]
    public float successResolutionDelaySeconds = 0.5f;
    [Tooltip("Applique uniquement par le dernier step de la sequence.")]
    public CombatHealthThresholdSuccessResult successResult = CombatHealthThresholdSuccessResult.ResumeCombat;

    [Header("Failure")]
    [Range(0.02f, 0.25f), Tooltip("Fondu vers CombatIdle si ce step echoue.")]
    public float failureIdleBlendSeconds = 0.10f;
    public ThresholdSequenceFailureResult failureResult = ThresholdSequenceFailureResult.ResumeCombat;
    [Tooltip("Skill ennemi joue seulement lorsque Failure Result vaut Enemy Skill.")]
    public SkillSO failureRetaliationSkill;
    [Min(0.25f), Tooltip("Marge temps reel apres le clip de riposte avant recuperation forcee.")]
    public float failureRetaliationGraceSeconds = 1.5f;

    public bool TryGetValidationIssue(out string issue)
    {
        if (qtePresentationClip == null) { issue = "clip QTEPresentation absent"; return false; }
        if (successPlayerAnimationClip == null) { issue = "clip de succes absent"; return false; }
        if (qteDurationSeconds < 0.01f) { issue = "duree QTE inferieure a 0,01 s"; return false; }
        if (qteGlobalTimeScale < 0.01f || qteGlobalTimeScale > 1f) { issue = "ralentissement QTE hors de [0,01; 1]"; return false; }
        if (qteEventTimeoutSeconds < 0.1f) { issue = "watchdog QTE inferieur a 0,1 s"; return false; }
        if (successResolutionDelaySeconds < 0f) { issue = "delai de succes negatif"; return false; }
        if (failureResult == ThresholdSequenceFailureResult.EnemySkill && failureRetaliationSkill == null)
        {
            issue = "SkillSO de riposte requis lorsque Failure Result vaut Enemy Skill";
            return false;
        }

        AnimationEvent[] events = qtePresentationClip.events;
        int qteCount = 0;
        for (int index = 0; index < events.Length; index++)
        {
            AnimationEvent animationEvent = events[index];
            if (animationEvent.functionName != "QTE") continue;
            qteCount++;
            string input = (animationEvent.stringParameter ?? string.Empty).Trim().ToUpperInvariant();
            if (input != "Y" && input != "B" && input != "A" && input != "X")
            {
                issue = "Animation Event QTE invalide ('" + animationEvent.stringParameter + "')";
                return false;
            }
        }

        if (qteCount == 0)
        {
            issue = "le clip QTEPresentation doit contenir au moins un Animation Event QTE(string)";
            return false;
        }

        issue = null;
        return true;
    }
}

/// <summary>Complete authored response for one enemy health threshold.</summary>
[CreateAssetMenu(fileName = "ThresholdSequence", menuName = "Scriptable Objects/Combat/Threshold Sequence")]
public sealed class ThresholdSequence : ScriptableObject
{
    public const string DefaultQteAnimatorState = "ThresholdSequence_QTE";
    public const string DefaultSuccessAnimatorState = "Threshold_Succes";

    [Tooltip("Chaque succes intermediaire enchaine automatiquement le step suivant. Le resultat du dernier step seul resout le palier.")]
    public List<ThresholdSequenceStep> steps = new List<ThresholdSequenceStep>();

    public string PlayerQteStateName => DefaultQteAnimatorState;
    public string SuccessPlayerStateName => DefaultSuccessAnimatorState;
    public int StepCount => steps != null ? steps.Count : 0;
    public int TotalQteCount
    {
        get
        {
            if (steps == null) return 0;

            int total = 0;
            for (int index = 0; index < steps.Count; index++)
            {
                total += CountQteEvents(steps[index] != null ? steps[index].qtePresentationClip : null);
            }

            return total;
        }
    }
    public bool HasValidRuntimeStates => !string.IsNullOrWhiteSpace(PlayerQteStateName) && !string.IsNullOrWhiteSpace(SuccessPlayerStateName);
    public bool IsComplete => HasValidRuntimeStates && StepCount > 0 && TryGetStepValidationIssue(out _);

    public bool TryGetStepValidationIssue(out string issue)
    {
        if (StepCount == 0) { issue = "ajoutez au moins un step"; return false; }
        for (int index = 0; index < StepCount; index++)
        {
            ThresholdSequenceStep step = steps[index];
            if (step != null && step.TryGetValidationIssue(out _)) continue;
            issue = "Etape " + (index + 1) + " : " + (step == null ? "absente" : GetStepIssue(step));
            return false;
        }
        issue = null;
        return true;
    }

    private static string GetStepIssue(ThresholdSequenceStep step)
    {
        step.TryGetValidationIssue(out string issue);
        return issue;
    }

    public static int CountQteEvents(AnimationClip clip)
    {
        if (clip == null) return 0;

        int count = 0;
        AnimationEvent[] events = clip.events;
        for (int index = 0; index < events.Length; index++)
        {
            if (events[index].functionName == "QTE") count++;
        }

        return count;
    }
}
