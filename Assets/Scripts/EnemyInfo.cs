using UnityEngine;

public class EnemyInfo : MonoBehaviour
{
    [SerializeField]
    private CharacterData enemy;

    public CharacterData Enemy => enemy;
    public CharacterData CharacterData => enemy;

    public void SetEnemy(CharacterData data)
    {
        enemy = data;
    }

    public void SetCharacterData(CharacterData data)
    {
        enemy = data;
    }
}
