using System.Collections.Generic;
using UnityEngine;

// Gere la disponibilite des compagnons dans le hub.
public class HubRosterManager : MonoBehaviour
{
    public static HubRosterManager Instance { get; private set; }

    [Header("Hub")]
    [Tooltip("Autorise le swap meme en dehors du hub.")]
    public bool allowSwapOutsideHub = false;
    [Tooltip("Indique si un personnage est actuellement dans le hub.")]
    public bool playerInHub = false;

    private readonly Dictionary<CharacterData, HubCompanionSwapTrigger> companions = new Dictionary<CharacterData, HubCompanionSwapTrigger>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public bool CanSwap()
    {
        // Swap autorise si dans le hub ou override actif.
        return allowSwapOutsideHub || playerInHub;
    }

    public void Register(HubCompanionSwapTrigger companion)
    {
        if (companion == null || companion.CharacterData == null)
        {
            return;
        }

        CharacterData runtimeCharacter = SquadManager.Instance != null
            ? SquadManager.Instance.GetRuntimeCharacter(companion.CharacterData)
            : companion.CharacterData;

        companions[runtimeCharacter] = companion;
        UpdateCompanionAvailability(runtimeCharacter);
    }

    public void Unregister(HubCompanionSwapTrigger companion)
    {
        if (companion == null || companion.CharacterData == null)
        {
            return;
        }

        CharacterData runtimeCharacter = SquadManager.Instance != null
            ? SquadManager.Instance.GetRuntimeCharacter(companion.CharacterData)
            : companion.CharacterData;

        if (companions.TryGetValue(runtimeCharacter, out HubCompanionSwapTrigger existing)
            && existing == companion)
        {
            companions.Remove(runtimeCharacter);
        }
    }

    public void SetInSquad(CharacterData character, bool inSquad)
    {
        if (character == null)
        {
            return;
        }

        CharacterData runtimeCharacter = SquadManager.Instance != null
            ? SquadManager.Instance.GetRuntimeCharacter(character)
            : character;

        if (companions.TryGetValue(runtimeCharacter, out HubCompanionSwapTrigger companion) && companion != null)
        {
            companion.SetInSquad(inSquad);
        }
    }

    public void UpdateCompanionAvailability(CharacterData character)
    {
        if (character == null)
        {
            return;
        }

        CharacterData runtimeCharacter = SquadManager.Instance != null
            ? SquadManager.Instance.GetRuntimeCharacter(character)
            : character;

        bool inSquad = SquadManager.Instance != null
            && SquadManager.Instance.currentSquad != null
            && SquadManager.Instance.currentSquad.Contains(runtimeCharacter);

        SetInSquad(runtimeCharacter, inSquad);
    }
}
