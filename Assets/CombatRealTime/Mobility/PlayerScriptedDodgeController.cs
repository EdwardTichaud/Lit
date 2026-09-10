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
    private readonly PlayerModuleConfiguration<PlayerDodgeSettings> moduleConfiguration = new PlayerModuleConfiguration<PlayerDodgeSettings>();
    private PlayerDodgeSettings ModuleSettings => moduleConfiguration.Resolve(this, data => data.dodge);

    public float impulseSpeed { get => ModuleSettings.impulseSpeed; set => ModuleSettings.impulseSpeed = value; }
    public float durationMultiplier { get => ModuleSettings.durationMultiplier; set => ModuleSettings.durationMultiplier = value; }
    public bool alignUnlockedDodgeToTravel { get => ModuleSettings.alignUnlockedDodgeToTravel; set => ModuleSettings.alignUnlockedDodgeToTravel = value; }
    public bool alignLockedForwardDodgeToTravel { get => ModuleSettings.alignLockedForwardDodgeToTravel; set => ModuleSettings.alignLockedForwardDodgeToTravel = value; }

    private Coroutine activeDodgeRoutine;
    private LitOpsiveLocomotionBridge activeBridge;
    private CombatTimeDomain activeTimeDomain;

    public bool IsActive => activeDodgeRoutine != null;

    public bool TryStartDodge(
        LitOpsiveLocomotionBridge bridge,
        PlayerActionPresentationController actionPresentation,
        Vector3 worldDirection,
        CombatDodgeDashProfile profile)
    {
        if (!isActiveAndEnabled || bridge == null || actionPresentation == null || profile == null ||
            profile.durationSeconds <= 0f || impulseSpeed <= 0f)
        {
            return false;
        }

        Vector3 direction = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
        if (direction.sqrMagnitude <= 0.0001f) return false;
        direction.Normalize();

        CancelDodge();
        activeBridge = bridge;
        activeTimeDomain = bridge.GetComponent<CombatTimeDomain>();
        bool alignToTravel = ShouldAlignToTravel(bridge, profile.statePath);
        if (!bridge.BeginScriptedPlanarMotion(this))
        {
            activeBridge = null;
            activeTimeDomain = null;
            return false;
        }
        bridge.BeginDodgeDirectionFacing(direction, alignToTravel);

        // This is intentionally a single velocity-change. The captured
        // direction cannot be steered afterwards; UCC owns collision, gravity
        // and the resulting inertial deceleration.
        float localScale = activeTimeDomain != null ? activeTimeDomain.Scale : 1f;
        if (!bridge.ApplyScriptedPlanarImpulse(this, direction * impulseSpeed * localScale))
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
            elapsed += activeTimeDomain != null ? activeTimeDomain.FixedDeltaTime : Time.fixedDeltaTime;
        }

        activeDodgeRoutine = null;
        EndDodge();
    }

    private void EndDodge()
    {
        if (activeBridge == null) return;

        if (activeBridge.IsScriptedPlanarMotionActive)
        {
            activeBridge.DriveScriptedPlanarMotion(this, Vector3.zero);
            activeBridge.EndScriptedPlanarMotion(this);
        }

        activeBridge.EndDodgeDirectionFacing();
        activeBridge = null;
        activeTimeDomain = null;
    }

    private bool ShouldAlignToTravel(LitOpsiveLocomotionBridge bridge, string statePath)
    {
        if (bridge == null || !bridge.IsCombatLockActive) return alignUnlockedDodgeToTravel;
        return alignLockedForwardDodgeToTravel && !string.IsNullOrEmpty(statePath) &&
               statePath.IndexOf("_Dodge_F_", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
