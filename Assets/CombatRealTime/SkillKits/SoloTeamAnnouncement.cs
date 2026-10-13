using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Non-modal local recruitment queue, owned by the gameplay session.</summary>
public sealed class SoloTeamAnnouncement : MonoBehaviour
{
    private readonly Queue<string> queue = new();
    private TMP_Text label;
    private float until;
    public static void Show(string message)
    {
        var team = SoloTeamProgression.Resolve();
        if (team == null) return;
        var presenter = team.GetComponent<SoloTeamAnnouncement>() ?? team.gameObject.AddComponent<SoloTeamAnnouncement>();
        presenter.queue.Enqueue(message);
    }
    private void Update()
    {
        if (Time.unscaledTime < until) return;
        if (queue.Count == 0) { if (label != null) label.gameObject.SetActive(false); return; }
        if (label == null)
        {
            var root = new GameObject("TeamAnnouncement", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
            var scaling = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaling.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaling.referenceResolution = new Vector2(1920, 1080);
            var text = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(root.transform, false);
            label = text.GetComponent<TMP_Text>(); label.raycastTarget = false; label.fontSize = 28;
            label.alignment = TextAlignmentOptions.Center; label.color = new Color(1f, .9f, .6f);
            var rect = text.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = new Vector2(0, -70); rect.sizeDelta = new Vector2(850, 110);
        }
        label.gameObject.SetActive(true); label.text = queue.Dequeue(); until = Time.unscaledTime + 5f;
    }
}
