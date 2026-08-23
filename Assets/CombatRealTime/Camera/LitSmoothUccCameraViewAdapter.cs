using System;
using System.Collections.Generic;
using Opsive.UltimateCharacterController.Camera.ViewTypes;
using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using UnityEngine;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;
using UccViewType = Opsive.UltimateCharacterController.Camera.ViewTypes.ViewType;

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
    [SerializeField, Min(0f)] private float followSmoothTime = 0.14f;
    [SerializeField, Min(0f)] private float maximumFollowSpeed = 30f;
    [SerializeField, Min(0f)] private float teleportSnapDistance = 3f;

    private void Reset()
    {
        cameraController = GetComponent<UccCameraController>();
    }

    private void Awake()
    {
        InstallSmoothGameplayView();
    }

    private void Start()
    {
        // Covers unusual script execution order in additive Bootstrap loading.
        InstallSmoothGameplayView();
    }

    private void InstallSmoothGameplayView()
    {
        if (cameraController == null)
        {
            cameraController = GetComponent<UccCameraController>();
        }

        if (cameraController == null)
        {
            return;
        }

        LitSmoothAdventureViewType smoothView = cameraController.GetViewType<LitSmoothAdventureViewType>();
        if (smoothView == null)
        {
            UccViewType[] existingViews = cameraController.ViewTypes;
            ThirdPerson gameplayView = FindGameplayAdventure(existingViews);
            if (gameplayView == null)
            {
                Debug.LogError("[UccCameraSmooth] Aucun ViewType Adventure de gameplay a lisser.", this);
                return;
            }

            smoothView = new LitSmoothAdventureViewType();
            smoothView.CopyGameplaySettingsFrom(gameplayView);

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
        }

        smoothView.ConfigureFollowDamping(followSmoothTime, maximumFollowSpeed, teleportSnapDistance);
        cameraController.ThirdPersonViewTypeFullName = typeof(LitSmoothAdventureViewType).FullName;
        cameraController.SetViewType(typeof(LitSmoothAdventureViewType), true);
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
