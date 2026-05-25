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
    public const int DefaultYearsPerBrasero = 100;

    public static AgeManager ActiveInstance { get; private set; }

    [Header("Ancient Braseros")]
    [SerializeField, Tooltip("Collecte automatiquement les Braseros anciens de la scene chargee.")]
    private bool autoCollectSceneAncientBraseros = true;
    [SerializeField, Tooltip("Inclut les Braseros anciens inactifs dans le calcul, utile pour les objets caches au demarrage.")]
    private bool includeInactiveAncientBraseros = true;
    [SerializeField, Tooltip("Seuls ces Braseros anciens pilotent l'AgeManager.")]
    private List<Brasero> ancientBraseros = new List<Brasero>();

    [Header("Age")]
    [SerializeField, Tooltip("Annee de depart du joueur.")]
    private int startYear = DefaultStartYear;
    [SerializeField, Tooltip("Annees reculees par Brasero allume qui pilote l'age.")]
    private int yearsPerBrasero = DefaultYearsPerBrasero;
    [SerializeField, Tooltip("Ecrit un log quand l'age change.")]
    private bool logAgeChanges;

    [Header("Master Shader Runtime")]
    [SerializeField, Tooltip("Pousse l'age global vers _AgeAmount sans instancier les materiaux.")]
    private bool driveMasterShaderAgeAmount = true;
    [SerializeField] private string masterShaderAgeAmountProperty = "_AgeAmount";
    [SerializeField, Tooltip("Inclut les renderers inactifs pour que les objets reveles plus tard aient deja le bon age.")]
    private bool includeInactiveMasterShaderRenderers = true;

    [Header("State")]
    [SerializeField, Tooltip("Nombre de Braseros qui pilotent l'age et sont allumes.")]
    private int litBrazierCount;
    [SerializeField, Tooltip("Annee courante canonique.")]
    private int currentYear = DefaultStartYear;

    private readonly HashSet<Brasero> subscribedBraseros = new HashSet<Brasero>();
    private MaterialPropertyBlock shaderAgePropertyBlock;
    private int masterShaderAgeAmountPropertyId;

    public IReadOnlyList<Brasero> Braseros => ancientBraseros;
    public IReadOnlyList<Brasero> AncientBraseros => ancientBraseros;
    public int TotalAgeDrivingBraseroCount => CountAncientBraseros();
    public int LitBrazierCount => litBrazierCount;
    public int CurrentYear => currentYear;
    public int CurrentYearOffsetFromStart => Mathf.Max(0, startYear - currentYear);
    public int StartYear => startYear;
    public int YearsPerBrasero => yearsPerBrasero;
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
        AgeManager existing = FindFirstObjectByType<AgeManager>();
#else
        AgeManager existing = FindObjectOfType<AgeManager>();
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
        UnsubscribeFromBraseros();

        if (ReferenceEquals(ActiveInstance, this))
        {
            ActiveInstance = null;
        }
    }

    private void OnValidate()
    {
        startYear = Mathf.Clamp(startYear, TemporalAgeUtility.MinYear, TemporalAgeUtility.MaxYear);
        yearsPerBrasero = Mathf.Max(1, yearsPerBrasero);
        currentYear = ClampYear(currentYear);
        CacheShaderPropertyIds();
    }

    public void RefreshAndResubscribe()
    {
        UnsubscribeFromBraseros();
        RefreshBraseros();
        SubscribeToBraseros();
        RecalculateAge(rescanTimePeriodVisibility: true);
    }

    public void RefreshBraseros()
    {
        RemoveMissingAncientBraseros();

        if (!autoCollectSceneAncientBraseros)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        Brasero[] sceneBraseros = includeInactiveAncientBraseros
            ? FindObjectsByType<Brasero>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : FindObjectsByType<Brasero>(FindObjectsSortMode.None);
#else
        Brasero[] sceneBraseros = FindObjectsOfType<Brasero>(includeInactiveAncientBraseros);
#endif
        AddAncientBraseros(sceneBraseros);
    }

    public void RecalculateAge(bool rescanTimePeriodVisibility = true)
    {
        int previousYear = currentYear;
        int previousLitCount = litBrazierCount;

        litBrazierCount = CountLitBraseros();
        currentYear = CalculateYearForLitCount(litBrazierCount);

        bool changed = previousYear != currentYear || previousLitCount != litBrazierCount;
        if (logAgeChanges && changed)
        {
            Debug.Log(
                $"[AgeManager] litBraseros={litBrazierCount}/{TotalAgeDrivingBraseroCount} currentYear={currentYear}",
                this);
        }

        if (changed)
        {
            AgeChanged?.Invoke(this, previousYear, currentYear);
        }

        TimePeriodVisibility.RefreshAllForAgeManager(this, rescanTimePeriodVisibility);
        BraseroDisplayManager.RefreshAllDisplays();
        ApplyShaderAgeAmountToScene();
    }

    public int CalculateYearForLitCount(int litCount)
    {
        int count = Mathf.Max(0, litCount);
        return ClampYear(startYear - count * yearsPerBrasero);
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

            case TimePeriodValueMode.LitBrazierCount:
                return LitBrazierCount;

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

#if UNITY_2023_1_OR_NEWER
        Renderer[] renderers = includeInactiveMasterShaderRenderers
            ? FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#else
        Renderer[] renderers = FindObjectsOfType<Renderer>(includeInactiveMasterShaderRenderers);
#endif

        for (int i = 0; i < renderers.Length; i++)
        {
            ApplyShaderAgeToRenderer(renderers[i]);
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

    private int CountLitBraseros()
    {
        int count = 0;

        if (ancientBraseros != null)
        {
            for (int i = 0; i < ancientBraseros.Count; i++)
            {
                Brasero brasero = ancientBraseros[i];
                if (brasero != null && brasero.IsAncientBrasero && brasero.IsLit)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int CountAncientBraseros()
    {
        int count = 0;

        if (ancientBraseros != null)
        {
            for (int i = 0; i < ancientBraseros.Count; i++)
            {
                Brasero brasero = ancientBraseros[i];
                if (brasero != null && brasero.IsAncientBrasero)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void SubscribeToBraseros()
    {
        if (ancientBraseros == null)
        {
            return;
        }

        for (int i = 0; i < ancientBraseros.Count; i++)
        {
            Brasero brasero = ancientBraseros[i];
            SubscribeToBrasero(brasero);
        }
    }

    private void UnsubscribeFromBraseros()
    {
        foreach (Brasero brasero in subscribedBraseros)
        {
            if (brasero != null)
            {
                brasero.StateChanged -= OnBraseroStateChanged;
            }
        }

        subscribedBraseros.Clear();
    }

    private void OnBraseroStateChanged(Brasero brasero, bool lit)
    {
        RecalculateAge();
    }

    private void SubscribeToBrasero(Brasero brasero)
    {
        if (brasero == null || !brasero.IsAncientBrasero || subscribedBraseros.Contains(brasero))
        {
            return;
        }

        brasero.StateChanged += OnBraseroStateChanged;
        subscribedBraseros.Add(brasero);
    }

    private void AddAncientBraseros(IList<Brasero> source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            AddAncientBrasero(source[i]);
        }
    }

    private void AddAncientBraseros(Brasero[] source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            AddAncientBrasero(source[i]);
        }
    }

    private void AddAncientBrasero(Brasero brasero)
    {
        if (brasero == null || !brasero.IsAncientBrasero)
        {
            return;
        }

        if (ancientBraseros == null)
        {
            ancientBraseros = new List<Brasero>();
        }

        if (!ancientBraseros.Contains(brasero))
        {
            ancientBraseros.Add(brasero);
        }
    }

    private void RemoveMissingAncientBraseros()
    {
        if (ancientBraseros == null || ancientBraseros.Count == 0)
        {
            return;
        }

        for (int i = ancientBraseros.Count - 1; i >= 0; i--)
        {
            Brasero brasero = ancientBraseros[i];
            if (brasero == null || !brasero.IsAncientBrasero)
            {
                ancientBraseros.RemoveAt(i);
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
