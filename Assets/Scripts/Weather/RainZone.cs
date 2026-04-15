using UnityEngine;

[DisallowMultipleComponent]
public sealed class RainZone : MonoBehaviour
{
    [SerializeField, Range(0f, 1f), Tooltip("Intensite de pluie appliquee quand la cible est dans cette zone.")]
    private float intensity = 1f;
    [SerializeField, Tooltip("Priorite utilisee quand plusieurs zones se chevauchent.")]
    private int priority;
    [SerializeField, Min(0f), Tooltip("Temps de fondu quand la pluie de cette zone devient active.")]
    private float fadeInSeconds = 1f;
    [SerializeField, Min(0f), Tooltip("Temps de fondu quand la pluie de cette zone s'arrete.")]
    private float fadeOutSeconds = 1.5f;
    [SerializeField, Min(0f), Tooltip("Multiplicateur des impacts au sol pour cette zone.")]
    private float splashMultiplier = 1f;
    [SerializeField, Tooltip("Inclut les colliders enfants dans le volume de pluie.")]
    private bool includeChildColliders;
    [SerializeField, Tooltip("Ignore la hauteur de la cible et utilise la zone comme une surface XZ. Utile pour les cameras hautes.")]
    private bool ignoreVerticalBounds = true;

    private Collider[] cachedColliders;

    public float Intensity => intensity;
    public int Priority => priority;
    public float FadeInSeconds => fadeInSeconds;
    public float FadeOutSeconds => fadeOutSeconds;
    public float SplashMultiplier => splashMultiplier;

    private void OnEnable()
    {
        RefreshColliders();
        RainSystem.RegisterZone(this);
    }

    private void OnDisable()
    {
        RainSystem.UnregisterZone(this);
    }

    private void OnValidate()
    {
        intensity = Mathf.Clamp01(intensity);
        fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
        fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
        splashMultiplier = Mathf.Max(0f, splashMultiplier);
        RefreshColliders();
    }

    public void RefreshColliders()
    {
        cachedColliders = includeChildColliders
            ? GetComponentsInChildren<Collider>(true)
            : GetComponents<Collider>();
    }

    public bool Contains(Vector3 worldPosition)
    {
        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            RefreshColliders();
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider zoneCollider = cachedColliders[i];
            if (zoneCollider == null || !zoneCollider.enabled || !zoneCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 testedPosition = GetTestedPosition(zoneCollider.bounds, worldPosition);
            if (!zoneCollider.bounds.Contains(testedPosition))
            {
                continue;
            }

            Vector3 closest = zoneCollider.ClosestPoint(testedPosition);
            if ((closest - testedPosition).sqrMagnitude <= 0.0001f)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetTestedPosition(Bounds bounds, Vector3 worldPosition)
    {
        if (!ignoreVerticalBounds)
        {
            return worldPosition;
        }

        float y = Mathf.Clamp(worldPosition.y, bounds.min.y, bounds.max.y);
        return new Vector3(worldPosition.x, y, worldPosition.z);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.55f, 1f, 0.22f);
        Collider[] colliders = includeChildColliders
            ? GetComponentsInChildren<Collider>(true)
            : GetComponents<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider zoneCollider = colliders[i];
            if (zoneCollider == null)
            {
                continue;
            }

            Gizmos.DrawCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
        }
    }
#endif
}
