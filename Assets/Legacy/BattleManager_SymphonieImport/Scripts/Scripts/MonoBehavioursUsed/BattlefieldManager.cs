using UnityEngine;
using System.Collections;

/// <summary>
/// Gère le champ de bataille actuellement utilisé. Contrairement à l'ancien
/// fonctionnement, un seul battlefield est instancié à la fois et uniquement
/// lorsque le combat débute. Cette approche limite la consommation mémoire et
/// évite des destructions inutiles.
/// </summary>
public class BattlefieldManager : MonoBehaviour
{
    public static BattlefieldManager Instance { get; private set; }

    [Header("Zone active (copiée depuis ZoneManager)")]
    public ZoneSO currentZone;

    // Référence vers le battlefield actuellement instancié
    private GameObject currentBattlefield;

    // Parent dans la hiérarchie pour accueillir le battlefield instancié
    [SerializeField] private Transform battlefieldParent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject);

        // Recherche automatique du parent si besoin pour éviter les erreurs
        if (battlefieldParent == null)
            battlefieldParent = GameObject.Find("BattleScene/Battlefields")?.transform;
    }

    /// <summary>
    /// Appelée par le <see cref="ZoneManager"/> lorsque la zone change.
    /// Détruit le battlefield courant et mémorise la nouvelle zone.
    /// </summary>
    public void SetCurrentZone(ZoneSO zone)
    {
        if (zone == null)
        {
            Debug.LogWarning("[BattlefieldManager] ZoneSO null !");
            return;
        }

        currentZone = zone;

        // Si un battlefield était encore présent, on le retire pour repartir propre
        UnloadCurrentBattlefield();

        if (battlefieldParent == null)
            battlefieldParent = GameObject.Find("BattleScene/Battlefields")?.transform;
    }

    /// <summary>
    /// Instancie le battlefield correspondant à l'indice fourni. Cette méthode
    /// est appelée pendant l'écran de Versus, avant que le joueur ne confirme le
    /// début du combat.
    /// </summary>
    public IEnumerator LoadBattlefield(int index)
    {
        if (currentZone == null)
        {
            Debug.LogWarning("[BattlefieldManager] Aucune zone active, impossible de charger un battlefield.");
            yield break;
        }

        if (index < 0 || index >= currentZone.battlefields.Count)
        {
            Debug.LogWarning($"[BattlefieldManager] Index {index} invalide pour {currentZone.zoneName}");
            yield break;
        }

        if (battlefieldParent == null)
            battlefieldParent = GameObject.Find("BattleScene/Battlefields")?.transform;

        // Détruit l'ancien champ de bataille s'il existe déjà
        UnloadCurrentBattlefield();

        // Instanciation du prefab voulu et activation immédiate
        var prefab = currentZone.battlefields[index];
        currentBattlefield = Instantiate(prefab, battlefieldParent.position, Quaternion.identity, battlefieldParent);
        currentBattlefield.SetActive(true);

        // ✅ S'assure que le conteneur des positions de combat utilise le layer "Battle_Unit"
        var positions = currentBattlefield.transform.Find("Battlefield_Positions");
        if (positions != null)
        {
            int battleUnitLayer = LayerMask.NameToLayer("Battle_Unit");
            SetLayerRecursively(positions.gameObject, battleUnitLayer); // Applique aussi aux enfants
        }
        else
        {
            Debug.LogWarning("[BattlefieldManager] GameObject 'Battlefield_Positions' introuvable dans le battlefield instancié.");
        }

        // On laisse une frame pour s'assurer que tous les éléments sont correctement initialisés
        yield return null;

        // ✅ Réapplique les Rendering Layers à toute la scène après le changement de layer
        var renderingLayerSetter = FindFirstObjectByType<SetRenderingLayer>(FindObjectsInactive.Include);
        renderingLayerSetter?.ApplyToAll();

        Debug.Log($"[BattlefieldManager] Battlefield #{index} chargé pour {currentZone.zoneName}.");
    }

    /// <summary>
    /// Détruit le battlefield actuellement chargé, s'il existe.
    /// </summary>
    public void UnloadCurrentBattlefield()
    {
        if (currentBattlefield != null)
        {
            Destroy(currentBattlefield);
            currentBattlefield = null;
            Debug.Log("[BattlefieldManager] Battlefield courant détruit.");
        }
    }

    public void SetBattlefieldVisible(bool visible)
    {
        if (currentBattlefield != null)
            currentBattlefield.SetActive(visible);
    }

    /// <summary>
    /// Applique récursivement un <c>Layer</c> Unity à un <see cref="GameObject"/> et à tous ses enfants.
    /// Utilisé ici pour que toutes les positions de combat héritent du layer "Battle_Unit".
    /// </summary>
    /// <param name="obj">Racine sur laquelle appliquer le layer.</param>
    /// <param name="layer">Identifiant du layer Unity à appliquer.</param>
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
