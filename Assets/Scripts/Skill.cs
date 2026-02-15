using UnityEngine;

// Stats possibles pour les checks de competence.
public enum StatType
{
    Strength,
    Dexterity,
    Constitution,
    Intelligence,
    Wisdom,
    Charisma
}

[CreateAssetMenu(fileName = "Skill", menuName = "Scriptable Objects/Skill")]
// Donnees d'une competence utilisable dans les skill checks.
public class Skill : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Nom affiche de la competence.")]
    public string skillName;
    [TextArea, Tooltip("Description visible dans l'UI.")]
    public string description;

    [Header("Check")]
    [Tooltip("Stat utilisee pour le jet.")]
    public StatType linkedStat = StatType.Intelligence;
    [Tooltip("Difficulte du jet (DC).")]
    public int difficultyClass = 10;
    [Tooltip("Declenche automatiquement un jet a proximite.")]
    public bool autoRollOnProximity = false;
    [Tooltip("Si false, le check passe automatiquement.")]
    public bool requiresRoll = true;
}
