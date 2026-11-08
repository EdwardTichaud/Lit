using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche les connaissances actuellement debloquees et leur description au survol.
/// </summary>
[DisallowMultipleComponent]
public sealed class KnowledgePanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform knowledgeListContent;
    [SerializeField] private KnowledgePanelSlot slotPrefab;
    [SerializeField] private TMP_Text descriptionText;

    private readonly List<KnowledgePanelSlot> activeSlots = new List<KnowledgePanelSlot>();
    private readonly List<KnowledgePanelSlot> pooledSlots = new List<KnowledgePanelSlot>();
    private KnowledgeManager knowledgeManager;
    private KnowledgeSO displayedKnowledge;
    private readonly List<TMP_Text> activeHeaders = new List<TMP_Text>();
    private readonly List<TMP_Text> pooledHeaders = new List<TMP_Text>();
    private CycleProgressionService cycleService;

    public RectTransform KnowledgeListContent => knowledgeListContent;

    private void Awake()
    {
        ResolveReferences();
        ClearDescription();
    }

    private void OnEnable()
    {
        BindKnowledgeManager();
        BindCycleService();
        Refresh();
    }

    private void Update()
    {
        // The manager can be created by the gameplay bootstrap after this UI is enabled.
        if (knowledgeManager == null && KnowledgeManager.Instance != null)
        {
            BindKnowledgeManager();
            Refresh();
        }
        if (cycleService != CycleProgressionService.Instance) { BindCycleService(); Refresh(); }

    }

    private void OnDisable()
    {
        UnbindKnowledgeManager();
        UnbindCycleService();
        ClearDescription();
    }

    private void OnDestroy()
    {
        UnbindKnowledgeManager();
        UnbindCycleService();
    }

    [ContextMenu("Refresh Knowledge List")]
    public void Refresh()
    {
        ResolveReferences();
        RecycleActiveSlots();

        if (knowledgeListContent == null || slotPrefab == null || knowledgeManager == null)
        {
            ClearDescription();
            return;
        }

        var definitions = new HashSet<CycleDefinition>(CycleCatalog.LoadDefinitions());
        if (cycleService != null) foreach (var cycle in cycleService.Definitions) definitions.Add(cycle);
        foreach (var group in KnowledgeCycleGrouping.Build(knowledgeManager.UnlockedKnowledge, definitions))
        {
            TMP_Text header = GetHeader();
            header.text = group.Title + " (" + group.Entries.Count + ")";
            var passages = SecretPassageNotes.Catalog;
            if (passages != null && group.Cycle == passages.notesCycle)
                header.text = group.Title + " (" + passages.CountRead(CycleProgressionService.Instance) + "/" + passages.entries.Length + " notes lues)";
            activeHeaders.Add(header);
            foreach (var knowledge in group.Entries)
            {
                KnowledgePanelSlot slot = GetSlot();
                slot.Initialize(this, knowledge);
                activeSlots.Add(slot);
            }
        }

        if (displayedKnowledge != null && !knowledgeManager.HasKnowledge(displayedKnowledge))
        {
            ClearDescription();
        }

    }

    public void ShowDescription(KnowledgeSO knowledge)
    {
        if (knowledge == null || descriptionText == null)
        {
            return;
        }

        displayedKnowledge = knowledge;
        descriptionText.text = knowledge.description != null ? knowledge.description.Trim() : string.Empty;
    }

    public void FocusSlot(KnowledgePanelSlot slot)
    {
        if (slot != null)
        {
            ShowDescription(slot.Knowledge);
        }
    }

    public void ClearDescription(KnowledgeSO knowledge = null)
    {
        if (knowledge != null && knowledge != displayedKnowledge)
        {
            return;
        }

        displayedKnowledge = null;
        if (descriptionText != null)
        {
            descriptionText.text = string.Empty;
        }
    }

    private void BindKnowledgeManager()
    {
        KnowledgeManager manager = KnowledgeManager.Instance;
        if (manager == knowledgeManager)
        {
            return;
        }

        UnbindKnowledgeManager();
        knowledgeManager = manager;
        if (knowledgeManager == null)
        {
            return;
        }

        knowledgeManager.KnowledgeListChanged += Refresh;
    }

    private void UnbindKnowledgeManager()
    {
        if (knowledgeManager != null)
        {
            knowledgeManager.KnowledgeListChanged -= Refresh;
        }

        knowledgeManager = null;
    }

    private void BindCycleService()
    {
        UnbindCycleService();
        cycleService = CycleProgressionService.Instance;
        if (cycleService != null) cycleService.Changed += Refresh;
    }

    private void UnbindCycleService()
    {
        if (cycleService != null) cycleService.Changed -= Refresh;
        cycleService = null;
    }

    private TMP_Text GetHeader()
    {
        TMP_Text header;
        if (pooledHeaders.Count > 0)
        {
            int last = pooledHeaders.Count - 1;
            header = pooledHeaders[last]; pooledHeaders.RemoveAt(last);
            header.gameObject.SetActive(true);
        }
        else
        {
            var root = new GameObject("KnowledgeCycleHeader", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            root.layer = knowledgeListContent.gameObject.layer;
            root.transform.SetParent(knowledgeListContent, false);
            header = root.GetComponent<TMP_Text>();
            TMP_Text style = slotPrefab.GetComponentInChildren<TMP_Text>(true);
            if (style != null)
            {
                header.font = style.font; header.fontSharedMaterial = style.fontSharedMaterial;
                header.color = style.color; header.fontSize = Mathf.Max(16, style.fontSize);
            }
            else header.fontSize = 20;
            header.fontStyle = FontStyles.Bold;
            header.raycastTarget = false;
            header.alignment = TextAlignmentOptions.MidlineLeft;
            header.margin = new Vector4(8, 8, 0, 4);
            var layout = root.GetComponent<LayoutElement>();
            layout.minHeight = 30; layout.preferredHeight = header.fontSize * 2;
            layout.flexibleWidth = 1;
        }
        header.transform.SetAsLastSibling();
        return header;
    }

    private KnowledgePanelSlot GetSlot()
    {
        KnowledgePanelSlot slot;
        int lastIndex = pooledSlots.Count - 1;
        if (lastIndex >= 0)
        {
            slot = pooledSlots[lastIndex];
            pooledSlots.RemoveAt(lastIndex);
            slot.gameObject.SetActive(true);
        }
        else
        {
            slot = Instantiate(slotPrefab, knowledgeListContent);
        }

        slot.transform.SetAsLastSibling();
        return slot;
    }

    private void RecycleActiveSlots()
    {
        for (int i = 0; i < activeSlots.Count; i++)
        {
            KnowledgePanelSlot slot = activeSlots[i];
            if (slot == null)
            {
                continue;
            }

            slot.ResetSlot();
            slot.gameObject.SetActive(false);
            pooledSlots.Add(slot);
        }

        activeSlots.Clear();
        foreach (var header in activeHeaders)
        {
            if (header == null) continue;
            header.gameObject.SetActive(false);
            pooledHeaders.Add(header);
        }
        activeHeaders.Clear();
    }

    private void ResolveReferences()
    {
        if (knowledgeListContent == null)
        {
            Transform content = transform.Find("KnowledgeList_Root/ScrollView/Viewport/Content");
            knowledgeListContent = content as RectTransform;

            if (knowledgeListContent == null)
            {
                Transform root = transform.Find("KnowledgeList_Root");
                knowledgeListContent = root as RectTransform;
            }
        }

        if (descriptionText == null)
        {
            Transform root = transform.Find("Description_Root/Description_Text");
            descriptionText = root != null ? root.GetComponent<TMP_Text>() : null;
        }
    }

}
