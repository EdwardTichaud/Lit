using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Traits;
using UnityEngine;

// Forwards Lit-owned health into UCC presentation events and mirrored attributes.
[DisallowMultipleComponent]
public class LitUccDamageBridge : MonoBehaviour
{
    [SerializeField] private UltimateCharacterLocomotion locomotion;
    [SerializeField] private SquadCharacterController squadController;
    [SerializeField] private CombatHealth combatHealth;
    [SerializeField] private CharacterAttributeManager attributeManager;
    [SerializeField] private CharacterHealth characterHealth;
    [SerializeField] private string healthAttributeName = "Health";
    [SerializeField, Tooltip("Keep UCC CharacterHealth/AttributeManager mirrored from Lit health without making UCC authoritative.")]
    private bool mirrorLitHealthToOpsiveAttributes = true;
    [SerializeField, Tooltip("Raise UCC OnHealthDamage so DamageVisualization and similar abilities can react.")]
    private bool raiseOpsiveDamageEvent = true;
    [SerializeField, Tooltip("Raise UCC OnDeath when Lit health reaches zero.")]
    private bool raiseOpsiveDeathEvent = true;
    [SerializeField, Tooltip("Start ImpactKnockBack manually after non-lethal damage when the ability exists.")]
    private bool startImpactKnockBackOnDamage;
    [SerializeField] private int impactKnockBackResponseId;
    [SerializeField, Min(0f)] private float defaultForceMagnitude = 3f;

    private int lastCombatHealthValue;
    private bool combatHealthInitialized;
    private SquadCharacterController subscribedSquadController;

    private void Awake()
    {
        ResolveReferences();
        CacheCombatHealth();
        MirrorLitHealthToOpsiveAttributes();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeSquadHealth();
        MirrorLitHealthToOpsiveAttributes();

        if (combatHealth != null)
        {
            CacheCombatHealth();
            combatHealth.HealthChanged += OnCombatHealthChanged;
        }
    }

    private void OnDisable()
    {
        UnsubscribeSquadHealth();

        if (combatHealth != null)
        {
            combatHealth.HealthChanged -= OnCombatHealthChanged;
        }
    }

    public void NotifyDamageApplied(int amount, bool killed, string source = null)
    {
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
        if (!isActiveAndEnabled || amount <= 0)
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

    private void CacheCombatHealth()
    {
        if (combatHealth == null)
        {
            combatHealthInitialized = false;
            lastCombatHealthValue = 0;
            return;
        }

        lastCombatHealthValue = combatHealth.CurrentHp;
        combatHealthInitialized = true;
    }

    private void OnCombatHealthChanged(CombatHealth health)
    {
        if (health == null)
        {
            return;
        }

        if (!combatHealthInitialized)
        {
            CacheCombatHealth();
            return;
        }

        int previous = lastCombatHealthValue;
        lastCombatHealthValue = health.CurrentHp;
        int appliedDamage = Mathf.Max(0, previous - health.CurrentHp);
        if (appliedDamage <= 0)
        {
            return;
        }

        Vector3 position = ResolveDamagePosition(null);
        Vector3 force = ResolveDamageForce(null);
        NotifyDamageApplied(appliedDamage, health.IsDead, position, force, null, null);
    }

    private void SubscribeSquadHealth()
    {
        if (subscribedSquadController == squadController)
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

    private void MirrorLitHealthToOpsiveAttributes()
    {
        if (!mirrorLitHealthToOpsiveAttributes || squadController == null || attributeManager == null)
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
