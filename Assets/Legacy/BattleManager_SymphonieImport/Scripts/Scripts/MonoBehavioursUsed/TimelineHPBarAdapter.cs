using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Adapte l'affichage de la timeline de combat au système standard de <see cref="HPBar"/>.
/// Cette passerelle écoute les événements d'un <see cref="CharacterUnit"/> pour refléter
/// instantanément l'état de ses points de vie dans la timeline sans perturber les barres
/// monde éventuellement associées à l'unité.
/// </summary>
[RequireComponent(typeof(BattleTimelineUnit))]
public class TimelineHPBarAdapter : HPBar
{
    [Header("Références visuelles")]
    [SerializeField] private Image timelineFillImage;
    [SerializeField] private TextMeshProUGUI timelineHPText;
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color deadColor = Color.red;
    [Header("Couleurs dynamiques de PV")]
    [SerializeField] private Color highHealthColor = Color.green; // Couleur utilisée lorsque la jauge est presque pleine.
    [SerializeField] private Color midHealthColor = Color.yellow; // Couleur d'avertissement pour une santé moyenne.
    [SerializeField] private Color lowHealthColor = new Color(0.75f, 0f, 0f); // Rouge légèrement assombri pour distinguer l'état critique.
    [Header("Feedback PV max")]
    [SerializeField] private bool showMaxHpFeedback = true;
    [SerializeField] private bool showMaxHpDeltaText = true;
    [SerializeField] private Color maxHpIncreaseColor = new Color(0.25f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color maxHpDecreaseColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private float maxHpPulseDuration = 0.12f;
    [SerializeField] private float maxHpHoldDuration = 0.35f;
    [SerializeField] private float maxHpPulseScale = 1.12f;

    /// <summary>Référence vers l'unité dont on suit les points de vie.</summary>
    private CharacterUnit owner;
    /// <summary>Valeur maximale actuelle utilisée pour le calcul du ratio.</summary>
    private float cachedMaxValue = 1f;
    /// <summary>Valeur courante retenue pour l'affichage.</summary>
    private float cachedCurrentValue = 1f;
    private bool hasInitializedMax;
    private bool maxHpFeedbackActive;
    private Color maxHpFeedbackColor = Color.white;
    private string maxHpFeedbackText;
    private Coroutine maxHpFeedbackRoutine;
    private int maxHpFeedbackToken;
    private Vector3 hpTextBaseScale = Vector3.one;
    private bool hasCachedTextScale;

    /// <summary>
    /// Configure l'adaptateur pour qu'il prenne le relais sur l'unité donnée.
    /// </summary>
    /// <param name="target">Unité dont on souhaite surveiller les PV.</param>
    /// <param name="fill">Image représentant le remplissage de la jauge.</param>
    /// <param name="hpLabel">Label textuel affichant les valeurs numériques.</param>
    /// <param name="alive">Couleur utilisée lorsque l'unité est en vie.</param>
    /// <param name="dead">Couleur utilisée lorsque l'unité est vaincue.</param>
    public void Setup(CharacterUnit target, Image fill, TextMeshProUGUI hpLabel, Color alive, Color dead)
    {
        if (owner != null)
            owner.OnHealthChanged -= HandleOwnerHealthChanged;

        owner = target;
        timelineFillImage = fill != null ? fill : timelineFillImage;
        timelineHPText = hpLabel != null ? hpLabel : timelineHPText;
        aliveColor = alive;
        deadColor = dead;
        hasInitializedMax = false;
        ResetMaxHpFeedbackVisuals();
        CacheTextScale();

        // --- Référence Slider ---------------------------------------------------------------
        // Certains prefabs n'assignent pas explicitement le Slider Unity utilisé par la barre de vie.
        // Lorsque ce champ reste à "null", la classe parente <see cref="HPBar"/> n'a aucun
        // composant à synchroniser : le slider visible en jeu reste bloqué à sa valeur maximale
        // (ex : 1f) même si les points de vie diminuent correctement.  Afin de fiabiliser la
        // configuration, on tente automatiquement de retrouver le slider correspondant dans la
        // hiérarchie de l'objet timeline.
        if (slider == null)
        {
            // On privilégie d'abord la recherche via l'image de remplissage car elle est en
            // général un enfant direct du slider, puis on élargit au besoin à tous les enfants.
            if (timelineFillImage != null)
            {
                slider = timelineFillImage.GetComponentInParent<Slider>();
            }

            if (slider == null)
            {
                slider = GetComponentInChildren<Slider>(includeInactive: true);
            }
        }

        if (owner == null)
            return;

        owner.OnHealthChanged += HandleOwnerHealthChanged;
    }

    /// <inheritdoc />
    public override void SetMaxValue(float max)
    {
        cachedMaxValue = Mathf.Max(0f, max);
        base.SetMaxValue(cachedMaxValue);

        // Utilise la valeur courante connue de l'unité pour maintenir la cohérence visuelle.
        float current = owner != null ? owner.currentHP : cachedCurrentValue;
        UpdateVisuals(current);
    }

    /// <inheritdoc />
    public override void SetValue(float current)
    {
        cachedCurrentValue = Mathf.Clamp(current, 0f, cachedMaxValue > 0f ? cachedMaxValue : current);
        base.SetValue(cachedCurrentValue);
        UpdateVisuals(cachedCurrentValue);
    }

    /// <summary>
    /// Synchronise immédiatement la jauge avec l'unité liée sans attendre un nouvel événement.
    /// </summary>
    public void SyncWithOwner()
    {
        if (owner == null)
            return;

        HandleOwnerHealthChanged(owner, owner.currentHP, owner.MaxHP);
    }

    private void OnEnable()
    {
        if (owner == null)
            return;

        // Sécurise la souscription lors d'un ré-activation (ex : pool UI) et force un rafraîchissement instantané.
        owner.OnHealthChanged -= HandleOwnerHealthChanged;
        owner.OnHealthChanged += HandleOwnerHealthChanged;
        SyncWithOwner();
    }

    private void OnDisable()
    {
        if (owner != null)
            owner.OnHealthChanged -= HandleOwnerHealthChanged;

        if (maxHpFeedbackRoutine != null)
            StopCoroutine(maxHpFeedbackRoutine);
        ResetMaxHpFeedbackVisuals();
    }

    /// <summary>
    /// Rafraîchit les éléments visuels (remplissage + texte) selon les valeurs calculées.
    /// </summary>
    /// <param name="current">Nombre de PV à afficher.</param>
    private void UpdateVisuals(float current)
    {
        float ratio = cachedMaxValue > 0f ? current / cachedMaxValue : 0f;
        ratio = Mathf.Clamp01(ratio);

        // Détermine la couleur adaptée avant d'appliquer les nouveaux ratios sur les éléments visuels.
        Color displayColor = DetermineHealthColor(ratio, current);
        Color appliedColor = maxHpFeedbackActive ? maxHpFeedbackColor : displayColor;

        if (timelineFillImage != null)
        {
            timelineFillImage.fillAmount = ratio;
            timelineFillImage.color = appliedColor;
        }

        if (timelineHPText != null)
        {
            int currentInt = Mathf.RoundToInt(current);
            int maxInt = Mathf.RoundToInt(cachedMaxValue);
            if (maxHpFeedbackActive && showMaxHpDeltaText && !string.IsNullOrEmpty(maxHpFeedbackText))
                timelineHPText.text = maxHpFeedbackText;
            else
                timelineHPText.text = $"{currentInt}/{maxInt}";
            timelineHPText.color = appliedColor;
        }
    }

    /// <summary>
    /// Calcule la couleur à afficher selon le ratio de PV actuel.
    /// Garantit un dégradé rouge → jaune → vert pour informer visuellement du danger.
    /// </summary>
    /// <param name="ratio">Ratio de points de vie entre 0 et 1.</param>
    /// <param name="current">Valeur actuelle brute des PV pour détecter l'état KO.</param>
    /// <returns>Couleur à utiliser pour la jauge et le texte.</returns>
    private Color DetermineHealthColor(float ratio, float current)
    {
        // Dès que les PV tombent à zéro ou moins on force l'utilisation de la couleur « mort ».
        if (current <= 0f)
            return deadColor;

        // Ratio très faible : mélange entre le rouge foncé et le jaune afin de souligner le danger.
        if (ratio <= 0.5f)
        {
            float t = Mathf.InverseLerp(0f, 0.5f, ratio);
            return Color.Lerp(lowHealthColor, midHealthColor, t);
        }

        // Ratio supérieur à la moitié : transition douce du jaune vers le vert au fur et à mesure que l'on se rapproche du maximum.
        float highT = Mathf.InverseLerp(0.5f, 1f, ratio);
        Color progressive = Color.Lerp(midHealthColor, highHealthColor, highT);

        // On laisse la possibilité de conserver une teinte personnalisée définie via aliveColor pour les cas non critiques.
        return Color.Lerp(progressive, aliveColor, 0.15f);
    }

    private void OnDestroy()
    {
        if (owner != null)
            owner.OnHealthChanged -= HandleOwnerHealthChanged;
    }

    /// <summary>
    /// Réagit aux variations de PV signalées par le <see cref="CharacterUnit"/> pour mettre à jour la jauge timeline.
    /// </summary>
    private void HandleOwnerHealthChanged(CharacterUnit unit, float current, float max)
    {
        if (owner == null || unit != owner)
            return;

        float previousMax = cachedMaxValue;
        cachedMaxValue = Mathf.Max(0f, max);
        cachedCurrentValue = Mathf.Clamp(current, 0f, cachedMaxValue > 0f ? cachedMaxValue : current);

        base.SetMaxValue(cachedMaxValue);
        base.SetValue(cachedCurrentValue);
        UpdateVisuals(cachedCurrentValue);

        if (!hasInitializedMax)
        {
            hasInitializedMax = true;
            return;
        }

        float delta = cachedMaxValue - previousMax;
        if (Mathf.Abs(delta) > 0.01f)
            TriggerMaxHpFeedback(delta);
    }

    private void TriggerMaxHpFeedback(float delta)
    {
        if (!showMaxHpFeedback)
            return;

        if (maxHpFeedbackRoutine != null)
            StopCoroutine(maxHpFeedbackRoutine);

        ResetMaxHpFeedbackVisuals();
        maxHpFeedbackToken++;
        maxHpFeedbackRoutine = StartCoroutine(MaxHpFeedbackRoutine(delta, maxHpFeedbackToken));
    }

    private IEnumerator MaxHpFeedbackRoutine(float delta, int token)
    {
        maxHpFeedbackActive = true;
        maxHpFeedbackColor = delta > 0f ? maxHpIncreaseColor : maxHpDecreaseColor;
        maxHpFeedbackText = BuildMaxHpFeedbackText(delta);

        UpdateVisuals(cachedCurrentValue);
        CacheTextScale();

        float pulseDuration = Mathf.Max(0.05f, maxHpPulseDuration);
        float holdDuration = Mathf.Max(0f, maxHpHoldDuration);
        float scaleFactor = Mathf.Max(1f, maxHpPulseScale);

        Vector3 baseScale = hpTextBaseScale;
        Vector3 targetScale = baseScale * scaleFactor;

        float timer = 0f;
        while (timer < pulseDuration)
        {
            if (token != maxHpFeedbackToken)
                yield break;

            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / pulseDuration);
            if (timelineHPText != null)
                timelineHPText.transform.localScale = Vector3.Lerp(baseScale, targetScale, t);
            yield return null;
        }

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        timer = 0f;
        while (timer < pulseDuration)
        {
            if (token != maxHpFeedbackToken)
                yield break;

            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / pulseDuration);
            if (timelineHPText != null)
                timelineHPText.transform.localScale = Vector3.Lerp(targetScale, baseScale, t);
            yield return null;
        }

        ResetMaxHpFeedbackVisuals();
        UpdateVisuals(cachedCurrentValue);
        maxHpFeedbackRoutine = null;
    }

    private string BuildMaxHpFeedbackText(float delta)
    {
        int currentInt = Mathf.RoundToInt(cachedCurrentValue);
        int maxInt = Mathf.RoundToInt(cachedMaxValue);
        int deltaInt = Mathf.RoundToInt(delta);
        string deltaLabel = deltaInt > 0 ? $"+{deltaInt}" : deltaInt.ToString();
        return $"{currentInt}/{maxInt} (MAX {deltaLabel})";
    }

    private void CacheTextScale()
    {
        if (timelineHPText == null || hasCachedTextScale)
            return;

        hpTextBaseScale = timelineHPText.transform.localScale;
        hasCachedTextScale = true;
    }

    private void ResetMaxHpFeedbackVisuals()
    {
        maxHpFeedbackActive = false;
        maxHpFeedbackText = null;
        if (timelineHPText != null && hasCachedTextScale)
            timelineHPText.transform.localScale = hpTextBaseScale;
        maxHpFeedbackRoutine = null;
    }
}
