using System.Collections.Generic;
using UnityEngine;

// Registre d'items par ID pour la resolution en runtime (Netcode).
public class ItemRegistry : MonoBehaviour
{
    public static ItemRegistry Instance { get; private set; }

    [Tooltip("Liste d'items disponibles pour la resolution par ID.")]
    public List<Item> items = new List<Item>();

    private readonly Dictionary<string, Item> lookup = new Dictionary<string, Item>();
    private bool initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildLookup();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Add only definitions referenced by newly prepared scene content. The
    // bootstrap catalogue is not necessarily exhaustive for additive zones.
    public static bool RegisterLoadedDefinition(Item item)
    {
        ItemRegistry registry = Instance;
        if (registry == null || item == null) return false;
        if (!registry.initialized) registry.BuildLookup();
        string id = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (registry.lookup.TryGetValue(id, out Item existing)) return existing == item;
        registry.lookup.Add(id, item);
        registry.items ??= new List<Item>();
        if (!registry.items.Contains(item)) registry.items.Add(item);
        return true;
    }

    public static Item Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        ItemRegistry registry = Instance;
        if (registry == null)
        {
            return null;
        }

        if (!registry.initialized)
        {
            registry.BuildLookup();
        }

        registry.lookup.TryGetValue(id, out Item item);
        return item;
    }

    public void BuildLookup()
    {
        lookup.Clear();
        initialized = true;

        if (items == null || items.Count == 0)
        {
            CharacterStateStore store = FindAnyObjectByType<CharacterStateStore>();
            if (store == null)
            {
                store = FindAnyObjectByType<CharacterStateStore>();
            }

            if (store != null && store.allItems != null)
            {
                items = new List<Item>(store.allItems);
            }
        }

        if (items == null || items.Count == 0)
        {
            Item[] found = Resources.FindObjectsOfTypeAll<Item>();
            if (found != null && found.Length > 0)
            {
                items = new List<Item>(found.Length);
                for (int i = 0; i < found.Length; i++)
                {
                    Item item = found[i];
                    if (item != null && !items.Contains(item))
                    {
                        items.Add(item);
                    }
                }
            }
        }

        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
            {
                continue;
            }

            string id = ItemIdUtils.GetItemId(item);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!lookup.ContainsKey(id))
            {
                lookup.Add(id, item);
            }
        }
    }
}
