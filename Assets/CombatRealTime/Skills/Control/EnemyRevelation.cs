using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyRevelation : MonoBehaviour
{
    public SquadCharacterController Owner { get; private set; }
    public ulong Generation { get; private set; }
    public float Remaining => Mathf.Max(0, until - Time.time);
    public bool IsActive => isActiveAndEnabled && Remaining > 0 && PlayerProtectionEffects.Alive(Owner) && IsLivingEnemy(transform);
    private float until, strength;
    private CombatDamageWorldFeedback label;
    public void Apply(SquadCharacterController owner, float seconds, float bonus, ulong generation = 0)
    {
        if (generation == 0) generation = ControlEffectGeneration.Next();
        if (generation <= Generation) return;
        Generation = generation;
        Owner = owner; until = Time.time + seconds; strength = Mathf.Clamp01(bonus);
        if (label == null) label = CombatDamageWorldFeedback.ShowPersistentMessage(transform, "Revelation", new Color(.55f, 1f, .8f), 3.3f);
    }
    public static bool IsLivingEnemy(Transform root)
    {
        if (root == null || !root.gameObject.activeInHierarchy) return false;
        var legacy = root.GetComponent<EnemyController>();
        if (legacy != null) return legacy.isActiveAndEnabled && (legacy.Health == null || !legacy.Health.IsDead);
        var lab = root.GetComponent<LitBrainsEnemy>();
        return lab != null && lab.isActiveAndEnabled && !lab.IsDead;
    }
    public static int ModifyDamage(Transform target, float damage)
    {
        var mark = target != null ? target.GetComponent<EnemyRevelation>() : null;
        return Mathf.Max(0, Mathf.RoundToInt(damage * (mark != null && mark.IsActive ? 1 + mark.strength : 1)));
    }
    private void Update()
    {
        if (!IsActive) { Clear(); return; }
        label?.SetPersistentMessage("Revelation : " + Remaining.ToString("0.0") + " s");
    }
    public void ClearIfOwner(SquadCharacterController owner) { if (Owner == owner) Clear(); }
    private void Clear()
    {
        Owner = null; until = 0;
        if (label != null) { label.gameObject.SetActive(false); Destroy(label.gameObject); }
        label = null;
    }
    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
