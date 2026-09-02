using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Scene-owned presentation for threshold QTEs. It has no gameplay authority:
/// callers provide the requested input and the normalized remaining time.
/// </summary>
[DisallowMultipleComponent]
public sealed class QTEPanelController : MonoBehaviour
{
    public static QTEPanelController Instance { get; private set; }

    [Serializable]
    private struct InputVisual
    {
        public CombatThresholdQteInput input;
        public Sprite sprite;
    }

    [Header("Scene References")]
    [SerializeField] private RectTransform qteZone;
    [SerializeField] private GameObject qteCircleSlider;
    [SerializeField] private Slider timeSlider;
    [SerializeField] private Image qteCircleImage;
    [SerializeField] private Image qteInputImage;
    [SerializeField, Min(0f)] private float safeEdgeMargin = 24f;
    [Header("Input Images")]
    [SerializeField] private List<InputVisual> inputVisuals = new List<InputVisual>();
    [SerializeField, Min(0.01f)] private float resultFeedbackSeconds = 0.12f;
    [SerializeField] private bool logDiagnostics = true;

    private Coroutine resultRoutine;
    private Color circleBaseColor = Color.white;
    private bool missingVisualReported;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[QTEPanel] Plusieurs panneaux QTE sont charges. Le panneau existant reste prioritaire.", this);
            enabled = false;
            return;
        }

        Instance = this;
        ResolveReferences();
        ConfigureCircleTimer();
        HideImmediate();
        Trace("Pret | zone=" + DescribeRect(qteZone) + " | circle=" + DescribeRect(qteCircleSlider != null ? qteCircleSlider.transform as RectTransform : null) + ".");
    }

    private void OnDisable()
    {
        if (resultRoutine != null) StopCoroutine(resultRoutine);
        resultRoutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(CombatThresholdQteInput input)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        CanvasGroup ownCanvasGroup = GetComponent<CanvasGroup>();
        if (ownCanvasGroup != null)
        {
            ownCanvasGroup.alpha = 1f;
            ownCanvasGroup.interactable = false;
            ownCanvasGroup.blocksRaycasts = false;
        }

        ResolveReferences();
        if (qteCircleSlider == null || qteZone == null)
        {
            Debug.LogWarning("[QTEPanel] References UI incompletes : QTE_Circle_Slider ou QTE_Zone manquant.", this);
            return;
        }

        if (resultRoutine != null) StopCoroutine(resultRoutine);
        resultRoutine = null;
        ConfigureCircleTimer();
        ApplyInputVisual(input);
        qteCircleSlider.SetActive(true);
        Canvas.ForceUpdateCanvases();
        PlaceCircleRandomly();
        SetProgress(0f);
        Trace("Affiche | input=" + input + " | position=" + (qteCircleSlider.transform as RectTransform).anchoredPosition +
              " | activeInHierarchy=" + isActiveAndEnabled + " | canvas=" + DescribeCanvasGroups() + ".");
    }

    /// <summary>Sets elapsed progress, where zero is a fresh QTE.</summary>
    public void SetProgress(float elapsed01)
    {
        float remaining = 1f - Mathf.Clamp01(elapsed01);
        if (timeSlider != null) timeSlider.SetValueWithoutNotify(remaining);
        if (qteCircleImage != null) qteCircleImage.fillAmount = remaining;
    }

    public void ResolveSuccess()
    {
        Resolve(new Color(0.3f, 1f, 0.52f, 1f));
    }

    public void ResolveFailure()
    {
        Resolve(new Color(1f, 0.22f, 0.28f, 1f));
    }

    public void HideImmediate()
    {
        if (resultRoutine != null) StopCoroutine(resultRoutine);
        resultRoutine = null;
        if (qteCircleImage != null) qteCircleImage.color = circleBaseColor;
        if (qteCircleSlider != null) qteCircleSlider.SetActive(false);
    }

    private void Resolve(Color feedbackColor)
    {
        if (qteCircleSlider == null || !qteCircleSlider.activeSelf)
        {
            HideImmediate();
            return;
        }

        if (qteCircleImage != null) qteCircleImage.color = feedbackColor;
        if (resultRoutine != null) StopCoroutine(resultRoutine);
        resultRoutine = StartCoroutine(HideAfterFeedback());
    }

    private IEnumerator HideAfterFeedback()
    {
        yield return new WaitForSecondsRealtime(resultFeedbackSeconds);
        HideImmediate();
    }

    private void PlaceCircleRandomly()
    {
        RectTransform circleRect = qteCircleSlider != null ? qteCircleSlider.transform as RectTransform : null;
        if (circleRect == null || qteZone == null) return;

        Vector2 zoneSize = qteZone.rect.size;
        if (zoneSize.x <= 0f || zoneSize.y <= 0f)
        {
            Debug.LogWarning("[QTEPanel] QTE_Zone a une taille invalide (" + zoneSize + "). Le cercle est centre pour rester visible.", this);
            circleRect.anchoredPosition = Vector2.zero;
            return;
        }

        Vector2 halfZone = zoneSize * .5f;
        Vector2 halfCircle = circleRect.rect.size * .5f + Vector2.one * safeEdgeMargin;
        float horizontalRange = Mathf.Max(0f, halfZone.x - halfCircle.x);
        float verticalRange = Mathf.Max(0f, halfZone.y - halfCircle.y);
        circleRect.anchoredPosition = new Vector2(
            UnityEngine.Random.Range(-horizontalRange, horizontalRange),
            UnityEngine.Random.Range(-verticalRange, verticalRange));
    }

    private void ConfigureCircleTimer()
    {
        if (timeSlider != null)
        {
            timeSlider.interactable = false;
            timeSlider.fillRect = null;
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 1f;
        }

        if (qteCircleImage != null)
        {
            RectTransform circleRect = qteCircleImage.rectTransform;
            circleRect.anchorMin = Vector2.zero;
            circleRect.anchorMax = Vector2.one;
            circleRect.offsetMin = Vector2.zero;
            circleRect.offsetMax = Vector2.zero;
            qteCircleImage.type = Image.Type.Filled;
            qteCircleImage.fillMethod = Image.FillMethod.Radial360;
            qteCircleImage.fillOrigin = 2;
            qteCircleImage.fillClockwise = false;
            circleBaseColor = qteCircleImage.color;
        }

        if (qteInputImage != null) qteInputImage.preserveAspect = true;
    }

    private void ApplyInputVisual(CombatThresholdQteInput input)
    {
        if (qteInputImage == null) return;
        for (int i = 0; i < inputVisuals.Count; i++)
        {
            if (inputVisuals[i].input != input || inputVisuals[i].sprite == null) continue;
            qteInputImage.sprite = inputVisuals[i].sprite;
            qteInputImage.enabled = true;
            return;
        }

        qteInputImage.enabled = false;
        if (!missingVisualReported)
        {
            missingVisualReported = true;
            Debug.LogWarning("[QTEPanel] Image manquante pour un input QTE. Verifie Input Images sur QTEPanel.", this);
        }
    }

    private void ResolveReferences()
    {
        if (qteZone == null) qteZone = transform.Find("QTE_Root/QTE_Zone") as RectTransform;
        if (qteCircleSlider == null && qteZone != null)
        {
            Transform slider = qteZone.Find("QTE_Circle_Slider");
            if (slider != null) qteCircleSlider = slider.gameObject;
        }

        if (timeSlider == null && qteCircleSlider != null) timeSlider = qteCircleSlider.GetComponent<Slider>();
        if (qteCircleImage == null && qteCircleSlider != null)
        {
            Transform circle = qteCircleSlider.transform.Find("QTE_Circle_Image");
            if (circle != null) qteCircleImage = circle.GetComponent<Image>();
        }

        if (qteInputImage == null && qteCircleSlider != null)
        {
            Transform input = qteCircleSlider.transform.Find("QTE_Input");
            if (input != null) qteInputImage = input.GetComponent<Image>();
        }
    }

    private void Trace(string message)
    {
        if (logDiagnostics) Debug.Log("[QTEPanel] " + message, this);
    }

    private string DescribeCanvasGroups()
    {
        CanvasGroup[] groups = GetComponentsInParent<CanvasGroup>(true);
        if (groups == null || groups.Length == 0) return "aucun CanvasGroup parent";

        string value = string.Empty;
        for (int i = 0; i < groups.Length; i++)
        {
            if (i > 0) value += ", ";
            value += groups[i].name + "=" + groups[i].alpha.ToString("F2");
        }

        return value;
    }

    private static string DescribeRect(RectTransform rect)
    {
        return rect == null ? "absent" : rect.rect.size.ToString();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        AssignDefaultInputVisuals();
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    private void Reset()
    {
        ResolveReferences();
        AssignDefaultInputVisuals();
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    private void AssignDefaultInputVisuals()
    {
        if (inputVisuals == null) inputVisuals = new List<InputVisual>();
        EnsureInputVisual(CombatThresholdQteInput.Y, "Assets/UI/Inputs/XBox GamePad NorthButton.png");
        EnsureInputVisual(CombatThresholdQteInput.B, "Assets/UI/Inputs/XBox GamePad EastButton.png");
        EnsureInputVisual(CombatThresholdQteInput.A, "Assets/UI/Inputs/XBox GamePad SouthButton.png");
        EnsureInputVisual(CombatThresholdQteInput.X, "Assets/UI/Inputs/XBox GamePad WestButton.png");
    }

    private void EnsureInputVisual(CombatThresholdQteInput input, string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        for (int i = 0; i < inputVisuals.Count; i++)
        {
            if (inputVisuals[i].input != input) continue;
            if (inputVisuals[i].sprite == null) inputVisuals[i] = new InputVisual { input = input, sprite = sprite };
            return;
        }

        inputVisuals.Add(new InputVisual { input = input, sprite = sprite });
    }
#endif
}
