using UnityEngine;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;

/// <summary>
/// Coordinates the combat lock with UCC. It deliberately never writes the camera Transform:
/// the active UCC view type remains the only camera driver.
/// </summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class CombatLockOnCameraController : MonoBehaviour
{
    [SerializeField] private Camera controlledCamera;
    [SerializeField] private UccCameraController cameraController;
    [SerializeField, Range(0f, 1f)] private float enemyFocusBias = 0.74f;

    [Header("Combat Lock Framing")]
    [SerializeField] private float playerLookHeight = 1.25f;
    [SerializeField] private float enemyLookHeight = 1.1f;
    [Tooltip("Offset de la camera UCC pendant un lock. Un Z plus negatif recule la camera.")]
    [SerializeField] private Vector3 lockCameraOffset = new Vector3(0.85f, 0.5f, -6.5f);
    [SerializeField, Range(15f, 100f)] private float lockFieldOfView = 66f;

    [Header("Combat Lock Motion")]
    [SerializeField, Range(20f, 360f)] private float maximumLockOrbitDegreesPerSecond = 95f;
    [SerializeField, Min(0.1f)] private float lockAxisSharpness = 7f;

    [Header("Action Impact Damping")]
    [Tooltip("Reduit globalement les decalages de cadrage demandes par les skills, sans modifier leurs profils.")]
    [SerializeField, Range(0f, 1f)] private float impactLookOffsetScale = 0.45f;
    [Tooltip("Reduit globalement les variations de FOV demandees par les skills, sans modifier leurs profils.")]
    [SerializeField, Range(0f, 1f)] private float impactFieldOfViewScale = 0.45f;
    [Tooltip("Un nouvel impact remplace progressivement le precedent au lieu de s'y additionner.")]
    [SerializeField, Range(0.05f, 1f)] private float impactReplacementBlend = 0.65f;
    [SerializeField, Min(0f)] private float maximumImpactLookOffset = 0.2f;
    [SerializeField, Range(0f, 15f)] private float maximumImpactFieldOfView = 1.25f;
    [SerializeField, Min(0.1f)] private float minimumImpactRecoverySharpness = 20f;

    [Header("Action Impact Shake")]
    [SerializeField, Min(0f)] private float impactShakeAmplitude = 0.012f;
    [SerializeField, Min(0.01f)] private float impactShakeDuration = 0.07f;
    [SerializeField, Min(0.1f)] private float impactShakeFrequency = 28f;

    private CombatLockUccCameraAdapter uccAdapter;
    private bool active;
    private bool cinematicFramingSuspended;
    private bool uccCameraOverrideActive;
    private bool cameraControllerEnabledBeforeCinematic;
    private Vector3 impactLookOffset;
    private float impactFieldOfView;
    private float impactRecoverySharpness = 18f;
    private float impactShakeElapsed;
    private float impactShakePhase;
    private Transform warningTarget;
    private CombatWarningProfile warningProfile;
    private bool warningRequested;
    private float warningBlend;

    private void Awake()
    {
        ResolveCameraController();
    }

    private void OnEnable()
    {
        if (RealTimeCombatManager.Instance != null)
        {
            RealTimeCombatManager.Instance.LockChanged += OnLockChanged;
            OnLockChanged(RealTimeCombatManager.Instance.LockedEnemy);
        }
    }

    private void OnDisable()
    {
        if (RealTimeCombatManager.Instance != null)
        {
            RealTimeCombatManager.Instance.LockChanged -= OnLockChanged;
        }

        RestoreGameplayCamera();
    }

    private void LateUpdate()
    {
        if (!active || cinematicFramingSuspended || uccCameraOverrideActive)
        {
            return;
        }

        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager == null || manager.PlayerRoot == null || manager.LockedEnemy == null)
        {
            RestoreGameplayCamera();
            return;
        }

        CombatLockUccCameraAdapter adapter = ResolveAdapter();
        if (adapter == null)
        {
            return;
        }

        float warningFade = warningProfile != null && warningProfile.fadeOutSeconds > 0f
            ? Time.unscaledDeltaTime / warningProfile.fadeOutSeconds
            : 1f;
        warningBlend = Mathf.MoveTowards(warningBlend, warningRequested ? 1f : 0f, warningFade);
        if (!warningRequested && warningBlend <= 0f)
        {
            warningTarget = null;
            warningProfile = null;
        }

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, impactRecoverySharpness) * Time.unscaledDeltaTime);
        impactLookOffset = Vector3.Lerp(impactLookOffset, Vector3.zero, blend);
        impactFieldOfView = Mathf.Lerp(impactFieldOfView, 0f, blend);
        adapter.SetImpactPresentation(impactLookOffset + GetImpactShakeOffset(), impactFieldOfView);

        CombatWarningProfile profile = warningProfile;
        float focusBias = profile != null ? Mathf.Lerp(enemyFocusBias, profile.enemyFocusBias, warningBlend) : enemyFocusBias;
        float maxOrbit = profile != null ? Mathf.Lerp(maximumLockOrbitDegreesPerSecond, profile.recenterDegreesPerSecond, warningBlend) : maximumLockOrbitDegreesPerSecond;
        float axisSharpness = profile != null ? Mathf.Lerp(lockAxisSharpness, profile.focusSharpness, warningBlend) : lockAxisSharpness;
        Transform target = warningTarget != null ? warningTarget : manager.LockedEnemy.LockPoint;
        adapter.ConfigureLockMotion(maxOrbit, axisSharpness);
        adapter.ConfigureLookPointSharpness(axisSharpness);
        adapter.SetWarningPresentation(profile != null ? profile.fieldOfViewOffset * warningBlend : 0f);

        adapter.UpdateLockContext(
            manager.PlayerRoot,
            target,
            focusBias,
            playerLookHeight,
            enemyLookHeight);
    }

    private void OnLockChanged(RealTimeCombatEnemy enemy)
    {
        if (enemy != null)
        {
            ActivateLockCamera();
        }
        else
        {
            RestoreGameplayCamera();
        }
    }

    private void ActivateLockCamera()
    {
        if (active)
        {
            return;
        }

        CombatLockUccCameraAdapter adapter = ResolveAdapter();
        if (adapter == null)
        {
            Debug.LogWarning("Combat lock camera could not resolve the UCC CameraController.", this);
            return;
        }

        active = true;
        adapter.ActivateLock();
    }

    private void RestoreGameplayCamera()
    {
        SetCinematicOverride(false);
        if (!active)
        {
            return;
        }

        impactLookOffset = Vector3.zero;
        impactFieldOfView = 0f;
        impactShakeElapsed = impactShakeDuration;
        uccAdapter?.SetImpactPresentation(Vector3.zero, 0f);
        uccAdapter?.SetWarningPresentation(0f);
        warningRequested = false;
        warningBlend = 0f;
        warningTarget = null;
        warningProfile = null;
        uccAdapter?.DeactivateLock();
        active = false;
    }

    public void PlayImpact(CombatCameraImpactProfile profile)
    {
        if (profile == null || !active)
        {
            return;
        }

        // A rapid combo should replace the previous kick, never accumulate it.
        Vector3 targetLookOffset = Vector3.ClampMagnitude(
            profile.lookOffsetKick * impactLookOffsetScale,
            maximumImpactLookOffset);
        float targetFieldOfView = Mathf.Clamp(
            profile.fieldOfViewKick * impactFieldOfViewScale,
            -maximumImpactFieldOfView,
            maximumImpactFieldOfView);

        impactLookOffset = Vector3.Lerp(impactLookOffset, targetLookOffset, impactReplacementBlend);
        impactFieldOfView = Mathf.Lerp(impactFieldOfView, targetFieldOfView, impactReplacementBlend);
        impactRecoverySharpness = Mathf.Max(minimumImpactRecoverySharpness, profile.recoverySharpness);
        impactShakeElapsed = 0f;
        impactShakePhase += 1.6180339f;
    }

    /// <summary>Prioritizes an attacking enemy without changing the manual lock target.</summary>
    public void BeginAttackWarning(Transform enemyLockPoint, CombatWarningProfile profile)
    {
        if (!active || enemyLockPoint == null || profile == null || !profile.enabled)
        {
            return;
        }

        warningTarget = enemyLockPoint;
        warningProfile = profile;
        warningRequested = true;
    }

    public void EndAttackWarning(Transform enemyLockPoint = null)
    {
        if (enemyLockPoint != null && warningTarget != null && enemyLockPoint != warningTarget)
        {
            return;
        }

        warningRequested = false;
    }

    /// <summary>
    /// Temporarily gives a Timeline full ownership of the camera Transform.
    /// UCC is restored to the active lock view when the cinematic ends.
    /// </summary>
    public void SetCinematicOverride(bool enabled)
    {
        if (uccCameraOverrideActive == enabled)
        {
            return;
        }

        ResolveCameraController();
        uccCameraOverrideActive = enabled;
        if (enabled)
        {
            if (cameraController != null)
            {
                cameraControllerEnabledBeforeCinematic = cameraController.enabled;
                cameraController.enabled = false;
            }

            return;
        }

        if (cameraController != null)
        {
            cameraController.enabled = cameraControllerEnabledBeforeCinematic;
            if (active && cameraController.enabled)
            {
                ResolveAdapter()?.ActivateLock();
            }
        }
    }

    /// <summary>
    /// Suspends only the lock framing while another system owns the gameplay
    /// camera. Unlike <see cref="SetCinematicOverride"/>, this never changes
    /// the UCC camera driver's enabled state.
    /// </summary>
    public void SetCinematicFramingSuspended(bool suspended)
    {
        if (cinematicFramingSuspended == suspended)
        {
            return;
        }

        cinematicFramingSuspended = suspended;
        if (!suspended && active)
        {
            ResolveAdapter()?.ActivateLock();
        }
    }

    public Camera ControlledCamera
    {
        get
        {
            ResolveCameraController();
            return controlledCamera;
        }
    }

    private Vector3 GetImpactShakeOffset()
    {
        if (impactShakeAmplitude <= 0f || impactShakeElapsed >= impactShakeDuration)
        {
            return Vector3.zero;
        }

        impactShakeElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(impactShakeElapsed / impactShakeDuration);
        float envelope = (1f - progress) * (1f - progress);
        float sample = impactShakePhase + impactShakeElapsed * impactShakeFrequency;

        return new Vector3(
            Mathf.Sin(sample),
            Mathf.Sin(sample * 1.37f + 0.8f) * 0.65f,
            0f) * (impactShakeAmplitude * envelope);
    }

    private CombatLockUccCameraAdapter ResolveAdapter()
    {
        ResolveCameraController();
        if (cameraController == null)
        {
            return null;
        }

        if (uccAdapter == null)
        {
            uccAdapter = cameraController.GetComponent<CombatLockUccCameraAdapter>();
            if (uccAdapter == null)
            {
                uccAdapter = cameraController.gameObject.AddComponent<CombatLockUccCameraAdapter>();
            }
        }

        uccAdapter.SetCameraController(cameraController);
        uccAdapter.ConfigureLockMotion(maximumLockOrbitDegreesPerSecond, lockAxisSharpness);
        uccAdapter.ConfigureLockFraming(lockCameraOffset, lockFieldOfView);
        return uccAdapter;
    }

    private void ResolveCameraController()
    {
        if (cameraController != null)
        {
            return;
        }

        if (controlledCamera == null)
        {
            controlledCamera = Camera.main;
        }

        if (controlledCamera != null)
        {
            cameraController = controlledCamera.GetComponent<UccCameraController>();
        }
    }
}
