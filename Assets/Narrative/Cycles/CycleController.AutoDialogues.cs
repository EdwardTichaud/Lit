using System;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// <summary>Presentation lifecycle kept separate from cycle progression and scene authoring.</summary>
public sealed partial class CycleController
{
    private CycleAutoDialogueSession autoDialogueSession;
    private int autoDialogueToken;
    private int localAutoDialogueToken;
    private int autoDialogueGeneration;
    private bool localAutoDialoguePending;
    private float nextAutoDialogueAttempt;
    private readonly object autoDialogueOwner = new object();
    private bool AutoDialogueIsBlocking => autoDialogueSession != null || localAutoDialoguePending;
    public string AutomaticDialogueStatus => autoDialogueSession != null
        ? $"{autoDialogueSession.SourceId} : {autoDialogueSession.CompletedCount}/{autoDialogueSession.Participants.Count} confirmations de fin"
        : localAutoDialoguePending ? "Réplique en attente de l'interface ou en cours"
        : Time.unscaledTime < nextAutoDialogueAttempt ? "Réplique interrompue : nouvelle tentative prévue" : "Aucune réplique automatique en cours";

    private void TryStartAutoDialogue()
    {
        if (!Authority || definition == null || localDialogue || cinematicRunning || sequencePending ||
            AutoDialogueIsBlocking || Time.unscaledTime < nextAutoDialogueAttempt || !DevSimulationActive && progression == null) return;
        foreach (var binding in autoDialogues ?? Array.Empty<CycleAutoDialogueBinding>())
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.id) || playedAutoDialogues.Contains(binding) ||
                !Evaluate(binding.condition)) continue;
            var step = definition.FindStep(binding.id);
            if (step == null || step.kind != CycleStepKind.DialogueCompleted || !IsStepActive(step)) continue;
            var audience = Online
                ? NetworkManager.Singleton.ConnectedClientsIds.Where(id => id != Unity.Netcode.NetworkManager.ServerClientId || NetworkManager.Singleton.IsHost).ToArray()
                : new[] { 0UL };
            var session = new CycleAutoDialogueSession(++autoDialogueToken, step.sourceId, audience,
                Time.realtimeSinceStartupAsDouble, binding.durationSeconds, Math.Max(30d, definition.completionPresentationTimeout));
            autoDialogueSession = session;
            playedAutoDialogues.Add(binding);
            if (Online && IsSpawned && audience.Length > 0)
                PlayAutoDialogueClientRpc(session.Token, binding.id, new ClientRpcParams
                { Send = new ClientRpcSendParams { TargetClientIds = audience } });
            else if (!Online) StartLocalAutoDialogue(session.Token, binding.id);
            StartCoroutine(WaitForAutoDialogue(session, binding));
            return;
        }
    }

    private IEnumerator WaitForAutoDialogue(CycleAutoDialogueSession session, CycleAutoDialogueBinding binding)
    {
        while (ReferenceEquals(autoDialogueSession, session) && session.Status == CycleAutoDialogueStatus.Waiting)
        {
            session.Poll(Online ? NetworkManager.Singleton.ConnectedClientsIds.ToArray() : new[] { 0UL }, Time.realtimeSinceStartupAsDouble);
            yield return null;
        }
        if (!ReferenceEquals(autoDialogueSession, session)) yield break;
        autoDialogueSession = null;
        if (Online && IsSpawned) StopAutoDialogueClientRpc(session.Token);
        else StopLocalAutoDialogue(session.Token);
        var step = definition != null ? definition.FindStep(binding.id) : null;
        bool success = Authority && session.Status == CycleAutoDialogueStatus.Completed && step != null &&
            step.sourceId == session.SourceId && Evaluate(binding.condition) && IsStepActive(step);
        if (success) Report(CycleStepKind.DialogueCompleted, session.SourceId);
        else
        {
            playedAutoDialogues.Remove(binding);
            nextAutoDialogueAttempt = Time.unscaledTime + 1f;
        }
    }

    [ClientRpc]
    private void PlayAutoDialogueClientRpc(int token, string id, ClientRpcParams targets = default) => StartLocalAutoDialogue(token, id);

    private void StartLocalAutoDialogue(int token, string id)
    {
        if (localAutoDialoguePending && token == localAutoDialogueToken) return;
        StopLocalAutoDialogue(localAutoDialogueToken);
        localAutoDialogueToken = token;
        localAutoDialoguePending = true;
        StartCoroutine(LocalAutoDialogue(token, id, ++autoDialogueGeneration));
    }

    private IEnumerator LocalAutoDialogue(int token, string id, int generation)
    {
        var binding = Array.Find(autoDialogues ?? Array.Empty<CycleAutoDialogueBinding>(), item => item != null && item.id == id);
        bool natural = false, finished = false;
        var manager = NetworkManager.Singleton;
        bool wasOnline = Online;
        double timeout = Time.realtimeSinceStartupAsDouble + Math.Max(.1d, binding != null ? binding.durationSeconds : .1d) +
            Math.Max(30d, definition != null ? definition.completionPresentationTimeout : 30d);
        try
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.line)) yield break;
            // Never replace a readable, another cycle's dialogue, or a cinematic.
            while (LocalAutoDialogueIsCurrent(generation) && Time.realtimeSinceStartupAsDouble < timeout &&
                (localDialogue || cinematicRunning || InputFocusStack.HasAnyFocus() || DialoguePanelUI.Instance != null && DialoguePanelUI.Instance.IsShowing))
                yield return null;
            if (!LocalAutoDialogueIsCurrent(generation) || Time.realtimeSinceStartupAsDouble >= timeout) yield break;
            double acknowledgeNotBefore = Time.realtimeSinceStartupAsDouble + Math.Max(.1d, binding.durationSeconds);
            bool shown = DialoguePanelUI.TryShowTimedConversation(binding.line, binding.durationSeconds,
                completed => { natural = completed; finished = true; }, autoDialogueOwner);
            if (!shown) yield break;
            while (LocalAutoDialogueIsCurrent(generation) && !finished && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            // A player may pass the line immediately. Keep the host's minimum-time
            // guard intact: an early successful acknowledgement would otherwise be
            // rejected and never resent, causing a timeout and a replay of the line.
            // Only the confirmation waits; the dismissed UI stays closed.
            while (LocalAutoDialogueIsCurrent(generation) && finished && natural &&
                Time.realtimeSinceStartupAsDouble < acknowledgeNotBefore && Time.realtimeSinceStartupAsDouble < timeout)
                yield return null;
        }
        finally
        {
            if (generation == autoDialogueGeneration)
            {
                localAutoDialoguePending = false;
                DialoguePanelUI.CancelTimedConversation(autoDialogueOwner);
                if (this != null && isActiveAndEnabled && wasOnline == Online && manager == NetworkManager.Singleton)
                    ReportAutoDialogueLocalResult(token, finished && natural);
            }
        }
    }

    private bool LocalAutoDialogueIsCurrent(int generation) => this != null && isActiveAndEnabled && generation == autoDialogueGeneration;

    private void ReportAutoDialogueLocalResult(int token, bool natural)
    {
        if (Online) { if (IsSpawned) AutoDialogueResultServerRpc(token, natural); }
        else if (Authority) autoDialogueSession?.Acknowledge(0, token, natural, Time.realtimeSinceStartupAsDouble);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void AutoDialogueResultServerRpc(int token, bool natural, RpcParams rpc = default)
    {
        if (Authority) autoDialogueSession?.Acknowledge(rpc.Receive.SenderClientId, token, natural, Time.realtimeSinceStartupAsDouble);
    }

    [ClientRpc] private void StopAutoDialogueClientRpc(int token) => StopLocalAutoDialogue(token);

    private void StopLocalAutoDialogue(int token)
    {
        if (token != localAutoDialogueToken) return;
        autoDialogueGeneration++;
        localAutoDialoguePending = false;
        DialoguePanelUI.CancelTimedConversation(autoDialogueOwner);
    }

    private void CancelAutoDialogues()
    {
        var session = autoDialogueSession;
        autoDialogueSession = null;
        session?.Cancel();
        if (Authority && Online && IsSpawned && session != null) StopAutoDialogueClientRpc(session.Token);
        StopLocalAutoDialogue(localAutoDialogueToken);
        playedAutoDialogues.Clear();
        nextAutoDialogueAttempt = 0f;
    }
}
