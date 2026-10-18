using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyTaunt : MonoBehaviour
{
    public SquadCharacterController Target { get; private set; }
    private float until;
    private CombatDamageWorldFeedback label;
    public ulong Generation { get; private set; }
    public float Remaining => IsActive ? Mathf.Max(0, until - Time.time) : 0;
    public bool IsActive => Time.time < until && PlayerProtectionEffects.Alive(Target);
    public void Set(SquadCharacterController target, float seconds, ulong generation = 0)
    {
        Generation = generation;
        Target = target; until = Time.time + seconds;
        if (label == null) label = CombatDamageWorldFeedback.ShowPersistentMessage(transform, "Sommation", new Color(.6f, .85f, 1f), 3.2f);
    }
    public void ClearIfOwner(SquadCharacterController owner, ulong generation)
    {
        if (Target == owner && Generation == generation) Clear();
    }
    private void Update()
    {
        var enemy = GetComponent<EnemyController>();
        var lab = GetComponent<LitBrainsEnemy>();
        if (!IsActive || enemy != null && enemy.Health != null && enemy.Health.IsDead || lab != null && lab.IsDead) { Clear(); return; }
        if (label != null) label.SetPersistentMessage("Sommation : " + (until - Time.time).ToString("0.0") + " s");
    }
    private void Clear()
    {
        Target = null; until = 0; Generation = 0;
        if (label != null) { label.gameObject.SetActive(false); Destroy(label.gameObject); }
        label = null;
    }
    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
