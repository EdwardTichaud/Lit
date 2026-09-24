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

    private bool initialSunRootActive;

    private void Awake()
    {
        if (sunVolume == null)
        {
            sunVolume = GetComponent<Volume>();
        }

        if (sunRoot == null || sunVolume == null)
        {
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

        SetVisible(EvaluateInfluence(sunVolume, anchor.position) > 0.001f);
    }

    private void SetVisible(bool visible)
    {
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

        Collider volumeCollider = volume.GetComponent<Collider>();
        if (volumeCollider == null || !volumeCollider.enabled)
        {
            return 0f;
        }

        float signedDistance = EvaluateSignedDistance(volumeCollider, position);
        if (signedDistance <= 0f)
        {
            return Mathf.Clamp01(volume.weight);
        }

        if (volume.blendDistance <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(volume.weight) * Mathf.Clamp01(1f - signedDistance / volume.blendDistance);
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
