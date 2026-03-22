using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CrpgCameraFocus
{
    [SerializeField] private float followSharpness = 8f;
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

    public Vector3 CurrentFocusPoint => currentFocusPoint;
    public bool FollowActive => followActive;
    public bool FreeCameraModeActive => freeCameraModeActive;

    public void Validate()
    {
        followSharpness = Mathf.Max(0f, followSharpness);
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
    }

    public void SnapTo(Vector3 point)
    {
        initialized = true;
        followActive = true;
        freeCameraModeActive = false;
        desiredFocusPoint = point;
        currentFocusPoint = point;
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
        }

        bool suppressPanForFrame = recenterRequested || (toggleFreeCameraRequested && !freeCameraModeActive);
        if (!suppressPanForFrame && worldPanDelta.sqrMagnitude > 0.000001f)
        {
            desiredFocusPoint += worldPanDelta;
            followActive = false;
        }

        if (followActive)
        {
            desiredFocusPoint = targetFocusPoint;
        }
        else if (freeCameraModeActive)
        {
            desiredFocusPoint = ClampFreeCameraPoint(desiredFocusPoint, targetFocusPoint);
        }

        float sharpness = followActive ? followSharpness : freePanSharpness;
        if (sharpness <= 0f)
        {
            currentFocusPoint = desiredFocusPoint;
        }
        else
        {
            float t = 1f - Mathf.Exp(-sharpness * deltaTime);
            currentFocusPoint = Vector3.Lerp(currentFocusPoint, desiredFocusPoint, t);
        }

        return currentFocusPoint;
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
