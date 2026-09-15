using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the shared main-menu focus color to the visual frame associated with
/// an interactive UI element. The normal color is preserved per Image.
/// </summary>
public static class MainMenuFrameHighlight
{
    private static readonly Color HighlightColor = new Color(1f, 0.72f, 0.23f, 1f);
    private const float TransitionDuration = 0.1f;
    private sealed class OriginalColor
    {
        public Color Value;
    }

    private static readonly ConditionalWeakTable<Image, OriginalColor> OriginalColors = new ConditionalWeakTable<Image, OriginalColor>();
    private static readonly List<Image> ResolvedFrames = new List<Image>(2);

    public static void SetFocused(Component source, bool focused)
    {
        if (source == null)
        {
            return;
        }

        MainMenuGameOptionsHoverVfx.SetHovered(source, focused);
        ResolveFrames(source.transform, ResolvedFrames);
        for (int i = 0; i < ResolvedFrames.Count; i++)
        {
            Image frame = ResolvedFrames[i];
            if (frame == null)
            {
                continue;
            }

            if (!OriginalColors.TryGetValue(frame, out OriginalColor originalColor))
            {
                originalColor = new OriginalColor { Value = frame.color };
                OriginalColors.Add(frame, originalColor);
            }

            Color normalColor = originalColor.Value;
            Color targetColor = focused
                ? new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, normalColor.a)
                : normalColor;
            frame.CrossFadeColor(targetColor, TransitionDuration, true, true);
        }
    }

    private static void ResolveFrames(Transform source, List<Image> frames)
    {
        frames.Clear();

        // Dynamic entries use Frame while static menu buttons use <Name>_Frame.
        for (int i = 0; i < source.childCount; i++)
        {
            Transform child = source.GetChild(i);
            if (IsFrame(child.name))
            {
                AddFrame(child.GetComponent<Image>(), frames);
            }
        }

        // Static menu actions are either the frame itself or a label inside it.
        AddFrame(source.GetComponent<Image>(), frames);
        for (Transform parent = source.parent; parent != null; parent = parent.parent)
        {
            if (!IsFrame(parent.name))
            {
                continue;
            }

            AddFrame(parent.GetComponent<Image>(), frames);
            break;
        }
    }

    private static bool IsFrame(string objectName)
    {
        return objectName == "Frame" || objectName.EndsWith("_Frame", System.StringComparison.Ordinal);
    }

    private static void AddFrame(Image frame, List<Image> frames)
    {
        if (frame != null && !frames.Contains(frame))
        {
            frames.Add(frame);
        }
    }
}
