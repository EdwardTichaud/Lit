using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace RPGFantasySFX
{
/// <summary>
/// Runtime controller for the RPG Fantasy SFX demo scene.
/// Audio clips are assigned automatically by the companion Editor tool.
/// </summary>
public class RPGFantasySFXDemoController : MonoBehaviour
{
    [SerializeField] private List<AudioClip> clips = new List<AudioClip>();
    [SerializeField] private int buttonsPerPage = 24;
    private AudioSource audioSource;
    private Transform contentRoot;
    private Text statusText;
    private int page;

    public List<AudioClip> Clips { get { return clips; } }
    public void SetClips(List<AudioClip> value) { clips = value; }

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        BuildUI();
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("Demo Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        var panel = MakeUIObject("Panel", canvasGO.transform, typeof(Image));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.075f, 0.09f, 0.14f, 0.98f);

        var title = MakeText("Title", panel.transform, "100 RPG & Fantasy Sound Effects", 30, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.98f));

        var hint = MakeText("Hint", panel.transform, "Select a sound to preview it. Use Next / Previous to browse the collection.", 16, TextAnchor.MiddleCenter);
        SetRect(hint.rectTransform, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.86f));

        var status = MakeText("Status", panel.transform, "Ready", 15, TextAnchor.MiddleCenter);
        SetRect(status.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.09f));
        statusText = status;

        var scrollGO = MakeUIObject("Scroll View", panel.transform, typeof(Image), typeof(ScrollRect));
        SetRect(scrollGO.GetComponent<RectTransform>(), new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.78f));
        scrollGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.05f);
        var viewport = MakeUIObject("Viewport", scrollGO.transform, typeof(Image), typeof(RectMask2D));
        SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        var content = MakeUIObject("Content", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1); contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12); layout.spacing = 8;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect; scroll.horizontal = false; scroll.vertical = true;
        contentRoot = content.transform;

        var nav = MakeUIObject("Navigation", panel.transform, typeof(HorizontalLayoutGroup));
        SetRect(nav.GetComponent<RectTransform>(), new Vector2(0.25f, 0.095f), new Vector2(0.75f, 0.15f));
        var navLayout = nav.GetComponent<HorizontalLayoutGroup>();
        navLayout.spacing = 14; navLayout.childAlignment = TextAnchor.MiddleCenter;
        navLayout.childControlWidth = true; navLayout.childControlHeight = true;
        navLayout.childForceExpandWidth = true; navLayout.childForceExpandHeight = true;
        MakeButton(nav.transform, "Previous", PreviousPage);
        MakeButton(nav.transform, "Stop Audio", StopAudio);
        MakeButton(nav.transform, "Next", NextPage);
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        if (contentRoot == null) return;
        for (int i = contentRoot.childCount - 1; i >= 0; i--) Destroy(contentRoot.GetChild(i).gameObject);
        int start = page * buttonsPerPage;
        int end = Mathf.Min(start + buttonsPerPage, clips.Count);
        for (int i = start; i < end; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null) continue;
            string label = (i + 1).ToString("000") + "  " + clip.name;
            MakeButton(contentRoot, label, () => PlayClip(clip));
        }
        if (statusText != null)
            statusText.text = clips.Count == 0 ? "No clips assigned. Re-run Tools > RPG Fantasy SFX > Create Demo Scene." :
                "Showing " + (start + 1) + "–" + end + " of " + clips.Count + " audio clips";
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
        if (statusText != null) statusText.text = "Playing: " + clip.name;
    }
    private void StopAudio() { if (audioSource != null) audioSource.Stop(); if (statusText != null) statusText.text = "Playback stopped"; }
    private void NextPage() { if ((page + 1) * buttonsPerPage < clips.Count) { page++; RefreshButtons(); } }
    private void PreviousPage() { if (page > 0) { page--; RefreshButtons(); } }

    private static GameObject MakeUIObject(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, components); go.transform.SetParent(parent, false); return go;
    }
    private static Text MakeText(string name, Transform parent, string value, int size, TextAnchor alignment)
    {
        var go = MakeUIObject(name, parent, typeof(Text));
        var t = go.GetComponent<Text>(); t.text = value; t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = size; t.alignment = alignment; t.color = Color.white; t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow; return t;
    }
    private static void MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var go = MakeUIObject("Button - " + label, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        go.GetComponent<Image>().color = new Color(0.20f, 0.29f, 0.43f, 1);
        var button = go.GetComponent<Button>(); button.onClick.AddListener(action);
        var element = go.GetComponent<LayoutElement>(); element.minHeight = 42; element.preferredHeight = 44;
        var text = MakeText("Label", go.transform, label, 16, TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one);
    }
    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }
}
}
