using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LightSkillPanelController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider chargeSlider;
    [SerializeField] private Image fillImage;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField] private Color chargingColor = Color.white;
    [SerializeField] private Color readyColor = new Color(0.55f, 0.92f, 1f, 1f);

    private LightSkillCombatController controller;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveController();
        Refresh();
    }

    private void OnEnable()
    {
        ResolveController();
        Refresh();
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.StateChanged -= Refresh;
        }
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (chargeSlider == null) chargeSlider = GetComponentInChildren<Slider>(true);
        if (fillImage == null && chargeSlider != null && chargeSlider.fillRect != null)
        {
            fillImage = chargeSlider.fillRect.GetComponent<Image>();
        }
    }

    private void ResolveController()
    {
        LightSkillCombatController resolved = FindAnyObjectByType<LightSkillCombatController>(FindObjectsInactive.Include);
        if (resolved == controller)
        {
            return;
        }

        if (controller != null)
        {
            controller.StateChanged -= Refresh;
        }

        controller = resolved;
        if (controller != null)
        {
            controller.StateChanged += Refresh;
        }
    }

    private void Refresh()
    {
        ResolveReferences();
        bool visible = controller != null && controller.IsCombatActive;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? visibleAlpha : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (chargeSlider != null)
        {
            chargeSlider.minValue = 0f;
            chargeSlider.maxValue = controller != null ? controller.RequiredClarity : 1f;
            chargeSlider.SetValueWithoutNotify(controller != null ? controller.Clarity : 0f);
        }

        if (fillImage != null)
        {
            fillImage.color = controller != null && controller.IsReady ? readyColor : chargingColor;
        }
    }
}
