using System;
using System.Collections.Generic;
using UnityEngine;

public enum AllegianceSide
{
    Player,
    Enemy
}

/// <summary>
/// Centralise l'allegeance runtime des personnages pour gerer les trahisons
/// sans modifier les ScriptableObjects.
/// </summary>
public class AllegianceManager : MonoBehaviour
{
    public static AllegianceManager Instance { get; private set; }

    private readonly Dictionary<CharacterData, AllegianceSide> allegianceOverrides = new();
    private readonly Dictionary<CharacterData, int> storedSquadIndices = new();

    public event Action<CharacterData, AllegianceSide> OnAllegianceChanged;

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

    public static AllegianceManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        var existing = FindFirstObjectByType<AllegianceManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        var go = new GameObject("AllegianceManager");
        return go.AddComponent<AllegianceManager>();
    }

    public bool TryGetOverride(CharacterData data, out AllegianceSide side)
    {
        if (data == null)
        {
            side = AllegianceSide.Player;
            return false;
        }

        return allegianceOverrides.TryGetValue(data, out side);
    }

    public AllegianceSide GetEffectiveAllegiance(CharacterData data)
    {
        if (data == null)
            return AllegianceSide.Player;

        if (allegianceOverrides.TryGetValue(data, out var side))
            return side;

        if (data.isPlayerControlled || data.characterType == CharacterType.SquadUnit)
            return AllegianceSide.Player;

        return AllegianceSide.Enemy;
    }

    public void ApplyToUnit(CharacterUnit unit)
    {
        if (unit == null || unit.Data == null)
            return;

        if (allegianceOverrides.TryGetValue(unit.Data, out var side))
            unit.ApplyAllegianceOverride(side, notifyManagers: false, notifyBattle: false);
    }

    public void SetAllegiance(CharacterUnit unit, AllegianceSide side)
    {
        if (unit == null || unit.Data == null)
            return;

        SetAllegiance(unit.Data, side, unit);
    }

    public void SetAllegiance(CharacterData data, AllegianceSide side, CharacterUnit sourceUnit = null)
    {
        if (data == null)
            return;

        allegianceOverrides[data] = side;
        UpdateSquadMembership(data, side);

        if (sourceUnit == null)
        {
            if (data.owner is CharacterUnit ownerUnit)
                ownerUnit.ApplyAllegianceOverride(side, notifyManagers: false, notifyBattle: true);
        }

        OnAllegianceChanged?.Invoke(data, side);
    }

    private void UpdateSquadMembership(CharacterData data, AllegianceSide side)
    {
        var squadManager = SquadManager.Instance;
        if (squadManager == null)
            return;

        if (side == AllegianceSide.Enemy)
        {
            if (squadManager.TryRemoveFromSquad(data, out int index))
                storedSquadIndices[data] = index;
        }
        else
        {
            if (!squadManager.Contains(data))
            {
                if (storedSquadIndices.TryGetValue(data, out int index))
                    squadManager.AddToSquadAt(data, index);
                else
                    squadManager.AddToSquad(data);
            }
        }
    }
}
