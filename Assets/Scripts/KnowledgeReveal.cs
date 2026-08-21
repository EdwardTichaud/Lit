using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Point d'entree unique des sources de Savoirs.</summary>
public static class KnowledgeReveal
{
    public static int Reveal(IReadOnlyList<KnowledgeSO> knowledge, GameObject revealer, string origin)
    {
        if (knowledge == null) return 0;
        int requested = 0;
        string name = ResolveRevealerName(revealer);
        for (int i = 0; i < knowledge.Count; i++)
        {
            if (Reveal(knowledge[i], name, origin)) requested++;
        }
        return requested;
    }

    public static bool Reveal(KnowledgeSO knowledge, GameObject revealer, string origin) => Reveal(knowledge, ResolveRevealerName(revealer), origin);

    public static bool Reveal(KnowledgeSO knowledge, string revealerName, string origin)
    {
        if (knowledge == null) return false;
        KnowledgeSynchronizationService service = KnowledgeSynchronizationService.Instance;
        if (service != null && service.IsSpawned) return service.RequestReveal(knowledge, revealerName, origin);
        return RequestLocalReveal(knowledge, revealerName, origin);
    }

    internal static bool RequestLocalReveal(KnowledgeSO knowledge, string revealerName, string origin)
    {
        bool added = KnowledgeManager.GetOrCreate().ApplyValidatedKnowledge(knowledge);
        if (added) KnowledgeRevealNotification.Enqueue(knowledge, revealerName, origin);
        return added;
    }

    public static string ResolveRevealerName(GameObject revealer)
    {
        if (revealer == null) revealer = LocalPlayerUtils.GetControlledCharacter();
        SquadCharacterController controller = revealer != null ? revealer.GetComponentInParent<SquadCharacterController>() : null;
        if (controller != null && controller.CharacterData != null && !string.IsNullOrWhiteSpace(controller.CharacterData.characterName)) return controller.CharacterData.characterName;
        return revealer != null ? revealer.name : "L'equipe";
    }
}

/// <summary>Panneau HUD auto-cree, non modal, avec une file locale.</summary>
public sealed class KnowledgeRevealNotification : MonoBehaviour
{
    private struct Entry { public KnowledgeSO knowledge; public string revealer; public string origin; }
    private static KnowledgeRevealNotification instance;
    private readonly Queue<Entry> queue = new Queue<Entry>();
    private TMP_Text label;
    private float hideAt;

    public static void Enqueue(KnowledgeSO knowledge, string revealer, string origin)
    {
        if (knowledge == null) return;
        EnsureInstance().queue.Enqueue(new Entry { knowledge = knowledge, revealer = revealer, origin = origin });
    }

    private static KnowledgeRevealNotification EnsureInstance()
    {
        if (instance != null) return instance;
        GameObject root = new GameObject("KnowledgeRevealNotification");
        Object.DontDestroyOnLoad(root);
        instance = root.AddComponent<KnowledgeRevealNotification>();
        instance.CreateUi();
        return instance;
    }

    private void Update()
    {
        if (label == null) return;
        if (hideAt > 0f && Time.unscaledTime >= hideAt) { label.gameObject.SetActive(false); hideAt = 0f; }
        if (hideAt <= 0f && queue.Count > 0) Show(queue.Dequeue());
    }

    private void Show(Entry entry)
    {
        string title = string.IsNullOrWhiteSpace(entry.knowledge.title) ? entry.knowledge.name : entry.knowledge.title;
        string category = entry.knowledge.category.ToString();
        string summary = string.IsNullOrWhiteSpace(entry.knowledge.description) ? string.Empty : "\n" + entry.knowledge.description;
        label.text = $"{(string.IsNullOrWhiteSpace(entry.revealer) ? "L'equipe" : entry.revealer)} a revele : {title}\n<size=70%>{category}</size>{summary}";
        label.gameObject.SetActive(true);
        hideAt = Time.unscaledTime + 4.5f;
    }

    private void CreateUi()
    {
        GameObject canvasObject = new GameObject("KnowledgeHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(canvasObject);
        Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        GameObject textObject = new GameObject("Announcement", typeof(RectTransform), typeof(TextMeshProUGUI)); textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.5f, 1f); rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = new Vector2(0f, -80f); rect.sizeDelta = new Vector2(760f, 180f);
        label = textObject.GetComponent<TextMeshProUGUI>(); label.alignment = TextAlignmentOptions.Top; label.fontSize = 30f; label.enableWordWrapping = true; label.color = Color.white; label.raycastTarget = false; textObject.SetActive(false);
    }
}
