using System.Collections.Generic;
using UnityEngine;

// Systeme global pour appliquer les effets passifs des items en fonction de leur position.
[DefaultExecutionOrder(-50)]
public class ItemPassiveEffectSystem : MonoBehaviour
{
    [Tooltip("Intervalle de rafraichissement des sources (secondes).")]
    public float refreshInterval = 0.5f;
    [Tooltip("Inclut les InteractableItem inactifs.")]
    public bool includeInactiveContainers = false;
    [Tooltip("Ne pas detruire a la charge de scene.")]
    public bool dontDestroyOnLoad = true;
    [Tooltip("Utilise Time.unscaledTime pour les timers internes.")]
    public bool useUnscaledTime = true;

    private static ItemPassiveEffectSystem instance;
    private readonly List<SquadCharacterController> characters = new List<SquadCharacterController>();
    private readonly List<PassiveSource> sources = new List<PassiveSource>();
    private float nextRefreshTime;

    private struct PassiveSource
    {
        public Item Item;
        public Transform Source;
        public int Quantity;
        public Effect Effect;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("ItemPassiveEffectSystem");
        host.AddComponent<ItemPassiveEffectSystem>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Update()
    {
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (time >= nextRefreshTime)
        {
            RefreshSources();
            nextRefreshTime = time + Mathf.Max(0.05f, refreshInterval);
        }

        TickSources();
    }

    private void RefreshSources()
    {
        sources.Clear();
        CollectCharacters();
        CollectInventorySources();
        CollectContainerSources();
        CollectBuildingSources();
    }

    private void CollectCharacters()
    {
        characters.Clear();

        if (SquadManager.Instance != null && SquadManager.Instance.squadCharacters != null)
        {
            List<GameObject> squad = SquadManager.Instance.squadCharacters;
            for (int i = 0; i < squad.Count; i++)
            {
                GameObject obj = squad[i];
                if (obj == null)
                {
                    continue;
                }

                SquadCharacterController controller = obj.GetComponent<SquadCharacterController>();
                if (controller != null)
                {
                    characters.Add(controller);
                }
            }

            return;
        }

        SquadCharacterController[] found = FindObjectsByType<SquadCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
            {
                characters.Add(found[i]);
            }
        }
    }

    private void CollectInventorySources()
    {
        for (int i = 0; i < characters.Count; i++)
        {
            SquadCharacterController controller = characters[i];
            if (controller == null)
            {
                continue;
            }

            IReadOnlyList<Item> items = controller.Items;
            if (items == null || items.Count == 0)
            {
                continue;
            }

            Dictionary<Item, int> counts = new Dictionary<Item, int>();
            for (int j = 0; j < items.Count; j++)
            {
                Item item = items[j];
                if (item == null || item.itemPassiveEffect == null)
                {
                    continue;
                }

                counts.TryGetValue(item, out int count);
                counts[item] = count + 1;
            }

            foreach (KeyValuePair<Item, int> entry in counts)
            {
                AddSource(entry.Key, controller.transform, entry.Value);
            }
        }
    }

    private void CollectContainerSources()
    {
#if UNITY_2023_1_OR_NEWER
        FindObjectsInactive inactiveFlag = includeInactiveContainers ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        InteractableItem[] containers = FindObjectsByType<InteractableItem>(inactiveFlag, FindObjectsSortMode.None);
#else
        InteractableItem[] containers = includeInactiveContainers
            ? FindObjectsOfType<InteractableItem>(true)
            : FindObjectsOfType<InteractableItem>();
#endif

        for (int i = 0; i < containers.Length; i++)
        {
            InteractableItem container = containers[i];
            if (container == null)
            {
                continue;
            }

            List<InteractableItem.LootItemEntry> entries = container.lootItems;
            if (entries == null || entries.Count == 0)
            {
                continue;
            }

            for (int j = 0; j < entries.Count; j++)
            {
                InteractableItem.LootItemEntry entry = entries[j];
                if (entry == null || entry.item == null || entry.quantity <= 0)
                {
                    continue;
                }

                if (entry.item.itemPassiveEffect == null)
                {
                    continue;
                }

                AddSource(entry.item, container.transform, entry.quantity);
            }
        }
    }

    private void CollectBuildingSources()
    {
#if UNITY_2023_1_OR_NEWER
        BuildingInfoInteractable[] buildings = FindObjectsByType<BuildingInfoInteractable>(FindObjectsSortMode.None);
#else
        BuildingInfoInteractable[] buildings = FindObjectsOfType<BuildingInfoInteractable>();
#endif
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingInfoInteractable info = buildings[i];
            if (info == null)
            {
                continue;
            }

            Item item = info.BuildingItem;
            if (item == null || item.itemPassiveEffect == null)
            {
                continue;
            }

            AddSource(item, info.transform, 1);
        }
    }

    private void AddSource(Item item, Transform source, int quantity)
    {
        if (item == null || source == null)
        {
            return;
        }

        Effect effect = item.itemPassiveEffect;
        if (effect == null)
        {
            return;
        }

        if (effect is not IItemPassiveEffect)
        {
            return;
        }

        sources.Add(new PassiveSource
        {
            Item = item,
            Source = source,
            Quantity = Mathf.Max(1, quantity),
            Effect = effect
        });
    }

    private void TickSources()
    {
        if (sources.Count == 0)
        {
            return;
        }

        for (int i = 0; i < sources.Count; i++)
        {
            PassiveSource source = sources[i];
            if (source.Effect is IItemPassiveEffect passiveEffect)
            {
                ItemPassiveContext context = new ItemPassiveContext(source.Item, source.Source, source.Quantity, characters);
                passiveEffect.Tick(context);
            }
        }
    }
}
