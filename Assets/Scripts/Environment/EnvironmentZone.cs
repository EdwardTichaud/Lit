using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class EnvironmentZone : MonoBehaviour
{
    // Zones are passive markers. They never edit Unity Volumes directly; each local EnvironmentManager
    // samples the registered zones against its own target position.
    private static readonly List<EnvironmentZone> registeredZones = new List<EnvironmentZone>();

    [SerializeField, Tooltip("HDRP Volume Profile used when the local target is inside this zone.")]
    private VolumeProfile profile;
    [SerializeField] private int priority;
    [SerializeField, Range(0f, 1f)] private float weight = 1f;
    [SerializeField, Min(0f), Tooltip("World-space fade band around the collider boundary. Higher values make zone entry/exit smoother.")]
    private float blendDistance = 5f;
    [SerializeField, Tooltip("Maps the soft-edge distance to a final zone weight.")]
    private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.7f, 1f, 0.18f);
    [SerializeField] private bool drawGizmo = true;

    private Collider zoneCollider;

    public static IReadOnlyList<EnvironmentZone> RegisteredZones => registeredZones;
    public VolumeProfile Profile => profile;
    public int Priority => priority;
    public float Weight => weight;
    public float BlendDistance => blendDistance;
    public Collider ZoneCollider => zoneCollider;

    private void Awake()
    {
        ResolveCollider();
    }

    private void OnEnable()
    {
        ResolveCollider();
        if (!registeredZones.Contains(this))
        {
            registeredZones.Add(this);
        }
    }

    private void OnDisable()
    {
        registeredZones.Remove(this);
    }

    private void Reset()
    {
        ResolveCollider();
        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        weight = Mathf.Clamp01(weight);
        blendDistance = Mathf.Max(0f, blendDistance);
        ResolveCollider();
        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    public bool Contains(Vector3 worldPosition)
    {
        return EvaluateSignedDistance(worldPosition, out float signedDistance) && signedDistance <= 0f;
    }

    public float EvaluateWeight(Vector3 worldPosition)
    {
        if (!EvaluateSignedDistance(worldPosition, out float signedDistance))
        {
            return 0f;
        }

        float influence;
        if (blendDistance <= 0f)
        {
            influence = signedDistance <= 0f ? 1f : 0f;
        }
        else
        {
            float normalized = Mathf.InverseLerp(blendDistance, -blendDistance, signedDistance);
            influence = blendCurve != null ? blendCurve.Evaluate(normalized) : normalized;
        }

        return weight * Mathf.Clamp01(influence);
    }

    private void ResolveCollider()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }
    }

    private bool EvaluateSignedDistance(Vector3 worldPosition, out float signedDistance)
    {
        signedDistance = float.PositiveInfinity;

        if (!isActiveAndEnabled || profile == null)
        {
            return false;
        }

        ResolveCollider();
        if (zoneCollider == null || !zoneCollider.enabled)
        {
            return false;
        }

        if (zoneCollider is SphereCollider sphereCollider)
        {
            signedDistance = EvaluateSphereSignedDistance(sphereCollider, worldPosition);
            return true;
        }

        if (zoneCollider is BoxCollider boxCollider)
        {
            signedDistance = EvaluateBoxSignedDistance(boxCollider, worldPosition);
            return true;
        }

        Vector3 closest = zoneCollider.ClosestPoint(worldPosition);
        float outsideDistance = Vector3.Distance(closest, worldPosition);
        signedDistance = outsideDistance <= 0.0001f ? -blendDistance : outsideDistance;
        return true;
    }

    private static float EvaluateSphereSignedDistance(SphereCollider sphereCollider, Vector3 worldPosition)
    {
        Vector3 center = sphereCollider.transform.TransformPoint(sphereCollider.center);
        Vector3 scale = sphereCollider.transform.lossyScale;
        float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        float radius = sphereCollider.radius * Mathf.Max(0.0001f, radiusScale);
        return Vector3.Distance(worldPosition, center) - radius;
    }

    private static float EvaluateBoxSignedDistance(BoxCollider boxCollider, Vector3 worldPosition)
    {
        Vector3 localPoint = boxCollider.transform.InverseTransformPoint(worldPosition) - boxCollider.center;
        Vector3 halfSize = boxCollider.size * 0.5f;
        Vector3 q = new Vector3(
            Mathf.Abs(localPoint.x) - halfSize.x,
            Mathf.Abs(localPoint.y) - halfSize.y,
            Mathf.Abs(localPoint.z) - halfSize.z);

        Vector3 outside = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f));
        float inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        float signedLocalDistance = outside.magnitude + inside;

        Vector3 scale = boxCollider.transform.lossyScale;
        float distanceScale = Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Max(Mathf.Abs(scale.y), Mathf.Max(Mathf.Abs(scale.z), 0.0001f)));
        return signedLocalDistance * distanceScale;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo)
        {
            return;
        }

        DrawZoneGizmo(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawZoneGizmo(true);
    }

    private void DrawZoneGizmo(bool selected)
    {
        ResolveCollider();
        if (zoneCollider == null)
        {
            return;
        }

        Color color = gizmoColor;
        if (selected)
        {
            color.a = Mathf.Clamp01(color.a * 1.8f + 0.08f);
        }

        Gizmos.color = color;
        Color wireColor = color;
        wireColor.a = Mathf.Clamp01(wireColor.a * 2.5f + 0.1f);
        Color blendWireColor = wireColor;
        blendWireColor.a = Mathf.Clamp01(blendWireColor.a * 0.6f);

        if (zoneCollider is SphereCollider sphereCollider)
        {
            DrawSphereGizmo(sphereCollider, color, wireColor, blendWireColor, blendDistance);
            return;
        }

        if (zoneCollider is BoxCollider boxCollider)
        {
            DrawBoxGizmo(boxCollider, color, wireColor, blendWireColor, blendDistance);
            return;
        }

        Bounds bounds = zoneCollider.bounds;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        if (blendDistance > 0f)
        {
            bounds.Expand(blendDistance * 2f);
            Gizmos.color = blendWireColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }

    private static void DrawSphereGizmo(
        SphereCollider sphereCollider,
        Color fillColor,
        Color wireColor,
        Color blendWireColor,
        float worldBlendDistance)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = sphereCollider.transform.localToWorldMatrix;

        Gizmos.color = fillColor;
        Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
        Gizmos.color = wireColor;
        Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);
        if (worldBlendDistance > 0f)
        {
            Vector3 scale = sphereCollider.transform.lossyScale;
            float radiusScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Max(Mathf.Abs(scale.y), Mathf.Max(Mathf.Abs(scale.z), 0.0001f)));
            Gizmos.color = blendWireColor;
            Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius + worldBlendDistance / radiusScale);
        }

        Gizmos.matrix = previousMatrix;
    }

    private static void DrawBoxGizmo(
        BoxCollider boxCollider,
        Color fillColor,
        Color wireColor,
        Color blendWireColor,
        float worldBlendDistance)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = boxCollider.transform.localToWorldMatrix;

        Gizmos.color = fillColor;
        Gizmos.DrawCube(boxCollider.center, boxCollider.size);
        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        if (worldBlendDistance > 0f)
        {
            Vector3 localExpansion = WorldDistanceToLocalExpansion(boxCollider.transform, worldBlendDistance);
            Gizmos.color = blendWireColor;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size + localExpansion * 2f);
        }

        Gizmos.matrix = previousMatrix;
    }

    private static Vector3 WorldDistanceToLocalExpansion(Transform transform, float worldDistance)
    {
        Vector3 scale = transform.lossyScale;
        return new Vector3(
            worldDistance / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            worldDistance / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
            worldDistance / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
    }
}
