using UnityEngine;

public enum CombatSkillKit { Lucian, Vesta, Soren, Aurell }

[CreateAssetMenu(menuName = "Lit/Combat/Skill Kit Catalog")]
public sealed class CombatSkillKitCatalog : ScriptableObject
{
    [SerializeField] private CharacterData lucianCharacter;
    [SerializeField] private CharacterData vestaCharacter;
    [SerializeField] private CharacterData sorenCharacter;
    [SerializeField] private CharacterData aurellCharacter;
    public CharacterData GetCharacter(CombatSkillKit kit) => kit switch
    {
        CombatSkillKit.Lucian => lucianCharacter, CombatSkillKit.Vesta => vestaCharacter,
        CombatSkillKit.Soren => sorenCharacter, CombatSkillKit.Aurell => aurellCharacter, _ => null
    };
    public bool IsCharacter(CharacterData character, CombatSkillKit kit)
    {
        var definition = GetCharacter(kit);
        if (character == null || definition == null) return false;
        // Runtime clones retain saved identities; display names are not identities.
        return character == definition ||
            !string.IsNullOrWhiteSpace(character.UniqueId) &&
                string.Equals(character.UniqueId, definition.UniqueId, System.StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(character.characterId) &&
                string.Equals(character.characterId, definition.characterId, System.StringComparison.OrdinalIgnoreCase);
    }

    public bool TryFind(SkillSO skill, out CombatSkillKit kit, out int slot)
    {
        foreach (CombatSkillKit candidate in System.Enum.GetValues(typeof(CombatSkillKit)))
            for (int i = 0; i < 4; i++)
                if (skill != null && GetSkill(candidate, i) == skill) { kit = candidate; slot = i; return true; }
        kit = default; slot = -1; return false;
    }
    // Stable slots: left, up, down, right (historically X, Y, A, B).
    // Never reorder saved unlock masks when changing input presentation.
    [SerializeField] private SkillSO[] lucian = new SkillSO[4];
    [SerializeField] private SkillSO[] vesta = new SkillSO[4];
    [SerializeField] private SkillSO[] soren = new SkillSO[4];
    [SerializeField] private SkillSO[] aurell = new SkillSO[4];

    public SkillSO[] GetSkills(CombatSkillKit kit) => kit switch
    {
        CombatSkillKit.Lucian => lucian,
        CombatSkillKit.Vesta => vesta,
        CombatSkillKit.Soren => soren,
        CombatSkillKit.Aurell => aurell,
        _ => null
    };

    public SkillSO GetSkill(CombatSkillKit kit, int slot)
    {
        var skills = GetSkills(kit);
        return skills != null && slot >= 0 && slot < Mathf.Min(4, skills.Length) ? skills[slot] : null;
    }

    public static bool TrySkillDirection(Vector2 direction, out int slot)
    {
        slot = -1;
        if (!TryDirection(direction, out var directionKit)) return false;
        slot = directionKit switch
        {
            CombatSkillKit.Aurell => 0, CombatSkillKit.Lucian => 1,
            CombatSkillKit.Soren => 2, CombatSkillKit.Vesta => 3, _ => -1
        };
        return slot >= 0;
    }

    public static bool TryDirection(Vector2 direction, out CombatSkillKit kit)
    {
        kit = CombatSkillKit.Lucian;
        // Reject diagonals instead of opening two intentions from one input.
        if (Mathf.Abs(direction.x) > .5f && Mathf.Abs(direction.y) > .5f) return false;
        if (direction.y > .5f) kit = CombatSkillKit.Lucian;
        else if (direction.x > .5f) kit = CombatSkillKit.Vesta;
        else if (direction.y < -.5f) kit = CombatSkillKit.Soren;
        else if (direction.x < -.5f) kit = CombatSkillKit.Aurell;
        else return false;
        return true;
    }
}
