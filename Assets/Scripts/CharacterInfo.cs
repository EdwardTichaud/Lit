using UnityEngine;

// Stocke un CharacterData pour les PNJ/prefabs en scene.
public class CharacterInfo : MonoBehaviour
{
    [SerializeField]
    private CharacterData characterData;

    public CharacterData CharacterData => characterData;

    public void SetCharacterData(CharacterData data)
    {
        characterData = data;
    }
}
