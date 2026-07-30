using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Point de depart visuel d'une sous-scene decorative chargee localement.
/// Place ce composant dans la scene Critical de la zone : la sous-scene cible
/// ne doit contenir ni NetworkObject ni collision indispensable au gameplay.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProximitySceneVolume : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Scene")]
    [Tooltip("Sous-scene decorative chargee localement. Elle est synchronisee vers son nom de runtime a la sauvegarde.")]
    [SerializeField] private SceneAsset sceneAsset;
#endif

    [SerializeField, HideInInspector] private string proximitySceneName;
    [Header("Distances")]
    [SerializeField, Min(1f), Tooltip("La sous-scene est prechargee a cette distance.")]
    private float preloadDistance = 60f;
    [SerializeField, Min(1f), Tooltip("La sous-scene est activee a cette distance apres son prechargement.")]
    private float activationDistance = 30f;

    public string SceneName => proximitySceneName;
    public float PreloadDistance => preloadDistance;
    public float ActivationDistance => activationDistance;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(proximitySceneName) && activationDistance <= preloadDistance;

#if UNITY_EDITOR
    private void OnValidate()
    {
        proximitySceneName = sceneAsset != null ? sceneAsset.name : string.Empty;
        activationDistance = Mathf.Clamp(activationDistance, 1f, preloadDistance);
    }
#endif

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.18f, 0.65f, 1f, 0.14f);
        Gizmos.DrawSphere(transform.position, preloadDistance);
        Gizmos.color = new Color(0.2f, 1f, 0.55f, 0.25f);
        Gizmos.DrawSphere(transform.position, activationDistance);
        Gizmos.color = new Color(0.18f, 0.65f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, preloadDistance);
        Gizmos.color = new Color(0.2f, 1f, 0.55f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, activationDistance);
    }
}
