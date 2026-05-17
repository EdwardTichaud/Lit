using UnityEngine;

// Role: petit composant qui relie un GameObject ennemi a son CharacterData.
// Usage: attache aux prefabs/objets ennemis pour que d'autres systemes resolvent leurs donnees.
// Responsibilities: exposer et remplacer la reference CharacterData ennemie.
// Dependencies: CharacterData.
// Precautions: conserver les alias Enemy et CharacterData, car differents scripts peuvent utiliser l'un ou l'autre.
/// <summary>
/// Reference de donnees pour un ennemi place en scene.
/// </summary>
public class EnemyInfo : MonoBehaviour
{
    /// <summary>
    /// Donnee de personnage utilisee pour cet ennemi.
    /// </summary>
    [SerializeField]
    private CharacterData enemy;

    /// <summary>
    /// Alias historique pour acceder a la donnee ennemie.
    /// </summary>
    public CharacterData Enemy => enemy;
    /// <summary>
    /// Alias generique utilise par certains systemes de personnage.
    /// </summary>
    public CharacterData CharacterData => enemy;

    /// <summary>
    /// Remplace la donnee ennemie.
    /// </summary>
    public void SetEnemy(CharacterData data)
    {
        enemy = data;
    }

    /// <summary>
    /// Remplace la donnee ennemie avec le nom utilise par les systemes CharacterData.
    /// </summary>
    public void SetCharacterData(CharacterData data)
    {
        enemy = data;
    }
}
