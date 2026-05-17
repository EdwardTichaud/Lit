// Role:
// ScriptableObject data for a skill check.
// Usage:
// Skill assets define the stat, difficulty, and automatic-roll behaviour used by SkillCheckSystem.
// Responsibilities:
// Store simple skill-check configuration only.
// Dependencies:
// SkillCheckSystem and any UI that displays skill information.
// Precautions:
// Keep this data stable; changing difficulty values affects authored gameplay checks.
using UnityEngine;

/// <summary>
/// Character statistics available for skill checks.
/// </summary>
public enum StatType
{
    Strength,
    Dexterity,
    Constitution,
    Intelligence,
    Wisdom,
    Charisma
}

/// <summary>
/// Data asset describing one skill check configuration.
/// </summary>
[CreateAssetMenu(fileName = "Skill", menuName = "Scriptable Objects/Skill")]
public class Skill : ScriptableObject
{
    [Header("Identity")]
    /// <summary>Display name of the skill.</summary>
    [Tooltip("Nom affiche de la competence.")]
    public string skillName;
    /// <summary>Description shown in UI.</summary>
    [TextArea, Tooltip("Description visible dans l'UI.")]
    public string description;

    [Header("Check")]
    /// <summary>Stat used for the roll.</summary>
    [Tooltip("Stat utilisee pour le jet.")]
    public StatType linkedStat = StatType.Intelligence;
    /// <summary>Difficulty class target for the roll.</summary>
    [Tooltip("Difficulte du jet (DC).")]
    public int difficultyClass = 10;
    /// <summary>If true, proximity can trigger the roll automatically.</summary>
    [Tooltip("Declenche automatiquement un jet a proximite.")]
    public bool autoRollOnProximity = false;
    /// <summary>If false, the check succeeds without a roll.</summary>
    [Tooltip("Si false, le check passe automatiquement.")]
    public bool requiresRoll = true;
}
