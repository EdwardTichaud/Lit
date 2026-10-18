using System.Collections;
using Lit.Combat.Migration.Network;
using Unity.Netcode;
using UnityEngine;

public sealed partial class CombatHealthThresholdController
{
    private CombatNetworkActorRuntime networkThresholdActor;
    private CombatNetworkQteSession networkThresholdSession;
    private int networkPresentationGeneration;
    private bool IsNetworkThresholdEngine => GetComponent<RealTimeCombatManager>() == null &&
        (GetComponent<SquadCharacterController>() != null || GetComponent<CharacterInfo>() != null ||
         GetComponent<CombatNetworkActorRuntime>() != null);
    private System.Collections.Generic.Dictionary<Transform, CombatHealthThresholdStage> ThresholdRetryStages =>
        IsNetworkThresholdEngine && Instance != null ? Instance.retryEligibleStages : retryEligibleStages;
    private bool NetworkThresholdActive => networkThresholdSession != null;
    private static bool NetworkThresholdListening => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private Animator ThresholdAnimator => thresholdPlayer != null
        ? thresholdPlayer.GetComponent<CharacterAnimationController>()?.Animator ?? thresholdPlayer.GetComponentInChildren<Animator>()
        : null;

    private static CombatHealthThresholdController FindNetworkThresholdEngine(Component enemy)
    {
        if (!NetworkThresholdListening || enemy == null) return null;
        foreach (var controller in FindObjectsByType<CombatHealthThresholdController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (controller.IsNetworkThresholdEngine && controller.state != SequenceState.Idle &&
                controller.activeEnemy != null && controller.activeEnemy.Matches(enemy)) return controller;
        return null;
    }

    // Presentation routes its accepted event to its own participant, never the host's singleton.
    public static CombatHealthThresholdController ForParticipant(Component participant) =>
        NetworkThresholdListening ? participant != null ? participant.GetComponent<CombatHealthThresholdController>() : null : Instance;

    private bool CanExecuteNetworkThresholdParticipant(SquadCharacterController source, out string issue)
    {
        if (!IsNetworkThresholdEngine)
        {
            issue = "Source actor is missing its authored CombatHealthThresholdController; no host substitution is permitted.";
            return false;
        }
        var network = NetworkManager.Singleton;
        var runtime = source != null ? source.GetComponent<CombatNetworkActorRuntime>() : null;
        if (!IsNetworkThresholdEngine || source == null || source.gameObject != gameObject || network == null || !network.IsServer || network.ShutdownInProgress || runtime == null ||
            !runtime.IsAssignedPlayer || source.CurrentHp <= 0 || !source.isActiveAndEnabled)
        {
            issue = "Threshold requires the living, assigned server damage-source actor.";
            return false;
        }
        var animator = source.GetComponent<CharacterAnimationController>()?.Animator ?? source.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
            !animator.HasState(0, Animator.StringToHash(ThresholdSequence.DefaultQteAnimatorState)) ||
            !animator.HasState(0, Animator.StringToHash(ThresholdSequence.DefaultSuccessAnimatorState)) ||
            source.GetComponent<PlayerActionPresentationController>() == null || source.GetComponent<LitOpsiveLocomotionBridge>() == null)
        {
            issue = "Threshold participant has no usable authored Animator states/presentation/UCC bridge; damage remains unclamped.";
            return false;
        }
        if (networkThresholdSession == null &&
            (FindControllerClip(animator.runtimeAnimatorController, ThresholdSequence.DefaultQteAnimatorState) == null ||
             FindControllerClip(animator.runtimeAnimatorController, "Threshold_Succes_Placeholder") == null))
        {
            issue = "Threshold participant is missing its authored QTE/success placeholder clips; damage remains unclamped.";
            return false;
        }
        if (networkThresholdSession != null && !thresholdKillApplied && !networkThresholdSession.IsActive)
        {
            issue = "Threshold participant reservation was released.";
            return false;
        }
        issue = null;
        return true;
    }

    private bool TryAcquireNetworkThreshold()
    {
        if (NetworkThresholdActive) return networkThresholdSession.IsActive;
        networkThresholdActor = thresholdSource.GetComponent<CombatNetworkActorRuntime>();
        if (networkThresholdActor == null || !networkThresholdActor.TryAcquireThreshold(activeEnemy.Target,
                out networkThresholdSession)) return false;
        StartCoroutine(WatchNetworkThreshold(sessionToken));
        return true;
    }

    private bool TryLockNetworkThresholdPlayer()
    {
        var bridge = thresholdPlayer.GetComponent<LitOpsiveLocomotionBridge>();
        var presentation = thresholdPlayer.GetComponent<PlayerActionPresentationController>();
        if (bridge == null || presentation == null ||
            !bridge.TryAcquireExternalLock(this, out playerLock, disableGameplayInput: false, stopActiveAbilities: true)) return false;
        presentation.ResolveReferences(ThresholdAnimator, bridge);
        networkPresentationGeneration = presentation.BeginExternalPresentation(this);
        if (networkPresentationGeneration == 0) { playerLock.Dispose(); return false; }
        int token = sessionToken;
        presentation.RegisterActionTermination(networkPresentationGeneration, reason =>
        {
            if (token == sessionToken && thresholdOwnershipHeld &&
                reason != PlayerActionPresentationController.ActionEndReason.Completed)
                AbortActiveSequence("session acteur de palier interrompue");
        });
        return true;
    }

    private IEnumerator WatchNetworkThreshold(int token)
    {
        // The existing controller owns progression and outcomes. Transport only admits input.
        while (token == sessionToken && networkThresholdSession != null)
        {
            if (networkThresholdActor == null || !networkThresholdActor.IsSpawned ||
                !thresholdKillApplied && (!networkThresholdSession.IsActive ||
                    !networkThresholdActor.IsThresholdEncounterActive(activeEnemy?.Target)))
            {
                AbortActiveSequence("reservation ou participant reseau perdu");
                yield break;
            }
            yield return null;
        }
    }

    private void PublishNetworkThresholdPresentation(bool success)
    {
        if (!NetworkThresholdActive || networkThresholdActor == null) return;
        var stages = activeEnemy.Data.combatHealthThresholdStages;
        int stageIndex = stages != null ? stages.IndexOf(activeStage) : -1;
        networkThresholdActor.PublishThresholdPresentation(networkThresholdSession, activeEnemy.Target,
            stageIndex, currentStepIndex, success);
    }

    private bool OpenNetworkThresholdWindow(CombatThresholdQteInput input)
    {
        if (!networkThresholdSession.TryOpenWindow((CombatNetworkQteButton)input, GetCurrentQteDuration())) return false;
        networkThresholdActor.PublishThresholdWindow(networkThresholdSession, input, GetCurrentQteDuration());
        return true;
    }

    private void PublishNetworkThresholdExit(float blend)
    {
        if (NetworkThresholdActive && networkThresholdActor != null)
            networkThresholdActor.PublishThresholdExit(networkThresholdSession, blend);
    }

    private IEnumerator NetworkThresholdFailureKnockback(int token, float distance)
    {
        var root = thresholdPlayer;
        var enemy = activeEnemy?.Root;
        var bridge = root != null ? root.GetComponent<LitOpsiveLocomotionBridge>() : null;
        if (enemy == null || bridge == null || !bridge.BeginScriptedPlanarMotion(this)) yield break;
        Vector3 direction = Vector3.ProjectOnPlane(root.position - enemy.position, Vector3.up).normalized;
        if (direction.sqrMagnitude < .001f) direction = -enemy.forward;
        float traveled = 0, stalled = 0;
        Vector3 previous = root.position;
        try
        {
            while (token == sessionToken && state == SequenceState.FailureRetaliation && root != null && traveled < distance && stalled < .12f)
            {
                bridge.DriveScriptedPlanarMotion(this, direction * 10f);
                yield return new WaitForFixedUpdate();
                if (root == null) break;
                float delta = Vector3.ProjectOnPlane(root.position - previous, Vector3.up).magnitude;
                traveled += delta;
                stalled = delta < .002f ? stalled + Time.fixedDeltaTime : 0;
                previous = root.position;
            }
        }
        finally
        {
            if (bridge != null) { bridge.DriveScriptedPlanarMotion(this, Vector3.zero); bridge.EndScriptedPlanarMotion(this); }
        }
    }

    internal void SubmitNetworkThresholdInput(CombatNetworkActorRuntime participant, ulong epoch, ulong generation,
        ulong window, ulong sequence, CombatNetworkQteButton button, bool pressed, ulong sender)
    {
        var session = networkThresholdSession;
        if (participant != networkThresholdActor || session == null || !qteOpen ||
            state != SequenceState.PlayingSequence || activeEnemy == null || activeEnemy.IsFlameDormant) return;
        var ticket = new CombatActionTicket(epoch, participant.NetworkObjectId, generation);
        var result = session.Submit(sender, ticket, window, sequence, button, pressed);
        if (result == CombatNetworkQteResult.Success) CompleteQteSuccess();
        else if (result == CombatNetworkQteResult.Failure || result == CombatNetworkQteResult.Expired)
            FailQte(result.ToString());
    }

    private void ResolveNetworkThresholdWindow(bool success)
    {
        if (networkThresholdActor != null && networkThresholdSession != null)
            networkThresholdActor.PublishThresholdResolution(networkThresholdSession, success);
    }

    private void ReleaseNetworkThreshold()
    {
        if (networkThresholdSession == null) return;
        var session = networkThresholdSession;
        networkThresholdSession = null;
        if (networkThresholdActor != null) networkThresholdActor.EndThresholdPresentation(session);
        networkThresholdActor = null;
        var presentation = thresholdPlayer != null ? thresholdPlayer.GetComponent<PlayerActionPresentationController>() : null;
        presentation?.EndExternalPresentation(networkPresentationGeneration);
        networkPresentationGeneration = 0;
        session.Dispose();
    }
}
