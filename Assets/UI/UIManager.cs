using UnityEngine;

public class UIManager : MonoBehaviour
{
    private const string DefaultOverlayName = "UI_Overlay";

    [Header("Startup Visibility")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private string[] visibleAtStartupPanelNames =
    {
        "MuninUIPanel",
        "MuninUI",
        "UI_Overlay_MuninUI"
    };

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

        Transform startupPanel = FindDescendantByNames(root, visibleAtStartupPanelNames);
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (ContainsTransform(child, startupPanel))
            {
                continue;
            }

            SetPanelVisible(child, false);
        }

        SetPanelVisible(startupPanel, true, false);
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

        panel.gameObject.SetActive(true);

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

    private static Transform FindDescendantByNames(Transform root, string[] names)
    {
        if (root == null || names == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (MatchesAnyName(child.name, names))
            {
                return child;
            }

            Transform match = FindDescendantByNames(child, names);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool MatchesAnyName(string objectName, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(objectName, names[i], System.StringComparison.Ordinal))
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
