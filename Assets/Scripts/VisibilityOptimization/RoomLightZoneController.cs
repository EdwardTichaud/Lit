using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
public sealed class RoomLightZoneController : MonoBehaviour
{
    public enum ActivationMode
    {
        CameraOrPlayerInside,
        PlayerInside,
        CameraInside,
        CameraOrPlayerNear
    }

    [Header("References")]
    [SerializeField] private Collider zoneCollider;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private bool fallbackToMainCamera = true;
    [SerializeField] private bool fallbackToControlledPlayer = true;

    [Header("Lights")]
    [SerializeField] private bool autoCollectLights = true;
    [SerializeField] private bool includeInactiveLights = true;
    [SerializeField] private Light[] controlledLights = Array.Empty<Light>();
    [SerializeField] private ActivationMode activationMode = ActivationMode.CameraOrPlayerNear;
    [SerializeField, Min(0f)] private float nearDistance = 8f;
    [SerializeField, Min(0.05f)] private float evaluationInterval = 0.25f;

    [Header("Performance Defaults")]
    [SerializeField] private bool disableRealtimeShadowsOnNonImportantLights = true;
    [SerializeField] private string importantLightNameToken = "key";
    [SerializeField] private LightShadows importantShadowMode = LightShadows.Soft;
    [SerializeField] private LightShadows nonImportantShadowMode = LightShadows.None;
    [SerializeField, Min(0f)] private float maxRange = 18f;
    [SerializeField, Min(128)] private int hdrpShadowResolution = 512;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos;
    [SerializeField] private bool logChanges;

    private bool[] originalEnabledStates = Array.Empty<bool>();
    private float[] originalRanges = Array.Empty<float>();
    private LightShadows[] originalShadowModes = Array.Empty<LightShadows>();
    private HdrpShadowResolutionState[] originalHdrpShadowStates = Array.Empty<HdrpShadowResolutionState>();
    private bool capturedStates;
    private bool active = true;
    private float nextEvaluationTime;

    private struct HdrpShadowResolutionState
    {
        public bool HasData;
        public bool UseOverride;
        public int OverrideResolution;
        public int Level;
    }

    private void Reset()
    {
        zoneCollider = GetComponent<Collider>();
        RefreshLights();
    }

    private void Awake()
    {
        if (autoCollectLights && !HasLightReferences())
        {
            RefreshLights();
        }

        CaptureLightStates();
        ApplyLightDefaults();
    }

    private void OnEnable()
    {
        CaptureLightStates();
        ApplyLightDefaults();
        active = true;
        nextEvaluationTime = 0f;
    }

    private void OnDisable()
    {
        RestoreLights();
    }

    private void OnValidate()
    {
        nearDistance = Mathf.Max(0f, nearDistance);
        evaluationInterval = Mathf.Max(0.05f, evaluationInterval);
        maxRange = Mathf.Max(0f, maxRange);
        hdrpShadowResolution = Mathf.Max(128, hdrpShadowResolution);
        if (!Application.isPlaying && autoCollectLights)
        {
            RefreshLights();
        }
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = Time.unscaledTime + evaluationInterval;
        ResolveReferences();
        bool shouldBeActive = ShouldActivate();
        if (active == shouldBeActive)
        {
            return;
        }

        active = shouldBeActive;
        ApplyActiveState(active);
    }

    public void RefreshLights()
    {
        controlledLights = GetComponentsInChildren<Light>(includeInactiveLights);
        capturedStates = false;
    }

    private void ResolveReferences()
    {
        if (targetCamera == null && fallbackToMainCamera)
        {
            targetCamera = Camera.main;
        }

        if (playerTarget == null && fallbackToControlledPlayer)
        {
            GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
            if (controlled != null && controlled.activeInHierarchy)
            {
                playerTarget = controlled.transform;
            }
        }
    }

    private bool ShouldActivate()
    {
        Bounds bounds = ResolveBounds();
        bool cameraInside = targetCamera != null && bounds.Contains(targetCamera.transform.position);
        bool playerInside = playerTarget != null && bounds.Contains(playerTarget.position);
        bool cameraNear = targetCamera != null && bounds.SqrDistance(targetCamera.transform.position) <= nearDistance * nearDistance;
        bool playerNear = playerTarget != null && bounds.SqrDistance(playerTarget.position) <= nearDistance * nearDistance;

        switch (activationMode)
        {
            case ActivationMode.PlayerInside:
                return playerInside;
            case ActivationMode.CameraInside:
                return cameraInside;
            case ActivationMode.CameraOrPlayerNear:
                return cameraInside || playerInside || cameraNear || playerNear;
            default:
                return cameraInside || playerInside;
        }
    }

    private Bounds ResolveBounds()
    {
        if (zoneCollider != null)
        {
            return zoneCollider.bounds;
        }

        return new Bounds(transform.position, Vector3.one * Mathf.Max(1f, nearDistance));
    }

    private void CaptureLightStates()
    {
        if (capturedStates)
        {
            return;
        }

        int length = controlledLights != null ? controlledLights.Length : 0;
        if (originalEnabledStates == null || originalEnabledStates.Length != length)
        {
            originalEnabledStates = new bool[length];
        }

        if (originalRanges == null || originalRanges.Length != length)
        {
            originalRanges = new float[length];
        }

        if (originalShadowModes == null || originalShadowModes.Length != length)
        {
            originalShadowModes = new LightShadows[length];
        }

        if (originalHdrpShadowStates == null || originalHdrpShadowStates.Length != length)
        {
            originalHdrpShadowStates = new HdrpShadowResolutionState[length];
        }

        for (int i = 0; controlledLights != null && i < controlledLights.Length; i++)
        {
            Light targetLight = controlledLights[i];
            originalEnabledStates[i] = targetLight != null && targetLight.enabled;
            originalRanges[i] = targetLight != null ? targetLight.range : 0f;
            originalShadowModes[i] = targetLight != null ? targetLight.shadows : LightShadows.None;

            HdrpShadowResolutionState shadowState = default;
            HDAdditionalLightData hdLight = targetLight != null ? targetLight.GetComponent<HDAdditionalLightData>() : null;
            if (hdLight != null)
            {
                shadowState.HasData = true;
                shadowState.UseOverride = hdLight.shadowResolution.useOverride;
                shadowState.OverrideResolution = hdLight.shadowResolution.@override;
                shadowState.Level = hdLight.shadowResolution.level;
            }

            originalHdrpShadowStates[i] = shadowState;
        }

        capturedStates = true;
    }

    private void ApplyActiveState(bool enabledState)
    {
        CaptureLightStates();
        for (int i = 0; controlledLights != null && i < controlledLights.Length; i++)
        {
            Light targetLight = controlledLights[i];
            if (targetLight == null)
            {
                continue;
            }

            bool originalEnabled = originalEnabledStates.Length > i && originalEnabledStates[i];
            targetLight.enabled = enabledState && originalEnabled;
        }

        if (logChanges)
        {
            Debug.Log($"[RoomLightZone] {name}: active={enabledState}", this);
        }
    }

    private void RestoreLights()
    {
        if (!capturedStates)
        {
            return;
        }

        for (int i = 0; controlledLights != null && i < controlledLights.Length; i++)
        {
            Light targetLight = controlledLights[i];
            if (targetLight != null && originalEnabledStates.Length > i)
            {
                targetLight.enabled = originalEnabledStates[i];
                if (originalRanges.Length > i)
                {
                    targetLight.range = originalRanges[i];
                }

                if (originalShadowModes.Length > i)
                {
                    targetLight.shadows = originalShadowModes[i];
                }

                if (originalHdrpShadowStates.Length > i)
                {
                    RestoreHdrpShadowResolution(targetLight, originalHdrpShadowStates[i]);
                }
            }
        }
    }

    private static void RestoreHdrpShadowResolution(Light targetLight, HdrpShadowResolutionState shadowState)
    {
        if (targetLight == null || !shadowState.HasData)
        {
            return;
        }

        HDAdditionalLightData hdLight = targetLight.GetComponent<HDAdditionalLightData>();
        if (hdLight == null)
        {
            return;
        }

        hdLight.SetShadowResolution(shadowState.OverrideResolution);
        hdLight.SetShadowResolutionLevel(shadowState.Level);
        hdLight.SetShadowResolutionOverride(shadowState.UseOverride);
    }

    private void ApplyLightDefaults()
    {
        for (int i = 0; controlledLights != null && i < controlledLights.Length; i++)
        {
            Light targetLight = controlledLights[i];
            if (targetLight == null)
            {
                continue;
            }

            if (maxRange > 0f && targetLight.type != LightType.Directional && targetLight.range > maxRange)
            {
                targetLight.range = maxRange;
            }

            bool important = IsImportantLight(targetLight);
            if (disableRealtimeShadowsOnNonImportantLights)
            {
                targetLight.shadows = important ? importantShadowMode : nonImportantShadowMode;
            }

            HDAdditionalLightData hdLight = targetLight.GetComponent<HDAdditionalLightData>();
            if (hdLight != null && targetLight.shadows != LightShadows.None)
            {
                hdLight.SetShadowResolution(hdrpShadowResolution);
            }
        }
    }

    private bool HasLightReferences()
    {
        if (controlledLights == null)
        {
            return false;
        }

        for (int i = 0; i < controlledLights.Length; i++)
        {
            if (controlledLights[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsImportantLight(Light targetLight)
    {
        if (targetLight == null || string.IsNullOrWhiteSpace(importantLightNameToken))
        {
            return false;
        }

        return targetLight.name.IndexOf(importantLightNameToken, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Bounds bounds = ResolveBounds();
        Gizmos.color = active ? Color.yellow : Color.gray;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
