using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant UI pour afficher la barre de vie d'une unit.
/// </summary>
public class HPBar : MonoBehaviour
{
    [Header("Références Slider")]
    [Tooltip("Slider Unity utilisé pour représenter la barre de vie classiquement (gauche -> droite).")] // Aide les artistes à identifier le champ attendu dans l'inspecteur.
    public Slider slider;

    [Header("Réduction visuelle optionnelle")]
    [Tooltip("RectTransform optionnel à contracter horizontalement pour des barres basées sur l'échelle plutôt que sur un Slider.")]
    [SerializeField] private RectTransform dynamicFillRect;

    /// <summary>Valeur maximale mémorisée pour calculer correctement le ratio d'affichage.</summary>
    private float maxValue = 1f;
    /// <summary>Valeur actuelle retenue pour piloter l'UI.</summary>
    private float currentValue = 1f;

    /// <summary>
    /// Définit la valeur maximale de la barre et réinitialise l'affichage pour garantir
    /// que la jauge est pleine lorsque l'unité est fraîchement créée ou soignée à 100%.
    /// </summary>
    /// <param name="max">Valeur maximale (vie max).</param>
    public virtual void SetMaxValue(float max)
    {
        // Avant toute modification, on sécurise l'accès au composant Slider Unity.
        // Certains prefabs historiques omettaient de renseigner le champ dans l'inspecteur,
        // empêchant l'évolution visuelle de la jauge malgré les mises à jour de points de vie.
        AcquireSliderReferenceIfNeeded();

        maxValue = Mathf.Max(0f, max); // Sécurise la valeur afin d'éviter toute division par zéro plus tard.

        // S'assure que la valeur courante reste cohérente avec la nouvelle limite.
        currentValue = Mathf.Clamp(currentValue, 0f, maxValue);

        UpdateSlider(currentValue, forceRefresh: true);
        UpdateDynamicFill();
    }

    /// <summary>
    /// Met à jour la valeur courante de la barre en répercutant immédiatement la perte/gain de PV
    /// sur les éléments visuels associés (Slider, barre qui se contracte, etc.).
    /// </summary>
    /// <param name="current">Valeur actuelle (vie restante).</param>
    public virtual void SetValue(float current)
    {
        // Même sécurité que dans SetMaxValue : on récupère le slider si la référence manque.
        AcquireSliderReferenceIfNeeded();

        // Certains objets appellent SetValue avant d'avoir défini la vie maximale
        // (par exemple juste après une instantiation dynamique). Dans ce cas, la
        // valeur transmise correspond en général à la vie actuelle qui sert aussi
        // de référence maximale implicite. Sans cette correction, le Slider Unity
        // garde sa valeur maximale par défaut (1f) et ne reflète jamais les pertes
        // de PV puisque l'attribut "value" est systématiquement clampé à 1.
        if (current > maxValue)
        {
            // On promeut la valeur courante comme nouvelle borne supérieure afin de
            // conserver un ratio cohérent même lorsque SetMaxValue n'a pas encore
            // été appelé. Cela évite un slider bloqué au maximum.
            maxValue = current;
        }

        // Empêche de dépasser les bornes connues afin de garder un ratio sain.
        currentValue = Mathf.Clamp(current, 0f, maxValue > 0f ? maxValue : Mathf.Abs(current));

        UpdateSlider(currentValue);
        UpdateDynamicFill();
    }

    /// <summary>
    /// Synchronise le Slider Unity si un artiste préfère ce workflow.
    /// </summary>
    /// <param name="value">Valeur à afficher.</param>
    /// <param name="forceRefresh">
    /// Indique si le slider doit être explicitement initialisé (utilisé lors d'un changement de vie max).
    /// </param>
    private void UpdateSlider(float value, bool forceRefresh = false)
    {
        if (slider == null)
            return;

        // On fige la valeur minimale à zéro pour éviter que le handle ne remonte si la maxValue change.
        slider.minValue = 0f;

        if (forceRefresh || slider.maxValue < maxValue)
        {
            // L'assignation est répétée si nécessaire car certains sliders
            // conservent la valeur par défaut (1f) lorsqu'on saute SetMaxValue.
            // On garantit ainsi que la jauge peut réellement descendre.
            slider.maxValue = maxValue;
        }

        slider.value = value;
    }

    /// <summary>
    /// Adapte dynamiquement la largeur (via l'échelle locale) d'un RectTransform optionnel.
    /// Cette approche est utile pour les barres stylisées qui ne reposent pas sur le composant Slider.
    /// </summary>
    private void UpdateDynamicFill()
    {
        if (dynamicFillRect == null)
            return;

        float ratio = maxValue > 0f ? currentValue / maxValue : 0f;
        ratio = Mathf.Clamp01(ratio);

        // Met à jour uniquement l'axe X afin de préserver l'échelle verticale et la profondeur éventuelle.
        Vector3 scale = dynamicFillRect.localScale;
        scale.x = ratio;
        dynamicFillRect.localScale = scale;
    }

    /// <summary>
    /// Recherche paresseusement un <see cref="Slider"/> si aucun n'est assigné pour synchroniser la jauge.
    /// </summary>
    private void AcquireSliderReferenceIfNeeded()
    {
        if (slider != null)
            return; // Référence déjà disponible : rien à faire.

        // Priorité au Slider placé directement sur le même GameObject que la HPBar.
        slider = GetComponent<Slider>();
        if (slider != null)
            return;

        // En dernier recours, on explore les enfants (même inactifs) afin de couvrir les variantes de prefabs.
        slider = GetComponentInChildren<Slider>(includeInactive: true);
    }
}
