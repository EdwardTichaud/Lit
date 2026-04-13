using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class DecorCullingManager : MonoBehaviour
{
    private static DecorCullingManager instance;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool fallbackToMainCamera = true;

    [Header("Culling")]
    [SerializeField, Min(10f)] private float maxVisibleDistance = 140f;
    [SerializeField] private bool requireCameraFrustum = true;
    [SerializeField, Min(0f)] private float offscreenGraceSeconds = 0.35f;
    [SerializeField, Min(0.05f)] private float evaluationInterval = 0.2f;

    private readonly List<DecorCullable> cullables = new List<DecorCullable>();
    private readonly Dictionary<DecorCullable, int> cullableIndices = new Dictionary<DecorCullable, int>();
    private BoundingSphere[] boundingSpheres = Array.Empty<BoundingSphere>();
    private float[] invisibleSince = Array.Empty<float>();
    private CullingGroup cullingGroup;
    private float[] distanceBands = Array.Empty<float>();
    private float nextEvaluationTime;
    private bool cullingGroupDirty = true;

    public static DecorCullingManager Instance => instance;

    public static void Register(DecorCullable cullable)
    {
        if (cullable == null)
        {
            return;
        }

        GetOrCreate().RegisterInternal(cullable);
    }

    public static void Unregister(DecorCullable cullable)
    {
        if (cullable == null || instance == null)
        {
            return;
        }

        instance.UnregisterInternal(cullable);
    }

    public static DecorCullingManager GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

#if UNITY_2023_1_OR_NEWER
        instance = FindFirstObjectByType<DecorCullingManager>();
#else
        instance = FindObjectOfType<DecorCullingManager>();
#endif
        if (instance != null)
        {
            return instance;
        }

        GameObject host = new GameObject("DecorCullingManager");
        instance = host.AddComponent<DecorCullingManager>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ClampSettings();
        EnsureCullingGroup();
    }

    private void OnEnable()
    {
        EnsureCullingGroup();
        cullingGroupDirty = true;
    }

    private void OnDisable()
    {
        SetAllVisible();
        DisposeCullingGroup();
    }

    private void OnDestroy()
    {
        SetAllVisible();
        DisposeCullingGroup();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnValidate()
    {
        ClampSettings();
        ConfigureDistances();
        cullingGroupDirty = true;
    }

    private void LateUpdate()
    {
        if (cullables.Count == 0)
        {
            return;
        }

        EnsureCullingGroup();
        if (!ResolveTargetCamera())
        {
            SetAllVisible();
            return;
        }

        float now = Time.unscaledTime;
        if (!cullingGroupDirty && now < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = now + evaluationInterval;
        RefreshBoundingSpheres();
        EvaluateAll();
        cullingGroupDirty = false;
    }

    private void RegisterInternal(DecorCullable cullable)
    {
        if (cullableIndices.ContainsKey(cullable))
        {
            return;
        }

        int index = cullables.Count;
        cullables.Add(cullable);
        cullableIndices[cullable] = index;
        EnsureCapacity(cullables.Count);
        invisibleSince[index] = -1f;
        boundingSpheres[index] = cullable.CurrentBoundingSphere;
        cullingGroupDirty = true;

        EnsureCullingGroup();
        cullingGroup.SetBoundingSphereCount(cullables.Count);
    }

    private void UnregisterInternal(DecorCullable cullable)
    {
        if (!cullableIndices.TryGetValue(cullable, out int index))
        {
            return;
        }

        RemoveAt(index);
        cullingGroupDirty = true;
    }

    private void RemoveAt(int index)
    {
        int lastIndex = cullables.Count - 1;
        DecorCullable removed = cullables[index];
        cullableIndices.Remove(removed);

        if (index != lastIndex)
        {
            DecorCullable moved = cullables[lastIndex];
            cullables[index] = moved;
            cullableIndices[moved] = index;
            boundingSpheres[index] = boundingSpheres[lastIndex];
            invisibleSince[index] = invisibleSince[lastIndex];
        }

        cullables.RemoveAt(lastIndex);
        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(cullables.Count);
        }
    }

    private void RefreshBoundingSpheres()
    {
        for (int i = cullables.Count - 1; i >= 0; i--)
        {
            if (cullables[i] == null)
            {
                RemoveAt(i);
            }
        }

        EnsureCapacity(cullables.Count);
        for (int i = 0; i < cullables.Count; i++)
        {
            boundingSpheres[i] = cullables[i].CurrentBoundingSphere;
        }

        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSpheres(boundingSpheres);
            cullingGroup.SetBoundingSphereCount(cullables.Count);
        }
    }

    private void EvaluateAll()
    {
        for (int i = 0; i < cullables.Count; i++)
        {
            EvaluateIndex(i, Time.unscaledTime);
        }
    }

    private void OnCullingStateChanged(CullingGroupEvent sphereEvent)
    {
        if (sphereEvent.index < 0 || sphereEvent.index >= cullables.Count)
        {
            return;
        }

        EvaluateIndex(sphereEvent.index, Time.unscaledTime);
    }

    private void EvaluateIndex(int index, float now)
    {
        DecorCullable cullable = cullables[index];
        if (cullable == null || cullingGroup == null)
        {
            return;
        }

        bool withinDistance = cullingGroup.GetDistance(index) == 0;
        bool withinCamera = !requireCameraFrustum || cullingGroup.IsVisible(index);
        bool shouldBeVisible = withinDistance && withinCamera;
        if (shouldBeVisible)
        {
            invisibleSince[index] = -1f;
            cullable.SetCulled(false);
            return;
        }

        bool distanceCulled = !withinDistance;
        if (distanceCulled || offscreenGraceSeconds <= 0f)
        {
            invisibleSince[index] = now;
            cullable.SetCulled(true);
            return;
        }

        if (invisibleSince[index] < 0f)
        {
            invisibleSince[index] = now;
        }

        if (now - invisibleSince[index] >= offscreenGraceSeconds)
        {
            cullable.SetCulled(true);
        }
    }

    private bool ResolveTargetCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
        {
            if (cullingGroup != null)
            {
                ApplyCameraToCullingGroup(targetCamera);
            }

            return true;
        }

        if (!fallbackToMainCamera)
        {
            return false;
        }

        targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return false;
        }

        if (cullingGroup != null)
        {
            ApplyCameraToCullingGroup(targetCamera);
        }

        return true;
    }

    private void EnsureCullingGroup()
    {
        if (cullingGroup != null)
        {
            return;
        }

        EnsureCapacity(Mathf.Max(1, cullables.Count));
        cullingGroup = new CullingGroup
        {
            onStateChanged = OnCullingStateChanged
        };
        cullingGroup.SetBoundingSpheres(boundingSpheres);
        cullingGroup.SetBoundingSphereCount(cullables.Count);
        ConfigureDistances();

        if (targetCamera != null)
        {
            ApplyCameraToCullingGroup(targetCamera);
        }
    }

    private void ApplyCameraToCullingGroup(Camera camera)
    {
        if (cullingGroup == null || camera == null)
        {
            return;
        }

        cullingGroup.targetCamera = camera;
        cullingGroup.SetDistanceReferencePoint(camera.transform);
    }

    private void ConfigureDistances()
    {
        distanceBands = new[] { Mathf.Max(10f, maxVisibleDistance) };
        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingDistances(distanceBands);
        }
    }

    private void EnsureCapacity(int requiredCount)
    {
        if (requiredCount <= 0)
        {
            requiredCount = 1;
        }

        if (boundingSpheres != null && boundingSpheres.Length >= requiredCount)
        {
            return;
        }

        int capacity = Mathf.NextPowerOfTwo(requiredCount);
        Array.Resize(ref boundingSpheres, capacity);
        Array.Resize(ref invisibleSince, capacity);

        for (int i = 0; i < invisibleSince.Length; i++)
        {
            if (Mathf.Approximately(invisibleSince[i], 0f))
            {
                invisibleSince[i] = -1f;
            }
        }

        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSpheres(boundingSpheres);
        }
    }

    private void SetAllVisible()
    {
        for (int i = 0; i < cullables.Count; i++)
        {
            DecorCullable cullable = cullables[i];
            if (cullable != null)
            {
                cullable.SetCulled(false);
            }

            if (i < invisibleSince.Length)
            {
                invisibleSince[i] = -1f;
            }
        }
    }

    private void DisposeCullingGroup()
    {
        if (cullingGroup == null)
        {
            return;
        }

        cullingGroup.Dispose();
        cullingGroup = null;
    }

    private void ClampSettings()
    {
        maxVisibleDistance = Mathf.Max(10f, maxVisibleDistance);
        offscreenGraceSeconds = Mathf.Max(0f, offscreenGraceSeconds);
        evaluationInterval = Mathf.Max(0.05f, evaluationInterval);
    }
}
