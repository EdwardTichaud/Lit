using UnityEngine;

public sealed class EnemyEntraveZone : MonoBehaviour
{
    public SquadCharacterController Owner { get; private set; }
    public ulong Generation { get; private set; }
    public float Remaining => Mathf.Max(0, until - Time.time);
    public float Radius { get; private set; }
    public float Strength { get; private set; }
    public bool IsActive => isActiveAndEnabled && Remaining > 0 && PlayerProtectionEffects.Alive(Owner);
    private float until;
    private LineRenderer ring;
    private Material material;
    public static EnemyEntraveZone Create(SquadCharacterController owner, Vector3 point, SkillSO skill, ulong generation = 0)
    {
        if (generation == 0) generation = ControlEffectGeneration.Next();
        foreach (var existing in Object.FindObjectsByType<EnemyEntraveZone>(FindObjectsInactive.Exclude))
            if (existing.Owner == owner && existing.Generation == generation) return existing;
        var root = new GameObject("Entrave_Zone"); root.transform.position = point;
        var zone = root.AddComponent<EnemyEntraveZone>();
        zone.Generation = generation;
        zone.Owner = owner; zone.until = Time.time + skill.control.durationSeconds;
        zone.Radius = skill.control.radius; zone.Strength = skill.control.strength;
        zone.ring = root.AddComponent<LineRenderer>(); zone.ring.useWorldSpace = false;
        zone.ring.loop = true; zone.ring.positionCount = 64; zone.ring.widthMultiplier = .08f;
        var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            zone.material = new Material(shader);
            zone.material.SetColor("_UnlitColor", new Color(.25f, 1f, .65f));
            if (zone.material.HasProperty("_BaseColor")) zone.material.SetColor("_BaseColor", new Color(.25f, 1f, .65f));
            if (zone.material.HasProperty("_Color")) zone.material.SetColor("_Color", new Color(.25f, 1f, .65f));
            zone.ring.sharedMaterial = zone.material;
        }
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2 / 64;
            zone.ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * zone.Radius, .06f, Mathf.Sin(angle) * zone.Radius));
        }
        return zone;
    }
    public static bool TryFindGround(Transform enemy, out Vector3 point)
    {
        point = default; if (enemy == null) return false;
        float nearest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(enemy.position + Vector3.up * .4f, Vector3.down, .9f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y < .45f || hit.collider.GetComponentInParent<SquadCharacterController>() != null ||
                hit.collider.GetComponentInParent<EnemyController>() != null || hit.collider.GetComponentInParent<LitBrainsEnemy>() != null || hit.distance >= nearest) continue;
            nearest = hit.distance; point = hit.point;
        }
        return nearest < float.PositiveInfinity;
    }
    public static float NavigationMultiplier(Vector3 position)
    {
        float slow = 0;
        foreach (var zone in Object.FindObjectsByType<EnemyEntraveZone>(FindObjectsInactive.Exclude))
            if (zone.IsActive && Mathf.Abs(position.y - zone.transform.position.y) <= 1.5f &&
                Vector3.ProjectOnPlane(position - zone.transform.position, Vector3.up).sqrMagnitude <= zone.Radius * zone.Radius)
                slow = Mathf.Max(slow, zone.Strength);
        return 1 - slow;
    }
    private void Update() { if (!IsActive) { gameObject.SetActive(false); Destroy(gameObject); } }
    public void ClearIfOwner(SquadCharacterController owner) { if (Owner == owner) { gameObject.SetActive(false); Destroy(gameObject); } }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
