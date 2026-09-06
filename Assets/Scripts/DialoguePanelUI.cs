using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Affiche les dialogues narratifs dans le DialoguePanel de la scene.
public class DialoguePanelUI : MonoBehaviour, IInputModeHandler
{
    public static DialoguePanelUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("Root du DialoguePanel.")]
    public GameObject dialoguePanelRoot;
    [Tooltip("Texte affiche dans le panel.")]
    public TMP_Text dialogueText;

    [Header("Behavior")]
    [Tooltip("Duree par defaut si le panel ne demande pas Interact pour fermer.")]
    public float defaultDuration = 1.5f;
    [Tooltip("Si actif, le DialoguePanel reste ouvert jusqu'a l'input Interact.")]
    public bool requireInteractToClose = true;
    [Tooltip("Duree du fondu.")]
    public float fadeDuration = 0.25f;
    [Tooltip("Vide le texte quand le panel se ferme.")]
    public bool clearWhenHidden = true;
    [Tooltip("Auto-resout les references au Awake/OnEnable.")]
    public bool autoFindOnAwake = true;

    private const string DialoguePanelObjectName = "DialoguePanel";
    private const string DialogueTextObjectName = "DialogueBox_Text";

    private CanvasGroup canvasGroup;
    private Coroutine hideRoutine;
    private bool isShowing;
    private bool externallyControlled;
    private int shownFrame = -1;
    private Action onHiddenCallback;
    private bool timedConversationCompleted;
    private bool timedConversation;
    private object timedConversationOwner;

    /// <summary>Reports natural timed completion separately from dismissal, replacement or teardown.</summary>
    public static bool TryShowTimedConversation(string message, float duration, Action<bool> finished, object owner = null)
    {
        var ui = GetOrCreate();
        bool shown = ui.ShowMessageInternal(message, duration, () => finished?.Invoke(ui.timedConversationCompleted), true, true);
        if (shown) ui.timedConversationOwner = owner;
        return shown;
    }

    public static void CancelTimedConversation(object owner)
    {
        if (owner != null && Instance != null && ReferenceEquals(Instance.timedConversationOwner, owner)) Instance.HideImmediate();
    }

    public bool IsShowing => isShowing;
    public bool IsExternallyControlled => externallyControlled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (autoFindOnAwake)
        {
            ResolveReferences();
        }
    }

    private void OnEnable()
    {
        if (autoFindOnAwake)
        {
            ResolveReferences();
        }

        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
    }

    private void OnDisable()
    {
        timedConversationCompleted = false;
        LocalInputRouter.Interact -= OnInteractPerformed;
        InputFocusStack.Pop(this);
        isShowing = false;
        externallyControlled = false;
        InvokeAndClearHiddenCallback();
    }

    public static bool TryShow(string message, float duration = 0f)
    {
        return TryShow(message, duration, null);
    }

    public static bool TryShow(string message, float duration, Action onHidden)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        DialoguePanelUI ui = GetOrCreate();
        return ui.ShowMessage(message, duration, onHidden);
    }

    /// <summary>
    /// Shows short world-reading text in the dialogue presentation without
    /// changing the active gameplay ActionMap. This is for inspectable props,
    /// not conversations that require the player to choose a response.
    /// </summary>
    public static bool TryShowNonBlocking(string message, float duration)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        DialoguePanelUI ui = GetOrCreate();
        bool shown = ui.ShowMessageInternal(message, duration, null, blocksGameplayInput: false);
        if (shown)
        {
            // Inspectable world text deliberately keeps Exploration active.
            // Re-read held movement after the interaction so no jump/re-press
            // is needed when an earlier modal reset the input vector.
            LocalPlayerInput.RequestHeldLocomotionReconciliation("Non-blocking dialogue");
        }

        return shown;
    }

    public static DialoguePanelUI GetOrCreate()
    {
        DialoguePanelUI ui = Instance;
        if (ui == null)
        {
#if UNITY_2023_1_OR_NEWER
            ui = FindAnyObjectByType<DialoguePanelUI>();
#else
            ui = FindObjectOfType<DialoguePanelUI>();
#endif
        }

        if (ui == null)
        {
            GameObject runner = new GameObject("DialoguePanelUI_Runtime");
            ui = runner.AddComponent<DialoguePanelUI>();
        }

        return ui;
    }

    public bool ShowMessage(string message, float duration, Action onHidden)
    {
        return ShowMessageInternal(message, duration, onHidden, blocksGameplayInput: true);
    }

    private bool ShowMessageInternal(string message, float duration, Action onHidden, bool blocksGameplayInput, bool forceTimed = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        ResolveReferences();
        if (dialogueText == null)
        {
            return false;
        }

        timedConversationCompleted = false;
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
            InvokeAndClearHiddenCallback();
        }
        else if (isShowing)
        {
            InvokeAndClearHiddenCallback();
        }

        externallyControlled = false;
        timedConversationCompleted = false;
        timedConversation = forceTimed;
        onHiddenCallback = onHidden;
        dialogueText.text = message;
        SetVisible(true);

        isShowing = true;
        shownFrame = Time.frameCount;
        InputFocusStack.Pop(this);
        if (blocksGameplayInput)
        {
            InputFocusStack.PushDialogue(this);
        }

        if (blocksGameplayInput && requireInteractToClose && !forceTimed)
        {
            hideRoutine = StartCoroutine(ShowUntilManualDismissRoutine());
        }
        else
        {
            float wait = duration > 0f ? duration : defaultDuration;
            hideRoutine = StartCoroutine(ShowAndHideRoutine(wait));
        }

        return true;
    }

    public bool ShowControlledMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        ResolveReferences();
        if (dialogueText == null)
        {
            return false;
        }

        CancelActivePresentation();
        externallyControlled = true;
        dialogueText.text = message;
        SetVisible(true);
        SetCanvasAlpha(0f);
        isShowing = true;
        shownFrame = Time.frameCount;
        InputFocusStack.PushDialogue(this);
        hideRoutine = StartCoroutine(ShowUntilManualDismissRoutine());
        return true;
    }

    public void SetControlledMessage(string message)
    {
        if (!externallyControlled || dialogueText == null)
        {
            return;
        }

        dialogueText.text = message ?? string.Empty;
    }

    public void HideControlled()
    {
        if (!externallyControlled || !isShowing)
        {
            return;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(HideRoutine());
    }

    public void HideImmediate()
    {
        timedConversationCompleted = false;
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        SetCanvasAlpha(0f);
        if (clearWhenHidden && dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        isShowing = false;
        externallyControlled = false;
        InputFocusStack.Pop(this);
        InvokeAndClearHiddenCallback();
    }

    public void HideControlledImmediate()
    {
        if (externallyControlled)
        {
            HideImmediate();
        }
    }

    private IEnumerator ShowUntilManualDismissRoutine()
    {
        yield return FadeIn();
        hideRoutine = null;
    }

    private IEnumerator ShowAndHideRoutine(float duration)
    {
        yield return FadeIn();

        float time = 0f;
        float hold = Mathf.Max(0f, duration);
        while (time < hold)
        {
            time += Time.unscaledDeltaTime;
            yield return null;
        }

        timedConversationCompleted = true;
        yield return HideRoutine();
    }

    private IEnumerator FadeIn()
    {
        EnsureCanvasGroup();
        float fade = Mathf.Max(0f, fadeDuration);
        if (fade <= 0f)
        {
            SetCanvasAlpha(1f);
            yield break;
        }

        float time = 0f;
        while (time < fade)
        {
            time += Time.unscaledDeltaTime;
            SetCanvasAlpha(Mathf.Clamp01(time / fade));
            yield return null;
        }

        SetCanvasAlpha(1f);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (externallyControlled ||
            !requireInteractToClose ||
            !isShowing ||
            !InputFocusStack.HasFocus(this))
        {
            return;
        }

        if (Time.frameCount == shownFrame)
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();
        HideManually();
    }

    public bool HandleInputModeAction(InputModeAction action, InputAction.CallbackContext context)
    {
        if (!isShowing || !InputFocusStack.HasFocus(this)) return false;
        if (timedConversation && action == InputModeAction.Cancel)
        {
            HideManually();
            return true;
        }
        if (action == InputModeAction.Submit && !externallyControlled && requireInteractToClose && Time.frameCount != shownFrame)
        {
            HideManually();
            return true;
        }
        return action == InputModeAction.Cancel;
    }

    private void HideManually()
    {
        timedConversationCompleted = false;
        if (!isShowing)
        {
            return;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(HideRoutine());
    }

    private IEnumerator HideRoutine()
    {
        float fade = Mathf.Max(0f, fadeDuration);
        float startAlpha = GetCanvasAlpha();
        if (fade > 0f)
        {
            float time = 0f;
            while (time < fade)
            {
                time += Time.unscaledDeltaTime;
                SetCanvasAlpha(Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(time / fade)));
                yield return null;
            }
        }

        SetCanvasAlpha(0f);
        if (clearWhenHidden && dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        isShowing = false;
        externallyControlled = false;
        InputFocusStack.Pop(this);
        hideRoutine = null;
        InvokeAndClearHiddenCallback();
    }

    private void CancelActivePresentation()
    {
        timedConversationCompleted = false;
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        InputFocusStack.Pop(this);
        isShowing = false;
        externallyControlled = false;
        InvokeAndClearHiddenCallback();
    }

    private void ResolveReferences()
    {
        if (dialogueText == null)
        {
            GameObject textObject = GameObject.Find(DialogueTextObjectName);
            if (textObject != null)
            {
                dialogueText = textObject.GetComponent<TMP_Text>();
            }
        }

        if (dialoguePanelRoot == null)
        {
            GameObject panelObject = GameObject.Find(DialoguePanelObjectName);
            if (panelObject != null)
            {
                dialoguePanelRoot = panelObject;
            }
            else if (dialogueText != null)
            {
                dialoguePanelRoot = FindRootByName(dialogueText.transform, DialoguePanelObjectName);
            }
        }

        EnsureRuntimeFallback();

        EnsureCanvasGroup();
    }

    private void EnsureRuntimeFallback()
    {
        if (dialogueText != null) return;
        GameObject canvasObject = new GameObject("DialoguePanel_RuntimeFallback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.1f, 0.08f);
        panelRect.anchorMax = new Vector2(0.9f, 0.28f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.05f, 0.94f);

        GameObject textObject = new GameObject("DialogueBox_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(36f, 24f);
        textRect.offsetMax = new Vector2(-36f, -24f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true;
        dialoguePanelRoot = panel;
        dialogueText = text;
        panel.SetActive(false);
    }

    private GameObject FindRootByName(Transform start, string objectName)
    {
        Transform current = start;
        while (current != null)
        {
            if (string.Equals(current.name, objectName, StringComparison.Ordinal))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup != null)
        {
            return;
        }

        if (dialoguePanelRoot == null)
        {
            return;
        }

        canvasGroup = dialoguePanelRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = dialoguePanelRoot.AddComponent<CanvasGroup>();
        }
    }

    private void SetVisible(bool visible)
    {
        if (dialoguePanelRoot != null && !dialoguePanelRoot.activeSelf)
        {
            dialoguePanelRoot.SetActive(true);
        }

        EnsureCanvasGroup();
        SetCanvasAlpha(visible ? 1f : 0f);
        if (canvasGroup != null)
        {
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (dialogueText != null && !dialogueText.gameObject.activeSelf)
        {
            dialogueText.gameObject.SetActive(true);
        }
    }

    private float GetCanvasAlpha()
    {
        EnsureCanvasGroup();
        return canvasGroup != null ? canvasGroup.alpha : 1f;
    }

    private void SetCanvasAlpha(float alpha)
    {
        EnsureCanvasGroup();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    private void InvokeAndClearHiddenCallback()
    {
        Action callback = onHiddenCallback;
        onHiddenCallback = null;
        timedConversationOwner = null;
        callback?.Invoke();
    }
}
