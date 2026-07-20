using UnityEngine;

/// <summary>
/// Racine des services qui doivent survivre aux changements de zone d'une partie,
/// mais pas au retour au menu principal.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplaySessionRoot : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
