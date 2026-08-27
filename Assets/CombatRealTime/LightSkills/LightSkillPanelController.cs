using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LightSkillPanelController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider chargeSlider;
    [SerializeField] private Image claritySprite;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private RealTimeCombatManager combatManager;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField] private Color chargingColor = Color.white;
    [SerializeField] private Color readyColor = new Color(0.55f, 0.92f, 1f, 1f);

    private Material clarityMaterial;

    private void Awake()
    {
        ResolveReferences();
        CreateClarityMaterial();
    }

    private void Start()
    {
        BindCombatManager();
        Refresh();
    }

    private void OnEnable()
    {
        BindCombatManager();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindCombatManager();
    }

    private void OnDestroy()
    {
        if (clarityMaterial != null)
        {
            Destroy(clarityMaterial);
        }
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (chargeSlider == null) chargeSlider = GetComponentInChildren<Slider>(true);
        if (claritySprite == null && chargeSlider != null && chargeSlider.fillRect != null)
        {
            claritySprite = chargeSlider.fillRect.GetComponent<Image>();
        }
    }

    private void BindCombatManager()
    {
        if (combatManager == null)
        {
            combatManager = RealTimeCombatManager.Instance;
        }

        if (combatManager == null)
        {
            return;
        }

        combatManager.ClarityChanged -= OnClarityChanged;
        combatManager.ClarityChanged += OnClarityChanged;
        combatManager.CombatStateChanged -= OnCombatStateChanged;
        combatManager.CombatStateChanged += OnCombatStateChanged;
    }

    private void Refresh()
    {
        ResolveReferences();
        bool visible = combatManager != null && combatManager.IsCombatActive;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? visibleAlpha : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (chargeSlider != null)
        {
            chargeSlider.minValue = 0f;
            chargeSlider.maxValue = combatManager != null ? combatManager.ClarityForS : 1f;
            chargeSlider.SetValueWithoutNotify(combatManager != null ? Mathf.Min(combatManager.Clarity, combatManager.ClarityForS) : 0f);
        }

        if (rankText != null)
        {
            rankText.text = combatManager != null ? combatManager.ClarityRank.ToString() : CombatClarityRank.E.ToString();
        }

        float normalizedClarity = combatManager != null ? combatManager.NormalizedClarity : 0f;
        if (clarityMaterial != null)
        {
            if (clarityMaterial.HasProperty("_FinalAlpha")) clarityMaterial.SetFloat("_FinalAlpha", normalizedClarity);
            if (clarityMaterial.HasProperty("_WarpTexTillingOffset"))
            {
                clarityMaterial.SetVector("_WarpTexTillingOffset", normalizedClarity >= 1f ? Vector4.zero : new Vector4(1f, 1f, 0f, 0f));
            }
        }

        if (claritySprite != null)
        {
            LightSkillSO equippedSkill = combatManager != null && combatManager.PlayerLoadout != null
                ? combatManager.PlayerLoadout.EquippedLightSkill
                : null;
            bool isReady = equippedSkill != null && combatManager != null &&
                           combatManager.Clarity >= combatManager.GetLightSkillRequiredClarity(equippedSkill.RequiredRank);
            claritySprite.color = isReady ? readyColor : chargingColor;
        }
    }

    private void CreateClarityMaterial()
    {
        if (claritySprite == null || claritySprite.material == null || clarityMaterial != null)
        {
            return;
        }

        clarityMaterial = new Material(claritySprite.material);
        clarityMaterial.name = claritySprite.material.name + " (ClarityPanel Runtime)";
        claritySprite.material = clarityMaterial;
    }

    private void OnClarityChanged(float clarity, CombatClarityRank rank) => Refresh();

    private void OnCombatStateChanged(bool active) => Refresh();

    private void UnbindCombatManager()
    {
        if (combatManager == null)
        {
            return;
        }

        combatManager.ClarityChanged -= OnClarityChanged;
        combatManager.CombatStateChanged -= OnCombatStateChanged;
    }
}
