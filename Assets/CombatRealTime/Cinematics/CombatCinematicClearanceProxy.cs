using UnityEngine;

/// <summary>Capsule de securite cinematographique, independante des colliders de gameplay.</summary>
[DisallowMultipleComponent]
public sealed class CombatCinematicClearanceProxy : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float radius = 0.45f;
    [SerializeField, Min(0.02f)] private float height = 1.8f;
    [SerializeField] private Vector3 center = new Vector3(0f, 0.9f, 0f);
    [SerializeField, Tooltip("Multiplie la capsule par la scale monde du ActorRoot. Necessaire pour les boss redimensionnes.")]
    private bool scaleWithActorRoot = true;
    [SerializeField] private Collider sourceCollider;

    public Collider SourceCollider => sourceCollider;

    private void Reset()
    {
        sourceCollider = GetComponentInChildren<CharacterController>(true);
        if (sourceCollider == null) sourceCollider = GetComponentInChildren<CapsuleCollider>(true);
        CopyFromSourceCollider();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        height = Mathf.Max(radius * 2f, height);
    }

    public void ConfigureFrom(Collider collider)
    {
        sourceCollider = collider;
        if (sourceCollider == null) return;
        if (sourceCollider is CapsuleCollider capsule)
        {
            radius = capsule.radius;
            height = Mathf.Max(capsule.height, radius * 2f);
            center = transform.InverseTransformPoint(capsule.transform.TransformPoint(capsule.center));
        }
        else if (sourceCollider is CharacterController controller)
        {
            radius = controller.radius;
            height = Mathf.Max(controller.height, radius * 2f);
            center = transform.InverseTransformPoint(controller.transform.TransformPoint(controller.center));
        }
    }

    public void GetWorldCapsule(Vector3 rootPosition, Quaternion rootRotation, float margin, out Vector3 first, out Vector3 second, out float worldRadius)
    {
        Vector3 scale = scaleWithActorRoot ? transform.lossyScale : Vector3.one;
        float verticalScale = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z), verticalScale);
        float safeRadius = Mathf.Max(0.01f, radius * radialScale + Mathf.Max(0f, margin));
        float scaledHeight = height * verticalScale;
        float halfLine = Mathf.Max(0f, (scaledHeight * 0.5f) - safeRadius);
        Vector3 worldCenter = rootPosition + rootRotation * Vector3.Scale(center, scale);
        Vector3 up = rootRotation * Vector3.up;
        first = worldCenter - up * halfLine;
        second = worldCenter + up * halfLine;
        worldRadius = safeRadius;
    }

    private void CopyFromSourceCollider()
    {
        if (sourceCollider == null) return;
        ConfigureFrom(sourceCollider);
    }
}
