using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small runtime-only HUD shared by every engaged boss; ordinary enemies never satisfy the lookup.</summary>
public sealed class BossCombatHud : MonoBehaviour
{
    private RealTimeCombatManager combat;
    private GameObject root;
    private TextMeshProUGUI title;
    private TextMeshProUGUI segments;
    private Image fill;

    private void Awake()
    {
        combat = GetComponent<RealTimeCombatManager>();
        Build();
        root.SetActive(false);
    }
    private void LateUpdate()
    {
        IBossEncounterBehaviour boss = combat != null && combat.IsCombatActive && combat.EngagedEnemy != null
            ? combat.EngagedEnemy.BossEncounter : null;
        bool visible = boss != null && boss.IsBossEngaged && !boss.IsBossResolved && boss.Definition != null && boss.Definition.ShowBossBar;
        if (!visible)
        {
            if (root != null && root.activeSelf) root.SetActive(false);
            return;
        }
        if (root == null) Build();
        if (!root.activeSelf) root.SetActive(true);
        int maximum = Mathf.Max(1, boss.MaximumSegments);
        int current = Mathf.Clamp(boss.CurrentSegments, 0, maximum);
        title.text = boss.Definition.DisplayName;
        segments.text = current + " / " + maximum;
        fill.fillAmount = (float)current / maximum;
    }
    private void OnDestroy()
    {
        if (root != null) Destroy(root);
    }
    private void Build()
    {
        root = new GameObject("BossCombatHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        GameObject panel = Create("Panel", root.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>(); panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, 1f); panelRect.pivot = new Vector2(.5f, 1f); panelRect.anchoredPosition = new Vector2(0, -52); panelRect.sizeDelta = new Vector2(520, 76);
        panel.GetComponent<Image>().color = new Color(.015f, .025f, .05f, .82f);
        title = CreateText("Name", panel.transform, new Vector2(0, 12), new Vector2(1, 1), 27, TextAlignmentOptions.Center);
        GameObject bar = Create("Segments", panel.transform, typeof(Image));
        RectTransform barRect = bar.GetComponent<RectTransform>(); barRect.anchorMin = new Vector2(0, 0); barRect.anchorMax = new Vector2(1, 0); barRect.offsetMin = new Vector2(28, 13); barRect.offsetMax = new Vector2(-28, 27);
        bar.GetComponent<Image>().color = new Color(.06f, .1f, .16f, 1f);
        GameObject value = Create("Fill", bar.transform, typeof(Image));
        fill = value.GetComponent<Image>(); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.color = new Color(.38f, .8f, 1f, 1f);
        RectTransform fillRect = value.GetComponent<RectTransform>(); fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        segments = CreateText("Count", bar.transform, Vector2.zero, Vector2.one, 20, TextAlignmentOptions.Center);
    }
    private static GameObject Create(string name, Transform parent, params System.Type[] components)
    {
        GameObject item = new(name, components); item.transform.SetParent(parent, false); return item;
    }
    private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 min, Vector2 max, float size, TextAlignmentOptions alignment)
    {
        GameObject item = Create(name, parent, typeof(TextMeshProUGUI)); RectTransform rect = item.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>(); text.fontSize = size; text.alignment = alignment; text.fontStyle = FontStyles.Bold; text.color = Color.white; text.raycastTarget = false; return text;
    }
}
