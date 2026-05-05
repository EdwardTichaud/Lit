using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ReadableSentencePuzzleUI : MonoBehaviour
{
    private const string ManagerObjectName = "ReadableSentencePuzzleUI";
    private const string SceneCanvasObjectName = "ReadableSentencePuzzleCanvas";
    private const string ScenePanelObjectName = "PuzzlePanel";
    private const string TitleObjectName = "Title";
    private const string PromptObjectName = "Prompt";
    private const string FeedbackObjectName = "Feedback";
    private const string AnswerInputObjectName = "AnswerInput";
    private const string KeyboardObjectName = "Keyboard";
    private const string CursorObjectName = "Cursor";
    private const string SubmitButtonObjectName = "Valider";
    private const string CancelButtonObjectName = "Annuler";
    private const string RuntimeCanvasObjectName = "ReadableSentencePuzzleCanvas_Auto";
    private const string RootObjectName = "ReadableSentencePuzzle_Root";
    private const string PanelObjectName = "ReadableSentencePuzzle_Panel";

    [Serializable]
    private sealed class PuzzleRequest
    {
        public object Owner;
        public string Title;
        public string Prompt;
        public Action<string> OnSubmit;
        public Action OnCancel;
        public string DebugContext;
    }

    [Header("Runtime UI")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private MenuInputFieldCaret inputCaret;
    [SerializeField] private GridLayoutGroup keyboardLayout;
    [SerializeField] private RectTransform keyboardRoot;
    [SerializeField] private CursorController cursor;
    [SerializeField] private MenuCursorNavigator navigator;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private bool createRuntimeFallback = true;
    [SerializeField] private int fallbackSortingOrder = 260;

    private readonly List<Button> interactiveButtons = new List<Button>();
    private readonly Dictionary<Button, UnityAction> sceneButtonBindings = new Dictionary<Button, UnityAction>();
    private PuzzleRequest activeRequest;
    private bool inputLocked;
    private Coroutine timedFeedbackRoutine;
    private Action timedFeedbackCallback;
    private object timedFeedbackOwner;
    private bool timedFeedbackDismissOnComplete;
    private bool timedFeedbackPending;

    private static ReadableSentencePuzzleUI instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (string.Equals(gameObject.name, ManagerObjectName, StringComparison.Ordinal))
        {
            DontDestroyOnLoad(gameObject);
        }

        ResolveSceneUiReferences();
        WireStaticButtons();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnDisable()
    {
        CancelTimedFeedback(invokeCallback: false);
        ReleaseInputLock();
        HideImmediate();
        activeRequest = null;
        if (instance == this)
        {
            instance = null;
        }
    }

    public static bool TryShow(
        object owner,
        string title,
        string prompt,
        Action<string> onSubmit,
        Action onCancel = null,
        string debugContext = null)
    {
        PuzzleRequest request = new PuzzleRequest
        {
            Owner = owner,
            Title = title,
            Prompt = prompt,
            OnSubmit = onSubmit,
            OnCancel = onCancel,
            DebugContext = debugContext
        };

        ReadableSentencePuzzleUI resolvedInstance = EnsureInstance();
        return resolvedInstance != null && resolvedInstance.TryShowInternal(request);
    }

    public static void Dismiss(object owner, bool invokeCancel = false)
    {
        if (instance == null)
        {
            return;
        }

        instance.DismissInternal(owner, invokeCancel);
    }

    public static bool IsShowingFor(object owner)
    {
        return instance != null && MatchesOwner(instance.activeRequest != null ? instance.activeRequest.Owner : null, owner);
    }

    public static bool BeginFeedbackAndDismiss(object owner, string message, bool isError, float delaySeconds, Action onDismissed = null)
    {
        if (instance == null)
        {
            return false;
        }

        return instance.BeginTimedFeedbackInternal(owner, message, isError, delaySeconds, dismissAfterDelay: true, onDismissed);
    }

    public static bool BeginFeedback(object owner, string message, bool isError, float delaySeconds, Action onCompleted = null)
    {
        if (instance == null)
        {
            return false;
        }

        return instance.BeginTimedFeedbackInternal(owner, message, isError, delaySeconds, dismissAfterDelay: false, onCompleted);
    }

    public static void SetInteractable(object owner, bool interactive)
    {
        if (instance == null || !MatchesOwner(instance.activeRequest != null ? instance.activeRequest.Owner : null, owner))
        {
            return;
        }

        instance.SetInteractive(interactive);
    }

    public static void SetFeedback(object owner, string message, bool isError)
    {
        if (instance == null || !MatchesOwner(instance.activeRequest != null ? instance.activeRequest.Owner : null, owner))
        {
            return;
        }

        instance.SetFeedbackInternal(message, isError);
    }

    private static ReadableSentencePuzzleUI EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

#if UNITY_2023_1_OR_NEWER
        instance = FindFirstObjectByType<ReadableSentencePuzzleUI>(FindObjectsInactive.Include);
#else
        instance = FindObjectOfType<ReadableSentencePuzzleUI>(true);
#endif
        if (instance != null)
        {
            return instance;
        }

        GameObject managerObject = new GameObject(ManagerObjectName);
        instance = managerObject.AddComponent<ReadableSentencePuzzleUI>();
        return instance;
    }

    private bool TryShowInternal(PuzzleRequest request)
    {
        if (request == null || request.OnSubmit == null)
        {
            return false;
        }

        if (!EnsureUiReferences())
        {
            return false;
        }

        if (activeRequest != null && !MatchesOwner(activeRequest.Owner, request.Owner))
        {
            return false;
        }

        CancelTimedFeedback(invokeCallback: false);
        activeRequest = request;
        SetRootVisible(true);
        titleText.text = string.IsNullOrWhiteSpace(request.Title) ? "Enigme" : request.Title.Trim();
        promptText.text = string.IsNullOrWhiteSpace(request.Prompt) ? string.Empty : request.Prompt.Trim();
        SetFeedbackInternal(string.Empty, isError: false);
        inputField.text = string.Empty;
        SetInteractive(true);
        AcquireInputLock();
        FocusInputField();
        RefreshKeyboardCursor();
        return true;
    }

    private void DismissInternal(object owner, bool invokeCancel)
    {
        if (activeRequest == null)
        {
            return;
        }

        if (owner != null && !MatchesOwner(activeRequest.Owner, owner))
        {
            return;
        }

        CancelTimedFeedback(invokeCallback: false);
        PuzzleRequest request = activeRequest;
        HideAndClear();
        if (invokeCancel)
        {
            SafeInvoke(request.OnCancel);
        }
    }

    private bool BeginTimedFeedbackInternal(object owner, string message, bool isError, float delaySeconds, bool dismissAfterDelay, Action onCompleted)
    {
        if (activeRequest == null)
        {
            return false;
        }

        if (owner != null && !MatchesOwner(activeRequest.Owner, owner))
        {
            return false;
        }

        CancelTimedFeedback(invokeCallback: false);
        timedFeedbackPending = true;
        timedFeedbackDismissOnComplete = dismissAfterDelay;
        timedFeedbackCallback = onCompleted;
        timedFeedbackOwner = activeRequest.Owner;

        SetInteractive(false);
        SetFeedbackInternal(message, isError);
        ClearInputSelection(clearText: false);

        float resolvedDelay = Mathf.Max(0f, delaySeconds);
        if (resolvedDelay <= 0f)
        {
            CompleteTimedFeedback();
            return true;
        }

        timedFeedbackRoutine = StartCoroutine(CompleteFeedbackAfterDelay(resolvedDelay));
        return true;
    }

    private bool EnsureUiReferences()
    {
        if (!HasResolvedUiReferences())
        {
            ResolveSceneUiReferences();
        }

        if (!HasResolvedUiReferences())
        {
            Debug.LogWarning($"[ReadableSentencePuzzleUI] PuzzlePanel/ReadableSentencePuzzleCanvas introuvable ou incomplet. Elements manquants: {DescribeMissingUiReferences()}", this);
            return false;
        }

        WireStaticButtons();
        ConfigureNavigator();
        return true;
    }

    private bool HasResolvedUiReferences()
    {
        return rootCanvasGroup != null &&
            titleText != null &&
            promptText != null &&
            feedbackText != null &&
            inputField != null &&
            submitButton != null &&
            cancelButton != null;
    }

    private void ResolveSceneUiReferences()
    {
        rootCanvasGroup = rootCanvasGroup != null
            ? rootCanvasGroup
            : FindSceneComponentByName<CanvasGroup>(ScenePanelObjectName);

        Transform panelRoot = rootCanvasGroup != null
            ? rootCanvasGroup.transform
            : FindSceneTransform(ScenePanelObjectName);
        Transform canvasRoot = FindSceneTransform(SceneCanvasObjectName);

        if (rootCanvasGroup == null && canvasRoot != null)
        {
            rootCanvasGroup = canvasRoot.GetComponentInParent<CanvasGroup>(true);
        }

        if (rootCanvasGroup == null && panelRoot != null)
        {
            rootCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = panelRoot.GetComponentInParent<CanvasGroup>(true);
            }
        }

        Transform searchRoot = rootCanvasGroup != null ? rootCanvasGroup.transform : panelRoot;
        if (searchRoot == null && canvasRoot == null)
        {
            return;
        }

        titleText = ResolveNamedComponent(titleText, TitleObjectName, searchRoot, canvasRoot);
        promptText = ResolveNamedComponent(promptText, PromptObjectName, searchRoot, canvasRoot);
        feedbackText = ResolveNamedComponent(feedbackText, FeedbackObjectName, searchRoot, canvasRoot);
        Transform inputRoot = ResolveNamedTransform(AnswerInputObjectName, searchRoot, canvasRoot);
        inputField = ResolveInputField(inputField, inputRoot, searchRoot, canvasRoot);
        inputCaret = inputCaret != null
            ? inputCaret
            : ResolveInputCaret(inputField, inputRoot);
        keyboardLayout = ResolveNamedComponent(keyboardLayout, KeyboardObjectName, searchRoot, canvasRoot);
        keyboardLayout = ResolveComponentInRoots(keyboardLayout, searchRoot, canvasRoot);
        keyboardRoot = keyboardRoot != null
            ? keyboardRoot
            : ResolveNamedComponent(keyboardRoot, KeyboardObjectName, searchRoot, canvasRoot);
        if (keyboardRoot == null && keyboardLayout != null)
        {
            keyboardRoot = keyboardLayout.transform as RectTransform;
        }

        cursor = ResolveNamedComponent(cursor, CursorObjectName, searchRoot, canvasRoot);
        cursor = ResolveComponentInRoots(cursor, searchRoot, canvasRoot);
        navigator = ResolveComponentInRoots(navigator, searchRoot, canvasRoot);
        submitButton = ResolveNamedComponent(submitButton, SubmitButtonObjectName, searchRoot, canvasRoot);
        cancelButton = ResolveNamedComponent(cancelButton, CancelButtonObjectName, searchRoot, canvasRoot);

        interactiveButtons.Clear();
        Button[] buttons = GetButtonsInRoots(searchRoot, canvasRoot);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && !interactiveButtons.Contains(button))
            {
                interactiveButtons.Add(button);
            }
        }
    }

    private string DescribeMissingUiReferences()
    {
        List<string> missing = new List<string>();
        if (rootCanvasGroup == null)
        {
            missing.Add(ScenePanelObjectName);
        }

        if (titleText == null)
        {
            missing.Add(TitleObjectName);
        }

        if (promptText == null)
        {
            missing.Add(PromptObjectName);
        }

        if (feedbackText == null)
        {
            missing.Add(FeedbackObjectName);
        }

        if (inputField == null)
        {
            missing.Add(AnswerInputObjectName);
        }

        if (submitButton == null)
        {
            missing.Add(SubmitButtonObjectName);
        }

        if (cancelButton == null)
        {
            missing.Add(CancelButtonObjectName);
        }

        return missing.Count == 0 ? "aucun" : string.Join(", ", missing);
    }

    private void ConfigureNavigator()
    {
        if (navigator == null || cursor == null)
        {
            return;
        }

        navigator.ConfigureRuntime(
            cursor,
            runtimeRequireFocus: false,
            runtimePushFocusOnEnable: false,
            runtimeUseInteractInput: true,
            runtimeUseReturnInput: true,
            runtimeAllowButtonFallback: true);
        navigator.ReplaceCancelHandler(CancelActiveRequest);
    }

    private static T ResolveNamedComponent<T>(T current, string objectName, params Transform[] roots) where T : Component
    {
        if (current != null)
        {
            return current;
        }

        Transform target = ResolveNamedTransform(objectName, roots);
        if (target == null)
        {
            return FindSceneComponentByName<T>(objectName);
        }

        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return target.GetComponentInChildren<T>(true);
    }

    private static Transform ResolveNamedTransform(string objectName, params Transform[] roots)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i];
            if (root == null)
            {
                continue;
            }

            Transform target = FindChildRecursive(root, objectName);
            if (target == null)
            {
                continue;
            }

            return target;
        }

        return FindSceneTransform(objectName);
    }

    private static T ResolveComponentInRoots<T>(T current, params Transform[] roots) where T : Component
    {
        if (current != null)
        {
            return current;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i];
            if (root == null)
            {
                continue;
            }

            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static Button[] GetButtonsInRoots(params Transform[] roots)
    {
        List<Button> buttons = new List<Button>();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i];
            if (root == null)
            {
                continue;
            }

            Button[] foundButtons = root.GetComponentsInChildren<Button>(true);
            for (int j = 0; j < foundButtons.Length; j++)
            {
                Button button = foundButtons[j];
                if (button != null && !buttons.Contains(button))
                {
                    buttons.Add(button);
                }
            }
        }

        return buttons.ToArray();
    }

    private static TMP_InputField ResolveInputField(TMP_InputField current, Transform inputRoot, params Transform[] roots)
    {
        if (current != null)
        {
            return current;
        }

        TMP_InputField resolved = null;
        if (inputRoot != null)
        {
            resolved = inputRoot.GetComponent<TMP_InputField>();
            if (resolved == null)
            {
                resolved = inputRoot.GetComponentInChildren<TMP_InputField>(true);
            }

            if (resolved == null)
            {
                resolved = ConfigureExistingSceneInputField(inputRoot);
            }
        }

        if (resolved != null)
        {
            return resolved;
        }

        return ResolveComponentInRoots<TMP_InputField>(null, roots);
    }

    private static MenuInputFieldCaret ResolveInputCaret(TMP_InputField field, Transform inputRoot)
    {
        if (field != null)
        {
            MenuInputFieldCaret caret = field.GetComponent<MenuInputFieldCaret>();
            if (caret == null)
            {
                caret = field.GetComponentInParent<MenuInputFieldCaret>(true);
            }

            if (caret != null)
            {
                return caret;
            }
        }

        return inputRoot != null ? inputRoot.GetComponent<MenuInputFieldCaret>() : null;
    }

    private static TMP_InputField ConfigureExistingSceneInputField(Transform inputRoot)
    {
        if (inputRoot == null)
        {
            return null;
        }

        TMP_InputField field = inputRoot.GetComponent<TMP_InputField>();
        if (field == null)
        {
            field = inputRoot.gameObject.AddComponent<TMP_InputField>();
        }

        RectTransform textArea = ResolveNamedChildComponent<RectTransform>(inputRoot, "Text Area");
        if (textArea == null)
        {
            textArea = inputRoot as RectTransform;
        }

        TMP_Text text = ResolveNamedChildComponent<TMP_Text>(inputRoot, "Text");
        TMP_Text placeholder = ResolveNamedChildComponent<TMP_Text>(inputRoot, "Placeholder");
        if (text == null)
        {
            TMP_Text[] texts = inputRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];
                if (candidate == null || candidate == placeholder)
                {
                    continue;
                }

                text = candidate;
                break;
            }
        }

        if (text == null)
        {
            return null;
        }

        field.textViewport = textArea;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = field.characterLimit > 0 ? field.characterLimit : 512;
        return field;
    }

    private static T ResolveNamedChildComponent<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform target = FindChildRecursive(root, objectName);
        if (target == null)
        {
            return null;
        }

        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return target.GetComponentInChildren<T>(true);
    }

    private static Transform FindSceneTransform(string objectName)
    {
        return FindSceneComponentByName<Transform>(objectName);
    }

    private static T FindSceneComponentByName<T>(string objectName) where T : Component
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        T[] candidates = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            GameObject candidateObject = candidate.gameObject;
            if (candidateObject == null ||
                !candidateObject.scene.IsValid() ||
                !string.Equals(candidateObject.name, objectName, StringComparison.Ordinal))
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

            if (string.Equals(child.name, objectName, StringComparison.Ordinal))
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

    private void CreateRuntimeFallbackUi()
    {
        GameObject canvasObject = new GameObject(RuntimeCanvasObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = fallbackSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObject = new GameObject(RootObjectName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.SetParent(canvasObject.transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.72f);
        rootImage.raycastTarget = true;
        rootCanvasGroup = rootObject.GetComponent<CanvasGroup>();

        GameObject panelObject = new GameObject(PanelObjectName, typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.SetParent(rootRect, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1220f, 760f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.09f, 0.07f, 0.05f, 0.98f);
        panelImage.raycastTarget = true;

        titleText = CreateText("Title", panelRect, new Vector2(0.5f, 0.91f), new Vector2(1020f, 64f), 44f, TextAlignmentOptions.Center);
        promptText = CreateText("Prompt", panelRect, new Vector2(0.5f, 0.79f), new Vector2(1020f, 120f), 28f, TextAlignmentOptions.Center);
        promptText.textWrappingMode = TextWrappingModes.Normal;

        feedbackText = CreateText("Feedback", panelRect, new Vector2(0.5f, 0.685f), new Vector2(980f, 44f), 24f, TextAlignmentOptions.Center);

        inputField = CreateInputField(panelRect);
        inputCaret = inputField.GetComponent<MenuInputFieldCaret>();

        keyboardRoot = CreateKeyboardRoot(panelRect, out keyboardLayout);
        cursor = CreateKeyboardCursor(panelRect, keyboardRoot, keyboardLayout);
        navigator = CreateKeyboardNavigator(panelObject, cursor);

        BuildKeyboardButtons();
    }

    private void BuildKeyboardButtons()
    {
        interactiveButtons.Clear();
        CreateKeyButton("A", () => AppendText("A"));
        CreateKeyButton("B", () => AppendText("B"));
        CreateKeyButton("C", () => AppendText("C"));
        CreateKeyButton("D", () => AppendText("D"));
        CreateKeyButton("E", () => AppendText("E"));
        CreateKeyButton("F", () => AppendText("F"));
        CreateKeyButton("G", () => AppendText("G"));
        CreateKeyButton("H", () => AppendText("H"));
        CreateKeyButton("I", () => AppendText("I"));
        CreateKeyButton("J", () => AppendText("J"));
        CreateKeyButton("K", () => AppendText("K"));
        CreateKeyButton("L", () => AppendText("L"));
        CreateKeyButton("M", () => AppendText("M"));
        CreateKeyButton("N", () => AppendText("N"));
        CreateKeyButton("O", () => AppendText("O"));
        CreateKeyButton("P", () => AppendText("P"));
        CreateKeyButton("Q", () => AppendText("Q"));
        CreateKeyButton("R", () => AppendText("R"));
        CreateKeyButton("S", () => AppendText("S"));
        CreateKeyButton("T", () => AppendText("T"));
        CreateKeyButton("U", () => AppendText("U"));
        CreateKeyButton("V", () => AppendText("V"));
        CreateKeyButton("W", () => AppendText("W"));
        CreateKeyButton("X", () => AppendText("X"));
        CreateKeyButton("Y", () => AppendText("Y"));
        CreateKeyButton("Z", () => AppendText("Z"));
        CreateKeyButton("0", () => AppendText("0"));
        CreateKeyButton("1", () => AppendText("1"));
        CreateKeyButton("2", () => AppendText("2"));
        CreateKeyButton("3", () => AppendText("3"));
        CreateKeyButton("4", () => AppendText("4"));
        CreateKeyButton("5", () => AppendText("5"));
        CreateKeyButton("6", () => AppendText("6"));
        CreateKeyButton("7", () => AppendText("7"));
        CreateKeyButton("8", () => AppendText("8"));
        CreateKeyButton("9", () => AppendText("9"));
        CreateKeyButton("Espace", () => AppendText(" "), minWidth: 220f);
        CreateKeyButton("Effacer", Backspace, minWidth: 220f);
        CreateKeyButton("Vider", ClearInput, minWidth: 220f);
        submitButton = CreateKeyButton("Valider", SubmitCurrentAnswer, minWidth: 220f);
        cancelButton = CreateKeyButton("Annuler", CancelActiveRequest, minWidth: 220f);
    }

    private void WireStaticButtons()
    {
        if (inputField != null)
        {
            inputField.onSubmit.RemoveListener(OnInputFieldSubmitted);
        }

        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(SubmitCurrentAnswer);
            submitButton.onClick.AddListener(SubmitCurrentAnswer);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(CancelActiveRequest);
            cancelButton.onClick.AddListener(CancelActiveRequest);
        }

        WireSceneKeyboardButtons();
    }

    private void WireSceneKeyboardButtons()
    {
        if (sceneButtonBindings.Count > 0)
        {
            List<Button> staleButtons = null;
            foreach (KeyValuePair<Button, UnityAction> pair in sceneButtonBindings)
            {
                if (pair.Key != null && interactiveButtons.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Key != null && pair.Value != null)
                {
                    pair.Key.onClick.RemoveListener(pair.Value);
                }

                if (staleButtons == null)
                {
                    staleButtons = new List<Button>();
                }

                staleButtons.Add(pair.Key);
            }

            if (staleButtons != null)
            {
                for (int i = 0; i < staleButtons.Count; i++)
                {
                    sceneButtonBindings.Remove(staleButtons[i]);
                }
            }
        }

        for (int i = 0; i < interactiveButtons.Count; i++)
        {
            Button button = interactiveButtons[i];
            if (button == null || button == submitButton || button == cancelButton)
            {
                continue;
            }

            if (sceneButtonBindings.TryGetValue(button, out UnityAction previousAction) && previousAction != null)
            {
                button.onClick.RemoveListener(previousAction);
                sceneButtonBindings.Remove(button);
            }

            if (!TryCreateSceneKeyboardAction(button, out UnityAction action) || action == null)
            {
                continue;
            }

            button.onClick.AddListener(action);
            sceneButtonBindings[button] = action;
        }
    }

    private bool TryCreateSceneKeyboardAction(Button button, out UnityAction action)
    {
        action = null;
        string command = ResolveSceneButtonCommand(button);
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        switch (command)
        {
            case SubmitButtonObjectName:
            case CancelButtonObjectName:
                return false;
            case "Espace":
                action = () => AppendText(" ");
                return true;
            case "Effacer":
                action = Backspace;
                return true;
            case "Vider":
                action = ClearInput;
                return true;
        }

        if (command.Length == 1 && char.IsLetterOrDigit(command[0]))
        {
            string value = command;
            action = () => AppendText(value);
            return true;
        }

        return false;
    }

    private static string ResolveSceneButtonCommand(Button button)
    {
        if (button == null)
        {
            return string.Empty;
        }

        string name = button.gameObject != null ? button.gameObject.name : string.Empty;
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        return label != null && !string.IsNullOrWhiteSpace(label.text)
            ? label.text.Trim()
            : string.Empty;
    }

    private void SubmitCurrentAnswer()
    {
        if (activeRequest == null || timedFeedbackPending || !IsInteractive())
        {
            return;
        }

        string value = inputField != null ? inputField.text ?? string.Empty : string.Empty;
        SafeInvoke(activeRequest.OnSubmit, value);
    }

    private void CancelActiveRequest()
    {
        if (timedFeedbackPending)
        {
            return;
        }

        DismissInternal(activeRequest != null ? activeRequest.Owner : null, invokeCancel: true);
    }

    private void OnInputFieldSubmitted(string _)
    {
        FocusInputField();
    }

    private void AppendText(string value)
    {
        if (inputField == null || !inputField.interactable)
        {
            return;
        }

        inputField.text += value;
        inputField.ForceLabelUpdate();
        FocusInputField();
    }

    private void Backspace()
    {
        if (inputField == null || !inputField.interactable)
        {
            return;
        }

        string value = inputField.text ?? string.Empty;
        if (value.Length == 0)
        {
            return;
        }

        inputField.text = value.Substring(0, value.Length - 1);
        inputField.ForceLabelUpdate();
        FocusInputField();
    }

    private void ClearInput()
    {
        if (inputField == null || !inputField.interactable)
        {
            return;
        }

        inputField.text = string.Empty;
        inputField.ForceLabelUpdate();
        FocusInputField();
    }

    private void SetInteractive(bool interactive)
    {
        if (inputField != null)
        {
            inputField.interactable = interactive;
        }

        for (int i = 0; i < interactiveButtons.Count; i++)
        {
            if (interactiveButtons[i] != null)
            {
                interactiveButtons[i].interactable = interactive;
            }
        }

        if (cursor != null)
        {
            cursor.allowInput = interactive;
            if (interactive)
            {
                cursor.Refresh();
            }

            if (cursor.cursor != null)
            {
                cursor.cursor.gameObject.SetActive(interactive && cursor.CurrentItem != null);
            }
        }
    }

    private bool IsInteractive()
    {
        return inputField != null && inputField.interactable;
    }

    private void SetFeedbackInternal(string message, bool isError)
    {
        if (feedbackText == null)
        {
            return;
        }

        feedbackText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        feedbackText.color = isError ? new Color(1f, 0.55f, 0.55f, 1f) : new Color(0.75f, 0.92f, 0.74f, 1f);
    }

    private void RefreshKeyboardCursor()
    {
        if (cursor == null)
        {
            return;
        }

        cursor.Refresh();
        cursor.SelectFirst();
    }

    private void FocusInputField()
    {
        if (inputField == null || !inputField.isActiveAndEnabled || !inputField.interactable)
        {
            return;
        }

        inputField.Select();
        inputField.ActivateInputField();
        if (inputCaret != null)
        {
            inputCaret.Bind(inputField);
        }
    }

    private void AcquireInputLock()
    {
        if (inputLocked)
        {
            InputFocusStack.Push(this);
            return;
        }

        inputLocked = true;
        InputFocusStack.Push(this);
        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.SetInputLocked(true);
        }
    }

    private void ReleaseInputLock()
    {
        if (!inputLocked)
        {
            InputFocusStack.Pop(this);
            return;
        }

        inputLocked = false;
        InputFocusStack.Pop(this);
        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.SetInputLocked(false);
        }
    }

    private void HideAndClear()
    {
        CancelTimedFeedback(invokeCallback: false);
        ClearInputSelection(clearText: true);
        SetInteractive(false);
        ReleaseInputLock();
        SetFeedbackInternal(string.Empty, isError: false);
        SetRootVisible(false);
        activeRequest = null;
    }

    private void ClearInputSelection(bool clearText)
    {
        if (inputField != null)
        {
            if (clearText)
            {
                inputField.text = string.Empty;
            }

            inputField.DeactivateInputField();
        }

        EventSystem currentEventSystem = EventSystem.current;
        if (currentEventSystem == null)
        {
            return;
        }

        GameObject selectedObject = currentEventSystem.currentSelectedGameObject;
        if (selectedObject == null)
        {
            return;
        }

        Transform root = rootCanvasGroup != null ? rootCanvasGroup.transform : transform;
        if (selectedObject == inputField?.gameObject || selectedObject.transform.IsChildOf(root))
        {
            currentEventSystem.SetSelectedGameObject(null);
        }
    }

    private IEnumerator CompleteFeedbackAfterDelay(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        timedFeedbackRoutine = null;
        CompleteTimedFeedback();
    }

    private void CompleteTimedFeedback()
    {
        Action callback = timedFeedbackCallback;
        object owner = timedFeedbackOwner;
        bool dismissAfterDelay = timedFeedbackDismissOnComplete;

        timedFeedbackPending = false;
        timedFeedbackDismissOnComplete = false;
        timedFeedbackCallback = null;
        timedFeedbackOwner = null;

        if (dismissAfterDelay)
        {
            DismissInternal(owner, invokeCancel: false);
        }
        else if (activeRequest != null && MatchesOwner(activeRequest.Owner, owner))
        {
            SetFeedbackInternal(string.Empty, isError: false);
            SetInteractive(true);
            FocusInputField();
            RefreshKeyboardCursor();
        }

        SafeInvoke(callback);
    }

    private void CancelTimedFeedback(bool invokeCallback)
    {
        if (timedFeedbackRoutine != null)
        {
            StopCoroutine(timedFeedbackRoutine);
            timedFeedbackRoutine = null;
        }

        Action callback = timedFeedbackCallback;
        timedFeedbackPending = false;
        timedFeedbackDismissOnComplete = false;
        timedFeedbackCallback = null;
        timedFeedbackOwner = null;

        if (invokeCallback)
        {
            SafeInvoke(callback);
        }
    }

    private void HideImmediate()
    {
        if (rootCanvasGroup == null)
        {
            return;
        }

        rootCanvasGroup.alpha = 0f;
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
        rootCanvasGroup.gameObject.SetActive(false);
    }

    private void SetRootVisible(bool visible)
    {
        if (rootCanvasGroup == null)
        {
            return;
        }

        rootCanvasGroup.gameObject.SetActive(true);
        rootCanvasGroup.alpha = visible ? 1f : 0f;
        rootCanvasGroup.interactable = visible;
        rootCanvasGroup.blocksRaycasts = visible;
        if (!visible)
        {
            rootCanvasGroup.gameObject.SetActive(false);
        }
    }

    private Button CreateKeyButton(string label, UnityAction action, float minWidth = 0f)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(MenuCursorButtonHandler));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(keyboardRoot, false);
        rect.sizeDelta = new Vector2(Mathf.Max(110f, minWidth), 60f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.21f, 0.18f, 0.14f, 1f);

        LayoutElement element = buttonObject.GetComponent<LayoutElement>();
        if (minWidth > 0f)
        {
            element.minWidth = minWidth;
        }
        element.minHeight = 60f;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.21f, 0.18f, 0.14f, 1f);
        colors.highlightedColor = new Color(0.36f, 0.27f, 0.16f, 1f);
        colors.pressedColor = new Color(0.58f, 0.4f, 0.2f, 1f);
        colors.selectedColor = new Color(0.43f, 0.31f, 0.18f, 1f);
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        button.colors = colors;
        button.onClick.AddListener(action);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.color = Color.white;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        interactiveButtons.Add(button);
        return button;
    }

    private static TextMeshProUGUI CreateText(string objectName, RectTransform parent, Vector2 anchor, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return text;
    }

    private static TMP_InputField CreateInputField(RectTransform parent)
    {
        GameObject fieldObject = new GameObject("AnswerInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField), typeof(MenuInputFieldCaret));
        RectTransform fieldRect = fieldObject.GetComponent<RectTransform>();
        fieldRect.SetParent(parent, false);
        fieldRect.anchorMin = new Vector2(0.5f, 0.58f);
        fieldRect.anchorMax = new Vector2(0.5f, 0.58f);
        fieldRect.pivot = new Vector2(0.5f, 0.5f);
        fieldRect.sizeDelta = new Vector2(1000f, 72f);

        Image fieldImage = fieldObject.GetComponent<Image>();
        fieldImage.color = new Color(0.14f, 0.12f, 0.1f, 1f);

        TMP_InputField field = fieldObject.GetComponent<TMP_InputField>();
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 512;

        GameObject textAreaObject = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        RectTransform textAreaRect = textAreaObject.GetComponent<RectTransform>();
        textAreaRect.SetParent(fieldRect, false);
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(18f, 10f);
        textAreaRect.offsetMax = new Vector2(-18f, -10f);

        TextMeshProUGUI text = CreateText("Text", textAreaRect, new Vector2(0.5f, 0.5f), Vector2.zero, 30f, TextAlignmentOptions.Left);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI placeholder = CreateText("Placeholder", textAreaRect, new Vector2(0.5f, 0.5f), Vector2.zero, 30f, TextAlignmentOptions.Left);
        RectTransform placeholderRect = placeholder.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        placeholder.text = "Entrez votre reponse";
        placeholder.color = new Color(1f, 1f, 1f, 0.38f);

        field.textViewport = textAreaRect;
        field.textComponent = text;
        field.placeholder = placeholder;

        return field;
    }

    private static RectTransform CreateKeyboardRoot(RectTransform parent, out GridLayoutGroup layout)
    {
        GameObject keyboardObject = new GameObject("Keyboard", typeof(RectTransform), typeof(GridLayoutGroup));
        RectTransform keyboardRect = keyboardObject.GetComponent<RectTransform>();
        keyboardRect.SetParent(parent, false);
        keyboardRect.anchorMin = new Vector2(0.5f, 0.23f);
        keyboardRect.anchorMax = new Vector2(0.5f, 0.23f);
        keyboardRect.pivot = new Vector2(0.5f, 0.5f);
        keyboardRect.sizeDelta = new Vector2(1020f, 320f);

        layout = keyboardObject.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(110f, 60f);
        layout.spacing = new Vector2(12f, 12f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 7;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperCenter;

        return keyboardRect;
    }

    private static CursorController CreateKeyboardCursor(RectTransform parent, RectTransform keyboardRoot, GridLayoutGroup layout)
    {
        GameObject cursorObject = new GameObject("Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CursorController));
        RectTransform cursorRect = cursorObject.GetComponent<RectTransform>();
        cursorRect.SetParent(parent, false);
        cursorRect.anchorMin = new Vector2(0.5f, 0.5f);
        cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        cursorRect.pivot = new Vector2(0.5f, 0.5f);
        cursorRect.sizeDelta = new Vector2(26f, 26f);

        Image cursorImage = cursorObject.GetComponent<Image>();
        cursorImage.color = new Color(0.96f, 0.8f, 0.33f, 1f);
        cursorImage.raycastTarget = false;

        CursorController sharedCursor = cursorObject.GetComponent<CursorController>();
        sharedCursor.layoutGroup = layout;
        sharedCursor.itemsParent = keyboardRoot;
        sharedCursor.cursor = cursorRect;
        sharedCursor.cursorParentOverride = parent;
        sharedCursor.placement = CursorController.CursorPlacement.RightOfTarget;
        sharedCursor.rightOffset = new Vector2(24f, 0f);
        sharedCursor.matchTargetSize = false;
        sharedCursor.itemFilter = CursorController.ItemFilter.MenuCursorHandlerOnly;
        sharedCursor.allowInput = true;
        sharedCursor.resetToFirstOnEnable = true;
        sharedCursor.startIndex = 0;
        sharedCursor.wrap = true;
        return sharedCursor;
    }

    private static MenuCursorNavigator CreateKeyboardNavigator(GameObject panelObject, CursorController cursor)
    {
        MenuCursorNavigator createdNavigator = panelObject.AddComponent<MenuCursorNavigator>();
        createdNavigator.ConfigureRuntime(
            cursor,
            runtimeRequireFocus: false,
            runtimePushFocusOnEnable: false,
            runtimeUseInteractInput: true,
            runtimeUseReturnInput: true,
            runtimeAllowButtonFallback: false);
        createdNavigator.ReplaceCancelHandler(instance.CancelActiveRequest);
        return createdNavigator;
    }

    private static bool MatchesOwner(object left, object right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        return ReferenceEquals(left, right);
    }

    private static void SafeInvoke(Action callback)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private static void SafeInvoke(Action<string> callback, string value)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback.Invoke(value);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
