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
    private bool cinematicOverride;
    private bool cameraControllerEnabledBeforeCinematic;
    private Vector3 impactLookOffset;
    private float impactFieldOfView;
    private float impactRecoverySharpness = 18f;
    private float impactShakeElapsed;
    private float impactShakePhase;

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
        if (!active || cinematicOverride)
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

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, impactRecoverySharpness) * Time.unscaledDeltaTime);
        impactLookOffset = Vector3.Lerp(impactLookOffset, Vector3.zero, blend);
        impactFieldOfView = Mathf.Lerp(impactFieldOfView, 0f, blend);
        adapter.SetImpactPresentation(impactLookOffset + GetImpactShakeOffset(), impactFieldOfView);

        adapter.UpdateLockContext(
            manager.PlayerRoot,
            manager.LockedEnemy.LockPoint,
            enemyFocusBias,
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

    /// <summary>
    /// Temporarily gives a Timeline full ownership of the camera Transform.
    /// UCC is restored to the active lock view when the cinematic ends.
    /// </summary>
    public void SetCinematicOverride(bool enabled)
    {
        if (cinematicOverride == enabled)
        {
            return;
        }

        ResolveCameraController();
        cinematicOverride = enabled;
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
