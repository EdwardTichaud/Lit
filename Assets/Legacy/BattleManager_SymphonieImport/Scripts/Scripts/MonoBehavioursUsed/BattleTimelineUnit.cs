using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleTimelineUnit : MonoBehaviour
{
    [Header("Références UI")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image hpBarImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Slider atbSlider;
    [SerializeField] private CustomBar customBar;
    [SerializeField] private TextMeshProUGUI harmonicsText;
    [SerializeField] private TextMeshProUGUI movementText;

    [Header("Couleurs")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Color deadColor = Color.red;

    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>Référence directe vers l'unité affichée pour faciliter l'accès aux stats dynamiques.</summary>
    private CharacterUnit boundUnit;
    /// <summary>Adaptateur reliant la barre de vie de la timeline au CharacterUnit.</summary>
    private TimelineHPBarAdapter timelineHPBarAdapter;

    private float baseScale = 1f;
    private float highlightMultiplier = 1f;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void Initialize(CharacterUnit unit)
    {
        if (unit == null || unit.Data == null)
        {
            Debug.LogWarning("[TimelineUnit] Unité invalide.");
            return;
        }

        boundUnit = unit;

        // Portrait et nom
        if (portraitImage) portraitImage.sprite = boundUnit.Data.portrait;
        if (nameText) nameText.text = boundUnit.Data.characterName;

        // HP : on crée/initialise l'adaptateur qui mettra à jour la jauge timeline
        if (timelineHPBarAdapter == null)
            timelineHPBarAdapter = GetComponent<TimelineHPBarAdapter>();

        if (timelineHPBarAdapter == null)
            timelineHPBarAdapter = gameObject.AddComponent<TimelineHPBarAdapter>();

        Color aliveColor = hpBarImage != null ? hpBarImage.color : Color.white;
        timelineHPBarAdapter.Setup(boundUnit, hpBarImage, hpText, aliveColor, deadColor);
        // Appel explicite pour synchroniser immédiatement l'affichage avant le prochain événement de PV.
        timelineHPBarAdapter.SyncWithOwner();

        // Custom bar (Rage/Fatigue/Concentration)
        if (customBar != null)
        {
            if (boundUnit.Data.gameplayType == GameplayType.Rage)
            {
                customBar.SetMaxValue(boundUnit.Data.maxRage);
                customBar.SetValue(unit.currentRage);
            }
            else if (boundUnit.Data.gameplayType == GameplayType.Fatigue)
            {
                customBar.SetMaxValue(boundUnit.Data.maxFatigue);
                customBar.SetValue(unit.currentFatigue);
            }
            else if (unit.TryGetComponent<ConcentrationSystem>(out var c))
            {
                customBar.SetMaxValue(c.maxConcentration);
                customBar.SetValue(c.currentConcentration);
            }
        }

        // ATB
        if (atbSlider != null)
        {
            atbSlider.maxValue = unit.ATBMax;
            atbSlider.value = unit.currentATB;
        }

        UpdateHarmonicsDisplay();

        SetHighlight(false);
    }

    void Update()
    {
        UpdateHarmonicsDisplay();
    }

    public void UpdateATBGauge()
    {
        if (boundUnit == null || atbSlider == null)
            return;

        // Plus besoin de rechercher l'unité dans le NewBattleManager : on lit directement la valeur courante.
        atbSlider.value = boundUnit.currentATB;
    }

    public void UpdateCustomBar()
    {
        if (boundUnit == null || customBar == null)
            return;

        // La barre personnalisée dépend du gameplayType de la fiche personnage : on récupère donc
        // l'information directement depuis les données de l'unité liée.
        if (boundUnit.Data.gameplayType == GameplayType.Rage)
        {
            customBar.SetValue(boundUnit.currentRage);
        }
        else if (boundUnit.Data.gameplayType == GameplayType.Fatigue)
        {
            customBar.SetValue(boundUnit.currentFatigue);
        }
        else if (boundUnit.TryGetComponent<ConcentrationSystem>(out var c))
        {
            customBar.SetValue(c.currentConcentration);
        }
    }

    public void UpdateHarmonicsDisplay()
    {
        if (boundUnit == null)
            return;

        // Affiche en permanence le total d'harmoniques disponibles et les points de déplacement restants.
        int count = boundUnit.GetTotalHarmonicCount();
        float currentMovement = boundUnit.CurrentMovementPoints;
        if (harmonicsText != null)
        {
            harmonicsText.text = movementText == null
                ? $"H {count} | PM {currentMovement:0.#}"
                : $"{count}";
        }
        if (movementText != null)
            movementText.text = $"{currentMovement:0.#}";
    }

    public void UpdateHPBar()
    {
        timelineHPBarAdapter?.SyncWithOwner();
    }

    public void SetHighlight(bool active)
    {
        if (backgroundImage != null)
            backgroundImage.color = active ? highlightColor : normalColor;

        highlightMultiplier = active ? 1.15f : 1f;
        ApplyScale();
    }

    public void SetAppearance(float scale, float alpha)
    {
        baseScale = scale;
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;

        ApplyScale();
    }

    private void ApplyScale()
    {
        transform.localScale = Vector3.one * baseScale * highlightMultiplier;
    }

    /// <summary>
    /// Fournit l'unité actuellement liée à cette vignette de timeline.
    /// Cette propriété permet d'éviter aux autres gestionnaires de devoir
    /// maintenir leurs propres références ou de rechercher l'unité via
    /// <see cref="NewBattleManager"/>.
    /// </summary>
    public CharacterUnit BoundUnit => boundUnit;

    /// <summary>
    /// Expose les données statiques du personnage associé à la vignette.
    /// L'ancien champ <c>characterData</c> a été remplacé par cette
    /// propriété afin de conserver la compatibilité avec les systèmes
    /// existants tout en centralisant la logique d'accès dans
    /// <see cref="BattleTimelineUnit"/>.
    /// </summary>
    public CharacterData characterData => boundUnit != null ? boundUnit.Data : null;
}

