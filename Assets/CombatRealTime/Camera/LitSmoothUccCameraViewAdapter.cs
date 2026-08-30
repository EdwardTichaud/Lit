using System;
using System.Collections.Generic;
using Opsive.UltimateCharacterController.Camera.ViewTypes;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using UnityEngine;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;
using UccViewType = Opsive.UltimateCharacterController.Camera.ViewTypes.ViewType;

public enum CameraSnapReason
{
    InitialBind,
    CharacterSwitch,
    SceneLoad,
    ManualRecenter,
    Teleport,
    ExternalCameraReturn,
    Collision
}

/// <summary>
/// Installs the smooth gameplay view after UCC has initialized its serialized
/// Adventure view. Keeping Adventure serialized avoids UCC managed-reference
/// deserialization failures during Bootstrap startup.
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(UccCameraController))]
public sealed class LitSmoothUccCameraViewAdapter : MonoBehaviour
{
    [SerializeField] private UccCameraController cameraController;
    [Header("Exploration Framing")]
    [SerializeField, Min(0f), Tooltip("Native UCC horizontal pivot slack. Zero keeps the lateral follow continuous and prevents its sign-based threshold from trembling in one direction.")]
    private float horizontalPivotFreedom;
    [SerializeField, Min(0f), Tooltip("Native UCC smoothing for the look offset only; aim input remains immediate.")]
    private float lookOffsetSmoothing = 0.08f;
    [SerializeField, Min(0f), Tooltip("Maximum temporary vertical framing offset while the character is airborne.")]
    private float airborneVerticalMaximumOffset = 0.32f;
    [SerializeField, Min(0f), Tooltip("Height gained since takeoff converted into temporary framing offset while airborne.")]
    private float airborneVerticalHeightCompression = 0.20f;
    [SerializeField, Min(0.001f)] private float airborneVerticalRiseSmoothTime = 0.14f;
    [SerializeField, Min(0.001f)] private float airborneVerticalFallSmoothTime = 0.16f;
    [SerializeField, Min(0.001f)] private float groundedVerticalRestoreSmoothTime = 0.12f;
    [Header("Stable Character Anchor")]
    [SerializeField, Range(0.1f, 1f), Tooltip("Stable root-anchor height expressed as a fraction of the UCC capsule height. This prevents animation head bob from moving the camera.")]
    private float rootAnchorHeightFraction = 0.72f;
    [SerializeField, Min(0f), Tooltip("Minimum world-space root-anchor height used for small avatars.")]
    private float minimumRootAnchorHeight = 1.35f;
    [Header("Development Diagnostics")]
    [SerializeField, Tooltip("Keeps a fixed-size camera motion history. It is never replicated and allocates only when dumped to the Console.")]
    private bool recordMotionDiagnostics;
    [Header("Startup Diagnostics")]
    [SerializeField, Tooltip("Logs only a failed startup handoff after the retry window. Useful when a camera extension is present but UCC never binds Lucian.")]
    private bool logFailedStartupHandoff = true;
    [SerializeField, Min(0.1f)] private float startupRetrySeconds = 3f;

    private LitUccCameraCharacterBinder characterBinder;
    private Coroutine startupRoutine;
    private bool smoothViewInstalled;

    /// <summary>The only gameplay-facing path allowed to request an intentional UCC camera snap.</summary>
    public bool RequestImmediatePose(CameraSnapReason reason)
    {
        if (!InstallSmoothGameplayView() || cameraController == null)
        {
            return false;
        }

        LitSmoothAdventureViewType smoothView = cameraController.GetViewType<LitSmoothAdventureViewType>();
        if (smoothView == null)
        {
            return false;
        }

        smoothView.RequestImmediatePose(reason);
        cameraController.PositionImmediately(true);
        return true;
    }

    [ContextMenu("Dump Camera Motion Diagnostics")]
    public void DumpMotionDiagnostics()
    {
        LitSmoothAdventureViewType smoothView = cameraController != null
            ? cameraController.GetViewType<LitSmoothAdventureViewType>()
            : null;
        Debug.Log(smoothView != null ? smoothView.BuildMotionDiagnosticsReport() : "[UccCameraMotion] Vue lissée indisponible.", this);
    }

    /// <summary>Called by the character diagnostic toggle; recording uses a fixed-size buffer.</summary>
    public void SetMotionDiagnosticsEnabled(bool enabled)
    {
        recordMotionDiagnostics = enabled;
        LitSmoothAdventureViewType smoothView = cameraController != null
            ? cameraController.GetViewType<LitSmoothAdventureViewType>()
            : null;
        smoothView?.SetMotionDiagnosticsEnabled(enabled);
    }

    private void Reset()
    {
        cameraController = GetComponent<UccCameraController>();
    }

    private void Awake()
    {
        ResolveReferences();
        InstallSmoothGameplayView();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (characterBinder != null)
        {
            characterBinder.CharacterBound += OnCharacterBound;
        }

        // Covers additive Bootstrap startup: the UCC controller is intentionally
        // disabled until the local character has been bound by the binder.
        if (startupRoutine == null)
        {
            startupRoutine = StartCoroutine(InstallWhenUccIsReady());
        }
    }

    private void OnDisable()
    {
        if (characterBinder != null)
        {
            characterBinder.CharacterBound -= OnCharacterBound;
        }

        if (startupRoutine != null)
        {
            StopCoroutine(startupRoutine);
            startupRoutine = null;
        }
    }

    private void OnCharacterBound(UccCameraController boundController, Transform _)
    {
        if (boundController != null)
        {
            cameraController = boundController;
        }

        InstallSmoothGameplayView();
        ConfigureStableRootAnchor();
    }

    private void LateUpdate()
    {
        if (cameraController == null || cameraController.Character == null)
        {
            return;
        }

        ConfigureStableRootAnchor();

        // A late scene or perspective initialization can restore the
        // serialized Adventure view after this adapter first installs its
        // extension. Reassert the exploration contract, but never interrupt
        // the dedicated combat view or an intentional UCC transition.
        if (!cameraController.IsTransitioning &&
            cameraController.ActiveViewType is not LitSmoothAdventureViewType)
        {
            smoothViewInstalled = false;
            InstallSmoothGameplayView();
        }
    }

    private System.Collections.IEnumerator InstallWhenUccIsReady()
    {
        float deadline = Time.realtimeSinceStartup + startupRetrySeconds;
        while (isActiveAndEnabled && !smoothViewInstalled && Time.realtimeSinceStartup < deadline)
        {
            InstallSmoothGameplayView();
            if (smoothViewInstalled)
            {
                break;
            }

            yield return null;
        }

        if (!smoothViewInstalled && logFailedStartupHandoff)
        {
            string cameraState = cameraController == null
                ? "CameraController missing"
                : $"CameraController enabled={cameraController.enabled}, character={(cameraController.Character != null ? cameraController.Character.name : "none")}, views={cameraController.ViewTypes?.Length ?? 0}";
            Debug.LogWarning($"[UccCameraStartup] La vue lissée UCC n'a pas été installée après {startupRetrySeconds:0.##} s. {cameraState}", this);
        }

        startupRoutine = null;
    }

    private bool InstallSmoothGameplayView()
    {
        ResolveReferences();

        if (cameraController == null)
        {
            return false;
        }

        LitSmoothAdventureViewType smoothView = cameraController.GetViewType<LitSmoothAdventureViewType>();
        if (smoothView == null)
        {
            UccViewType[] existingViews = cameraController.ViewTypes;
            ThirdPerson gameplayView = FindGameplayAdventure(existingViews);
            if (gameplayView == null)
            {
                return false;
            }

            smoothView = new LitSmoothAdventureViewType();

            List<UccViewType> views = new List<UccViewType>(existingViews ?? Array.Empty<UccViewType>())
            {
                smoothView
            };
            cameraController.ViewTypes = views.ToArray();
            cameraController.InitializeViewTypes();
            smoothView.Awake();
            if (cameraController.Character != null)
            {
                smoothView.AttachCharacter(cameraController.Character);
            }

            // ViewType property setters access the UCC camera internals. The
            // new view must therefore be initialized before copying values
            // such as FieldOfView; doing this in Awake caused an intermittent
            // startup NullReferenceException during session instantiation.
            smoothView.CopyGameplaySettingsFrom(gameplayView);
        }

        smoothView.SetMotionDiagnosticsEnabled(recordMotionDiagnostics);
        smoothView.ConfigureExplorationFraming(
            horizontalPivotFreedom,
            lookOffsetSmoothing,
            airborneVerticalMaximumOffset,
            airborneVerticalHeightCompression,
            airborneVerticalRiseSmoothTime,
            airborneVerticalFallSmoothTime,
            groundedVerticalRestoreSmoothTime);
        bool combatViewActive = cameraController.ActiveViewType is CombatLockAdventureViewType;
        if (!combatViewActive)
        {
            cameraController.ThirdPersonViewTypeFullName = typeof(LitSmoothAdventureViewType).FullName;
            if (cameraController.enabled && cameraController.Character != null)
            {
                cameraController.SetViewType(typeof(LitSmoothAdventureViewType), true);
            }
        }

        smoothViewInstalled = true;
        return true;
    }

    private void ResolveReferences()
    {
        if (cameraController == null)
        {
            cameraController = GetComponent<UccCameraController>();
        }

        if (characterBinder == null)
        {
            characterBinder = GetComponent<LitUccCameraCharacterBinder>();
        }
    }

    private void ConfigureStableRootAnchor()
    {
        if (cameraController == null || cameraController.Character == null)
        {
            return;
        }

        Transform root = cameraController.Character.transform;
        if (cameraController.Anchor != root)
        {
            cameraController.Anchor = root;
        }

        UltimateCharacterLocomotion locomotion = cameraController.CharacterLocomotion;
        float height = locomotion != null ? locomotion.Height : 0f;
        float stableHeight = Mathf.Max(minimumRootAnchorHeight, height * rootAnchorHeightFraction);
        Vector3 offset = cameraController.AnchorOffset;
        if (Mathf.Abs(offset.y - stableHeight) <= 0.001f)
        {
            return;
        }

        offset.y = stableHeight;
        cameraController.AnchorOffset = offset;
    }

    private static ThirdPerson FindGameplayAdventure(UccViewType[] views)
    {
        if (views == null)
        {
            return null;
        }

        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] is Adventure adventure && views[i] is not LitSmoothAdventureViewType)
            {
                return adventure;
            }
        }

        return null;
    }
}
