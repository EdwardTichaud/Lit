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

    private CombatLockUccCameraAdapter uccAdapter;
    private bool active;

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
        if (!active)
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
        if (!active)
        {
            return;
        }

        uccAdapter?.DeactivateLock();
        active = false;
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
