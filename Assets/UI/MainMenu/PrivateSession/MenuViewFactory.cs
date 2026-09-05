using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Explicit construction of reusable menu views; no hierarchy-name discovery.
public static class MenuViewFactory
{
    public static TMP_Text Label(Transform parent, string text, float height = 42)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        TMP_Text label = go.GetComponent<TMP_Text>();
        label.text = text; label.fontSize = 25; label.color = new Color(.94f, .9f, .8f);
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return label;
    }

    public static Button Button(Transform parent, string text, Action action)
    {
        GameObject go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(.16f, .18f, .2f, .97f);
        go.GetComponent<LayoutElement>().preferredHeight = 46;
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(() => action());
        TMP_Text label = Label(go.transform, text);
        label.alignment = TextAlignmentOptions.Center;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10, 0); rect.offsetMax = new Vector2(-10, 0);
        return button;
    }

    public static void MakeScrollable(GameObject root)
    {
        VerticalLayoutGroup previous = root.GetComponent<VerticalLayoutGroup>();
        previous.enabled = false;
        Transform[] children = new Transform[root.transform.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = root.transform.GetChild(i);
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(28, 24); viewportRect.offsetMax = new Vector2(-28, -24);
        GameObject content = Column(viewport.transform, "Content");
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(.5f, 1); contentRect.sizeDelta = Vector2.zero;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (Transform child in children) child.SetParent(content.transform, false);
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect; scroll.content = contentRect; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
    }

    public static GameObject Column(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 9; layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        return go;
    }
}
