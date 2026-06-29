using UnityEngine;

/// <summary>
///     ScriptableObject décrivant un Sceau modulant la difficulté selon la documentation « Répertoire des MusicalMoves ».
///     Chaque instance encode les ajustements de score, les effets de gameplay et les conseils narratifs afin que
///     l'éditeur Unity affiche des données homogènes pour les game designers.
/// </summary>
[CreateAssetMenu(fileName = "NewSceau", menuName = "Symphonie/Sceau")]
public class SceauSO : ScriptableObject
{
    [Header("Identité et catégorisation"), Tooltip("Nom affiché dans l'inventaire et les interfaces.")]
    public string sealName = "Nouveau Sceau";

    [Tooltip("Regroupe le Sceau selon la famille décrite dans la documentation (universel, défi, spécifique).")]
    public SceauArchetype archetype = SceauArchetype.Universel;

    [Tooltip("Indique si le Sceau facilite (valeurs négatives) ou augmente la difficulté (valeurs positives).")]
    public SceauIntensity intensity = SceauIntensity.Facilitation;

    [Tooltip("Icône optionnelle pour illustrer le Sceau dans les menus.")]
    public Sprite icon;

    [Header("Effets de gameplay"), Tooltip("Résumé concis de l'effet principal pour les designers.")]
    [TextArea]
    public string effectSummary;

    [Tooltip("Conseils sur les meilleures situations d'utilisation afin de guider les nouveaux joueurs.")]
    [TextArea]
    public string usageTips;

    [Tooltip("Modification du score final en pourcentage. Utiliser des valeurs négatives pour les Sceaux de facilitation.")]
    public float scoreModifierPercent;

    [Header("Contexte narratif"), Tooltip("Note facultative pour relier le Sceau à l'histoire de Symphonie.")]
    [TextArea]
    public string loreNote;
}

/// <summary>
/// Catégories décrivant le périmètre d'utilisation du Sceau.
/// </summary>
public enum SceauArchetype
{
    Universel,
    UniverselDefi,
    Specifique
}

/// <summary>
/// Renseigne si le Sceau rend la partie plus accessible ou propose un défi supplémentaire.
/// </summary>
public enum SceauIntensity
{
    Facilitation,
    Defi
}
