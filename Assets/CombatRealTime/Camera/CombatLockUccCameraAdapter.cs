using System;
using System.Collections.Generic;
using UnityEngine;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;
using UccViewType = Opsive.UltimateCharacterController.Camera.ViewTypes.ViewType;
using UccThirdPersonViewType = Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes.ThirdPerson;

/// <summary>
/// Owns the lock context and switches UCC between its regular view and CombatLock.
/// This component does not write to the camera Transform.
/// </summary>
[DefaultExecutionOrder(450)]
[DisallowMultipleComponent]
public sealed class CombatLockUccCameraAdapter : MonoBehaviour
{
    [SerializeField] private UccCameraController cameraController;
    [SerializeField, Min(0f)] private float lookPointSharpness = 16f;
    [Header("Diagnostics")]
    [SerializeField] private bool logViewTransitions;

    private Transform player;
    private Transform enemy;
    private float enemyFocusBias;
    private float playerLookHeight;
    private float enemyLookHeight;
    private Vector3 smoothedLookPoint;
    private bool hasSmoothedLookPoint;
    private bool lockActive;
    private string previousViewTypeFullName;
    private CombatLockAdventureViewType combatLockView;

    public bool LockActive => lockActive;
    public string PreviousViewTypeFullName => previousViewTypeFullName;
    public string ActiveViewTypeFullName => cameraController != null && cameraController.ActiveViewType != null
        ? cameraController.ActiveViewType.GetType().FullName
        : string.Empty;

    public void SetCameraController(UccCameraController controller)
    {
        if (cameraController == controller)
        {
            return;
        }

        cameraController = controller;
        combatLockView = null;
    }

    public void ActivateLock()
    {
        if (!ResolveCombatLockView())
        {
            return;
        }

        if (!lockActive)
        {
            previousViewTypeFullName = cameraController.ActiveViewType != null
                ? cameraController.ActiveViewType.GetType().FullName
                : null;
            lockActive = true;
            hasSmoothedLookPoint = false;
        }

        cameraController.SetViewType(typeof(CombatLockAdventureViewType), false);
        LogTransition("entered CombatLock");
    }

    public void UpdateLockContext(Transform playerTransform, Transform enemyTransform, float focusBias, float playerHeight, float enemyHeight)
    {
        player = playerTransform;
        enemy = enemyTransform;
        enemyFocusBias = Mathf.Clamp01(focusBias);
        playerLookHeight = playerHeight;
        enemyLookHeight = enemyHeight;
        RefreshLookPoint();
    }

    public void DeactivateLock()
    {
        if (!lockActive)
        {
            return;
        }

        lockActive = false;
        player = null;
        enemy = null;
        hasSmoothedLookPoint = false;

        if (cameraController != null && !string.IsNullOrEmpty(previousViewTypeFullName))
        {
            UccViewType[] views = cameraController.ViewTypes;
            for (int i = 0; views != null && i < views.Length; i++)
            {
                if (views[i] != null && views[i].GetType().FullName == previousViewTypeFullName)
                {
                    cameraController.SetViewType(views[i].GetType(), false);
                    break;
                }
            }
        }

        LogTransition("restored gameplay view");
        previousViewTypeFullName = null;
    }

    public bool TryGetLookPoint(out Vector3 lookPoint)
    {
        RefreshLookPoint();
        lookPoint = smoothedLookPoint;
        return lockActive && hasSmoothedLookPoint;
    }

    public bool TryGetPlayerToEnemyDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!lockActive || player == null || enemy == null)
        {
            return false;
        }

        direction = enemy.position - player.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        direction.Normalize();
        return true;
    }

    private bool ResolveCombatLockView()
    {
        if (cameraController == null)
        {
            cameraController = GetComponent<UccCameraController>();
        }

        if (cameraController == null)
        {
            return false;
        }

        combatLockView = cameraController.GetViewType<CombatLockAdventureViewType>();
        if (combatLockView != null)
        {
            return true;
        }

        UccViewType[] existingViews = cameraController.ViewTypes;
        List<UccViewType> views = new List<UccViewType>(existingViews ?? Array.Empty<UccViewType>());
        combatLockView = new CombatLockAdventureViewType();

        views.Add(combatLockView);
        cameraController.ViewTypes = views.ToArray();
        combatLockView.Initialize(cameraController);
        combatLockView.Awake();
        if (cameraController.Character != null)
        {
            combatLockView.AttachCharacter(cameraController.Character);
        }

        // FieldOfView invokes UCC immediately. Copy only after the new view has
        // received its controller, camera and character references.
        UccThirdPersonViewType gameplayView = cameraController.ActiveViewType as UccThirdPersonViewType;
        if (gameplayView != null)
        {
            combatLockView.CopyGameplaySettingsFrom(gameplayView);
        }
        combatLockView.ApplyCombatFraming();

        return true;
    }

    private void RefreshLookPoint()
    {
        if (!lockActive || player == null || enemy == null)
        {
            return;
        }

        Vector3 target = Vector3.Lerp(
            player.position + Vector3.up * playerLookHeight,
            enemy.position + Vector3.up * enemyLookHeight,
            enemyFocusBias);

        if (!hasSmoothedLookPoint)
        {
            smoothedLookPoint = target;
            hasSmoothedLookPoint = true;
            return;
        }

        float blend = lookPointSharpness <= 0f
            ? 1f
            : 1f - Mathf.Exp(-lookPointSharpness * Time.unscaledDeltaTime);
        smoothedLookPoint = Vector3.Lerp(smoothedLookPoint, target, blend);
    }

    private void LogTransition(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logViewTransitions)
        {
            Debug.Log($"[CombatLockCamera] {message}; activeView={ActiveViewTypeFullName}; previousView={previousViewTypeFullName}", this);
        }
#endif
    }
}
