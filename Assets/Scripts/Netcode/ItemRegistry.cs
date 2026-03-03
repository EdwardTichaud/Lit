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
            CharacterStateStore store = FindFirstObjectByType<CharacterStateStore>();
            if (store == null)
            {
                store = FindObjectOfType<CharacterStateStore>();
            }

            if (store != null && store.allItems != null)
            {
                items = new List<Item>(store.allItems);
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
