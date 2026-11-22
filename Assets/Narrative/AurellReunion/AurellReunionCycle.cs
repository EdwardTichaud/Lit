using System.Collections;
using Lit.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Solo scene presentation. Only named cycle steps persist; no local completion flags.</summary>
public sealed class AurellReunionCycle : MonoBehaviour
{
    public CycleController cycle;
    public StorySequenceRunner reunion;
    public StorySequenceSceneBindings bindings;
    public AurellReunionAnchor meeting;
    public GameObject presentation;
    public StorySequenceActor aurell;
    public CycleAutoDialogueBinding[] introduction = System.Array.Empty<CycleAutoDialogueBinding>();
    private Coroutine introductionRoutine;
    private readonly object dialogueOwner = new object();
    private bool ownsIntroduction;
    private CycleProgressionService Service => CycleProgressionService.Instance;
    public bool IntroCompleted => cycle != null && cycle.definition != null && introduction.Length > 0 &&
        Service != null && Service.IsStepCompleted(cycle.definition, introduction[introduction.Length - 1].id);

    private void OnEnable() => introductionRoutine = StartCoroutine(PresentIntroduction());
    private void OnDisable()
    {
        if (introductionRoutine != null) StopCoroutine(introductionRoutine);
        introductionRoutine = null;
        DialoguePanelUI.CancelTimedConversation(dialogueOwner);
        ownsIntroduction = false;
        if (reunion != null && reunion.IsPlaying) reunion.Abort();
    }

    private bool CanExplore()
    {
        if (!SoloTeamProgression.IsSolo || cycle == null || cycle.definition == null || Service == null || !Service.IsReady ||
            LoadingScreenService.IsLoading || GameFlowService.IsPreparingGameplayScene ||
            GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning ||
            SceneManager.GetActiveScene().name != "District_1_Core" || InputModeCoordinator.CurrentMode != InputMode.Exploration ||
            InputFocusStack.HasAnyFocus() || StorySequenceRunner.IsAnySequencePlaying ||
            RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.IsCombatActive) return false;
        var root = LocalPlayerContext.LocalCharacterRoot;
        var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
        return actor != null && actor.isActiveAndEnabled && actor.CurrentHp > 0 && actor.CharacterData != null &&
            MaisonCompanionReunion.IsLucian(actor.CharacterData.characterId, actor.CharacterData.characterName);
    }

    private void Update()
    {
        if (presentation == null) return;
        bool visible = SoloTeamProgression.IsSolo && meeting != null && meeting.placementConfirmed &&
            Service != null && cycle != null && !Service.IsCompleted(cycle.definition);
        if (presentation.activeSelf != visible) presentation.SetActive(visible);

    }

    private IEnumerator PresentIntroduction()
    {
        // Scene activation alone is not an arrival: wait for GameFlow's successful gameplay handoff.
        while (!CanExplore()) yield return null;
        Service.ApplyEvent(cycle.definition, CycleStepKind.ZoneEntered, CompanionReunionState.ArrivalStep);
        foreach (var line in introduction)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.id)) continue;
            while (Service == null || !Service.IsStepCompleted(cycle.definition, line.id))
            {
                if (!CanExplore() || DialoguePanelUI.Instance != null && DialoguePanelUI.Instance.IsShowing)
                { yield return null; continue; }
                bool finished = false, natural = false;
                ownsIntroduction = DialoguePanelUI.TryShowTimedConversation(line.line, line.durationSeconds,
                    completed => { natural = completed; finished = true; }, dialogueOwner, line.speakerCharacterData, false);
                if (!ownsIntroduction) { yield return new WaitForSecondsRealtime(.25f); continue; }
                using (var speaking = new DialoguePresentationSession(this, LocalPlayerContext.LocalCharacterRoot, null, false, null))
                {
                    while (!finished && CanExplore()) yield return null;
                }
                if (!finished) DialoguePanelUI.CancelTimedConversation(dialogueOwner);
                ownsIntroduction = false;
                if (finished && natural)
                    Service.ApplyEvent(cycle.definition, CycleStepKind.NamedFact, line.id);
                yield return null;
            }
        }
        introductionRoutine = null;
    }

    public bool CanMeet => meeting != null && meeting.placementConfirmed && presentation != null && presentation.activeInHierarchy &&
        !ownsIntroduction && IntroCompleted && CanExplore() &&
        !(DialoguePanelUI.Instance != null && DialoguePanelUI.Instance.IsShowing) &&
        !Service.IsCompleted(cycle.definition);

    public bool TryMeet()
    {
        if (!CanMeet || reunion == null || bindings == null) return false;
        var root = LocalPlayerContext.LocalCharacterRoot;
        if (!CharacterInteractionDetection.IsCharacterWithinRange(root, meeting.GetInteractionDetectionCollider(), meeting.transform, meeting.interactionDistance)) return false;
        var player = root.GetComponent<StorySequenceActor>();
        if (player == null || aurell == null) return false;
        bindings.BindRuntimeActor("Lucian", player);
        bindings.BindRuntimeActor("Aurell", aurell);
        return reunion.Play();
    }

    // Bound only to the runner's successful completion, after camera/input restoration.
    public void CompleteReunion()
    {
        if (!SoloTeamProgression.IsSolo || Service == null || !IntroCompleted || cycle == null) return;
        Service.ApplyEvent(cycle.definition, CycleStepKind.SequenceCompleted, CompanionReunionState.ReunionStep);
    }
}
