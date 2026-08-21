using System;
using System.Collections.Generic;
using UnityEngine;

// Role: persist the resolved/understood state of a knowledge-driven ghost.
// Usage: attach beside GhostController on scene ghosts that must survive save/load.
// Responsibilities: capture and restore only runtime state; GhostData remains authoring data.
// Dependencies: GhostController, IPersistentStateProvider, PersistentStateJson.
// Precautions: keep ProviderId stable because it is serialized in world snapshots.
[DisallowMultipleComponent]
public class PersistentGhostState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class GhostStateData
    {
        public string GhostId;
        public bool Understood;
        public int CurrentPuzzleStepIndex;
        public bool CurrentStepQuestionPresented;
        public List<string> CompletedPuzzleStepIds;
        public List<string> ExecutedResolutionActionIds;
    }

    [SerializeField] private GhostController ghost;

    public string ProviderId => "ghost";

    private void Awake()
    {
        ResolveGhost();
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        ResolveGhost();
        if (ghost == null)
        {
            PersistentStateValidation.LogValidation(
                "ghost_knowledge_state",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' ghostMissing=true capture=true",
                this,
                context);
            return Array.Empty<byte>();
        }

        return PersistentStateJson.ToBytes(new GhostStateData
        {
            GhostId = ghost.GetPersistentGhostId(),
            Understood = ghost.IsUnderstood,
            CurrentPuzzleStepIndex = ghost.GetCurrentPuzzleStepIndex(),
            CurrentStepQuestionPresented = ghost.HasPresentedCurrentPuzzleStep(),
            CompletedPuzzleStepIds = ghost.GetCompletedPuzzleStepIds(),
            ExecutedResolutionActionIds = ghost.GetExecutedResolutionActionIds()
        });
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (phase != PersistentApplyPhase.ApplyGameplayState)
        {
            return;
        }

        ResolveGhost();
        if (ghost == null)
        {
            PersistentStateValidation.LogValidation(
                "ghost_knowledge_state",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' ghostMissing=true apply=true",
                this,
                context);
            return;
        }

        if (!PersistentStateJson.TryFromBytes(state, ProviderId, ghost, context, out GhostStateData data))
        {
            return;
        }

        ghost.RestoreUnderstoodState(data.Understood);
        ghost.RestorePuzzleProgress(
            data.CurrentPuzzleStepIndex,
            data.CurrentStepQuestionPresented,
            data.CompletedPuzzleStepIds,
            data.ExecutedResolutionActionIds);
        string actualGhostId = ghost.GetPersistentGhostId();
        bool sameGhost = string.IsNullOrWhiteSpace(data.GhostId) ||
            string.IsNullOrWhiteSpace(actualGhostId) ||
            string.Equals(data.GhostId, actualGhostId, StringComparison.Ordinal);
        PersistentStateValidation.LogValidation(
            "ghost_knowledge_state",
            sameGhost && ghost.IsUnderstood == data.Understood,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(ghost)}' ghostId='{actualGhostId}' expectedGhostId='{data.GhostId}' understood={ghost.IsUnderstood}",
            ghost,
            context);
    }

    private void ResolveGhost()
    {
        if (ghost == null)
        {
            ghost = GetComponent<GhostController>();
        }

        if (ghost == null)
        {
            ghost = GetComponentInChildren<GhostController>(true);
        }
    }
}
