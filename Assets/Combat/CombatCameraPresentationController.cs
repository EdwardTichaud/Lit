using UnityEngine;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;

// Role: applique une presentation camera locale pendant les combats sans toucher au timeScale global.
// Usage: cree par CombatSessionManager a l'entree en combat; lit le contexte local expose par le manager.
// Responsibilities: cadrer joueur/ennemi via l'override rotationnel Opsive, puis restaurer la camera precedente.
// Dependencies: CombatSessionManager, LocalPlayerContext via le manager, Opsive CameraController.
// Precautions: presentation locale uniquement; ne pilote aucune logique serveur.
public sealed class CombatCameraPresentationController : MonoBehaviour
{
    public static CombatCameraPresentationController Instance { get; private set; }

    [SerializeField] private UccCameraController cameraController;
    [SerializeField] private bool useOpsiveCombatViewType = true;
    [SerializeField] private bool applyShoulderAnchorOffset = true;
    [SerializeField] private Vector3 playerDecisionAnchorOffset = new Vector3(0.45f, 1.65f, 0f);
    [SerializeField] private Vector3 enemyDecisionAnchorOffset = new Vector3(-0.35f, 1.65f, 0f);
    [SerializeField, Min(0.05f)] private float decisionBlendSeconds = 2f;
    [SerializeField, Min(0.05f)] private float actionBlendSeconds = 0.35f;
    [SerializeField, Min(0f)] private float targetLookHeight = 1.25f;
    [SerializeField, Min(0.01f)] private float minLookDistance = 0.25f;

    private string originalViewTypeFullName;
    private Vector3 originalAnchorOffset;
    private bool originalAnchorOffsetStored;
    private bool combatViewTypeApplied;
    private bool rotationalOverrideApplied;
    private float localPauseWeight;

    public static CombatCameraPresentationController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<CombatCameraPresentationController>();
#else
        Instance = FindAnyObjectByType<CombatCameraPresentationController>();
#endif
        if (Instance != null)
        {
            return Instance;
        }

        GameObject host = new GameObject("CombatCameraPresentationController");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<CombatCameraPresentationController>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDisable()
    {
        RestoreCameraPresentation();
    }

    private void OnDestroy()
    {
        RestoreCameraPresentation();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        CombatSessionManager manager = CombatSessionManager.Instance;
        Transform player = null;
        Transform enemy = null;
        bool playerTurn = false;
        CombatSessionPhase phase = CombatSessionPhase.Finished;
        bool hasContext = manager != null &&
            manager.TryGetLocalCombatCameraContext(
                out player,
                out enemy,
                out playerTurn,
                out phase);

        if (!hasContext)
        {
            UpdateLocalPauseWeight(holding: false);
            TimeManager.Instance?.SetCombatTimeTargets(null, null, defensiveReactionActive: false);
            if (localPauseWeight <= 0f)
            {
                RestoreCameraPresentation();
            }

            return;
        }

        UpdateLocalPauseWeight(ShouldHoldLocalPause(playerTurn, phase));
        TimeManager.EnsureInstance().SetCombatTimeTargets(player, enemy, ShouldSlowDefensiveReaction(playerTurn, phase));
        if (!EnsureCameraController())
        {
            return;
        }

        ApplyCameraPresentation();
        UpdateAnchorOffset(playerTurn);
    }

    private bool EnsureCameraController()
    {
        if (cameraController != null && cameraController.isActiveAndEnabled)
        {
            return true;
        }

        RestoreCameraState();
        cameraController = null;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraController = mainCamera.GetComponentInParent<UccCameraController>();
        }

        if (cameraController == null)
        {
#if UNITY_2023_1_OR_NEWER
            cameraController = FindAnyObjectByType<UccCameraController>();
#else
            cameraController = FindAnyObjectByType<UccCameraController>();
#endif
        }

        return cameraController != null && cameraController.isActiveAndEnabled;
    }

    private void ApplyCameraPresentation()
    {
        if (cameraController == null)
        {
            return;
        }

        if (!originalAnchorOffsetStored)
        {
            originalAnchorOffset = cameraController.AnchorOffset;
            originalAnchorOffsetStored = true;
        }

        if (!rotationalOverrideApplied)
        {
            cameraController.SetRotationalOverride(ResolveCombatRotation);
            rotationalOverrideApplied = true;
        }

        if (!useOpsiveCombatViewType || combatViewTypeApplied)
        {
            return;
        }

        if (cameraController.GetViewType<global::Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes.Combat>() == null)
        {
            return;
        }

        originalViewTypeFullName = cameraController.ViewTypeFullName;
        cameraController.SetViewType(typeof(global::Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes.Combat), false);
        combatViewTypeApplied = true;
    }

    private Quaternion ResolveCombatRotation(Vector3 cameraPosition, Quaternion currentRotation)
    {
        CombatSessionManager manager = CombatSessionManager.Instance;
        if (manager == null ||
            !manager.TryGetLocalCombatCameraContext(out Transform player, out Transform enemy, out _, out _))
        {
            return currentRotation;
        }

        Transform target = ResolveLookTarget(player, enemy);
        if (target == null)
        {
            return currentRotation;
        }

        Vector3 targetPosition = target.position + Vector3.up * targetLookHeight;
        Vector3 direction = targetPosition - cameraPosition;
        if (direction.sqrMagnitude < minLookDistance * minLookDistance && player != null && target != player)
        {
            direction = targetPosition - (player.position + Vector3.up * targetLookHeight);
        }

        if (direction.sqrMagnitude < minLookDistance * minLookDistance)
        {
            return currentRotation;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void UpdateLocalPauseWeight(bool holding)
    {
        float target = holding ? 1f : 0f;
        float duration = target > localPauseWeight ? decisionBlendSeconds : actionBlendSeconds;
        localPauseWeight = Mathf.MoveTowards(
            localPauseWeight,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
    }

    private void UpdateAnchorOffset(bool playerTurn)
    {
        if (!applyShoulderAnchorOffset || cameraController == null || !originalAnchorOffsetStored)
        {
            return;
        }

        Vector3 targetOffset = playerTurn ? playerDecisionAnchorOffset : enemyDecisionAnchorOffset;
        cameraController.AnchorOffset = Vector3.Lerp(originalAnchorOffset, targetOffset, localPauseWeight);
    }

    private static bool ShouldHoldLocalPause(bool playerTurn, CombatSessionPhase phase)
    {
        if (phase == CombatSessionPhase.Decision)
        {
            return true;
        }

        return playerTurn && phase == CombatSessionPhase.TurnActive;
    }

    private static bool ShouldSlowDefensiveReaction(bool playerTurn, CombatSessionPhase phase)
    {
        return !playerTurn && phase == CombatSessionPhase.Decision;
    }

    private static Transform ResolveLookTarget(Transform player, Transform enemy)
    {
        // The Opsive camera stays bound to the local player through LitUccCameraCharacterBinder.
        // Looking back at the player during the enemy turn makes the camera orbit the player.
        return enemy != null ? enemy : player;
    }

    private void RestoreCameraPresentation()
    {
        TimeManager.Instance?.RestoreCombatTime();
        RestoreCameraState();
    }

    private void RestoreCameraState()
    {
        if (cameraController != null)
        {
            if (rotationalOverrideApplied)
            {
                cameraController.SetRotationalOverride(null);
            }

            if (combatViewTypeApplied && !string.IsNullOrWhiteSpace(originalViewTypeFullName))
            {
                cameraController.ViewTypeFullName = originalViewTypeFullName;
            }

            if (originalAnchorOffsetStored)
            {
                cameraController.AnchorOffset = originalAnchorOffset;
            }
        }

        rotationalOverrideApplied = false;
        combatViewTypeApplied = false;
        originalAnchorOffsetStored = false;
        originalViewTypeFullName = null;
        localPauseWeight = 0f;
    }
}
