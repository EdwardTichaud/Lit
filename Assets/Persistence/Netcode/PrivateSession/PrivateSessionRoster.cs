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
            if (saved.squadIds != null && saved.squadIds.Count > 0)
                available = available.Where(c => c != null && saved.squadIds.Contains(c.characterId));
        }
        return available.Where(c => c != null && !string.IsNullOrWhiteSpace(c.characterId))
            .GroupBy(c => c.characterId).Select(g => g.First()).Take(4).ToArray();
    }
}
