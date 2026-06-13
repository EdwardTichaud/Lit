using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-5000)]
public sealed class LitInfluenceParticleSystemController : MonoBehaviour
{
    private const string DefaultCommonLightTag = "Light";

    private static LitInfluenceParticleSystemController instance;
    private static bool initialized;

    [Header("Scanning")]
    [SerializeField] private string commonLightTag = DefaultCommonLightTag;
    [SerializeField, Min(0.05f)] private float commonLightScanInterval = 0.5f;
    [SerializeField, Min(0.05f)] private float influenceRefreshInterval = 0.2f;
    [SerializeField] private bool includeInactiveCommonLights = true;

    [Header("Activation")]
    [SerializeField, Min(0f)] private float activationDelay = 1f;
    [SerializeField, Min(0f)] private float orderedActivationStepDelay = 0.25f;
    [SerializeField, Min(0f)] private float commonLightInfluenceRadius = 4f;
    [SerializeField] private bool useUnityLightRangeAsCommonInfluence = false;

    [Header("Playback")]
    [SerializeField] private bool disablePlayOnAwake = true;
    [SerializeField] private bool useRendererBoundsCenter = true;
    [SerializeField] private bool colorUnityLightsFromSource = true;
    [SerializeField] private bool reactToTorchInfluence = true;
    [SerializeField] private bool reactToBraseroInfluence = true;

    private readonly List<CommonLightEntry> commonLights = new List<CommonLightEntry>();
    private readonly Dictionary<GameObject, CommonLightEntry> commonLightLookup = new Dictionary<GameObject, CommonLightEntry>();
    private readonly HashSet<GameObject> scannedCommonRoots = new HashSet<GameObject>();
    private readonly List<Torch> torches = new List<Torch>();
    private readonly List<Brasero> braseros = new List<Brasero>();
    private readonly HashSet<Torch> subscribedTorches = new HashSet<Torch>();
    private readonly HashSet<Brasero> subscribedBraseros = new HashSet<Brasero>();
    private readonly List<Torch> staleSubscribedTorches = new List<Torch>();
    private readonly List<Brasero> staleSubscribedBraseros = new List<Brasero>();
    private readonly HashSet<MonoBehaviour> activeRootSources = new HashSet<MonoBehaviour>();
    private readonly Dictionary<CommonLightEntry, ActivationRequest> activationRequests = new Dictionary<CommonLightEntry, ActivationRequest>();
    private readonly List<CommonLightEntry> sourceOrderedBuffer = new List<CommonLightEntry>();

    private bool hasScannedCommonLights;
    private float nextCommonLightScanTime;
    private float nextInfluenceRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
        initialized = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!initialized)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            initialized = true;
        }

        EnsureInstance();
    }

    public static bool TryRegisterAndApply(IReadOnlyList<ParticleSystem> systems)
    {
        if (!Application.isPlaying || systems == null || systems.Count == 0)
        {
            return false;
        }

        LitInfluenceParticleSystemController controller = EnsureInstance();
        if (controller == null)
        {
            return false;
        }

        bool registeredAny = controller.RegisterParticleSystems(systems);
        if (!registeredAny)
        {
            return false;
        }

        controller.RefreshInfluenceSources();
        controller.ApplyInfluenceStates();
        return true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LitInfluenceParticleSystemController controller = EnsureInstance();
        if (controller == null)
        {
            return;
        }

        controller.RefreshCommonLights();
        controller.RefreshInfluenceSources();
        controller.ApplyInfluenceStates();
    }

    private static LitInfluenceParticleSystemController EnsureInstance()
    {
        if (!Application.isPlaying)
        {
            return null;
        }

        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<LitInfluenceParticleSystemController>(FindObjectsInactive.Include);
        if (instance != null)
        {
            return instance;
        }

        GameObject controllerObject = new GameObject("Lit Influence Particle System Controller")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        DontDestroyOnLoad(controllerObject);
        instance = controllerObject.AddComponent<LitInfluenceParticleSystemController>();
        return instance;
    }

    private void OnEnable()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
        RefreshCommonLights();
        RefreshInfluenceSources();
        ApplyInfluenceStates();
        ScheduleNextScans();
    }

    private void OnDisable()
    {
        UnsubscribeAllSourceEvents();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now >= nextCommonLightScanTime)
        {
            RefreshCommonLights();
            RefreshInfluenceSources();
            nextCommonLightScanTime = now + Mathf.Max(0.05f, commonLightScanInterval);
        }

        if (now < nextInfluenceRefreshTime)
        {
            return;
        }

        ApplyInfluenceStates();
        nextInfluenceRefreshTime = now + Mathf.Max(0.05f, influenceRefreshInterval);
    }

    private void RefreshCommonLights()
    {
        scannedCommonRoots.Clear();

        FindObjectsInactive inactiveMode = includeInactiveCommonLights
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;

        ParticleSystem[] foundParticleSystems = FindObjectsByType<ParticleSystem>(inactiveMode);
        for (int i = 0; i < foundParticleSystems.Length; i++)
        {
            TryRegisterTaggedCommonRoot(foundParticleSystems[i] != null ? foundParticleSystems[i].transform : null);
        }

        Light[] foundLights = FindObjectsByType<Light>(inactiveMode);
        for (int i = 0; i < foundLights.Length; i++)
        {
            TryRegisterTaggedCommonRoot(foundLights[i] != null ? foundLights[i].transform : null);
        }

        hasScannedCommonLights = true;
        RemoveMissingCommonLights();
    }

    private bool RegisterParticleSystems(IReadOnlyList<ParticleSystem> systems)
    {
        bool registeredAny = false;
        for (int i = 0; i < systems.Count; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null)
            {
                continue;
            }

            if (TryResolveCommonLightRoot(system.transform, out GameObject root))
            {
                scannedCommonRoots.Add(root);
                registeredAny |= RegisterOrRefreshCommonLight(root);
            }
        }

        return registeredAny;
    }

    private void TryRegisterTaggedCommonRoot(Transform start)
    {
        if (!TryResolveCommonLightRoot(start, out GameObject root) || root == null)
        {
            return;
        }

        scannedCommonRoots.Add(root);
        RegisterOrRefreshCommonLight(root);
    }

    private bool RegisterOrRefreshCommonLight(GameObject root)
    {
        if (root == null)
        {
            return false;
        }

        if (!commonLightLookup.TryGetValue(root, out CommonLightEntry entry))
        {
            entry = new CommonLightEntry(root);
            commonLightLookup.Add(root, entry);
            commonLights.Add(entry);
        }

        entry.Refresh(disablePlayOnAwake);
        if (!entry.HasAnyControllable)
        {
            return false;
        }

        if (!entry.IsActive)
        {
            entry.ApplyInactiveState();
        }

        return true;
    }

    private void RemoveMissingCommonLights()
    {
        for (int i = commonLights.Count - 1; i >= 0; i--)
        {
            CommonLightEntry entry = commonLights[i];
            if (entry != null
                && entry.IsValid(commonLightTag)
                && entry.HasAnyControllable
                && (!hasScannedCommonLights || scannedCommonRoots.Contains(entry.Root)))
            {
                continue;
            }

            if (entry != null && entry.Root != null)
            {
                commonLightLookup.Remove(entry.Root);
            }

            commonLights.RemoveAt(i);
        }
    }

    private void RefreshInfluenceSources()
    {
        torches.Clear();
        braseros.Clear();
        activeRootSources.Clear();

        if (reactToTorchInfluence)
        {
            Torch[] sceneTorches = FindObjectsByType<Torch>(FindObjectsInactive.Exclude);
            for (int i = 0; i < sceneTorches.Length; i++)
            {
                Torch torch = sceneTorches[i];
                if (torch == null || !torch.isActiveAndEnabled)
                {
                    continue;
                }

                torches.Add(torch);
                if (torch.IsLit)
                {
                    activeRootSources.Add(torch);
                }
            }
        }

        if (reactToBraseroInfluence)
        {
            Brasero[] sceneBraseros = FindObjectsByType<Brasero>(FindObjectsInactive.Exclude);
            for (int i = 0; i < sceneBraseros.Length; i++)
            {
                Brasero brasero = sceneBraseros[i];
                if (brasero == null || !brasero.isActiveAndEnabled)
                {
                    continue;
                }

                braseros.Add(brasero);
                if (brasero.IsLit)
                {
                    activeRootSources.Add(brasero);
                }
            }
        }

        SyncSourceEventSubscriptions();
    }

    private void SyncSourceEventSubscriptions()
    {
        staleSubscribedTorches.Clear();
        foreach (Torch torch in subscribedTorches)
        {
            if (torch == null || !torches.Contains(torch))
            {
                staleSubscribedTorches.Add(torch);
            }
        }

        for (int i = 0; i < staleSubscribedTorches.Count; i++)
        {
            UnsubscribeTorch(staleSubscribedTorches[i]);
        }

        staleSubscribedTorches.Clear();

        for (int i = 0; i < torches.Count; i++)
        {
            SubscribeTorch(torches[i]);
        }

        staleSubscribedBraseros.Clear();
        foreach (Brasero brasero in subscribedBraseros)
        {
            if (brasero == null || !braseros.Contains(brasero))
            {
                staleSubscribedBraseros.Add(brasero);
            }
        }

        for (int i = 0; i < staleSubscribedBraseros.Count; i++)
        {
            UnsubscribeBrasero(staleSubscribedBraseros[i]);
        }

        staleSubscribedBraseros.Clear();

        for (int i = 0; i < braseros.Count; i++)
        {
            SubscribeBrasero(braseros[i]);
        }
    }

    private void SubscribeTorch(Torch torch)
    {
        if (torch == null || !subscribedTorches.Add(torch))
        {
            return;
        }

        torch.StateChanged += OnTorchStateChanged;
    }

    private void UnsubscribeTorch(Torch torch)
    {
        if (torch != null)
        {
            torch.StateChanged -= OnTorchStateChanged;
        }

        subscribedTorches.Remove(torch);
    }

    private void SubscribeBrasero(Brasero brasero)
    {
        if (brasero == null || !subscribedBraseros.Add(brasero))
        {
            return;
        }

        brasero.StateChanged += OnBraseroStateChanged;
    }

    private void UnsubscribeBrasero(Brasero brasero)
    {
        if (brasero != null)
        {
            brasero.StateChanged -= OnBraseroStateChanged;
        }

        subscribedBraseros.Remove(brasero);
    }

    private void UnsubscribeAllSourceEvents()
    {
        foreach (Torch torch in subscribedTorches)
        {
            if (torch != null)
            {
                torch.StateChanged -= OnTorchStateChanged;
            }
        }

        subscribedTorches.Clear();
        staleSubscribedTorches.Clear();

        foreach (Brasero brasero in subscribedBraseros)
        {
            if (brasero != null)
            {
                brasero.StateChanged -= OnBraseroStateChanged;
            }
        }

        subscribedBraseros.Clear();
        staleSubscribedBraseros.Clear();
    }

    private void OnTorchStateChanged(Torch torch, bool isLit)
    {
        ApplyInfluenceSourceStateChange();
    }

    private void OnBraseroStateChanged(Brasero brasero, bool isLit)
    {
        ApplyInfluenceSourceStateChange();
    }

    private void ApplyInfluenceSourceStateChange()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        RefreshInfluenceSources();
        ApplyInfluenceStates();
        nextInfluenceRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, influenceRefreshInterval);
    }

    private void ApplyInfluenceStates()
    {
        RemoveMissingCommonLights();
        activationRequests.Clear();

        float now = Time.unscaledTime;
        CollectTorchInfluenceRequests(now);
        CollectBraseroInfluenceRequests(now);
        CollectCommonLightPropagationRequests(now);
        ApplyActivationRequests(now);
    }

    private void CollectTorchInfluenceRequests(float now)
    {
        for (int i = 0; i < torches.Count; i++)
        {
            Torch torch = torches[i];
            if (torch == null || !torch.isActiveAndEnabled || !torch.IsLit)
            {
                continue;
            }

            CollectSourceInfluenceRequests(
                torch.transform,
                torch,
                torch.FlameColor,
                torch.CommonLightActivationOrder,
                (entry) => torch.ProvidesLitInfluenceTo(entry.Collider, entry.ResolveWorldPoint(useRendererBoundsCenter)),
                now);
        }
    }

    private void CollectBraseroInfluenceRequests(float now)
    {
        for (int i = 0; i < braseros.Count; i++)
        {
            Brasero brasero = braseros[i];
            if (brasero == null || !brasero.isActiveAndEnabled || !brasero.IsLit)
            {
                continue;
            }

            CollectSourceInfluenceRequests(
                brasero.transform,
                brasero,
                brasero.FlameColor,
                brasero.CommonLightActivationOrder,
                (entry) => brasero.ProvidesLitInfluenceTo(entry.Collider, entry.ResolveWorldPoint(useRendererBoundsCenter)),
                now);
        }
    }

    private void CollectSourceInfluenceRequests(
        Transform sourceTransform,
        MonoBehaviour rootSource,
        Color sourceColor,
        IReadOnlyList<GameObject> sourceOrder,
        Func<CommonLightEntry, bool> touchesEntry,
        float now)
    {
        if (sourceTransform == null || touchesEntry == null)
        {
            return;
        }

        sourceOrderedBuffer.Clear();
        for (int i = 0; i < commonLights.Count; i++)
        {
            CommonLightEntry entry = commonLights[i];
            if (entry == null || !entry.HasAnyControllable || entry.ContainsTransform(sourceTransform))
            {
                continue;
            }

            if (touchesEntry(entry))
            {
                sourceOrderedBuffer.Add(entry);
            }
        }

        sourceOrderedBuffer.Sort((left, right) => CompareForSourceOrder(sourceTransform.position, sourceOrder, left, right));

        for (int i = 0; i < sourceOrderedBuffer.Count; i++)
        {
            CommonLightEntry entry = sourceOrderedBuffer[i];
            float dueTime = now + activationDelay + (i * orderedActivationStepDelay);
            float distanceSqr = (entry.ResolveWorldPoint(useRendererBoundsCenter) - sourceTransform.position).sqrMagnitude;
            int orderIndex = ResolveOrderIndex(sourceOrder, entry);
            AddActivationRequest(entry, sourceColor, rootSource, dueTime, orderIndex, distanceSqr);
        }
    }

    private void CollectCommonLightPropagationRequests(float now)
    {
        for (int i = 0; i < commonLights.Count; i++)
        {
            CommonLightEntry source = commonLights[i];
            if (source == null || !source.IsActive || !IsRootSourceStillActive(source.ActiveRootSource))
            {
                continue;
            }

            sourceOrderedBuffer.Clear();
            for (int targetIndex = 0; targetIndex < commonLights.Count; targetIndex++)
            {
                CommonLightEntry target = commonLights[targetIndex];
                if (target == null || target == source || !target.HasAnyControllable)
                {
                    continue;
                }

                if (TouchesCommonLightInfluence(source, target))
                {
                    sourceOrderedBuffer.Add(target);
                }
            }

            Vector3 sourcePoint = source.ResolveWorldPoint(useRendererBoundsCenter);
            sourceOrderedBuffer.Sort((left, right) => CompareByDistanceAndHierarchy(sourcePoint, left, right));

            for (int targetIndex = 0; targetIndex < sourceOrderedBuffer.Count; targetIndex++)
            {
                CommonLightEntry target = sourceOrderedBuffer[targetIndex];
                float dueTime = now + activationDelay + (targetIndex * orderedActivationStepDelay);
                float distanceSqr = (target.ResolveWorldPoint(useRendererBoundsCenter) - sourcePoint).sqrMagnitude;
                AddActivationRequest(target, source.ActiveColor, source.ActiveRootSource, dueTime, int.MaxValue, distanceSqr);
            }
        }
    }

    private void ApplyActivationRequests(float now)
    {
        for (int i = 0; i < commonLights.Count; i++)
        {
            CommonLightEntry entry = commonLights[i];
            if (entry == null)
            {
                continue;
            }

            if (activationRequests.TryGetValue(entry, out ActivationRequest request))
            {
                if (entry.IsActive)
                {
                    entry.CancelPendingActivation();
                    entry.UpdateActiveState(request.Color, request.RootSource, colorUnityLightsFromSource);
                    continue;
                }

                entry.ScheduleActivation(request.DueTime, request.Color, request.RootSource);
                if (entry.HasPendingActivation && now >= entry.PendingActivationTime)
                {
                    entry.ApplyActiveState(entry.PendingColor, entry.PendingRootSource, colorUnityLightsFromSource);
                }

                continue;
            }

            entry.CancelPendingActivation();
            if (entry.IsActive)
            {
                entry.ApplyInactiveState();
            }
        }
    }

    private void AddActivationRequest(
        CommonLightEntry entry,
        Color color,
        MonoBehaviour rootSource,
        float dueTime,
        int orderIndex,
        float distanceSqr)
    {
        if (entry == null || rootSource == null)
        {
            return;
        }

        ActivationRequest request = new ActivationRequest(color, rootSource, dueTime, orderIndex, distanceSqr);
        if (!activationRequests.TryGetValue(entry, out ActivationRequest existing) || request.IsBetterThan(existing))
        {
            activationRequests[entry] = request;
        }
    }

    private bool TouchesCommonLightInfluence(CommonLightEntry source, CommonLightEntry target)
    {
        if (source == null || target == null)
        {
            return false;
        }

        Vector3 center = source.ResolveWorldPoint(useRendererBoundsCenter);
        float radius = source.ResolveInfluenceRadius(commonLightInfluenceRadius, useUnityLightRangeAsCommonInfluence);
        if (radius <= 0f)
        {
            return false;
        }

        Vector3 targetPoint = target.ResolveWorldPoint(useRendererBoundsCenter);
        if (target.Collider != null)
        {
            targetPoint = target.Collider.ClosestPoint(center);
        }

        return (targetPoint - center).sqrMagnitude <= radius * radius;
    }

    private bool IsRootSourceStillActive(MonoBehaviour rootSource)
    {
        if (rootSource is Torch torch)
        {
            return torch != null && torch.isActiveAndEnabled && torch.IsLit;
        }

        if (rootSource is Brasero brasero)
        {
            return brasero != null && brasero.isActiveAndEnabled && brasero.IsLit;
        }

        return rootSource != null && activeRootSources.Contains(rootSource);
    }

    private bool TryResolveCommonLightRoot(Transform start, out GameObject root)
    {
        root = null;
        Transform current = start;
        while (current != null)
        {
            if (IsCommonLightTagged(current.gameObject))
            {
                root = current.gameObject;
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool IsCommonLightTagged(GameObject candidate)
    {
        return candidate != null
            && !string.IsNullOrWhiteSpace(commonLightTag)
            && string.Equals(candidate.tag, commonLightTag, StringComparison.Ordinal);
    }

    private int CompareForSourceOrder(
        Vector3 sourcePosition,
        IReadOnlyList<GameObject> sourceOrder,
        CommonLightEntry left,
        CommonLightEntry right)
    {
        int leftOrder = ResolveOrderIndex(sourceOrder, left);
        int rightOrder = ResolveOrderIndex(sourceOrder, right);
        if (leftOrder != rightOrder)
        {
            return leftOrder.CompareTo(rightOrder);
        }

        return CompareByDistanceAndHierarchy(sourcePosition, left, right);
    }

    private int CompareByDistanceAndHierarchy(Vector3 sourcePosition, CommonLightEntry left, CommonLightEntry right)
    {
        float leftDistance = (left.ResolveWorldPoint(useRendererBoundsCenter) - sourcePosition).sqrMagnitude;
        float rightDistance = (right.ResolveWorldPoint(useRendererBoundsCenter) - sourcePosition).sqrMagnitude;
        int distanceComparison = leftDistance.CompareTo(rightDistance);
        if (distanceComparison != 0)
        {
            return distanceComparison;
        }

        return string.CompareOrdinal(left.Root != null ? left.Root.name : string.Empty, right.Root != null ? right.Root.name : string.Empty);
    }

    private int ResolveOrderIndex(IReadOnlyList<GameObject> sourceOrder, CommonLightEntry entry)
    {
        if (sourceOrder == null || entry == null || entry.Root == null)
        {
            return int.MaxValue;
        }

        for (int i = 0; i < sourceOrder.Count; i++)
        {
            GameObject orderedObject = sourceOrder[i];
            if (MatchesCommonLightEntry(orderedObject, entry))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static bool MatchesCommonLightEntry(GameObject orderedObject, CommonLightEntry entry)
    {
        if (orderedObject == null || entry == null || entry.Root == null)
        {
            return false;
        }

        Transform orderedTransform = orderedObject.transform;
        Transform entryTransform = entry.Root.transform;
        return orderedTransform == entryTransform
            || orderedTransform.IsChildOf(entryTransform)
            || entryTransform.IsChildOf(orderedTransform);
    }

    private void ScheduleNextScans()
    {
        float now = Time.unscaledTime;
        nextCommonLightScanTime = now + Mathf.Max(0.05f, commonLightScanInterval);
        nextInfluenceRefreshTime = now + Mathf.Max(0.05f, influenceRefreshInterval);
    }

    private sealed class CommonLightEntry
    {
        private Light[] lights = Array.Empty<Light>();
        private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();
        private Collider collider;

        public CommonLightEntry(GameObject root)
        {
            Root = root;
        }

        public GameObject Root { get; }
        public Collider Collider => collider;
        public bool IsActive { get; private set; }
        public MonoBehaviour ActiveRootSource { get; private set; }
        public Color ActiveColor { get; private set; } = Color.white;
        public bool HasPendingActivation { get; private set; }
        public float PendingActivationTime { get; private set; }
        public Color PendingColor { get; private set; } = Color.white;
        public MonoBehaviour PendingRootSource { get; private set; }
        public bool HasAnyControllable => lights.Length > 0 || particleSystems.Length > 0;

        public void Refresh(bool disablePlayOnAwake)
        {
            if (Root == null)
            {
                lights = Array.Empty<Light>();
                particleSystems = Array.Empty<ParticleSystem>();
                collider = null;
                return;
            }

            lights = Root.GetComponentsInChildren<Light>(true);
            particleSystems = Root.GetComponentsInChildren<ParticleSystem>(true);
            collider = Root.GetComponent<Collider>();
            if (collider == null)
            {
                collider = Root.GetComponentInChildren<Collider>(true);
            }

            if (!disablePlayOnAwake)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem system = particleSystems[i];
                if (system == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
            }
        }

        public bool IsValid(string expectedTag)
        {
            return Root != null
                && Root.scene.IsValid()
                && !string.IsNullOrWhiteSpace(expectedTag)
                && string.Equals(Root.tag, expectedTag, StringComparison.Ordinal);
        }

        public bool ContainsTransform(Transform transform)
        {
            return Root != null
                && transform != null
                && (transform.IsChildOf(Root.transform) || Root.transform.IsChildOf(transform));
        }

        public Vector3 ResolveWorldPoint(bool useRendererBoundsCenter)
        {
            if (Root == null)
            {
                return Vector3.zero;
            }

            if (useRendererBoundsCenter && TryResolveRendererBounds(out Bounds bounds))
            {
                return bounds.center;
            }

            if (collider != null)
            {
                return collider.bounds.center;
            }

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    return lights[i].transform.position;
                }
            }

            return Root.transform.position;
        }

        public float ResolveInfluenceRadius(float defaultRadius, bool includeLightRange)
        {
            float radius = Mathf.Max(0f, defaultRadius);
            if (!includeLightRange)
            {
                return radius;
            }

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null)
                {
                    radius = Mathf.Max(radius, light.range);
                }
            }

            return radius;
        }

        public void ScheduleActivation(float dueTime, Color color, MonoBehaviour rootSource)
        {
            if (rootSource == null)
            {
                return;
            }

            if (HasPendingActivation && PendingActivationTime <= dueTime)
            {
                PendingColor = color;
                PendingRootSource = rootSource;
                return;
            }

            HasPendingActivation = true;
            PendingActivationTime = dueTime;
            PendingColor = color;
            PendingRootSource = rootSource;
        }

        public void CancelPendingActivation()
        {
            HasPendingActivation = false;
            PendingActivationTime = 0f;
            PendingRootSource = null;
        }

        public void UpdateActiveState(Color color, MonoBehaviour rootSource, bool colorUnityLights)
        {
            ActiveRootSource = rootSource;
            ActiveColor = color;
            ApplyColor(color, colorUnityLights);
        }

        public void ApplyActiveState(Color color, MonoBehaviour rootSource, bool colorUnityLights)
        {
            CancelPendingActivation();
            IsActive = true;
            ActiveRootSource = rootSource;
            ActiveColor = color;
            ApplyColor(color, colorUnityLights);

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null)
                {
                    light.enabled = true;
                }
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem system = particleSystems[i];
                if (system != null && !system.isPlaying)
                {
                    system.Play(false);
                }
            }
        }

        public void ApplyInactiveState()
        {
            CancelPendingActivation();
            IsActive = false;
            ActiveRootSource = null;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem system = particleSystems[i];
                if (system != null)
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null)
                {
                    light.enabled = false;
                }
            }
        }

        private void ApplyColor(Color color, bool colorUnityLights)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem system = particleSystems[i];
                if (system == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = system.main;
                main.startColor = color;
            }

            if (!colorUnityLights)
            {
                return;
            }

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null)
                {
                    light.color = color;
                }
            }
        }

        private bool TryResolveRendererBounds(out Bounds resolvedBounds)
        {
            resolvedBounds = new Bounds();
            bool hasBounds = false;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem system = particleSystems[i];
                Renderer renderer = system != null ? system.GetComponent<Renderer>() : null;
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    resolvedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    resolvedBounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }
    }

    private readonly struct ActivationRequest
    {
        public ActivationRequest(Color color, MonoBehaviour rootSource, float dueTime, int orderIndex, float distanceSqr)
        {
            Color = color;
            RootSource = rootSource;
            DueTime = dueTime;
            OrderIndex = orderIndex;
            DistanceSqr = distanceSqr;
        }

        public Color Color { get; }
        public MonoBehaviour RootSource { get; }
        public float DueTime { get; }
        public int OrderIndex { get; }
        public float DistanceSqr { get; }

        public bool IsBetterThan(ActivationRequest other)
        {
            if (!Mathf.Approximately(DueTime, other.DueTime))
            {
                return DueTime < other.DueTime;
            }

            if (OrderIndex != other.OrderIndex)
            {
                return OrderIndex < other.OrderIndex;
            }

            return DistanceSqr < other.DistanceSqr;
        }
    }
}

internal static class LitFlameColorUtility
{
    public static Color ResolveFlameColor(Light flameLight, GameObject flameObject, Color fallback)
    {
        if (flameLight != null)
        {
            return flameLight.color;
        }

        if (flameObject == null)
        {
            return fallback;
        }

        ParticleSystem[] particleSystems = flameObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (TryResolveParticleColor(particleSystems[i], out Color color))
            {
                return color;
            }
        }

        return fallback;
    }

    public static bool TryResolveParticleColor(ParticleSystem system, out Color color)
    {
        color = Color.white;
        if (system == null)
        {
            return false;
        }

        ParticleSystem.MinMaxGradient startColor = system.main.startColor;
        switch (startColor.mode)
        {
            case ParticleSystemGradientMode.Color:
                color = startColor.color;
                return true;
            case ParticleSystemGradientMode.TwoColors:
                color = Color.Lerp(startColor.colorMin, startColor.colorMax, 0.5f);
                return true;
            case ParticleSystemGradientMode.Gradient:
                if (startColor.gradient != null)
                {
                    color = startColor.gradient.Evaluate(1f);
                    return true;
                }
                break;
            case ParticleSystemGradientMode.TwoGradients:
                if (startColor.gradientMin != null && startColor.gradientMax != null)
                {
                    color = Color.Lerp(startColor.gradientMin.Evaluate(1f), startColor.gradientMax.Evaluate(1f), 0.5f);
                    return true;
                }
                break;
            case ParticleSystemGradientMode.RandomColor:
                if (startColor.gradient != null)
                {
                    color = startColor.gradient.Evaluate(1f);
                    return true;
                }
                break;
        }

        return false;
    }
}
