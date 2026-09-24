using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

    public RectTransform KnowledgeListContent => knowledgeListContent;

    private void Awake()
    {
        ResolveReferences();
        ClearDescription();
    }

    private void OnEnable()
    {
        BindKnowledgeManager();
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

    }

    private void OnDisable()
    {
        UnbindKnowledgeManager();
        ClearDescription();
    }

    private void OnDestroy()
    {
        UnbindKnowledgeManager();
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

        IReadOnlyList<KnowledgeSO> unlockedKnowledge = knowledgeManager.UnlockedKnowledge;
        for (int i = 0; i < unlockedKnowledge.Count; i++)
        {
            KnowledgeSO knowledge = unlockedKnowledge[i];
            if (knowledge == null)
            {
                continue;
            }

            KnowledgePanelSlot slot = GetSlot();
            slot.Initialize(this, knowledge);
            activeSlots.Add(slot);
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
