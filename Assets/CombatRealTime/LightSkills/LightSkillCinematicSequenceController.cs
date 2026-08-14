using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Timeline;

[DisallowMultipleComponent]
public sealed class LightSkillCinematicSequenceController : MonoBehaviour, ICombatCinematicParticipant
{
    [SerializeField] private SignalReceiver signalReceiver;

    private RealTimeCombatManager combatManager;
    private LightSkillSO lightSkill;
    private RealTimeCombatEnemy targetEnemy;
    private RealTimeCombatEnemyBehaviour enemyBehaviour;
    private System.Action resolveImpact;
    private Coroutine projectileRoutine;
    private GameObject projectileInstance;
    private bool active;
    private bool projectileSpawned;
    private bool impactVfxSpawned;
    private bool damageResolved;

    private void Reset() => signalReceiver = GetComponent<SignalReceiver>();

    private void Awake()
    {
        if (signalReceiver == null) signalReceiver = GetComponent<SignalReceiver>();
    }

    private void OnDisable() => End();

    public bool Begin(CombatCinematicContext context)
    {
        if (active || context == null || context.CombatManager == null ||
            context.Definition is not LightSkillSO skill || context.TargetEnemy == null)
        {
            return false;
        }

        combatManager = context.CombatManager;
        lightSkill = skill;
        targetEnemy = context.TargetEnemy;
        resolveImpact = context.ResolveImpact;
        enemyBehaviour = targetEnemy.GetComponent<RealTimeCombatEnemyBehaviour>();
        active = true;
        projectileSpawned = false;
        impactVfxSpawned = false;
        damageResolved = false;

        BindSignals();
        enemyBehaviour?.SetCinematicSuspended(true);
        combatManager.SetCinematicSequenceActive(true);
        return true;
    }

    public void End()
    {
        if (!active) return;

        UnbindSignals();
        StopAndDestroyProjectile();
        enemyBehaviour?.SetCinematicSuspended(false);
        combatManager?.SetCinematicSequenceActive(false);

        active = false;
        combatManager = null;
        lightSkill = null;
        targetEnemy = null;
        enemyBehaviour = null;
        resolveImpact = null;
    }

    public void SpawnProjectile()
    {
        if (!active || projectileSpawned || lightSkill == null || lightSkill.ProjectileVfxPrefab == null) return;

        Transform caster = combatManager != null && combatManager.PlayerAnimator != null
            ? combatManager.PlayerAnimator.transform
            : combatManager != null ? combatManager.PlayerRoot : null;
        if (caster == null) return;

        Transform spawn = FindTransform(caster, lightSkill.ProjectileSpawnTransformPath) ?? caster;
        projectileInstance = Instantiate(lightSkill.ProjectileVfxPrefab, spawn);
        projectileInstance.transform.localPosition = lightSkill.ProjectileSpawnLocalOffset;
        projectileInstance.transform.localRotation = Quaternion.identity;
        projectileSpawned = true;
    }

    public void LaunchProjectile()
    {
        if (!active || projectileInstance == null || projectileRoutine != null || targetEnemy == null) return;

        projectileInstance.transform.SetParent(null, true);
        projectileRoutine = StartCoroutine(MoveProjectileToTarget());
    }

    public void SpawnImpactVfx()
    {
        if (!active || impactVfxSpawned || lightSkill == null || lightSkill.ImpactVfxPrefab == null || targetEnemy == null) return;

        Transform target = targetEnemy.LockPoint != null ? targetEnemy.LockPoint : targetEnemy.transform;
        GameObject impact = Instantiate(lightSkill.ImpactVfxPrefab, target.position + lightSkill.ImpactVfxOffset, target.rotation);
        Destroy(impact, 10f);
        impactVfxSpawned = true;
    }

    public void ResolveImpact()
    {
        if (!active || damageResolved) return;

        damageResolved = true;
        resolveImpact?.Invoke();
    }

    private void BindSignals()
    {
        BindSignal(lightSkill.SpawnProjectileSignal, SpawnProjectile);
        BindSignal(lightSkill.LaunchProjectileSignal, LaunchProjectile);
        BindSignal(lightSkill.SpawnImpactVfxSignal, SpawnImpactVfx);
        BindSignal(lightSkill.ResolveDamageSignal, ResolveImpact);
    }

    private void UnbindSignals()
    {
        BindSignal(lightSkill != null ? lightSkill.SpawnProjectileSignal : null, SpawnProjectile, false);
        BindSignal(lightSkill != null ? lightSkill.LaunchProjectileSignal : null, LaunchProjectile, false);
        BindSignal(lightSkill != null ? lightSkill.SpawnImpactVfxSignal : null, SpawnImpactVfx, false);
        BindSignal(lightSkill != null ? lightSkill.ResolveDamageSignal : null, ResolveImpact, false);
    }

    private void BindSignal(SignalAsset signal, UnityAction action, bool add = true)
    {
        if (signal == null || action == null || signalReceiver == null) return;
        UnityEvent reaction = signalReceiver.GetReaction(signal);
        if (reaction == null)
        {
            if (!add) return;
            reaction = new UnityEvent();
            signalReceiver.AddReaction(signal, reaction);
        }

        reaction.RemoveListener(action);
        if (add) reaction.AddListener(action);
    }

    private IEnumerator MoveProjectileToTarget()
    {
        float speed = Mathf.Max(0.01f, lightSkill != null ? lightSkill.ProjectileSpeed : 1f);
        while (active && projectileInstance != null && targetEnemy != null)
        {
            Transform target = targetEnemy.LockPoint != null ? targetEnemy.LockPoint : targetEnemy.transform;
            Vector3 destination = target.position + (lightSkill != null ? lightSkill.ImpactVfxOffset : Vector3.zero);
            Vector3 delta = destination - projectileInstance.transform.position;
            if (delta.sqrMagnitude <= 0.0025f)
            {
                projectileInstance.transform.position = destination;
                break;
            }

            projectileInstance.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            projectileInstance.transform.position = Vector3.MoveTowards(
                projectileInstance.transform.position, destination, speed * Time.unscaledDeltaTime);
            yield return null;
        }

        projectileRoutine = null;
    }

    private void StopAndDestroyProjectile()
    {
        if (projectileRoutine != null) StopCoroutine(projectileRoutine);
        projectileRoutine = null;
        if (projectileInstance != null) Destroy(projectileInstance);
        projectileInstance = null;
    }

    private static Transform FindTransform(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path)) return null;
        Transform direct = root.Find(path.Trim());
        if (direct != null) return direct;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, path.Trim(), System.StringComparison.Ordinal)) return child;
        }

        return null;
    }
}
