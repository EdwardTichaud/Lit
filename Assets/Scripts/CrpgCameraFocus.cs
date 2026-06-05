using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CrpgCameraFocus
{
    [SerializeField] private float followSharpness = 8f;
    [SerializeField, Tooltip("Sharpness used when the followed target is moving fast. Lower values add more camera lag/smoothing.")]
    private float highSpeedFollowSharpness = 4.25f;
    [SerializeField, Tooltip("Target horizontal speed where extra follow smoothing starts.")]
    private float highSpeedSmoothingStart = 4.5f;
    [SerializeField, Tooltip("Target horizontal speed where extra follow smoothing reaches full strength.")]
    private float highSpeedSmoothingFull = 7f;
    [SerializeField, Tooltip("Maximum distance the smoothed focus can lag behind the followed target.")]
    private float maxFollowLagDistance = 2.25f;
    [SerializeField, Tooltip("Distance above which the focus snaps to a new target instead of smoothing, mostly for teleports or scene changes.")]
    private float followSnapDistance = 12f;
    [SerializeField, Tooltip("Smoothing applied to the estimated target speed used by the adaptive follow.")]
    private float targetSpeedSharpness = 10f;
    [SerializeField] private float freePanSharpness = 12f;
    [Header("Free Camera")]
    [SerializeField] private float freeCameraMaxDistance = 20f;
    [SerializeField] private float freeCameraSoftZone = 4f;

    private struct OverrideEntry
    {
        public Transform target;
    }

    private readonly List<OverrideEntry> overrides = new List<OverrideEntry>();
    private bool initialized;
    private bool followActive = true;
    private bool freeCameraModeActive;
    private Vector3 desiredFocusPoint;
    private Vector3 currentFocusPoint;
    private Vector3 lastTargetFocusPoint;
    private bool hasLastTargetFocusPoint;
    private float smoothedTargetSpeed;

    public Vector3 CurrentFocusPoint => currentFocusPoint;
    public bool FollowActive => followActive;
    public bool FreeCameraModeActive => freeCameraModeActive;

    public void Validate()
    {
        followSharpness = Mathf.Max(0f, followSharpness);
        highSpeedFollowSharpness = Mathf.Max(0f, highSpeedFollowSharpness);
        highSpeedSmoothingStart = Mathf.Max(0f, highSpeedSmoothingStart);
        highSpeedSmoothingFull = Mathf.Max(highSpeedSmoothingStart + 0.01f, highSpeedSmoothingFull);
        maxFollowLagDistance = Mathf.Max(0f, maxFollowLagDistance);
        followSnapDistance = Mathf.Max(0f, followSnapDistance);
        targetSpeedSharpness = Mathf.Max(0f, targetSpeedSharpness);
        freePanSharpness = Mathf.Max(0f, freePanSharpness);
        freeCameraMaxDistance = Mathf.Max(0f, freeCameraMaxDistance);
        freeCameraSoftZone = Mathf.Clamp(freeCameraSoftZone, 0f, freeCameraMaxDistance);
    }

    public void Reset()
    {
        overrides.Clear();
        initialized = false;
        followActive = true;
        freeCameraModeActive = false;
        desiredFocusPoint = Vector3.zero;
        currentFocusPoint = Vector3.zero;
        lastTargetFocusPoint = Vector3.zero;
        hasLastTargetFocusPoint = false;
        smoothedTargetSpeed = 0f;
    }

    public void SnapTo(Vector3 point)
    {
        initialized = true;
        followActive = true;
        freeCameraModeActive = false;
        desiredFocusPoint = point;
        currentFocusPoint = point;
        lastTargetFocusPoint = point;
        hasLastTargetFocusPoint = true;
        smoothedTargetSpeed = 0f;
    }

    public void SetFreeCameraMode(bool active)
    {
        freeCameraModeActive = active;
        followActive = !active;
    }

    public void PushOverride(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Transform currentTop = GetTopOverrideTarget();
        if (currentTop == target)
        {
            followActive = true;
            return;
        }

        overrides.Add(new OverrideEntry
        {
            target = target
        });
        followActive = true;
    }

    public void ClearOverride(Transform target)
    {
        if (target == null)
        {
            return;
        }

        for (int i = overrides.Count - 1; i >= 0; i--)
        {
            if (overrides[i].target == target)
            {
                overrides.RemoveAt(i);
                break;
            }
        }

        followActive = true;
    }

    public Transform GetTopOverrideTarget()
    {
        PruneNullOverrides();
        return overrides.Count > 0 ? overrides[overrides.Count - 1].target : null;
    }

    public Vector3 Update(
        Vector3 targetFocusPoint,
        Vector3 worldPanDelta,
        bool recenterRequested,
        bool toggleFreeCameraRequested,
        float deltaTime)
    {
        if (!initialized)
        {
            SnapTo(targetFocusPoint);
            return currentFocusPoint;
        }

        if (toggleFreeCameraRequested)
        {
            SetFreeCameraMode(!freeCameraModeActive);
        }

        if (recenterRequested)
        {
            freeCameraModeActive = false;
            followActive = true;
            ResetTargetSpeed(targetFocusPoint);
        }

        bool suppressPanForFrame = recenterRequested || (toggleFreeCameraRequested && !freeCameraModeActive);
        if (!suppressPanForFrame && worldPanDelta.sqrMagnitude > 0.000001f)
        {
            desiredFocusPoint += worldPanDelta;
            followActive = false;
        }

        if (followActive)
        {
            UpdateTargetSpeed(targetFocusPoint, deltaTime);
            desiredFocusPoint = targetFocusPoint;
        }
        else if (freeCameraModeActive)
        {
            ResetTargetSpeed(targetFocusPoint);
            desiredFocusPoint = ClampFreeCameraPoint(desiredFocusPoint, targetFocusPoint);
        }

        if (ShouldSnapToFollowTarget(targetFocusPoint))
        {
            SnapTo(targetFocusPoint);
            return currentFocusPoint;
        }

        float sharpness = followActive ? ResolveFollowSharpness() : freePanSharpness;
        if (sharpness <= 0f)
        {
            currentFocusPoint = desiredFocusPoint;
        }
        else
        {
            float t = 1f - Mathf.Exp(-sharpness * deltaTime);
            currentFocusPoint = Vector3.Lerp(currentFocusPoint, desiredFocusPoint, t);
        }

        ClampFollowLag();
        return currentFocusPoint;
    }

    private void UpdateTargetSpeed(Vector3 targetFocusPoint, float deltaTime)
    {
        if (!hasLastTargetFocusPoint || deltaTime <= 0f)
        {
            ResetTargetSpeed(targetFocusPoint);
            return;
        }

        Vector3 delta = targetFocusPoint - lastTargetFocusPoint;
        lastTargetFocusPoint = targetFocusPoint;

        Vector3 planarDelta = Vector3.ProjectOnPlane(delta, Vector3.up);
        float rawSpeed = planarDelta.magnitude / deltaTime;
        float t = targetSpeedSharpness <= 0f ? 1f : 1f - Mathf.Exp(-targetSpeedSharpness * deltaTime);
        smoothedTargetSpeed = Mathf.Lerp(smoothedTargetSpeed, rawSpeed, t);
    }

    private void ResetTargetSpeed(Vector3 targetFocusPoint)
    {
        lastTargetFocusPoint = targetFocusPoint;
        hasLastTargetFocusPoint = true;
        smoothedTargetSpeed = 0f;
    }

    private float ResolveFollowSharpness()
    {
        float speedWeight = Mathf.InverseLerp(highSpeedSmoothingStart, highSpeedSmoothingFull, smoothedTargetSpeed);
        return Mathf.Lerp(followSharpness, highSpeedFollowSharpness, speedWeight);
    }

    private bool ShouldSnapToFollowTarget(Vector3 targetFocusPoint)
    {
        if (!followActive || followSnapDistance <= 0f)
        {
            return false;
        }

        return Vector3.Distance(currentFocusPoint, targetFocusPoint) > followSnapDistance;
    }

    private void ClampFollowLag()
    {
        if (!followActive || maxFollowLagDistance <= 0f)
        {
            return;
        }

        Vector3 lag = currentFocusPoint - desiredFocusPoint;
        float lagDistance = lag.magnitude;
        if (lagDistance <= maxFollowLagDistance || lagDistance <= 0.0001f)
        {
            return;
        }

        currentFocusPoint = desiredFocusPoint + lag.normalized * maxFollowLagDistance;
    }

    private Vector3 ClampFreeCameraPoint(Vector3 point, Vector3 center)
    {
        if (freeCameraMaxDistance <= 0f)
        {
            return center;
        }

        Vector3 planarOffset = Vector3.ProjectOnPlane(point - center, Vector3.up);
        float planarDistance = planarOffset.magnitude;
        if (planarDistance <= 0.0001f)
        {
            return point;
        }

        float softZone = Mathf.Clamp(freeCameraSoftZone, 0f, freeCameraMaxDistance);
        float softStart = Mathf.Max(0f, freeCameraMaxDistance - softZone);
        if (planarDistance <= softStart)
        {
            return point;
        }

        float over = planarDistance - softStart;
        float compressedOver = softZone <= 0.0001f
            ? 0f
            : softZone * (1f - Mathf.Exp(-over / softZone));
        float targetDistance = Mathf.Min(softStart + compressedOver, freeCameraMaxDistance);
        Vector3 clampedPlanar = planarOffset.normalized * targetDistance;

        return new Vector3(center.x + clampedPlanar.x, point.y, center.z + clampedPlanar.z);
    }

    private void PruneNullOverrides()
    {
        for (int i = overrides.Count - 1; i >= 0; i--)
        {
            if (overrides[i].target == null)
            {
                overrides.RemoveAt(i);
            }
        }
    }
}
