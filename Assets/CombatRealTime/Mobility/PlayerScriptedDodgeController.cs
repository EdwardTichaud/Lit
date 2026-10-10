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

    private int dodgeGeneration;
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
        bool alignToTravel = !string.IsNullOrEmpty(profile.statePath) &&
            profile.statePath.IndexOf("_Dodge_F_", System.StringComparison.OrdinalIgnoreCase) >= 0;
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

        int generation = ++dodgeGeneration;
        int actionGeneration = actionPresentation.ActionGeneration;
        if (!actionPresentation.RegisterActionCleanup(actionGeneration, () =>
            { if (generation == dodgeGeneration) CancelDodge(); }))
        {
            CancelDodge();
            return false;
        }
        activeDodgeRoutine = StartCoroutine(RunDodge(actionPresentation, actionGeneration, generation,
            profile.durationSeconds * durationMultiplier));
        return true;
    }

    public void CancelDodge()
    {
        dodgeGeneration++;
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

    private IEnumerator RunDodge(PlayerActionPresentationController actionPresentation, int actionGeneration, int generation, float maximumDuration)
    {
        float elapsed = 0f;
        while (actionPresentation != null && actionPresentation.IsCurrentSession(actionGeneration) && generation == dodgeGeneration && elapsed < maximumDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += activeTimeDomain != null ? activeTimeDomain.FixedDeltaTime : Time.fixedDeltaTime;
        }

        if (generation != dodgeGeneration) yield break;
        // Translation ends on its authored duration, facing stays owned until
        // the action presentation actually hands control back.
        activeBridge?.DriveScriptedPlanarMotion(this, Vector3.zero);
        while (actionPresentation != null && actionPresentation.IsCurrentSession(actionGeneration) && generation == dodgeGeneration)
            yield return null;
        if (generation != dodgeGeneration) yield break;
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

}
