using UnityEngine;

/// <summary>
/// Objet scriptable représentant un déclencheur de QTE depuis une Timeline.
/// Contient l'icône à afficher et la durée de la fenêtre de saisie.
/// </summary>
[CreateAssetMenu(menuName = "Symphonie/QTE Trigger")]
public class QTETriggerSO : ScriptableObject
{
    [Tooltip("Définition complète de l'input QTE (sprite + input Battle).")] public QTEInputSO qteInput;
    [Tooltip("Icône de l'input à afficher")] public Sprite inputIcon;
    [Tooltip("Fenêtre de saisie en millisecondes")] public float windowDelay = 200f;

    [Tooltip("Position du visuel dans le canvas (pixels)")] public Vector2 uiPosition = Vector2.zero;
}
