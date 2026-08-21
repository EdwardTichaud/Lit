using UnityEngine;

// Stocke un CharacterData pour les personnages et ennemis places en scene.
public class CharacterInfo : MonoBehaviour
{
    [SerializeField]
    private CharacterData characterData;

    public CharacterData CharacterData => characterData;
    /// <summary>Alias semantique pour les consommateurs ennemis historiques.</summary>
    public CharacterData Enemy => characterData;

    public void SetCharacterData(CharacterData data)
    {
        characterData = data;
    }

    /// <summary>Alias semantique pour configurer un ennemi.</summary>
    public void SetEnemy(CharacterData data)
    {
        characterData = data;
    }
}
