using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyTaunt : MonoBehaviour
{
    public SquadCharacterController Target { get; private set; }
    private float until;
    private CombatDamageWorldFeedback label;
    public bool IsActive => Time.time < until && PlayerProtectionEffects.Alive(Target);
    public void Set(SquadCharacterController target, float seconds)
    {
        Target = target; until = Time.time + seconds;
        if (label == null) label = CombatDamageWorldFeedback.ShowPersistentMessage(transform, "Sommation", new Color(.6f, .85f, 1f), 3.2f);
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
        Target = null; until = 0;
        if (label != null) { label.gameObject.SetActive(false); Destroy(label.gameObject); }
        label = null;
    }
    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
