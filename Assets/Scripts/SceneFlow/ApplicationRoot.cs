using UnityEngine;

/// <summary>
/// Marqueur de la racine qui vit pendant toute l'application.
/// Les services globaux sont crees sous cette racine par GameFlowService.
/// </summary>
[DisallowMultipleComponent]
public sealed class ApplicationRoot : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        if (GetComponent<TimeManager>() == null)
        {
            gameObject.AddComponent<TimeManager>();
        }
    }
}
