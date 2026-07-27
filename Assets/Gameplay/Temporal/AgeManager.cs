using System;
using System.Collections.Generic;
using UnityEngine;

// Source canonique de l'age de scene.
// La logique gameplay doit lire ce manager pour eviter les calculs divergents.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class AgeManager : MonoBehaviour
{
    public const int DefaultStartYear = TemporalAgeUtility.MaxYear;
    public const int DefaultYearsPerAncientFlame = TemporalAgeUtility.StepYears;

    public static AgeManager ActiveInstance { get; private set; }

    [Header("Ancient Flames")]
    [SerializeField, Tooltip("Collecte automatiquement les AncientFlame de la scene chargee.")]
    private bool autoCollectSceneAncientFlames = true;
    [SerializeField, Tooltip("Inclut les AncientFlame inactifs dans le calcul, utile pour les objets caches au demarrage.")]
    private bool includeInactiveAncientFlames = true;
    [SerializeField, Tooltip("Seuls ces AncientFlame pilotent l'AgeManager.")]
    private List<Flame> ancientFlames = new List<Flame>();

    [Header("Age")]
    [SerializeField, Tooltip("Annee de depart du joueur.")]
    private int startYear = DefaultStartYear;
    [SerializeField, Tooltip("Ecrit un log quand l'age change.")]
    private bool logAgeChanges;

    [Header("Master Shader Runtime")]
    [SerializeField, Tooltip("Pousse l'age global vers _AgeAmount sans instancier les materiaux.")]
    private bool driveMasterShaderAgeAmount = true;
    [SerializeField] private string masterShaderAgeAmountProperty = "_AgeAmount";
    [SerializeField, Tooltip("Inclut les renderers inactifs pour que les objets reveles plus tard aient deja le bon age.")]
    private bool includeInactiveMasterShaderRenderers = true;

    [Header("State")]
    [SerializeField, Tooltip("Nombre de Flames qui pilotent l'age et sont allumes.")]
    private int litAncientFlameCount;
    [SerializeField, Tooltip("Annee courante canonique.")]
    private int currentYear = DefaultStartYear;

    private readonly HashSet<Flame> subscribedFlames = new HashSet<Flame>();
    private readonly List<Renderer> shaderAgeRenderers = new List<Renderer>();
    private MaterialPropertyBlock shaderAgePropertyBlock;
    private int masterShaderAgeAmountPropertyId;
    private bool shaderAgeRendererCacheDirty = true;

    public IReadOnlyList<Flame> AncientFlames => ancientFlames;
    public int TotalAncientFlameCount => CountAncientFlames();
    public int LitAncientFlameCount => litAncientFlameCount;
    public int CurrentYear => currentYear;
    public int CurrentYearOffsetFromStart => Mathf.Max(0, startYear - currentYear);
    public int StartYear => startYear;
    public int YearsPerAncientFlame => DefaultYearsPerAncientFlame;
    public TemporalAge CurrentTemporalAge => TemporalAgeUtility.IntToAge(currentYear);
    public int CurrentTemporalAgeStep => TemporalAgeUtility.AgeToStep(CurrentTemporalAge);

    public event Action<AgeManager, int, int> AgeChanged;

    public static AgeManager GetOrCreate()
    {
        if (ActiveInstance != null)
        {
            return ActiveInstance;
        }

#if UNITY_2023_1_OR_NEWER
        AgeManager existing = FindAnyObjectByType<AgeManager>();
#else
        AgeManager existing = FindAnyObjectByType<AgeManager>();
#endif
        if (existing != null)
        {
            ActiveInstance = existing;
            return existing;
        }

        if (!Application.isPlaying)
        {
            return null;
        }

        GameObject host = new GameObject("AgeManager");
        return host.AddComponent<AgeManager>();
    }

    private void OnEnable()
    {
        if (ActiveInstance != null && ActiveInstance != this)
        {
            Debug.LogWarning("Plusieurs AgeManager actifs. Le premier reste la source canonique.", this);
        }
        else
        {
            ActiveInstance = this;
        }

        CacheShaderPropertyIds();
        RefreshAndResubscribe();
    }

    private void OnDisable()
    {
        UnsubscribeFromFlames();
        shaderAgeRenderers.Clear();
        shaderAgeRendererCacheDirty = true;

        if (ReferenceEquals(ActiveInstance, this))
        {
            ActiveInstance = null;
        }
    }

    private void OnValidate()
    {
        startYear = Mathf.Clamp(startYear, TemporalAgeUtility.MinYear, TemporalAgeUtility.MaxYear);
        currentYear = ClampYear(currentYear);
        CacheShaderPropertyIds();
        shaderAgeRendererCacheDirty = true;
    }

    public void RefreshAndResubscribe()
    {
        UnsubscribeFromFlames();
        RefreshFlames();
        SubscribeToFlames();
        RecalculateAge(rescanTimePeriodVisibility: true);
    }

    public void RefreshFlames()
    {
        RemoveMissingAncientFlames();

        if (!autoCollectSceneAncientFlames)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        Flame[] sceneFlames = includeInactiveAncientFlames
            ? FindObjectsByType<Flame>(FindObjectsInactive.Include)
            : FindObjectsByType<Flame>();
#else
        Flame[] sceneFlames = includeInactiveAncientFlames
            ? FindObjectsByType<Flame>(FindObjectsInactive.Include)
            : FindObjectsByType<Flame>();
#endif
        AddAncientFlames(sceneFlames);
    }

    public void RecalculateAge(bool rescanTimePeriodVisibility = true)
    {
        int previousYear = currentYear;
        int previousLitCount = litAncientFlameCount;

        litAncientFlameCount = CountLitAncientFlames();
        currentYear = CalculateYearForLitCount(litAncientFlameCount);

        bool changed = previousYear != currentYear || previousLitCount != litAncientFlameCount;
        if (logAgeChanges && changed)
        {
            Debug.Log(
                $"[AgeManager] litFlames={litAncientFlameCount}/{TotalAncientFlameCount} currentYear={currentYear}",
                this);
        }

        if (changed)
        {
            AgeChanged?.Invoke(this, previousYear, currentYear);
        }

        TimePeriodVisibility.RefreshAllForAgeManager(this, rescanTimePeriodVisibility);
        AncientFlameDisplayManager.RefreshAllDisplays();
        ApplyShaderAgeAmountToScene();
    }

    public int CalculateYearForLitCount(int litCount)
    {
        int count = Mathf.Max(0, litCount);
        return ClampYear(startYear - count * DefaultYearsPerAncientFlame);
    }

    public bool IsYearInCurrentAge(int year)
    {
        return ClampYear(year) == currentYear;
    }

    public int GetComparisonValue(TimePeriodValueMode valueMode)
    {
        switch (valueMode)
        {
            case TimePeriodValueMode.YearOffsetFromBase:
                return CurrentYearOffsetFromStart;

            case TimePeriodValueMode.LitAncientFlameCount:
                return LitAncientFlameCount;

            case TimePeriodValueMode.TemporalAgeYear:
                return TemporalAgeUtility.AgeToInt(CurrentTemporalAge);

            case TimePeriodValueMode.TemporalAgeStep:
                return CurrentTemporalAgeStep;

            case TimePeriodValueMode.AbsoluteYear:
            default:
                return CurrentYear;
        }
    }

    public void ApplyShaderAgeAmountToScene()
    {
        if (!CanDriveMasterShaderAgeAmount())
        {
            return;
        }

        EnsureShaderAgeRendererCache();
        for (int i = shaderAgeRenderers.Count - 1; i >= 0; i--)
        {
            Renderer renderer = shaderAgeRenderers[i];
            if (renderer == null)
            {
                shaderAgeRenderers.RemoveAt(i);
                continue;
            }

            ApplyShaderAgePropertyBlock(renderer);
        }
    }

    public void RefreshShaderAgeRendererCache()
    {
        shaderAgeRendererCacheDirty = false;
        shaderAgeRenderers.Clear();

        if (!CanDriveMasterShaderAgeAmount())
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        Renderer[] renderers = includeInactiveMasterShaderRenderers
            ? FindObjectsByType<Renderer>(FindObjectsInactive.Include)
            : FindObjectsByType<Renderer>();
#else
        Renderer[] renderers = includeInactiveMasterShaderRenderers
            ? FindObjectsByType<Renderer>(FindObjectsInactive.Include)
            : FindObjectsByType<Renderer>();
#endif

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (RendererHasProperty(renderer, masterShaderAgeAmountPropertyId))
            {
                shaderAgeRenderers.Add(renderer);
            }
        }
    }

    public void ApplyShaderAgeToRenderer(Renderer renderer)
    {
        if (!CanDriveMasterShaderAgeAmount() || renderer == null)
        {
            return;
        }

        if (!RendererHasProperty(renderer, masterShaderAgeAmountPropertyId))
        {
            return;
        }

        ApplyShaderAgePropertyBlock(renderer);
    }

    private void EnsureShaderAgeRendererCache()
    {
        if (shaderAgeRendererCacheDirty)
        {
            RefreshShaderAgeRendererCache();
        }
    }

    private void ApplyShaderAgePropertyBlock(Renderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        if (shaderAgePropertyBlock == null)
        {
            shaderAgePropertyBlock = new MaterialPropertyBlock();
        }

        // AgeAmount and DissolveAmount intentionally share the same MPB surface:
        // each driver only writes its own property so ageing never changes visibility.
        renderer.GetPropertyBlock(shaderAgePropertyBlock);
        shaderAgePropertyBlock.SetFloat(masterShaderAgeAmountPropertyId, currentYear);
        renderer.SetPropertyBlock(shaderAgePropertyBlock);
    }

    private int CountLitAncientFlames()
    {
        int count = 0;

        if (ancientFlames != null)
        {
            for (int i = 0; i < ancientFlames.Count; i++)
            {
                Flame flame = ancientFlames[i];
                if (flame != null && flame.IsAncientFlame && flame.IsEffectivelyLit)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int CountAncientFlames()
    {
        int count = 0;

        if (ancientFlames != null)
        {
            for (int i = 0; i < ancientFlames.Count; i++)
            {
                Flame flame = ancientFlames[i];
                if (flame != null && flame.IsAncientFlame)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void SubscribeToFlames()
    {
        if (ancientFlames == null)
        {
            return;
        }

        for (int i = 0; i < ancientFlames.Count; i++)
        {
            Flame flame = ancientFlames[i];
            SubscribeToFlame(flame);
        }
    }

    private void UnsubscribeFromFlames()
    {
        foreach (Flame flame in subscribedFlames)
        {
            if (flame != null)
            {
                flame.StateChanged -= OnFlameStateChanged;
            }
        }

        subscribedFlames.Clear();
    }

    private void OnFlameStateChanged(Flame flame, bool lit)
    {
        RecalculateAge();
    }

    private void SubscribeToFlame(Flame flame)
    {
        if (flame == null || !flame.IsAncientFlame || subscribedFlames.Contains(flame))
        {
            return;
        }

        flame.StateChanged += OnFlameStateChanged;
        subscribedFlames.Add(flame);
    }

    private void AddAncientFlames(IList<Flame> source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            AddAncientFlame(source[i]);
        }
    }

    private void AddAncientFlames(Flame[] source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            AddAncientFlame(source[i]);
        }
    }

    private void AddAncientFlame(Flame flame)
    {
        if (flame == null || !flame.IsAncientFlame)
        {
            return;
        }

        if (ancientFlames == null)
        {
            ancientFlames = new List<Flame>();
        }

        if (!ancientFlames.Contains(flame))
        {
            ancientFlames.Add(flame);
        }
    }

    private void RemoveMissingAncientFlames()
    {
        if (ancientFlames == null || ancientFlames.Count == 0)
        {
            return;
        }

        for (int i = ancientFlames.Count - 1; i >= 0; i--)
        {
            Flame flame = ancientFlames[i];
            if (flame == null || !flame.IsAncientFlame)
            {
                ancientFlames.RemoveAt(i);
            }
        }
    }

    private static int ClampYear(int year)
    {
        return Mathf.Clamp(year, TemporalAgeUtility.MinYear, TemporalAgeUtility.MaxYear);
    }

    private void CacheShaderPropertyIds()
    {
        masterShaderAgeAmountPropertyId = string.IsNullOrWhiteSpace(masterShaderAgeAmountProperty)
            ? 0
            : Shader.PropertyToID(masterShaderAgeAmountProperty);
    }

    private bool CanDriveMasterShaderAgeAmount()
    {
        return driveMasterShaderAgeAmount && masterShaderAgeAmountPropertyId != 0;
    }

    private static bool RendererHasProperty(Renderer renderer, int propertyId)
    {
        Material[] materials = renderer != null ? renderer.sharedMaterials : null;
        if (materials == null)
        {
            return false;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && material.HasProperty(propertyId))
            {
                return true;
            }
        }

        return false;
    }
}
