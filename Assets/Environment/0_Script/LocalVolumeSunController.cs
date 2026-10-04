using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Active une source lumineuse directionnelle uniquement lorsque le personnage
/// se trouve dans le Volume HDRP local associe.
/// </summary>
[DefaultExecutionOrder(-490)]
[DisallowMultipleComponent]
public sealed class LocalVolumeSunController : MonoBehaviour
{
    [SerializeField, Tooltip("Racine qui contient la source lumineuse et ses lumieres directionnelles.")]
    private GameObject sunRoot;

    [SerializeField, Tooltip("Laisser vide pour utiliser le Volume porte par ce GameObject.")]
    private Volume sunVolume;

    [SerializeField, Tooltip("Ancre explicite. Laisser vide pour suivre le personnage controle.")]
    private Transform anchor;

    [Header("Runtime debug")]
    [SerializeField, Tooltip("Influence actuellement evaluee pour la zone de lumiere.")]
    private float currentInfluence;
    [SerializeField, Tooltip("Etat actuellement applique a Sun Root.")]
    private bool lightIsActive;

    private bool initialSunRootActive;

    private void Awake()
    {
        if (sunVolume == null)
        {
            sunVolume = GetComponent<Volume>();
        }

        if (sunRoot == null || sunVolume == null)
        {
            Debug.LogError("[LocalVolumeSunController] Configuration incomplete sur " + name +
                           ". Renseigner Sun Root et Sun Volume.", this);
            enabled = false;
            return;
        }

        initialSunRootActive = sunRoot.activeSelf;
        SetVisible(false);
    }

    private void LateUpdate()
    {
        GameObject controlledCharacter = LocalPlayerUtils.GetControlledCharacter();
        if (controlledCharacter != null)
        {
            anchor = controlledCharacter.transform;
        }

        if (anchor == null)
        {
            return;
        }

        currentInfluence = EvaluateInfluence(sunVolume, anchor.position);
        SetVisible(currentInfluence > 0.001f);
    }

    private void SetVisible(bool visible)
    {
        lightIsActive = visible;
        if (sunRoot.activeSelf != visible)
        {
            sunRoot.SetActive(visible);
        }
    }

    private void OnDisable()
    {
        if (sunRoot == null)
        {
            return;
        }

        sunRoot.SetActive(initialSunRootActive);
    }

    private static float EvaluateInfluence(Volume volume, Vector3 position)
    {
        if (volume == null || !volume.isActiveAndEnabled)
        {
            return 0f;
        }

        if (volume.isGlobal)
        {
            return Mathf.Clamp01(volume.weight);
        }

        // HDRP enregistre tous les colliders portes par un Volume. La zone
        // d'influence est leur union, avec le meilleur poids parmi eux.
        float influence = 0f;
        for (int i = 0; i < volume.colliders.Count; i++)
        {
            Collider collider = volume.colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            float signedDistance = EvaluateSignedDistance(collider, position);
            float colliderInfluence = signedDistance <= 0f
                ? Mathf.Clamp01(volume.weight)
                : volume.blendDistance <= 0f
                    ? 0f
                    : Mathf.Clamp01(volume.weight) * Mathf.Clamp01(1f - signedDistance / volume.blendDistance);
            influence = Mathf.Max(influence, colliderInfluence);
        }

        return influence;
    }

    private static float EvaluateSignedDistance(Collider volumeCollider, Vector3 position)
    {
        if (volumeCollider is BoxCollider boxCollider)
        {
            Vector3 point = boxCollider.transform.InverseTransformPoint(position) - boxCollider.center;
            Vector3 halfSize = boxCollider.size * .5f;
            Vector3 distance = new Vector3(
                Mathf.Abs(point.x) - halfSize.x,
                Mathf.Abs(point.y) - halfSize.y,
                Mathf.Abs(point.z) - halfSize.z);
            Vector3 outside = new Vector3(Mathf.Max(distance.x, 0f), Mathf.Max(distance.y, 0f), Mathf.Max(distance.z, 0f));
            float inside = Mathf.Min(Mathf.Max(distance.x, Mathf.Max(distance.y, distance.z)), 0f);
            Vector3 scale = boxCollider.transform.lossyScale;
            float scaleFactor = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return (outside.magnitude + inside) * scaleFactor;
        }

        if (volumeCollider is SphereCollider sphereCollider)
        {
            Vector3 scale = sphereCollider.transform.lossyScale;
            float scaleFactor = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return Vector3.Distance(position, sphereCollider.transform.TransformPoint(sphereCollider.center)) - sphereCollider.radius * scaleFactor;
        }

        Vector3 closestPoint = volumeCollider.ClosestPoint(position);
        return Vector3.Distance(closestPoint, position);
    }
}
