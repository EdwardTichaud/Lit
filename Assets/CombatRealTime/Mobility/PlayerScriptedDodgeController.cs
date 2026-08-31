using System.Collections;
using UnityEngine;

/// <summary>
/// Sole runtime owner of an in-place combat dodge.
/// It captures the chosen direction once, applies one UCC impulse, and restores
/// normal facing only after the dodge presentation has completed.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerScriptedDodgeController : MonoBehaviour
{
    [Header("Dodge Tuning")]
    [Min(0.01f), Tooltip("Initial planar UCC velocity-change applied when the dodge starts. Higher values make the dodge more forceful.")]
    public float impulseSpeed = 16f;
    [Min(0.1f), Tooltip("Multiplies each dodge animation's authored action duration without changing its locked direction.")]
    public float durationMultiplier = 1f;
    [Tooltip("Outside lock-on, align every roll with its travel direction.")]
    public bool alignUnlockedDodgeToTravel = true;
    [Tooltip("During lock-on, only forward rolls turn toward their travel direction. Back and side rolls keep facing the enemy.")]
    public bool alignLockedForwardDodgeToTravel = true;

    private Coroutine activeDodgeRoutine;
    private LitOpsiveLocomotionBridge activeBridge;

    public bool IsActive => activeDodgeRoutine != null;

    public bool TryStartDodge(
        LitOpsiveLocomotionBridge bridge,
        PlayerActionPresentationController actionPresentation,
        Vector3 worldDirection,
        CombatDodgeDashProfile profile)
    {
        if (bridge == null || actionPresentation == null || profile == null ||
            profile.durationSeconds <= 0f || impulseSpeed <= 0f)
        {
            return false;
        }

        Vector3 direction = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
        if (direction.sqrMagnitude <= 0.0001f) return false;
        direction.Normalize();

        CancelDodge();
        activeBridge = bridge;
        bool alignToTravel = ShouldAlignToTravel(bridge, profile.statePath);
        bridge.BeginDodgeDirectionFacing(direction, alignToTravel);
        if (!bridge.BeginScriptedPlanarMotion())
        {
            EndDodge();
            return false;
        }

        // This is intentionally a single velocity-change. The captured
        // direction cannot be steered afterwards; UCC owns collision, gravity
        // and the resulting inertial deceleration.
        if (!bridge.ApplyScriptedPlanarImpulse(direction * impulseSpeed))
        {
            EndDodge();
            return false;
        }

        activeDodgeRoutine = StartCoroutine(RunDodge(actionPresentation, profile.durationSeconds * durationMultiplier));
        return true;
    }

    public void CancelDodge()
    {
        if (activeDodgeRoutine != null)
        {
            StopCoroutine(activeDodgeRoutine);
            activeDodgeRoutine = null;
        }

        EndDodge();
    }

    private void OnDisable()
    {
        CancelDodge();
    }

    private IEnumerator RunDodge(PlayerActionPresentationController actionPresentation, float maximumDuration)
    {
        float elapsed = 0f;
        while (actionPresentation != null && actionPresentation.IsActionActive && elapsed < maximumDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        activeDodgeRoutine = null;
        EndDodge();
    }

    private void EndDodge()
    {
        if (activeBridge == null) return;

        if (activeBridge.IsScriptedPlanarMotionActive)
        {
            activeBridge.DriveScriptedPlanarMotion(Vector3.zero);
            activeBridge.EndScriptedPlanarMotion();
        }

        activeBridge.EndDodgeDirectionFacing();
        activeBridge = null;
    }

    private bool ShouldAlignToTravel(LitOpsiveLocomotionBridge bridge, string statePath)
    {
        if (bridge == null || !bridge.IsCombatLockActive) return alignUnlockedDodgeToTravel;
        return alignLockedForwardDodgeToTravel && !string.IsNullOrEmpty(statePath) &&
               statePath.IndexOf("_Dodge_F_", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
