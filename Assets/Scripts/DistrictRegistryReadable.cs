// Role:
// Runtime bridge between DistrictRegistry data, the existing Item/BookPanel
// readable flow, and the temporal systems driven by AncientFlame.
// Usage:
// Add this to a register prop, assign a DistrictRegistry, and use the registry's
// readable Item in the existing readable/inventory flow.
// Responsibilities:
// Resolve the effective temporal year, rebuild the assigned Item pages only when
// the year changes, and use the dominant zone/flame ancien age.
// Dependencies:
// DistrictRegistry, Item, TemporalZone, AgeManager, InventoryPanelController.
// Precautions:
// This mutates ScriptableObject Item pages at runtime. That is intentional so the
// old BookPanel and page-turning code stay unchanged. Do not store per-save facts
// in generated page text; regenerate from ResidentRecord data instead.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps one district registry readable synchronized with the currently consulted temporal year.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Lit/Narrative/District Registry Readable")]
public class DistrictRegistryReadable : MonoBehaviour
{
    private static readonly List<DistrictRegistryReadable> ActiveReadables = new List<DistrictRegistryReadable>();
    private static readonly Dictionary<Item, int> LastAppliedYearByItem = new Dictionary<Item, int>();

    [Header("Registry")]
    [SerializeField, Tooltip("Données habitants du registre.")]
    private DistrictRegistry registry;
    [SerializeField, Tooltip("Item livre utilisé par le BookPanel. Laisse vide pour utiliser celui du DistrictRegistry.")]
    private Item readableItem;

    [Header("Dominant Age")]
    [SerializeField, Tooltip("Zone temporelle dominante. Laisse vide pour chercher dans les parents.")]
    private TemporalZone zone;
    [SerializeField, Tooltip("Cherche automatiquement une TemporalZone dans les parents.")]
    private bool autoFindZoneInParents = true;
    [SerializeField, Tooltip("Si true, la TemporalZone prime sur AgeManager pour l'âge dominant.")]
    private bool preferZoneAge = true;
    [SerializeField, Tooltip("Âge de secours si aucune source temporelle n'existe dans la scène.")]
    private TemporalAge fallbackAge = TemporalAge.Age666;

    [Header("Runtime")]
    [SerializeField, Tooltip("Reconstruit les pages dès l'activation.")]
    private bool refreshOnEnable = true;
    [SerializeField, Tooltip("Logs de diagnostic lors des reconstructions de pages.")]
    private bool logRefreshes;

    private AgeManager subscribedAgeManager;
    private int lastAppliedYear = int.MinValue;

    public DistrictRegistry Registry => registry;
    public Item ReadableItem => ResolveReadableItem();
    public int EffectiveYear => ResolveEffectiveYear();

    /// <summary>
    /// Called by InventoryPanelController just before opening a readable Item.
    /// This preserves the old BookPanel flow while letting registry books rebuild
    /// themselves from the current temporal context.
    /// </summary>
    public static bool RefreshReadableItemForCurrentTemporalContext(Item item)
    {
        if (item == null)
        {
            return false;
        }

        DistrictRegistryReadable readable = FindActiveReadableForItem(item);
        if (readable != null)
        {
            return readable.RefreshReadableItem(force: false);
        }

        DistrictRegistry source = item.refreshTemporalDistrictRegistryOnRead
            ? item.temporalDistrictRegistry
            : null;
        if (source == null)
        {
            return false;
        }

        int year = ResolveGlobalDominantYear();
        return ApplyRegistryPagesToItem(source, item, year, force: false);
    }

    private void OnEnable()
    {
        if (!ActiveReadables.Contains(this))
        {
            ActiveReadables.Add(this);
        }

        ResolveReferences();
        Subscribe();

        if (refreshOnEnable)
        {
            RefreshReadableItem(force: true);
        }
    }

    private void OnDisable()
    {
        ActiveReadables.Remove(this);
        Unsubscribe();
    }

    private void OnValidate()
    {
        if (readableItem == null && registry != null)
        {
            readableItem = registry.readableItem;
        }
    }

    [ContextMenu("Refresh Readable Item")]
    public void RefreshReadableItemFromContextMenu()
    {
        RefreshReadableItem(force: true);
    }

    public bool RefreshReadableItem(bool force)
    {
        ResolveReferences();
        Item targetItem = ResolveReadableItem();
        if (registry == null || targetItem == null)
        {
            return false;
        }

        int year = ResolveEffectiveYear();
        bool changed = ApplyRegistryPagesToItem(registry, targetItem, year, force || lastAppliedYear != year);
        if (changed)
        {
            lastAppliedYear = year;
            if (logRefreshes)
            {
                Debug.Log($"[DistrictRegistryReadable] {name} -> {registry.registryId} An {year}", this);
            }
        }

        return changed;
    }

    private static DistrictRegistryReadable FindActiveReadableForItem(Item item)
    {
        for (int i = ActiveReadables.Count - 1; i >= 0; i--)
        {
            DistrictRegistryReadable readable = ActiveReadables[i];
            if (readable == null)
            {
                ActiveReadables.RemoveAt(i);
                continue;
            }

            if (readable.ResolveReadableItem() == item)
            {
                return readable;
            }
        }

        return null;
    }

    private static bool ApplyRegistryPagesToItem(DistrictRegistry source, Item targetItem, int year, bool force)
    {
        if (source == null || targetItem == null)
        {
            return false;
        }

        int snappedYear = SnapYearToTemporalAge(year);
        bool hasPages = targetItem.bookPages != null && targetItem.bookPages.Count > 0;
        if (!force
            && hasPages
            && LastAppliedYearByItem.TryGetValue(targetItem, out int previousYear)
            && previousYear == snappedYear)
        {
            return false;
        }

        source.ApplyToReadableItem(targetItem, snappedYear);
        LastAppliedYearByItem[targetItem] = snappedYear;
        return true;
    }

    private void ResolveReferences()
    {
        if (readableItem == null && registry != null)
        {
            readableItem = registry.readableItem;
        }

        if (zone == null && autoFindZoneInParents)
        {
            zone = GetComponentInParent<TemporalZone>(true);
        }
    }

    private Item ResolveReadableItem()
    {
        if (readableItem != null)
        {
            return readableItem;
        }

        return registry != null ? registry.readableItem : null;
    }

    private int ResolveEffectiveYear()
    {
        return ResolveDominantYear();
    }

    private int ResolveDominantYear()
    {
        if (preferZoneAge && zone != null)
        {
            return TemporalAgeUtility.AgeToInt(zone.CurrentAge);
        }

        AgeManager manager = AgeManager.ActiveInstance;
        if (manager != null)
        {
            return manager.CurrentYear;
        }

        if (zone != null)
        {
            return TemporalAgeUtility.AgeToInt(zone.CurrentAge);
        }

        return TemporalAgeUtility.AgeToInt(fallbackAge);
    }

    private static int ResolveGlobalDominantYear()
    {
        AgeManager manager = AgeManager.ActiveInstance;
        if (manager != null)
        {
            return manager.CurrentYear;
        }

        return TemporalAgeUtility.MaxYear;
    }

    private void Subscribe()
    {
        if (zone != null)
        {
            zone.AgeChanged += OnZoneAgeChanged;
        }

        subscribedAgeManager = AgeManager.ActiveInstance;
        if (subscribedAgeManager != null)
        {
            subscribedAgeManager.AgeChanged += OnAgeManagerChanged;
        }

    }

    private void Unsubscribe()
    {
        if (zone != null)
        {
            zone.AgeChanged -= OnZoneAgeChanged;
        }

        if (subscribedAgeManager != null)
        {
            subscribedAgeManager.AgeChanged -= OnAgeManagerChanged;
            subscribedAgeManager = null;
        }

    }

    private void OnZoneAgeChanged(TemporalZone temporalZone, TemporalAge previous, TemporalAge current)
    {
        RefreshReadableItem(force: false);
    }

    private void OnAgeManagerChanged(AgeManager manager, int previousYear, int currentYear)
    {
        RefreshReadableItem(force: false);
    }

    private static int SnapYearToTemporalAge(int year)
    {
        TemporalAge snappedAge = TemporalAgeUtility.IntToAge(year);
        return TemporalAgeUtility.AgeToInt(snappedAge);
    }
}
