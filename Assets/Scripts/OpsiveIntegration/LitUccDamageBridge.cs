using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Traits;
using Opsive.UltimateCharacterController.Traits.Damage;
using UnityEngine;

// Bridges Lit health consumers with UCC CharacterHealth, which is the authoritative runtime health store.
[DisallowMultipleComponent]
public class LitUccDamageBridge : MonoBehaviour
{
    [SerializeField] private UltimateCharacterLocomotion locomotion;
    [SerializeField] private SquadCharacterController squadController;
    [SerializeField] private CombatHealth combatHealth;
    [SerializeField] private CharacterAttributeManager attributeManager;
    [SerializeField] private CharacterHealth characterHealth;
    [SerializeField] private string healthAttributeName = "Health";
    [SerializeField, Tooltip("Use UCC CharacterHealth/AttributeManager as the authoritative runtime health source.")]
    private bool characterHealthIsAuthority = true;
    [SerializeField, Tooltip("Keep SquadCharacterController current/max HP synchronized from CharacterHealth.")]
    private bool syncSquadHealthFromCharacterHealth = true;
    [SerializeField, Tooltip("Legacy fallback: mirror Lit health into UCC attributes only when CharacterHealth is not authoritative.")]
    private bool mirrorLitHealthToOpsiveAttributes;
    [SerializeField, Tooltip("Legacy fallback: raise UCC OnHealthDamage so DamageVisualization and similar abilities can react.")]
    private bool raiseOpsiveDamageEvent = true;
    [SerializeField, Tooltip("Legacy fallback: raise UCC OnDeath when Lit health reaches zero.")]
    private bool raiseOpsiveDeathEvent = true;
    [SerializeField, Tooltip("Start ImpactKnockBack manually after non-lethal damage when the ability exists.")]
    private bool startImpactKnockBackOnDamage;
    [SerializeField] private int impactKnockBackResponseId;
    [SerializeField, Min(0f)] private float defaultForceMagnitude = 3f;

    private float lastCombatHealthValue;
    private bool combatHealthInitialized;
    private float lastCharacterHealthValue;
    private bool characterHealthInitialized;
    private bool subscribedCharacterHealthEvents;
    private bool characterHealthRuntimeReady;
    private SquadCharacterController subscribedSquadController;
    private string pendingLitDamageSource;

    private void Awake()
    {
        ResolveReferences();
        ConfigureCharacterHealthAuthority();
        CacheCombatHealth();
        CacheCharacterHealth();
        SyncSquadHealthFromCharacterHealth();
        MirrorLitHealthToOpsiveAttributes();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureCharacterHealthAuthority();
        SubscribeCharacterHealthEvents();
        SubscribeSquadHealth();
        CacheCharacterHealth();
        SyncSquadHealthFromCharacterHealth();
        MirrorLitHealthToOpsiveAttributes();

        if (combatHealth != null)
        {
            CacheCombatHealth();
            combatHealth.HealthChanged += OnCombatHealthChanged;
        }
    }

    private void Start()
    {
        characterHealthRuntimeReady = true;
        ResolveReferences();
        ConfigureCharacterHealthAuthority();
        CacheCharacterHealth();
        SyncSquadHealthFromCharacterHealth();
    }

    private void OnDisable()
    {
        UnsubscribeCharacterHealthEvents();
        UnsubscribeSquadHealth();

        if (combatHealth != null)
        {
            combatHealth.HealthChanged -= OnCombatHealthChanged;
        }
    }

    public bool TryApplyDamageToAuthority(int amount, string source, out int applied)
    {
        applied = 0;
        int sanitizedAmount = Mathf.Max(0, amount);
        if (sanitizedAmount <= 0)
        {
            return CanUseCharacterHealthAuthority();
        }

        if (!CanUseCharacterHealthAuthority())
        {
            return false;
        }

        Opsive.UltimateCharacterController.Traits.Attribute healthAttribute = ResolveHealthAttribute();
        if (healthAttribute == null)
        {
            return false;
        }

        float previousHealth = healthAttribute.Value;
        pendingLitDamageSource = source;
        try
        {
            Vector3 force = ResolveDamageForce(null);
            float forceMagnitude = force.magnitude;
            Vector3 direction = forceMagnitude > 0.0001f ? force / forceMagnitude : Vector3.zero;
            characterHealth.Damage(
                sanitizedAmount,
                ResolveDamagePosition(null),
                direction,
                forceMagnitude,
                null);
        }
        finally
        {
            pendingLitDamageSource = null;
        }

        applied = Mathf.CeilToInt(Mathf.Max(0f, previousHealth - healthAttribute.Value));
        SyncSquadHealthFromCharacterHealth();
        return true;
    }

    public bool TrySetAuthorityHealth(int current, int max)
    {
        if (!CanUseCharacterHealthAuthority())
        {
            return false;
        }

        Opsive.UltimateCharacterController.Traits.Attribute healthAttribute = ResolveHealthAttribute();
        if (healthAttribute == null)
        {
            return false;
        }

        int resolvedMax = Mathf.Max(1, max);
        int resolvedCurrent = Mathf.Clamp(current, 0, resolvedMax);
        healthAttribute.MaxValue = resolvedMax;

        if (resolvedCurrent <= 0 && healthAttribute.Value > healthAttribute.MinValue && characterHealth.IsAlive())
        {
            Vector3 force = ResolveDamageForce(null);
            float forceMagnitude = force.magnitude;
            Vector3 direction = forceMagnitude > 0.0001f ? force / forceMagnitude : Vector3.zero;
            characterHealth.ImmediateDeath(ResolveDamagePosition(null), direction, forceMagnitude);
        }
        else
        {
            healthAttribute.Value = resolvedCurrent;
        }

        SyncSquadHealthFromCharacterHealth();
        return true;
    }

    public bool TrySetAuthorityCurrentHealth(int current)
    {
        if (!CanUseCharacterHealthAuthority())
        {
            return false;
        }

        Opsive.UltimateCharacterController.Traits.Attribute healthAttribute = ResolveHealthAttribute();
        int resolvedMax = healthAttribute != null
            ? Mathf.Max(1, Mathf.CeilToInt(healthAttribute.MaxValue))
            : Mathf.Max(1, squadController != null ? squadController.MaxHp : 1);
        return TrySetAuthorityHealth(current, resolvedMax);
    }

    public bool TrySetAuthorityMaxHealth(int max, bool keepCurrent)
    {
        if (!CanUseCharacterHealthAuthority())
        {
            return false;
        }

        Opsive.UltimateCharacterController.Traits.Attribute healthAttribute = ResolveHealthAttribute();
        int resolvedMax = Mathf.Max(1, max);
        int resolvedCurrent = keepCurrent && healthAttribute != null
            ? Mathf.CeilToInt(healthAttribute.Value)
            : resolvedMax;
        return TrySetAuthorityHealth(resolvedCurrent, resolvedMax);
    }

    public void NotifyDamageApplied(int amount, bool killed, string source = null)
    {
        if (CanUseCharacterHealthAuthority())
        {
            return;
        }

        Vector3 position = ResolveDamagePosition(null);
        Vector3 force = ResolveDamageForce(null);
        NotifyDamageApplied(amount, killed, position, force, null, null);
    }

    public void NotifyDamageApplied(
        int amount,
        bool killed,
        Vector3 position,
        Vector3 force,
        GameObject attacker,
        Collider hitCollider)
    {
        if (!isActiveAndEnabled || amount <= 0 || CanUseCharacterHealthAuthority())
        {
            return;
        }

        ResolveReferences();
        if (locomotion == null)
        {
            return;
        }

        if (raiseOpsiveDamageEvent)
        {
            EventHandler.ExecuteEvent<float, Vector3, Vector3, GameObject, Collider>(
                gameObject,
                "OnHealthDamage",
                amount,
                position,
                force,
                attacker,
                hitCollider);
        }

        if (killed)
        {
            if (raiseOpsiveDeathEvent)
            {
                EventHandler.ExecuteEvent<Vector3, Vector3, GameObject>(
                    gameObject,
                    "OnDeath",
                    position,
                    force,
                    attacker);
            }

            return;
        }

        if (startImpactKnockBackOnDamage)
        {
            ImpactKnockBack knockBack = locomotion.GetAbility<ImpactKnockBack>();
            if (knockBack != null && !knockBack.IsActive)
            {
                knockBack.StartKnockBackResponse(impactKnockBackResponseId);
            }
        }
    }

    private void ResolveReferences()
    {
        if (locomotion == null)
        {
            locomotion = GetComponent<UltimateCharacterLocomotion>();
        }

        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (squadController == null)
        {
            squadController = GetComponent<SquadCharacterController>();
        }

        if (attributeManager == null)
        {
            attributeManager = GetComponent<CharacterAttributeManager>();
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }
    }

    private void ConfigureCharacterHealthAuthority()
    {
        if (!characterHealthIsAuthority || characterHealth == null)
        {
            return;
        }

        if (!Application.isPlaying || characterHealthRuntimeReady || characterHealth.HealthAttribute != null)
        {
            characterHealth.HealthAttributeName = healthAttributeName;
            characterHealth.ShieldAttributeName = string.Empty;
        }

        characterHealth.Invincible = false;
        characterHealth.TimeInvincibleAfterSpawn = 0f;
        characterHealth.DeactivateOnDeath = false;
    }

    private bool CanUseCharacterHealthAuthority()
    {
        if (!characterHealthIsAuthority)
        {
            return false;
        }

        ResolveReferences();
        return characterHealth != null && ResolveHealthAttribute() != null;
    }

    private Opsive.UltimateCharacterController.Traits.Attribute ResolveHealthAttribute()
    {
        if (attributeManager == null)
        {
            return null;
        }

        return attributeManager.GetAttribute(healthAttributeName);
    }

    private void CacheCombatHealth()
    {
        if (combatHealth == null)
        {
            combatHealthInitialized = false;
            lastCombatHealthValue = 0f;
            return;
        }

        lastCombatHealthValue = combatHealth.CurrentHp;
        combatHealthInitialized = true;
    }

    private void CacheCharacterHealth()
    {
        Opsive.UltimateCharacterController.Traits.Attribute healthAttribute = ResolveHealthAttribute();
        if (healthAttribute == null)
        {
            characterHealthInitialized = false;
            lastCharacterHealthValue = 0f;
            return;
        }

        lastCharacterHealthValue = healthAttribute.Value;
        characterHealthInitialized = true;
    }

    private void OnCombatHealthChanged(CombatHealth health)
    {
        if (health == null || CanUseCharacterHealthAuthority())
        {
            return;
        }

        if (!combatHealthInitialized)
        {
            CacheCombatHealth();
            return;
        }

        float previous = lastCombatHealthValue;
        lastCombatHealthValue = health.CurrentHp;
        int appliedDamage = Mathf.CeilToInt(Mathf.Max(0f, previous - health.CurrentHp));
        if (appliedDamage <= 0)
        {
            return;
        }

        Vector3 position = ResolveDamagePosition(null);
        Vector3 force = ResolveDamageForce(null);
        NotifyDamageApplied(appliedDamage, health.IsDead, position, force, null, null);
    }

    private void SubscribeCharacterHealthEvents()
    {
        if (!characterHealthIsAuthority || subscribedCharacterHealthEvents)
        {
            return;
        }

        EventHandler.RegisterEvent<DamageData>(gameObject, "OnHealthDamageWithData", OnCharacterHealthDamage);
        EventHandler.RegisterEvent<float>(gameObject, "OnHealthHeal", OnCharacterHealthHeal);
        EventHandler.RegisterEvent<Vector3, Vector3, GameObject>(gameObject, "OnDeath", OnCharacterHealthDeath);
        subscribedCharacterHealthEvents = true;
    }

    private void UnsubscribeCharacterHealthEvents()
    {
        if (!subscribedCharacterHealthEvents)
        {
            return;
        }

        EventHandler.UnregisterEvent<DamageData>(gameObject, "OnHealthDamageWithData", OnCharacterHealthDamage);
        EventHandler.UnregisterEvent<float>(gameObject, "OnHealthHeal", OnCharacterHealthHeal);
        EventHandler.UnregisterEvent<Vector3, Vector3, GameObject>(gameObject, "OnDeath", OnCharacterHealthDeath);
        subscribedCharacterHealthEvents = false;
    }

    private void OnCharacterHealthDamage(DamageData damageData)
    {
        if (!characterHealthIsAuthority || squadController == null)
        {
            return;
        }

        Opsive.UltimateCharacterController.Traits.Attribute healthAttribute = ResolveHealthAttribute();
        if (healthAttribute == null)
        {
            return;
        }

        if (!characterHealthInitialized)
        {
            CacheCharacterHealth();
        }

        float previousHealth = lastCharacterHealthValue;
        int previousHp = ToLitHealthValue(previousHealth, healthAttribute.MaxValue);
        int requestedAmount = damageData != null ? Mathf.CeilToInt(Mathf.Max(0f, damageData.Amount)) : 0;
        SyncSquadHealthFromCharacterHealth();

        int applied = Mathf.CeilToInt(Mathf.Max(0f, previousHealth - healthAttribute.Value));
        if (applied <= 0 && requestedAmount > 0)
        {
            applied = Mathf.Max(0, previousHp - squadController.CurrentHp);
        }

        if (applied <= 0)
        {
            return;
        }

        squadController.RecordDamageApplied(
            requestedAmount > 0 ? requestedAmount : applied,
            applied,
            previousHp,
            squadController.CurrentHp,
            ResolveDamageSourceLabel(damageData));
    }

    private void OnCharacterHealthHeal(float amount)
    {
        if (!characterHealthIsAuthority)
        {
            return;
        }

        SyncSquadHealthFromCharacterHealth();
    }

    private void OnCharacterHealthDeath(Vector3 position, Vector3 force, GameObject attacker)
    {
        if (!characterHealthIsAuthority)
        {
            return;
        }

        SyncSquadHealthFromCharacterHealth();
    }

    private void SubscribeSquadHealth()
    {
        if (CanUseCharacterHealthAuthority() || subscribedSquadController == squadController)
        {
            return;
        }

        UnsubscribeSquadHealth();

        if (squadController != null)
        {
            subscribedSquadController = squadController;
            subscribedSquadController.HealthChanged += OnSquadHealthChanged;
        }
    }

    private void UnsubscribeSquadHealth()
    {
        if (subscribedSquadController != null)
        {
            subscribedSquadController.HealthChanged -= OnSquadHealthChanged;
            subscribedSquadController = null;
        }
    }

    private void OnSquadHealthChanged(SquadCharacterController controller)
    {
        MirrorLitHealthToOpsiveAttributes();
    }

    private void SyncSquadHealthFromCharacterHealth()
    {
        if (!characterHealthIsAuthority || !syncSquadHealthFromCharacterHealth || squadController == null)
        {
            return;
        }

        Opsive.UltimateCharacterController.Traits.Attribute healthAttribute = ResolveHealthAttribute();
        if (healthAttribute == null)
        {
            return;
        }

        int max = Mathf.Max(1, Mathf.CeilToInt(healthAttribute.MaxValue));
        int current = ToLitHealthValue(healthAttribute.Value, max);
        squadController.SetHealthFromAuthority(current, max);

        lastCharacterHealthValue = healthAttribute.Value;
        characterHealthInitialized = true;
    }

    private void MirrorLitHealthToOpsiveAttributes()
    {
        if (CanUseCharacterHealthAuthority() || !mirrorLitHealthToOpsiveAttributes || squadController == null || attributeManager == null)
        {
            return;
        }

        Opsive.UltimateCharacterController.Traits.Attribute healthAttribute =
            attributeManager.GetAttribute(healthAttributeName);
        if (healthAttribute == null)
        {
            return;
        }

        healthAttribute.MaxValue = Mathf.Max(1, squadController.MaxHp);
        healthAttribute.Value = Mathf.Clamp(squadController.CurrentHp, 0, squadController.MaxHp);

        if (characterHealth != null)
        {
            characterHealth.HealthAttributeName = healthAttributeName;
            characterHealth.HealthValue = healthAttribute.Value;
        }
    }

    private string ResolveDamageSourceLabel(DamageData damageData)
    {
        if (!string.IsNullOrEmpty(pendingLitDamageSource))
        {
            return pendingLitDamageSource;
        }

        GameObject source = damageData?.DamageSource?.SourceOwner;
        if (source == null)
        {
            source = damageData?.DamageSource?.SourceGameObject;
        }

        return source != null ? source.name : "ucc";
    }

    private static int ToLitHealthValue(float value, float max)
    {
        int resolvedMax = Mathf.Max(1, Mathf.CeilToInt(max));
        if (value <= 0f)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.CeilToInt(value), 1, resolvedMax);
    }

    private Vector3 ResolveDamagePosition(GameObject attacker)
    {
        if (attacker != null)
        {
            return attacker.transform.position;
        }

        return transform.position + Vector3.up;
    }

    private Vector3 ResolveDamageForce(GameObject attacker)
    {
        Vector3 direction = transform.forward;
        if (attacker != null)
        {
            direction = transform.position - attacker.transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
            }
        }

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        return direction.normalized * defaultForceMagnitude;
    }
}
