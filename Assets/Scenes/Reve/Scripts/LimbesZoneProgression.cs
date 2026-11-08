using System;
using UnityEngine;

/// <summary>Scene effects derived from the existing persistent cycle service, never a second save store.</summary>
public sealed class LimbesZoneProgression : MonoBehaviour
{
    public CycleDefinition[] explorationCycles = Array.Empty<CycleDefinition>();
    public CycleDefinition conclusionCycle;
    public GameObject arenaAccess;
    public GameObject victoryExit;
    public LimbesAngelBoss boss;
    private CycleController conclusionController;
    public Lit.Story.StorySequenceAsset bossIntroduction;
    private float nextCheck;

    private void OnEnable() => Apply();
    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .2f;
        Apply();
    }
    private void Apply()
    {
        var service = CycleProgressionService.Instance;
        bool ready = service != null && service.IsReady && explorationCycles.Length >= 5;
        if (ready) foreach (var cycle in explorationCycles)
            if (cycle == null || !service.IsCompleted(cycle)) { ready = false; break; }
        bool completed = service != null && service.IsReady && conclusionCycle != null && service.IsCompleted(conclusionCycle);
        if (arenaAccess != null && arenaAccess.activeSelf != ready) arenaAccess.SetActive(ready);
        if (victoryExit != null && victoryExit.activeSelf != completed) victoryExit.SetActive(completed);
        if (boss != null)
        {
            bool introduced = bossIntroduction != null && Lit.Story.StorySequenceCompletionStore.IsCompleted(bossIntroduction);
            if (service != null && service.Authority) boss.unlocked = ready && introduced && !completed;
            if (service != null && service.Authority && boss.Actor?.Health != null && boss.Actor.Health.IsDead)
            {
                if (conclusionController == null)
                    foreach (var candidate in FindObjectsByType<CycleController>())
                        if (candidate.definition == conclusionCycle) { conclusionController = candidate; break; }
                conclusionController?.TryReportSceneEvent(CycleStepKind.NamedFact, "angel_defeat");
            }
        }
    }
}
