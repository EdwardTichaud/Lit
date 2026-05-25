using System.Collections.Generic;
using UnityEngine;

// Structure de sauvegarde globale (JSON).
[System.Serializable]
public class CharacterSaveData
{
    public int dataVersion = 2;
    public List<string> squadIds = new List<string>();
    public int currentIndex = 0;
    public List<CharacterSaveEntry> characters = new List<CharacterSaveEntry>();
    public List<ItemStackData> homeItems = new List<ItemStackData>();
    public List<BuiltConstructionData> builtConstructions = new List<BuiltConstructionData>();
    public List<PlayerCharacterBinding> playerBindings = new List<PlayerCharacterBinding>();
    public List<BraseroSaveEntry> braseros = new List<BraseroSaveEntry>();
    public List<TorchSaveEntry> torches = new List<TorchSaveEntry>();
    public List<ReadableGeneratedContentData> readableGeneratedContents = new List<ReadableGeneratedContentData>();
}

// Association playerId -> characterId (continuite multijoueur).
[System.Serializable]
public class PlayerCharacterBinding
{
    public string playerId;
    public string characterId;
}

// Sauvegarde d'un personnage et de son inventaire.
[System.Serializable]
public class CharacterSaveEntry
{
    public string characterId;
    public bool inSquad;
    public Vector3 position;
    public Quaternion rotation;
    public int torchSeconds;
    public bool torchEquipped;
    public bool muninChargesInitialized;
    public int muninCharges;
    public int muninMaxCharges;
    public List<ItemStackData> items = new List<ItemStackData>();
    public bool itemsInitialized;
    public List<string> skillIds = new List<string>();
    public bool skillsInitialized;
}

// Pair itemId + quantite pour la persistence.
[System.Serializable]
public class ItemStackData
{
    public string itemId;
    public int quantity;
}

// Sauvegarde d'une construction instanciee dans la scene.
[System.Serializable]
public class BuiltConstructionData
{
    public string buildId;
    public string itemId;
    public string buildingDataId;
    public int level = 1;
    public bool isHomeChest;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}

// Sauvegarde de l'etat d'un brasero.
[System.Serializable]
public class BraseroSaveEntry
{
    public string braseroId;
    public bool isLit;
}

// Sauvegarde de l'etat d'une torche de monde.
[System.Serializable]
public class TorchSaveEntry
{
    public string torchId;
    public bool isLit;
}

[System.Serializable]
public class ReadableGeneratedContentData
{
    public string itemId;
    public int seed;
    public List<string> generatedSentences = new List<string>();
    public List<string> bookPages = new List<string>();
    public string parchmentText;
}
