using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private const string DefaultOverlayName = "UI_Overlay";

    [Header("Startup Visibility")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private List<RectTransform> visibleAtStartupPanels = new List<RectTransform>();

    private void Start()
    {
        ApplyStartupVisibility();
    }

    public void ApplyStartupVisibility()
    {
        Transform root = ResolveUiRoot();
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (ContainsStartupPanel(child))
            {
                continue;
            }

            SetPanelVisible(child, false);
        }

        if (visibleAtStartupPanels == null)
        {
            return;
        }

        for (int i = 0; i < visibleAtStartupPanels.Count; i++)
        {
            SetPanelVisible(visibleAtStartupPanels[i], true, false);
        }
    }

    public void SetPanelVisible(Transform panel, bool visible)
    {
        SetPanelVisible(panel, visible, visible);
    }

    private static void SetPanelVisible(Transform panel, bool visible, bool receiveInput)
    {
        if (panel == null)
        {
            return;
        }

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible && receiveInput;
        canvasGroup.blocksRaycasts = visible && receiveInput;
    }

    private Transform ResolveUiRoot()
    {
        if (uiRoot != null)
        {
            return uiRoot;
        }

        if (string.Equals(name, DefaultOverlayName, System.StringComparison.Ordinal))
        {
            uiRoot = transform;
            return uiRoot;
        }

        GameObject overlay = GameObject.Find(DefaultOverlayName);
        uiRoot = overlay != null ? overlay.transform : transform;
        return uiRoot;
    }

    private bool ContainsStartupPanel(Transform root)
    {
        if (root == null || visibleAtStartupPanels == null)
        {
            return false;
        }

        for (int i = 0; i < visibleAtStartupPanels.Count; i++)
        {
            RectTransform startupPanel = visibleAtStartupPanels[i];
            if (startupPanel != null && ContainsTransform(root, startupPanel))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTransform(Transform root, Transform target)
    {
        return root == target || (target != null && target.IsChildOf(root));
    }
}
