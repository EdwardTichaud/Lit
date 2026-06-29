using System.Collections.Generic;
using UnityEngine;

public class SquadManager : MonoBehaviour
{
    public static SquadManager Instance { get; private set; }

    [SerializeField] private List<CharacterData> squadCharacters = new();

    public IReadOnlyList<CharacterData> SquadCharacters => squadCharacters;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetSquad(List<CharacterData> characters)
    {
        squadCharacters = new List<CharacterData>(characters);
    }

    public bool Contains(CharacterData data)
    {
        return data != null && squadCharacters.Contains(data);
    }

    public void AddToSquad(CharacterData data)
    {
        if (data == null || squadCharacters.Contains(data))
            return;

        squadCharacters.Add(data);
    }

    public void AddToSquadAt(CharacterData data, int index)
    {
        if (data == null || squadCharacters.Contains(data))
            return;

        if (index < 0 || index > squadCharacters.Count)
        {
            squadCharacters.Add(data);
            return;
        }

        squadCharacters.Insert(index, data);
    }

    public bool TryRemoveFromSquad(CharacterData data, out int index)
    {
        index = -1;
        if (data == null)
            return false;

        index = squadCharacters.IndexOf(data);
        if (index < 0)
            return false;

        squadCharacters.RemoveAt(index);
        return true;
    }

    public void MoveCharacter(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= squadCharacters.Count ||
            toIndex < 0 || toIndex >= squadCharacters.Count ||
            fromIndex == toIndex)
            return;

        CharacterData cd = squadCharacters[fromIndex];
        squadCharacters.RemoveAt(fromIndex);
        squadCharacters.Insert(toIndex, cd);
    }
}
