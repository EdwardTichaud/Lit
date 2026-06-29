using UnityEngine;

/// <summary>
/// Garde un placeholder ScriptableObject pour les anciens assets \"BattleAI\".
/// L'IA runtime est désormais gérée par <see cref=\"BattleAIStrategy\"/>.
/// </summary>
[CreateAssetMenu(fileName = "BattleAIProfile", menuName = "Symphonie/AI/Battle AI Profile")]
public class BattleAI : ScriptableObject
{
    [TextArea]
    [Tooltip("Notes ou paramètres conservés pour rétrocompatibilité.")]
    public string designerNotes;
}
