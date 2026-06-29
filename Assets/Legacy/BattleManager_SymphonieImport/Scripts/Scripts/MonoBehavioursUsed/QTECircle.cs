using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class QTECircle : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private QTEInputSO qteInput;
    [SerializeField] private BattleInputType fallbackInput = BattleInputType.Confirm;
    [SerializeField] private Image inputImage;

    [Header("Circle")]
    [SerializeField] private Transform movingCircle;
    [SerializeField] private Image fixCircleImage;
    [SerializeField] private Vector3 targetScale = Vector3.one;
    [FormerlySerializedAs("shrinkDuration")]
    [SerializeField] private float responseDelaySeconds = 1f;
    [SerializeField] private float responseScale = 2f;
    [SerializeField] private float inputHideScale = 1.5f;
    [SerializeField] private float failFadeDuration = 0.35f;
    [SerializeField] private float successRingDuration = 1f;
    [SerializeField] private float successRingScaleMultiplier = 1.5f;
    [SerializeField] private bool resetScaleOnEnable = true;

    [Header("Result Feedback")]
    [SerializeField] private Color successColor = new Color(0.25f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color perfectColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color failColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private float failHoldDuration = 0.2f;

    [Header("Success Windows")]
    [SerializeField] private float successMinScale = 1.5f;
    [SerializeField] private float successMaxScale = 2.5f;
    [SerializeField] private float perfectMinScale = 1.9f;
    [SerializeField] private float perfectMaxScale = 2.1f;
    [Header("Events")]
    [SerializeField] private UnityEvent onSuccess;
    [SerializeField] private UnityEvent onPerfect;
    [SerializeField] private UnityEvent onFail;

    private const float scaleEpsilon = 0.001f;
    private bool resolved;
    private float elapsed;
    private float totalDurationSeconds;
    private Vector3 initialScale;
    private float designStartScale;
    private float shrinkSpeed;
    private float currentResponseDelaySeconds;
    private bool shrinkSpeedInitialized;
    private bool inputHidden;
    private QTEFeedback resultFeedback = QTEFeedback.Miss;
    private static int lastInputHandledFrame = -1;
    private bool autoDestroyAfterFade = true;
    private bool visualsHidden;

    private InputAction requiredAction;
    private InputAction[] cachedActions;
    private bool cachedActionsValid;
    private CanvasGroup canvasGroup;
    private Image movingCircleImage;

    private static readonly List<QTECircle> inputQueue = new();

    public bool IsResolved => resolved;
    public bool WasSuccessful => resultFeedback == QTEFeedback.Good || resultFeedback == QTEFeedback.Perfect;
    public bool WasPerfect => resultFeedback == QTEFeedback.Perfect;
    public QTEFeedback Feedback => resultFeedback;

    public float EstimateLifetimeSeconds(float responseDelay)
    {
        ResolveReferences();
        InitializeShrinkSpeed();
        float startScale = ComputeStartScaleForDelay(responseDelay);
        float speed = Mathf.Max(0.001f, shrinkSpeed);
        return Mathf.Max(0f, (startScale - targetScale.x) / speed);
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureCanvasGroup();
        InitializeShrinkSpeed();
        currentResponseDelaySeconds = responseDelaySeconds;
        ConfigureResponseDelay(currentResponseDelaySeconds, log: false);
        ApplyInputSprite(null);
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureCanvasGroup();
        InitializeShrinkSpeed();
        currentResponseDelaySeconds = responseDelaySeconds;
        ConfigureResponseDelay(currentResponseDelaySeconds, log: false);

        elapsed = 0f;
        resolved = false;
        resultFeedback = QTEFeedback.Miss;
        inputHidden = false;
        RegisterInputQueue();
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        CacheInputActions();
        ApplyInputSprite(null);
    }

    private void OnDisable()
    {
        UnregisterInputQueue();
    }

    private void Update()
    {
        if (resolved)
            return;

        if (movingCircle == null)
        {
            ResolveFail();
            return;
        }

        if (IsInputOwner() && TryHandleInput())
            return;

        UpdateShrink();
        UpdateInputVisibility();

        if (movingCircle.localScale.x <= targetScale.x + scaleEpsilon)
            ResolveFail();
    }

    public void Initialize(QTEInputSO input, BattleInputType fallback, Sprite iconOverride, float responseDelay)
    {
        qteInput = input;
        fallbackInput = fallback;
        if (responseDelay > 0f)
            responseDelaySeconds = responseDelay;

        currentResponseDelaySeconds = responseDelay > 0f ? responseDelay : responseDelaySeconds;

        ResolveReferences();
        InitializeShrinkSpeed();
        ConfigureResponseDelay(currentResponseDelaySeconds, log: true);
        elapsed = 0f;
        inputHidden = false;
        ApplyInputSprite(iconOverride);
        CacheInputActions();
    }

    private void ResolveReferences()
    {
        if (movingCircle == null && transform.childCount > 0)
            movingCircle = transform.GetChild(0);
        if (movingCircleImage == null && movingCircle != null)
            movingCircleImage = movingCircle.GetComponent<Image>();

        if (fixCircleImage == null)
        {
            var fixTransform = transform.Find("FixCircle");
            if (fixTransform != null)
                fixCircleImage = fixTransform.GetComponent<Image>();
        }

        if (inputImage == null)
        {
            var inputTransform = transform.Find("Input");
            if (inputTransform != null)
                inputImage = inputTransform.GetComponent<Image>();
        }
    }

    private void UpdateShrink()
    {
        if (totalDurationSeconds <= 0f)
        {
            movingCircle.localScale = Vector3.MoveTowards(
                movingCircle.localScale,
                targetScale,
                Time.unscaledDeltaTime);
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / totalDurationSeconds);
        movingCircle.localScale = Vector3.Lerp(initialScale, targetScale, t);
    }

    private void UpdateInputVisibility()
    {
        if (inputHidden || inputImage == null)
            return;

        if (movingCircle.localScale.x <= inputHideScale)
        {
            inputHidden = true;
            inputImage.enabled = false;
        }
    }

    private bool TryHandleInput()
    {
        if (lastInputHandledFrame == Time.frameCount)
            return false;

        if (!TryGetTriggeredInput(out bool correctInput))
            return false;

        if (!correctInput)
        {
            ResolveFail();
            return true;
        }

        float scale = movingCircle.localScale.x;
        if (scale >= perfectMinScale && scale <= perfectMaxScale)
            ResolvePerfect();
        else if (scale >= successMinScale && scale <= successMaxScale)
            ResolveSuccess();
        else
            ResolveFail();

        lastInputHandledFrame = Time.frameCount;

        return true;
    }

    private bool TryGetTriggeredInput(out bool correctInput)
    {
        correctInput = false;
        if (!cachedActionsValid)
            CacheInputActions();

        if (requiredAction != null && requiredAction.triggered)
        {
            correctInput = true;
            return true;
        }

        if (cachedActions == null || cachedActions.Length == 0)
            return false;

        foreach (var action in cachedActions)
        {
            if (action == null || action == requiredAction)
                continue;

            if (action.triggered)
                return true;
        }

        return false;
    }

    private bool IsInputOwner()
    {
        while (inputQueue.Count > 0 && inputQueue[0] == null)
            inputQueue.RemoveAt(0);

        return inputQueue.Count > 0 && inputQueue[0] == this;
    }

    private void RegisterInputQueue()
    {
        inputQueue.Remove(this);
        inputQueue.Add(this);
    }

    private void UnregisterInputQueue()
    {
        inputQueue.Remove(this);
    }

    private void CacheInputActions()
    {
        cachedActionsValid = false;
        requiredAction = null;

        var inputs = InputsManager.Instance != null ? InputsManager.Instance.playerInputs : null;
        if (inputs == null)
            return;

        var battle = inputs.Battle;
        BattleInputType inputType = qteInput != null ? qteInput.BattleInput : fallbackInput;
        requiredAction = BattleInputResolver.Resolve(battle, inputType);

        cachedActions = new[]
        {
            battle.Back,
            battle.Select1,
            battle.Select2,
            battle.Select3,
            battle.Confirm,
            battle.Action,
            battle.EnemiesGroupSelection,
            battle.SquadGroupSelection,
            battle.Awake,
            battle.LeftShoulder,
            battle.RightShoulder,
            battle.BaseAttack,
            battle.Menu
        };

        cachedActionsValid = true;
    }

    private void ApplyInputSprite(Sprite overrideSprite)
    {
        if (inputImage == null)
            return;

        Sprite spriteToUse = qteInput != null ? qteInput.InputSprite : overrideSprite;
        if (spriteToUse != null)
        {
            inputImage.sprite = spriteToUse;
            inputImage.enabled = true;
            inputImage.gameObject.SetActive(true);
        }
    }

    private void ResolveSuccess()
    {
        ResolveWithFeedback(QTEFeedback.Good, onSuccess, true);
    }

    private void ResolvePerfect()
    {
        ResolveWithFeedback(QTEFeedback.Perfect, onPerfect, true);
    }

    private void ResolveFail()
    {
        ResolveWithFeedback(QTEFeedback.Miss, onFail, false);
    }

    private void ResolveWithFeedback(QTEFeedback feedback, UnityEvent feedbackEvent, bool showSuccessRing)
    {
        if (resolved)
            return;

        resolved = true;
        resultFeedback = feedback;
        UnregisterInputQueue();
        feedbackEvent?.Invoke();
        ApplyResultColor(feedback);

        if (showSuccessRing)
            StartCoroutine(PlaySuccessRing());
        else
            StartCoroutine(PlayFailFeedback());
    }

    private Color GetResultColor(QTEFeedback feedback)
    {
        return feedback switch
        {
            QTEFeedback.Perfect => perfectColor,
            QTEFeedback.Good => successColor,
            _ => failColor
        };
    }

    private void ApplyResultColor(QTEFeedback feedback)
    {
        Color color = GetResultColor(feedback);
        color.a = 1f;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        if (movingCircleImage != null)
            movingCircleImage.color = color;
        if (fixCircleImage != null)
            fixCircleImage.color = color;
        if (inputImage != null)
        {
            inputImage.color = color;
            inputImage.enabled = true;
            inputImage.gameObject.SetActive(true);
        }
    }

    private IEnumerator PlayFailFeedback()
    {
        float hold = Mathf.Max(0f, failHoldDuration);
        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);

        yield return FadeOutVisuals();
    }

    private IEnumerator FadeOutVisuals()
    {
        if (canvasGroup == null || failFadeDuration <= 0f)
        {
            HideAllVisuals();
            FinishAfterFade();
            yield break;
        }

        float startAlpha = canvasGroup.alpha;
        float timer = 0f;
        while (timer < failFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / failFadeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        HideAllVisuals();
        FinishAfterFade();
    }

    private IEnumerator PlaySuccessRing()
    {
        if (movingCircle != null)
            movingCircle.gameObject.SetActive(false);
        if (inputImage != null)
            inputImage.enabled = false;

        if (fixCircleImage == null)
        {
            HideAllVisuals();
            FinishAfterFade();
            yield break;
        }

        RectTransform fixTransform = fixCircleImage.rectTransform;
        Vector3 startScale = fixTransform.localScale;
        Vector3 endScale = startScale * Mathf.Max(1f, successRingScaleMultiplier);
        Color startColor = fixCircleImage.color;
        float timer = 0f;

        if (successRingDuration <= 0f)
        {
            HideAllVisuals();
            FinishAfterFade();
            yield break;
        }

        while (timer < successRingDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / successRingDuration);
            fixTransform.localScale = Vector3.Lerp(startScale, endScale, t);
            fixCircleImage.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
            yield return null;
        }

        HideAllVisuals();
        FinishAfterFade();
    }

    private void InitializeShrinkSpeed()
    {
        if (shrinkSpeedInitialized || movingCircle == null)
            return;

        designStartScale = movingCircle.localScale.x;
        float validDelay = Mathf.Max(0.01f, responseDelaySeconds);
        float difference = designStartScale - responseScale;
        if (difference <= 0f)
            shrinkSpeed = Mathf.Max(0.01f, designStartScale - targetScale.x) / validDelay;
        else
            shrinkSpeed = difference / validDelay;
        shrinkSpeedInitialized = true;
    }

    private float ComputeStartScaleForDelay(float responseDelay)
    {
        InitializeShrinkSpeed();
        float validDelay = Mathf.Max(0f, responseDelay);
        float speed = Mathf.Max(0.001f, shrinkSpeed);
        float scale = responseScale + speed * validDelay;
        return Mathf.Max(scale, responseScale + 0.01f);
    }

    private void ConfigureResponseDelay(float delay, bool log)
    {
        float validDelay = Mathf.Max(0f, delay);
        float startScale = ComputeStartScaleForDelay(validDelay);
        if (movingCircle != null)
        {
            movingCircle.localScale = Vector3.one * startScale;
            initialScale = movingCircle.localScale;
        }

        float speed = Mathf.Max(0.001f, shrinkSpeed);
        totalDurationSeconds = Mathf.Max(0f, (startScale - targetScale.x) / speed);
        responseDelaySeconds = validDelay;
        currentResponseDelaySeconds = validDelay;

        if (log)
        {
            string inputName = qteInput != null ? qteInput.name : "none";
            Debug.Log(
                $"[QTECircle] input={inputName} delay={validDelay:F2}s startScale={startScale:F2} duration={totalDurationSeconds:F2}s speed={speed:F2} queue={inputQueue.Count}");
        }
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void HideAllVisuals()
    {
        visualsHidden = true;
        if (movingCircle != null)
            movingCircle.gameObject.SetActive(false);
        if (inputImage != null)
            inputImage.enabled = false;
        if (fixCircleImage != null)
            fixCircleImage.gameObject.SetActive(false);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void FinishAfterFade()
    {
        if (autoDestroyAfterFade)
            Destroy(gameObject);
    }

    public void SetAutoDestroyAfterFade(bool value)
    {
        autoDestroyAfterFade = value;
    }

    public void ForceDestroy()
    {
        Destroy(gameObject);
    }
}
