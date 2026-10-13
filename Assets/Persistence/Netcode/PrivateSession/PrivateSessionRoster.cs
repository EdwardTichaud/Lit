using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Lit/Multiplayer/Private session roster")]
public sealed class PrivateSessionRoster : ScriptableObject
{
    public CharacterData[] characters;

    public CharacterData[] Resolve()
    {
        IEnumerable<CharacterData> available = characters ?? System.Array.Empty<CharacterData>();
        string path = SaveSessionManager.Instance?.GetActiveSaveFilePath("CharacterState.json");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            CharacterSaveData saved = JsonUtility.FromJson<CharacterSaveData>(File.ReadAllText(path));
            if (saved == null) throw new InvalidDataException("Sauvegarde de personnages illisible.");
            // A solo v8 roster describes the only physical actor, not the
            // multiplayer character catalog. Never shrink a private lobby to it.
            var soloCharacter = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default")?.GetCharacter(CombatSkillKit.Lucian);
            bool soloPhysicalRoster = saved.soloTeam != null && saved.squadIds != null &&
                saved.squadIds.Count == 1 && soloCharacter != null &&
                (saved.squadIds[0] == soloCharacter.characterId || saved.squadIds[0] == soloCharacter.UniqueId);
            if (!soloPhysicalRoster && saved.squadIds != null && saved.squadIds.Count > 0)
                available = available.Where(c => c != null && saved.squadIds.Contains(c.characterId));
        }
        return available.Where(c => c != null && !string.IsNullOrWhiteSpace(c.characterId))
            .GroupBy(c => c.characterId).Select(g => g.First()).Take(4).ToArray();
    }
}
