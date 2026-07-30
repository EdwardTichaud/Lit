using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche au host le code Relay pendant Maison. Il est cree au runtime pour ne
/// pas coupler le test multijoueur a la scene du menu ou a une prefab UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetcodeRelaySessionOverlay : MonoBehaviour
{
    private GameObject root;
    private Text codeText;
    private string lastCode = string.Empty;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        CreateUi();
    }

    private void Update()
    {
        NetcodeLauncher launcher = GetComponent<NetcodeLauncher>();
        NetworkManager manager = NetworkManager.Singleton;
        bool visible = manager != null && manager.IsHost && !string.IsNullOrWhiteSpace(launcher != null ? launcher.ActiveRelayJoinCode : string.Empty);
        if (root != null && root.activeSelf != visible)
        {
            root.SetActive(visible);
        }

        if (!visible || launcher == null || launcher.ActiveRelayJoinCode == lastCode)
        {
            return;
        }

        lastCode = launcher.ActiveRelayJoinCode;
        codeText.text = $"SESSION AMIS\nCode Relay : {lastCode}\n(copie dans le presse-papiers)";
    }

    private void CreateUi()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        root = new GameObject("RelaySessionOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        Image image = panel.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.68f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -24f);
        panelRect.sizeDelta = new Vector2(520f, 110f);

        GameObject label = new GameObject("Code", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(panel.transform, false);
        codeText = label.GetComponent<Text>();
        codeText.font = font;
        codeText.fontSize = 24;
        codeText.alignment = TextAnchor.MiddleCenter;
        codeText.color = Color.white;
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        root.SetActive(false);
    }
}
