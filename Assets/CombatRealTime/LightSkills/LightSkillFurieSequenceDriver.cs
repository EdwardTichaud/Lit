using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Timeline;

[DisallowMultipleComponent]
public sealed class LightSkillFurieSequenceDriver : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private LightSkillFurieCameraRig cameraRig;
    [SerializeField] private SignalReceiver signalReceiver;

    [Header("Temporary Player States")]
    [SerializeField] private string startState = "Base Layer.LightSkill_1_Furie_Start_Temp";
    [SerializeField] private string impulseState = "Base Layer.LightSkill_1_Furie_Impulse_Temp";
    [SerializeField] private string attackState = "Base Layer.LightSkill_1_Furie_Attack_Temp";

    [Header("Impulse")]
    [SerializeField, Min(0.1f)] private float attackDistance = 2.5f;
    [SerializeField, Min(0.1f)] private float maximumImpulseSeconds = 1f;
    [SerializeField, Min(0.1f)] private float preferredDashSpeed = 26f;
    [SerializeField, Min(1f)] private float maximumDashSpeed = 36f;
    [SerializeField, Range(0f, 0.25f)] private float animationTransitionSeconds = 0.05f;

    private RealTimeCombatManager combatManager;
    private LightSkillSO lightSkill;
    private RealTimeCombatEnemy targetEnemy;
    private LitOpsiveLocomotionBridge playerBridge;
    private RealTimeCombatEnemyBehaviour enemyBehaviour;
    private Coroutine impulseRoutine;
    private bool scriptedTraversalHeld;
    private bool active;
    private bool attackStarted;
    private bool impactPlayed;

    public CinemachineCamera VirtualCamera => virtualCamera;
    public SignalReceiver SignalReceiver => signalReceiver;
    public bool IsActive => active;

    private void Reset()
    {
        virtualCamera = GetComponentInChildren<CinemachineCamera>(true);
        cameraRig = virtualCamera != null ? virtualCamera.GetComponent<LightSkillFurieCameraRig>() : null;
        signalReceiver = GetComponent<SignalReceiver>();
    }

    private void OnDisable()
    {
        EndSequence();
    }

    public bool BeginSequence(RealTimeCombatManager manager, LightSkillSO skill)
    {
        if (active || manager == null || skill == null || manager.PlayerRoot == null || manager.LockedEnemy == null)
        {
            return false;
        }

        float distance = HorizontalDistance(manager.PlayerRoot.position, manager.LockedEnemy.LockPoint.position);
        if (distance > skill.MaximumCinematicStartDistance)
        {
            CombatDamageWorldFeedback.ShowMessage(
                manager.LockedEnemy.transform,
                "Rate (trop loin)",
                new Color(1f, 0.82f, 0.38f),
                2.25f);
            return false;
        }

        combatManager = manager;
        lightSkill = skill;
        targetEnemy = manager.LockedEnemy;
        playerBridge = manager.PlayerRoot.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        if (playerBridge == null || !playerBridge.BeginScriptedTraversal())
        {
            return false;
        }

        scriptedTraversalHeld = true;
        enemyBehaviour = targetEnemy.GetComponent<RealTimeCombatEnemyBehaviour>();
        active = true;
        attackStarted = false;
        impactPlayed = false;

        cameraRig?.Prepare(manager.PlayerRoot, targetEnemy.LockPoint);
        enemyBehaviour?.SetCinematicSuspended(true);
        combatManager.SetCinematicSequenceActive(true);
        return true;
    }

    public void BeginFurieStart()
    {
        if (!active)
        {
            return;
        }

        PlayPlayerState(startState);
        PlayAudio(lightSkill != null ? lightSkill.StartSfx : null, combatManager.PlayerRoot.position);
    }

    public void SetFurieRearShot()
    {
        if (active)
        {
            cameraRig?.SetRearShot();
        }
    }

    public void BeginFurieImpulse()
    {
        if (!active || attackStarted || combatManager == null || targetEnemy == null)
        {
            return;
        }

        PlayPlayerState(impulseState);
        cameraRig?.SetDashFollow();
        PlayAudio(lightSkill != null ? lightSkill.ImpulseSfx : null, combatManager.PlayerRoot.position);

        Vector3 direction = targetEnemy.LockPoint.position - combatManager.PlayerRoot.position;
        direction.y = 0f;
        float distance = direction.magnitude;
        if (distance <= attackDistance)
        {
            BeginAttack();
            return;
        }

        direction /= distance;
        playerBridge?.SetActionFacingDirection(direction);
        float travelDistance = Mathf.Max(0f, distance - attackDistance);
        float duration = Mathf.Clamp(travelDistance / preferredDashSpeed, 0.15f, maximumImpulseSeconds);
        impulseRoutine = StartCoroutine(TraverseToAttackRange(direction, travelDistance, duration));
    }

    public void NotifyImpactResolved()
    {
        if (!active || impactPlayed)
        {
            return;
        }

        impactPlayed = true;
        PlayAudio(lightSkill != null ? lightSkill.ImpactSfx : null, targetEnemy != null ? targetEnemy.LockPoint.position : transform.position);
    }

    public void EndSequence()
    {
        if (!active)
        {
            return;
        }

        if (impulseRoutine != null)
        {
            StopCoroutine(impulseRoutine);
            impulseRoutine = null;
        }

        StopImpulse();
        if (scriptedTraversalHeld)
        {
            playerBridge?.EndScriptedTraversal();
            scriptedTraversalHeld = false;
        }
        enemyBehaviour?.SetCinematicSuspended(false);
        combatManager?.SetCinematicSequenceActive(false);
        cameraRig?.Clear();

        active = false;
        combatManager = null;
        lightSkill = null;
        targetEnemy = null;
        playerBridge = null;
        enemyBehaviour = null;
    }

    private IEnumerator TraverseToAttackRange(Vector3 direction, float travelDistance, float duration)
    {
        if (playerBridge == null || combatManager == null || targetEnemy == null)
        {
            yield break;
        }

        Vector3 startPosition = playerBridge.WorldPosition;
        Vector3 targetPosition = startPosition + direction * travelDistance;
        float elapsed = 0f;
        while (active && !attackStarted && elapsed < duration)
        {
            if (IsWithinAttackRange())
            {
                BeginAttack();
                yield break;
            }

            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f);
            Vector3 position = Vector3.LerpUnclamped(startPosition, targetPosition, easedTime);
            playerBridge.ApplyScriptedTraversalPose(position, Quaternion.LookRotation(direction, Vector3.up));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (active && !attackStarted)
        {
            BeginAttack();
        }
    }

    private void BeginAttack()
    {
        if (attackStarted || !active)
        {
            return;
        }

        attackStarted = true;
        StopImpulse();
        FaceTarget();
        PlayPlayerState(attackState);
    }

    private bool IsWithinAttackRange()
    {
        return combatManager != null && targetEnemy != null &&
               HorizontalDistance(combatManager.PlayerRoot.position, targetEnemy.LockPoint.position) <= attackDistance;
    }

    private void StopImpulse()
    {
        playerBridge.StopBridgeInput();
    }

    private void FaceTarget()
    {
        if (combatManager == null || targetEnemy == null)
        {
            return;
        }

        Vector3 direction = targetEnemy.LockPoint.position - combatManager.PlayerRoot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            playerBridge?.SetActionFacingDirection(direction);
        }
    }

    private void PlayPlayerState(string stateName)
    {
        Animator animator = combatManager != null ? combatManager.PlayerAnimator : null;
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, stateHash))
        {
            animator.CrossFade(stateHash, animationTransitionSeconds, 0, 0f);
        }
    }

    private static void PlayAudio(AudioClipSO clip, Vector3 position)
    {
        if (clip != null)
        {
            AudioManager.PlayClipAtPoint(clip, position);
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
