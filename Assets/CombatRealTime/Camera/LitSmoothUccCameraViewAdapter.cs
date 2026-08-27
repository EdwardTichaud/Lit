using System;
using System.Collections.Generic;
using Opsive.UltimateCharacterController.Camera.ViewTypes;
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
    [Header("Gameplay Follow Damping")]
    [SerializeField, Min(0f)] private float followSmoothTime = 0.16f;
    [SerializeField, Min(0f)] private float maximumFollowSpeed = 30f;
    [SerializeField, Min(0f), Tooltip("Extra follow damping used only while the character is airborne. Camera aim remains immediate.")]
    private float airborneFollowSmoothTime = 0.24f;
    [SerializeField, Min(0f), Tooltip("Maximum follow speed used only while airborne.")]
    private float airborneMaximumFollowSpeed = 18f;
    [SerializeField, Min(0f)] private float teleportSnapDistance = 3f;
    [SerializeField, Min(0f), Tooltip("Distance reduction required before the camera snaps inward for a major wall. Smaller obstacle corrections remain smooth.")]
    private float hardCollisionSnapDistance = 1.25f;
    [SerializeField, Tooltip("Keep disabled for smoother lock framing in tight spaces. UCC native camera collision is still used.")]
    private bool useSupplementalCollisionConstraint;
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

        smoothView.ConfigureFollowDamping(
            followSmoothTime,
            maximumFollowSpeed,
            airborneFollowSmoothTime,
            airborneMaximumFollowSpeed,
            teleportSnapDistance,
            hardCollisionSnapDistance,
            useSupplementalCollisionConstraint,
            recordMotionDiagnostics);
        cameraController.ThirdPersonViewTypeFullName = typeof(LitSmoothAdventureViewType).FullName;
        if (cameraController.enabled && cameraController.Character != null)
        {
            cameraController.SetViewType(typeof(LitSmoothAdventureViewType), true);
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
