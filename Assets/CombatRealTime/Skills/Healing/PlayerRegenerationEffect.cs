using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerRegenerationEffect : MonoBehaviour
{
    public SquadCharacterController Recipient { get; private set; }
    public ICombatTarget EnemyRecipient { get; private set; }
    private SquadCharacterController caster;
    private SkillSO skill;
    private float nextTick;
    private CombatDamageWorldFeedback indicator;
    public event Action<SquadCharacterController, int> TickApplied;
    public event Action<ICombatTarget, int, int> LifeStealTickApplied;
    public event Action Ended;

    public static bool IsValidAlly(SquadCharacterController owner, SquadCharacterController target) =>
        owner != null && owner != target && target != null && target.isActiveAndEnabled && target.CurrentHp > 0 &&
        target.CharacterData != null && !target.CharacterData.isEnemy;

    public static bool IsValidRegenerationRecipient(SquadCharacterController owner, SquadCharacterController target)
    {
        if (IsValidAlly(owner, target)) return true;
        var network = NetworkManager.Singleton;
        return owner != null && owner == target && owner.isActiveAndEnabled && owner.CurrentHp > 0 &&
            (network == null || !network.IsListening) && owner.CharacterData != null && !owner.CharacterData.isEnemy &&
            string.Equals(owner.CharacterData.characterName, "Lucian", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidEnemy(SquadCharacterController owner, ICombatTarget target) =>
        owner != null && target != null && (!(target is UnityEngine.Object obj) || obj != null) &&
        target.Root != null && target.Root != owner.transform && target.Root.gameObject.activeInHierarchy && !target.IsDead;

    public bool TryAssignLifeSteal(SquadCharacterController owner, ICombatTarget target, SkillSO definition)
    {
        if (!isActiveAndEnabled || owner == null || owner.gameObject != gameObject || definition == null ||
            definition.healingTarget != SkillHealingTarget.EnemyLifeSteal || !SkillHealing.CanCast(owner) ||
            !IsValidEnemy(owner, target) || Vector3.Distance(owner.transform.position, target.Root.position) > definition.healingRadius) return false;
        if (EnemyRecipient != null && EnemyRecipient.Root == target.Root && skill == definition) return false;
        Clear();
        owner.GetComponent<PlayerProtectionEffects>()?.ClearAura();
        caster = owner;
        EnemyRecipient = target;
        skill = definition;
        nextTick = Time.time + Mathf.Max(.1f, skill.regenerationTickSeconds);
        indicator = CreateLifeStealIndicator(target.Root, definition.regenerationHealthPerTick, definition.regenerationTickSeconds);
        return true;
    }

    public static CombatDamageWorldFeedback CreateLifeStealIndicator(Transform target, int amount, float interval) =>
        CombatDamageWorldFeedback.ShowPersistentMessage(target,
            $"Siphon vital ({amount} PV / {interval:0.#} s)", new Color(.9f, .35f, .4f), 2.6f);

    public bool TryAssign(SquadCharacterController owner, SquadCharacterController target, SkillSO definition)
    {
        if (!isActiveAndEnabled || owner == null || owner.gameObject != gameObject || definition == null || definition.healingTarget != SkillHealingTarget.AllyRegeneration ||
            !SkillHealing.CanCast(owner) || !IsValidRegenerationRecipient(owner, target) ||
            Vector3.Distance(owner.transform.position, target.transform.position) > definition.healingRadius) return false;
        if (Recipient == target && skill == definition) return false;
        Clear();
        owner.GetComponent<PlayerProtectionEffects>()?.ClearAura();
        caster = owner;
        Recipient = target;
        skill = definition;
        nextTick = Time.time + Mathf.Max(.1f, skill.regenerationTickSeconds);
        indicator = CreateIndicator(target, definition.regenerationHealthPerTick, definition.regenerationTickSeconds);
        return true;
    }

    public static CombatDamageWorldFeedback CreateIndicator(SquadCharacterController target, int amount, float interval)
    {
        int existing = 0;
        foreach (var label in target.GetComponentsInChildren<CombatDamageWorldFeedback>())
            if (label.IsPersistent) existing++;
        return CombatDamageWorldFeedback.ShowPersistentMessage(target.transform,
            $"Regeneration ({amount} PV / {interval:0.#} s)", new Color(.3f, 1f, .6f), 2.6f + existing * .35f);
    }

    private void Update()
    {
        if (Recipient == null && caster == null && EnemyRecipient == null) return;
        var network = NetworkManager.Singleton;
        bool online = network != null && network.IsListening;
        var identity = caster != null ? caster.GetComponentInParent<NetworkObject>() : null;
        bool drain = EnemyRecipient != null;
        if (caster == null || !caster.isActiveAndEnabled || caster.CurrentHp <= 0 ||
            (drain ? !IsValidEnemy(caster, EnemyRecipient) : !IsValidRegenerationRecipient(caster, Recipient)) ||
            online && (!network.IsServer || identity == null || !identity.IsSpawned)) { Clear(); return; }
        if (drain && (Vector3.Distance(caster.transform.position, EnemyRecipient.Root.position) > skill.healingRadius ||
            EnemyRecipient.Root.GetComponent<EnemyController>()?.IsCinematicSuspended == true ||
            EnemyRecipient.Root.GetComponent<LitBrainsEnemy>()?.IsCinematicSuspended == true ||
            caster.GetComponent<LitOpsiveLocomotionBridge>()?.IsCinematicMotionSessionActive == true))
        {
            // No backlog from time spent out of range or under cinematic ownership.
            nextTick = Time.time + Mathf.Max(.1f, skill.regenerationTickSeconds);
            return;
        }
        if (Time.time < nextTick) return;
        float interval = Mathf.Max(.1f, skill.regenerationTickSeconds);
        int ticks = Mathf.FloorToInt((Time.time - nextTick) / interval) + 1;
        nextTick += ticks * interval;
        if (drain)
        {
            ApplyLifeStealTick((int)Math.Min(int.MaxValue, (long)ticks * Mathf.Max(1, skill.regenerationHealthPerTick)));
            return;
        }
        int amount = (int)Math.Min(Recipient.MaxHp - Recipient.CurrentHp, (long)ticks * Mathf.Max(1, skill.regenerationHealthPerTick));
        if (amount <= 0) return;
        int before = Recipient.CurrentHp;
        Recipient.SetCurrentHp(before + amount);
        int applied = Recipient.CurrentHp - before;
        if (applied <= 0) return;
        CombatDamageWorldFeedback.ShowMessage(Recipient.transform, "+" + applied + " PV", new Color(.3f, 1f, .6f), 2.1f);
        TickApplied?.Invoke(Recipient, applied);
    }

    private void ApplyLifeStealTick(int requested)
    {
        var target = EnemyRecipient;
        var root = target.Root;
        var legacy = root.GetComponent<EnemyController>();
        int stolen = legacy != null ? legacy.ReceivePassiveDamage(requested, caster) :
            target is ICombatImpactReceiver receiver ? receiver.ReceiveImpact(new CombatImpact(requested, 0, CombatImpactOrigin.Passive)) : target.ReceiveDamage(requested);
        if (stolen <= 0) return;
        if (caster == null || !caster.isActiveAndEnabled || caster.CurrentHp <= 0) { Clear(); return; }
        int healing = Mathf.Min(stolen, Mathf.Max(0, caster.MaxHp - caster.CurrentHp));
        int before = caster.CurrentHp;
        if (healing > 0) caster.SetCurrentHp(before + healing);
        healing = Mathf.Max(0, caster.CurrentHp - before);
        if (legacy == null) CombatDamageWorldFeedback.Show(root, stolen, new Color(.9f, .35f, .4f), 2.1f);
        if (healing > 0) CombatDamageWorldFeedback.ShowMessage(caster.transform, "+" + healing + " PV", new Color(.3f, 1f, .6f), 2.1f);
        LifeStealTickApplied?.Invoke(target, stolen, healing);
        if (ReferenceEquals(EnemyRecipient, target) && !IsValidEnemy(caster, target)) Clear();
    }

    public void Clear()
    {
        bool wasActive = caster != null || Recipient != null || EnemyRecipient != null;
        if (indicator != null)
        {
            indicator.gameObject.SetActive(false);
            Destroy(indicator.gameObject);
        }
        indicator = null;
        caster = Recipient = null;
        EnemyRecipient = null;
        skill = null;
        if (wasActive) Ended?.Invoke();
    }

    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
