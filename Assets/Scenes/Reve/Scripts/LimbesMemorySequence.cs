using System;
using Lit.Story;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local presentation; only the registered authoritative cycle commits its named fact.</summary>
public sealed class LimbesMemorySequence : MonoBehaviour
{
    public StorySequenceRunner runner;
    public CycleDefinition cycle;
    public string completionSource = "memory";
    public CycleDefinition[] requiredCycles = Array.Empty<CycleDefinition>();
    public float radius = 4;
    [Tooltip("Core de la zone proprietaire ; conserve Reve_Limbes pour les scenes existantes.")]
    public string primarySceneName = "Reve_Limbes";
    private CycleController controller;
    private bool ownsPlayback;
    private float nextCheck;

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .2f;
        if (runner == null || runner.Sequence == null || cycle == null) return;
        var progression = CycleProgressionService.Instance;
        if (progression == null || !progression.IsReady) return;
        if (controller == null)
            foreach (var candidate in FindObjectsByType<CycleController>())
                if (candidate.definition == cycle) { controller = candidate; break; }
        if (controller == null || !controller.IsSceneEventActive(CycleStepKind.NamedFact, completionSource)) return;
        foreach (var prerequisite in requiredCycles)
            if (prerequisite == null || !progression.IsCompleted(prerequisite)) return;
        if (StorySequenceCompletionStore.IsCompleted(runner.Sequence))
        {
            if (progression.Authority) controller.TryReportSceneEvent(CycleStepKind.NamedFact, completionSource);
            ownsPlayback = false;
            return;
        }
        var player = LocalPlayerContext.LocalCharacterRoot;
        if (player == null || SceneManager.GetActiveScene().name != primarySceneName)
        {
            if (ownsPlayback) runner.Abort();
            ownsPlayback = false;
            return;
        }
        if (ownsPlayback) { if (!runner.IsPlaying) ownsPlayback = false; return; }
        if ((player.position - transform.position).sqrMagnitude > radius * radius ||
            LoadingScreenService.IsLoading || GameFlowService.IsPreparingGameplayScene ||
            GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning ||
            StorySequenceRunner.IsAnySequencePlaying || InputModeCoordinator.IsGameplayBlocked) return;
        ownsPlayback = runner.Play();
    }

    private void OnDisable()
    {
        if (ownsPlayback && runner != null) runner.Abort();
        ownsPlayback = false;
        controller = null;
    }
}
