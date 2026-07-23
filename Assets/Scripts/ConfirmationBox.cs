using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Facade minimale sur le prefab ConfirmationBox. Seul le texte "Question" est modifie ici.
[DisallowMultipleComponent]
public class ConfirmationBox : MonoBehaviour
{
    private const string BoxObjectName = "ConfirmationBox";
    private const string QuestionObjectName = "Question";
    private const string YesObjectName = "Oui";
    private const string NoObjectName = "Non";
    private const string CursorObjectName = "Cursor";
    private const string DefaultConfirmLabel = "Oui";
    private const string DefaultCancelLabel = "Non";

    [Header("References")]
    [SerializeField] private RectTransform boxRoot;
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private TextMeshProUGUI yesText;
    [SerializeField] private TextMeshProUGUI noText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private RectTransform cursorRoot;
    [SerializeField] private CursorController cursorController;
    private bool optionTextDefaultsCached;
    private float defaultYesFontSize;
    private float defaultNoFontSize;
    private bool defaultYesAutoSizing;
    private bool defaultNoAutoSizing;

    public Button ConfirmButton => confirmButton;
    public Button CancelButton => cancelButton;
    public CursorController CursorController => cursorController;
    public RectTransform CursorRoot => cursorRoot;
    public RectTransform ConfirmTarget => yesText != null ? yesText.rectTransform : null;
    public RectTransform CancelTarget => noText != null ? noText.rectTransform : null;

    public bool ResolveReferences()
    {
        boxRoot = transform as RectTransform;

        if (questionText == null)
        {
            questionText = FindText(QuestionObjectName);
            if (questionText == null)
            {
                questionText = FindQuestionFallback();
            }
        }

        if (yesText == null)
        {
            yesText = FindText(YesObjectName);
        }

        if (noText == null)
        {
            noText = FindText(NoObjectName);
        }

        CacheOptionTextDefaults();

        if (confirmButton == null && yesText != null)
        {
            confirmButton = yesText.GetComponent<Button>();
        }

        if (cancelButton == null && noText != null)
        {
            cancelButton = noText.GetComponent<Button>();
        }

        if (cursorRoot == null)
        {
            cursorRoot = FindRect(CursorObjectName);
        }

        if (cursorController == null)
        {
            cursorController = cursorRoot != null
                ? cursorRoot.GetComponent<CursorController>()
                : GetComponentInChildren<CursorController>(true);
        }

        if (cursorRoot == null && cursorController != null)
        {
            cursorRoot = cursorController.cursor != null
                ? cursorController.cursor
                : cursorController.transform as RectTransform;
        }

        if (boxRoot == null && string.Equals(name, BoxObjectName, System.StringComparison.Ordinal))
        {
            boxRoot = transform as RectTransform;
        }

        return questionText != null;
    }

    public void SetQuestion(string message)
    {
        if (!ResolveReferences())
        {
            Debug.LogWarning($"[Confirmation] Question text not found on '{name}'.", this);
            return;
        }

        questionText.text = !string.IsNullOrWhiteSpace(message) ? message : "Confirmer ?";
    }

    public void SetOptions(string confirmLabel, string cancelLabel)
    {
        if (!ResolveReferences())
        {
            return;
        }

        ApplyOptionLabel(yesText, confirmLabel, DefaultConfirmLabel, defaultYesFontSize, defaultYesAutoSizing);
        ApplyOptionLabel(noText, cancelLabel, DefaultCancelLabel, defaultNoFontSize, defaultNoAutoSizing);
    }

    private void CacheOptionTextDefaults()
    {
        if (optionTextDefaultsCached)
        {
            return;
        }

        if (yesText != null)
        {
            defaultYesFontSize = yesText.fontSize;
            defaultYesAutoSizing = yesText.enableAutoSizing;
        }

        if (noText != null)
        {
            defaultNoFontSize = noText.fontSize;
            defaultNoAutoSizing = noText.enableAutoSizing;
        }

        optionTextDefaultsCached = yesText != null || noText != null;
    }

    private static void ApplyOptionLabel(
        TextMeshProUGUI text,
        string label,
        string fallback,
        float defaultFontSize,
        bool defaultAutoSizing)
    {
        if (text == null)
        {
            return;
        }

        bool hasCustomLabel = !string.IsNullOrWhiteSpace(label);
        text.text = hasCustomLabel ? label : fallback;
        text.enableAutoSizing = hasCustomLabel || defaultAutoSizing;
        text.fontSize = hasCustomLabel ? 56f : defaultFontSize;

        if (hasCustomLabel)
        {
            text.fontSizeMin = 24f;
            text.fontSizeMax = 56f;
        }
    }

    private TextMeshProUGUI FindText(string objectName)
    {
        Transform target = FindChildRecursive(transform, objectName);
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
    }

    private RectTransform FindRect(string objectName)
    {
        Transform target = FindChildRecursive(transform, objectName);
        return target as RectTransform;
    }

    private TextMeshProUGUI FindQuestionFallback()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI candidate = texts[i];
            if (candidate == null || candidate == yesText || candidate == noText)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (string.Equals(child.name, objectName, System.StringComparison.Ordinal))
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
