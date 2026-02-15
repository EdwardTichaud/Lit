using System.Collections.Generic;
using UnityEngine;

// Donnees d'un item utilisees par l'inventaire et le loot.
[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class Item : ScriptableObject
{
    [System.Serializable]
    public class BreakResult
    {
        [Tooltip("Item obtenu apres casse.")]
        public Item item;
        [Tooltip("Quantite obtenue.")]
        public int quantity = 1;
    }

    [System.Serializable]
    public class BuildingRequirement
    {
        [Tooltip("Item requis pour construire/ameliorer.")]
        public Item item;
        [Tooltip("Quantite requise par niveau.")]
        public int quantity = 1;
    }

    [Header("Identity")]
    [Tooltip("Identifiant unique (optionnel).")]
    public string itemId;
    [Tooltip("Nom affiche dans l'UI.")]
    public string itemName;

    [Header("UI")]
    [Tooltip("Sprite utilise pour les apercus/illustrations.")]
    public Sprite itemSprite;
    [TextArea]
    [Tooltip("Description affichee dans l'UI.")]
    public string description;

    [Header("Usage")]
    [Tooltip("Peut etre utilise via l'ActionBox.")]
    public bool canUse;
    [Tooltip("Peut etre pose dans le monde.")]
    public bool canPlace;
    [Tooltip("Est un container (coffre, sac, etc.).")]
    public bool isContainer;
    [Tooltip("Prefab a instancier lors de la pose.")]
    public GameObject worldPrefab;
    [Tooltip("Item special torche.")]
    public bool isTorch;
    [Tooltip("Effet applique lors de l'utilisation.")]
    public Effect itemEffect;

    [Header("Building")]
    [Tooltip("Si true, l'item est traite comme un building.")]
    public bool isBuilding = false;
    [Tooltip("Prefab instancie lors de la construction (fallback: worldPrefab).")]
    public GameObject buildingPrefab;
    [Min(1)]
    [Tooltip("Niveau maximal du building.")]
    public int buildingMaxLevel = 5;
    [Min(0)]
    [Tooltip("Niveau courant (0 = non construit).")]
    public int buildingCurrentLevel = 0;
    [Tooltip("Ressources necessaires pour construire ou ameliorer (par niveau).")]
    public List<BuildingRequirement> buildingRequirements = new List<BuildingRequirement>();
    [Tooltip("Effets appliques a chaque niveau gagne.")]
    public List<Effect> buildingEffects = new List<Effect>();
    [Tooltip("Si true, la construction est un coffre maison.")]
    public bool isHomeChest = false;

    [Header("Break")]
    [Tooltip("Peut etre casse via l'ActionBox.")]
    public bool canBreak;
    [Tooltip("Resultats de la casse.")]
    public List<BreakResult> breakResults = new List<BreakResult>();

    public bool HasBreakResults()
    {
        if (!canBreak || breakResults == null || breakResults.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < breakResults.Count; i++)
        {
            BreakResult result = breakResults[i];
            if (result != null && result.item != null && result.quantity > 0)
            {
                return true;
            }
        }

        return false;
    }
}
