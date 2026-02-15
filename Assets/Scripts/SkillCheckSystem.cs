using UnityEngine;

// Systeme statique pour declencher un skill check et afficher le feedback.
public static class SkillCheckSystem
{
    public static bool TryCheck(GameObject character, Skill skill, out int roll, out int modifier, out int total)
    {
        roll = 0;
        modifier = 0;
        total = 0;

        if (character == null || skill == null)
        {
            return false;
        }

        CharacterData data = GetCharacterData(character);
        if (data == null)
        {
            return false;
        }

        if (!data.HasSkill(skill))
        {
            return false;
        }

        bool success = data.TryCheckSkill(skill, out roll, out modifier, out total);
        if (skill.requiresRoll)
        {
            // Affiche le feedback uniquement si un jet est requis.
            ShowFeedback(character, skill, roll, modifier, total, success);
        }
        return success;
    }

    public static CharacterData GetCharacterData(GameObject character)
    {
        SquadManager manager = SquadManager.Instance;
        if (manager == null || manager.squadCharacters == null || manager.currentSquad == null)
        {
            return null;
        }

        int index = manager.squadCharacters.IndexOf(character);
        if (index < 0 || index >= manager.currentSquad.Count)
        {
            return null;
        }

        return manager.currentSquad[index];
    }

    private static void ShowFeedback(GameObject character, Skill skill, int roll, int modifier, int total, bool success)
    {
        SkillCheckFeedbackAnchor anchor = character.GetComponentInChildren<SkillCheckFeedbackAnchor>(true);
        if (anchor == null)
        {
            return;
        }

        // Delegue l'affichage au prefab de feedback.
        anchor.Show(skill, roll, modifier, total, success);
    }
}
