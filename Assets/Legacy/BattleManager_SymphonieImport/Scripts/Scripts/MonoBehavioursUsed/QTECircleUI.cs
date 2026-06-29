using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant à placer sur le prefab de cercle QTE.
/// Référence les images utilisées par le RhythmQTEManager
/// afin d'éviter toute recherche par nom dans la hiérarchie.
/// </summary>
public class QTECircleUI : MonoBehaviour
{
    [SerializeField] private Image delayFillImage; // cercle se remplissant
    [SerializeField] private Image inputIconImage; // icône de l'input au centre
    [SerializeField] private RectTransform targetRing; // cercle cible fixe pour le timing

    /// <summary>
    /// Accès en lecture à l'image représentant la progression du QTE.
    /// </summary>
    public Image DelayFillImage => delayFillImage;

    /// <summary>
    /// Accès en lecture à l'image de l'icône centrale.
    /// </summary>
    public Image InputIconImage => inputIconImage;

    /// <summary>
    /// Accès en lecture au cercle cible fixe.
    /// </summary>
    public RectTransform TargetRing => targetRing;
}
