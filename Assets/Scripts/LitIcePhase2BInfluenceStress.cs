#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// Development-only deterministic influence load used by the Phase 2B Player benchmark.
/// It never appears in production scenes or non-development builds.
/// </summary>
[DisallowMultipleComponent]
public sealed class LitIcePhase2BInfluenceStress : MonoBehaviour
{
    private const int InfluenceCount = 8;
    private const float TransitionDuration = 0.5f;
    private const float TogglePeriod = 3f;

    [SerializeField] private bool legacyPhase2AMode;
    [SerializeField] private float influenceRadius = 12f;

    private readonly LitIcePhase2BInfluenceToken[] sources =
        new LitIcePhase2BInfluenceToken[InfluenceCount];
    private Renderer[] targets;
    private float elapsed;
    private int lastPeriod = -1;
    private bool registered;

    public void Configure(bool legacyMode, float radius = 12f)
    {
        legacyPhase2AMode = legacyMode;
        influenceRadius = Mathf.Max(0.1f, radius);
    }

    private void Awake()
    {
        FlameInfluenceMaterialRuntime.ConfigurePhase2BBenchmark(legacyPhase2AMode);
        targets = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            var sourceObject = new GameObject($"Phase2B Influence {i:00}");
            sourceObject.transform.SetParent(transform, false);
            float angle = i * Mathf.PI * 2f / InfluenceCount;
            sourceObject.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * 4f,
                1.5f + (i % 2) * 1.25f,
                Mathf.Sin(angle) * 4f);
            sources[i] = sourceObject.AddComponent<LitIcePhase2BInfluenceToken>();
        }
    }

    private void OnEnable()
    {
        elapsed = 0f;
        lastPeriod = -1;
    }

    private void Start()
    {
        RegisterAll();
        lastPeriod = 0;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        int period = Mathf.FloorToInt(elapsed / TogglePeriod);
        if (period == lastPeriod)
            return;

        lastPeriod = period;
        if ((period & 1) == 0)
            RegisterAll();
        else
            UnregisterAll();
    }

    private void OnDisable()
    {
        UnregisterAll();
    }

    private void RegisterAll()
    {
        if (registered || targets == null)
            return;

        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            LitIcePhase2BInfluenceToken source = sources[sourceIndex];
            if (source == null)
                continue;

            var info = new LitInfluenceInfo(
                source,
                LitInfluenceSourceKind.Flame,
                source.transform.position,
                influenceRadius,
                TransitionDuration);
            for (int rendererIndex = 0; rendererIndex < targets.Length; rendererIndex++)
                FlameInfluenceMaterialRuntime.RegisterOrUpdate(info, targets[rendererIndex]);
        }

        registered = true;
    }

    private void UnregisterAll()
    {
        if (!registered || targets == null)
            return;

        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            LitIcePhase2BInfluenceToken source = sources[sourceIndex];
            if (source == null)
                continue;

            int sourceId = source.GetInstanceID();
            for (int rendererIndex = 0; rendererIndex < targets.Length; rendererIndex++)
                FlameInfluenceMaterialRuntime.Unregister(sourceId, targets[rendererIndex]);
        }

        registered = false;
    }
}

public sealed class LitIcePhase2BInfluenceToken : MonoBehaviour
{
}
#endif
