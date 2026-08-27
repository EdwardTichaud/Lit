using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Affiche les connaissances nouvellement debloquees dans le panneau UGUI de la scene.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UiPanel))]
public sealed class KnowledgeUnlockPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UiPanel panel;
    [SerializeField] private TMP_Text messageText;

    [Header("Display")]
    [SerializeField, Min(0f)] private float displayDuration = 4.5f;
    [SerializeField] private string heading = "CONNAISSANCE DEBLOQUEE";

    private readonly Queue<KnowledgeSO> pendingKnowledge = new Queue<KnowledgeSO>();
    private KnowledgeManager knowledgeManager;
    private Coroutine displayRoutine;

    private void Awake()
    {
        if (panel == null) panel = GetComponent<UiPanel>();
        if (messageText == null) messageText = GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        BindKnowledgeManager();
    }

    private void Update()
    {
        if (knowledgeManager == null)
        {
            BindKnowledgeManager();
        }
    }

    private void OnDisable()
    {
        UnbindKnowledgeManager();
    }

    private void OnDestroy()
    {
        UnbindKnowledgeManager();
    }

    private void BindKnowledgeManager()
    {
        KnowledgeManager manager = KnowledgeManager.Instance;
        if (manager == knowledgeManager) return;

        UnbindKnowledgeManager();
        knowledgeManager = manager;
        if (knowledgeManager != null)
        {
            knowledgeManager.KnowledgeUnlocked += OnKnowledgeUnlocked;
        }
    }

    private void UnbindKnowledgeManager()
    {
        if (knowledgeManager != null)
        {
            knowledgeManager.KnowledgeUnlocked -= OnKnowledgeUnlocked;
            knowledgeManager = null;
        }
    }

    private void OnKnowledgeUnlocked(KnowledgeSO knowledge)
    {
        if (knowledge == null) return;

        pendingKnowledge.Enqueue(knowledge);
        if (displayRoutine == null)
        {
            displayRoutine = StartCoroutine(DisplayPendingKnowledge());
        }
    }

    private System.Collections.IEnumerator DisplayPendingKnowledge()
    {
        while (pendingKnowledge.Count > 0)
        {
            KnowledgeSO knowledge = pendingKnowledge.Dequeue();
            SetMessage(knowledge);
            if (panel != null) panel.Show();

            if (displayDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(displayDuration);
            }

            if (panel != null) panel.Hide();
        }

        displayRoutine = null;
    }

    private void SetMessage(KnowledgeSO knowledge)
    {
        if (messageText == null) return;

        string title = !string.IsNullOrWhiteSpace(knowledge.title) ? knowledge.title : knowledge.name;
        string description = knowledge.description != null ? knowledge.description.Trim() : string.Empty;
        string category = knowledge.category != KnowledgeCategory.Unknown ? knowledge.category.ToString() : string.Empty;

        messageText.text = string.IsNullOrWhiteSpace(description)
            ? $"{heading}\n<size=140%>{title}</size>{FormatCategory(category)}"
            : $"{heading}\n<size=140%>{title}</size>{FormatCategory(category)}\n\n{description}";
    }

    private static string FormatCategory(string category)
    {
        return string.IsNullOrWhiteSpace(category) ? string.Empty : $"\n<size=75%>{category}</size>";
    }
}
