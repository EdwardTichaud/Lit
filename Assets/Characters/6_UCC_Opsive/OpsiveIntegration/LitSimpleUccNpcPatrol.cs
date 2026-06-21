using System;
using System.Collections;
using Opsive.Shared.Utility;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(UltimateCharacterLocomotion))]
public sealed class LitSimpleUccNpcPatrol : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
{
    [Serializable]
    public sealed class PatrolPoint
    {
        public Transform point;
        [Min(0f)] public float waitSeconds = 1f;
        public string waitAnimatorState;
        public string waitAnimatorTrigger;
        public UnityEvent onArrive;
    }

    [Header("Patrol")]
    [SerializeField] private PatrolPoint[] patrolPoints = Array.Empty<PatrolPoint>();
    [SerializeField] private bool startOnEnable = true;
    [SerializeField] private bool loop = true;
    [SerializeField, Range(0f, 1f)] private float moveInput = 1f;
    [SerializeField, Min(0.05f)] private float arrivalDistance = 0.35f;
    [SerializeField, Min(0f)] private float turnSpeed = 540f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float animatorCrossFadeDuration = 0.15f;
    [SerializeField] private string movingBoolParameter;
    [SerializeField] private string stoppedBoolParameter;

    [Header("Interaction")]
    [SerializeField] private bool canInteract = true;
    [SerializeField, Min(0.05f)] private float interactionMaxDistance = 2f;
    [SerializeField] private int interactionPriority = 90;
    [SerializeField] private bool faceInteractor = true;
    [SerializeField] private bool resumePatrolAfterInteraction = true;
    [SerializeField] private bool autoFinishInteraction = true;
    [SerializeField, Min(0f)] private float interactionDuration = 2f;
    [SerializeField] private string interactionAnimatorState;
    [SerializeField] private string interactionAnimatorTrigger;
    [SerializeField] private UnityEvent onInteractionStarted;
    [SerializeField] private UnityEvent onInteractionFinished;

    [Header("Interaction Collider")]
    [SerializeField] private SphereCollider interactionTrigger;
    [SerializeField, Min(0.05f)] private float interactionRadius = 1.75f;
    [SerializeField] private Vector3 interactionCenter = new Vector3(0f, 1f, 0f);

    private UltimateCharacterLocomotion locomotion;
    private LitSimpleUccNpcPatrolAbility patrolAbility;
    private Coroutine interactionRoutine;
    private GameObject detectedCharacter;
    private int currentPointIndex;
    private float waitUntil;
    private bool patrolRunning;
    private bool waiting;
    private bool interacting;
    private bool resumeAfterCurrentInteraction;
    private bool wasWaitingBeforeInteraction;
    private float savedWaitRemaining;

    public bool IsPatrolling => patrolRunning && !interacting;
    public bool IsInteracting => interacting;

    private void Reset()
    {
        ResolveReferences();
        EnsureInteractionTrigger();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureInteractionTrigger();
        EnsurePatrolAbility();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureInteractionTrigger();
        EnsurePatrolAbility();

        if (startOnEnable)
        {
            ResumePatrol();
        }
        else
        {
            PausePatrol();
        }
    }

    private void OnDisable()
    {
        if (interactionRoutine != null)
        {
            StopCoroutine(interactionRoutine);
            interactionRoutine = null;
        }

        interacting = false;
        detectedCharacter = null;
        PausePatrol();
    }

    private void OnValidate()
    {
        moveInput = Mathf.Clamp01(moveInput);
        arrivalDistance = Mathf.Max(0.05f, arrivalDistance);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        interactionMaxDistance = Mathf.Max(0.05f, interactionMaxDistance);
        interactionRadius = Mathf.Max(0.05f, interactionRadius);
        interactionDuration = Mathf.Max(0f, interactionDuration);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (interactionTrigger != null)
        {
            ConfigureInteractionTrigger();
        }
    }

    private void Update()
    {
        if (interacting)
        {
            UpdateInteractionFacing();
            return;
        }

        TickPatrol();
    }

    public void ResumePatrol()
    {
        if (!EnsurePatrolAbility())
        {
            return;
        }

        patrolRunning = true;
        waiting = false;
        StartPatrolAbility();
    }

    public void PausePatrol()
    {
        patrolRunning = false;
        waiting = false;
        ClearPatrolCommand();
        SetAnimatorMoving(false);

        if (locomotion != null && patrolAbility != null && patrolAbility.IsActive)
        {
            locomotion.TryStopAbility(patrolAbility);
        }
    }

    public void RestartPatrol()
    {
        currentPointIndex = 0;
        waitUntil = 0f;
        waiting = false;
        ResumePatrol();
    }

    public void SkipToNextPatrolPoint()
    {
        AdvancePatrolPoint();
        waiting = false;
    }

    public void FinishInteractionAndResume()
    {
        FinishInteraction(resumePatrolAfterInteraction);
    }

    public void FinishInteractionWithoutResume()
    {
        FinishInteraction(false);
    }

    public bool TryHandleLocalInteract()
    {
        if (!canInteract || interacting || detectedCharacter == null || InputFocusStack.HasAnyFocus())
        {
            return false;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked())
        {
            return false;
        }

        if (!IsDetectedCharacterInRange())
        {
            return false;
        }

        StartInteraction();
        return true;
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return canInteract && controller != null && isActiveAndEnabled;
    }

    public Collider GetInteractionDetectionCollider()
    {
        EnsureInteractionTrigger();
        return interactionTrigger;
    }

    public Transform GetInteractionAnchor()
    {
        return transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return Mathf.Max(0.05f, interactionMaxDistance);
    }

    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return interactionPriority;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        detectedCharacter = character;
    }

    private void TickPatrol()
    {
        if (!patrolRunning || patrolPoints == null || patrolPoints.Length == 0)
        {
            ClearPatrolCommand();
            SetAnimatorMoving(false);
            return;
        }

        if (!TryGetCurrentPoint(out PatrolPoint point))
        {
            PausePatrol();
            return;
        }

        if (waiting)
        {
            ClearPatrolCommand();
            SetAnimatorMoving(false);
            if (Time.time >= waitUntil)
            {
                waiting = false;
                AdvancePatrolPoint();
            }

            return;
        }

        Vector3 toTarget = Vector3.ProjectOnPlane(point.point.position - transform.position, transform.up);
        if (toTarget.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            BeginWait(point);
            return;
        }

        SetPatrolCommand(toTarget.normalized, true, toTarget.normalized);
        SetAnimatorMoving(true);
    }

    private void BeginWait(PatrolPoint point)
    {
        waiting = true;
        waitUntil = Time.time + Mathf.Max(0f, point.waitSeconds);
        ClearPatrolCommand();
        SetAnimatorMoving(false);
        PlayAnimatorCue(point.waitAnimatorState, point.waitAnimatorTrigger);
        point.onArrive?.Invoke();
    }

    private void AdvancePatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            currentPointIndex = 0;
            return;
        }

        currentPointIndex++;
        if (currentPointIndex < patrolPoints.Length)
        {
            return;
        }

        if (loop)
        {
            currentPointIndex = 0;
            return;
        }

        currentPointIndex = patrolPoints.Length - 1;
        PausePatrol();
    }

    private bool TryGetCurrentPoint(out PatrolPoint point)
    {
        point = null;
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return false;
        }

        currentPointIndex = Mathf.Clamp(currentPointIndex, 0, patrolPoints.Length - 1);
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            PatrolPoint candidate = patrolPoints[currentPointIndex];
            if (candidate != null && candidate.point != null)
            {
                point = candidate;
                return true;
            }

            AdvancePatrolPoint();
        }

        return false;
    }

    private void StartInteraction()
    {
        resumeAfterCurrentInteraction = patrolRunning && resumePatrolAfterInteraction;
        wasWaitingBeforeInteraction = waiting;
        savedWaitRemaining = waiting ? Mathf.Max(0f, waitUntil - Time.time) : 0f;
        patrolRunning = false;
        waiting = false;
        interacting = true;
        ClearPatrolCommand();
        SetAnimatorMoving(false);
        PlayAnimatorCue(interactionAnimatorState, interactionAnimatorTrigger);
        onInteractionStarted?.Invoke();

        if (interactionRoutine != null)
        {
            StopCoroutine(interactionRoutine);
        }

        if (autoFinishInteraction)
        {
            interactionRoutine = StartCoroutine(FinishInteractionAfterDelay());
        }
        else
        {
            interactionRoutine = null;
        }
    }

    private IEnumerator FinishInteractionAfterDelay()
    {
        if (interactionDuration > 0f)
        {
            yield return new WaitForSeconds(interactionDuration);
        }

        interactionRoutine = null;
        FinishInteraction(resumeAfterCurrentInteraction);
    }

    private void FinishInteraction(bool resumePatrol)
    {
        if (!interacting)
        {
            if (resumePatrol)
            {
                ResumePatrol();
            }

            return;
        }

        if (interactionRoutine != null)
        {
            StopCoroutine(interactionRoutine);
            interactionRoutine = null;
        }

        interacting = false;
        ClearPatrolCommand();
        onInteractionFinished?.Invoke();

        if (resumePatrol)
        {
            ResumePatrolAfterInteraction();
        }
        else
        {
            PausePatrol();
        }
    }

    private void ResumePatrolAfterInteraction()
    {
        if (!EnsurePatrolAbility())
        {
            return;
        }

        patrolRunning = true;
        waiting = wasWaitingBeforeInteraction;
        if (waiting)
        {
            waitUntil = Time.time + savedWaitRemaining;
        }

        wasWaitingBeforeInteraction = false;
        savedWaitRemaining = 0f;
        StartPatrolAbility();
    }

    private void UpdateInteractionFacing()
    {
        if (!faceInteractor || detectedCharacter == null)
        {
            ClearPatrolCommand();
            return;
        }

        Vector3 direction = Vector3.ProjectOnPlane(detectedCharacter.transform.position - transform.position, transform.up);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            ClearPatrolCommand();
            return;
        }

        StartPatrolAbility();
        SetPatrolCommand(Vector3.zero, true, direction.normalized);
    }

    private bool IsDetectedCharacterInRange()
    {
        if (detectedCharacter == null)
        {
            return false;
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            detectedCharacter.transform,
            GetInteractionDetectionCollider(),
            GetInteractionAnchor(),
            interactionMaxDistance);
    }

    private void SetPatrolCommand(Vector3 moveDirection, bool hasFacingDirection, Vector3 facingDirection)
    {
        if (!EnsurePatrolAbility())
        {
            return;
        }

        patrolAbility.SetCommand(moveDirection, moveInput, hasFacingDirection, facingDirection, turnSpeed);
    }

    private void ClearPatrolCommand()
    {
        if (patrolAbility != null)
        {
            patrolAbility.SetCommand(Vector3.zero, 0f, false, Vector3.zero, turnSpeed);
        }
    }

    private void StartPatrolAbility()
    {
        if (locomotion == null || patrolAbility == null || patrolAbility.IsActive)
        {
            return;
        }

        locomotion.TryStartAbility(patrolAbility, ignorePriority: true);
    }

    private bool EnsurePatrolAbility()
    {
        ResolveReferences();
        if (locomotion == null)
        {
            return false;
        }

        patrolAbility = locomotion.GetAbility<LitSimpleUccNpcPatrolAbility>();
        if (patrolAbility != null)
        {
            return true;
        }

        Ability[] abilities = locomotion.Abilities;
        int length = abilities != null ? abilities.Length : 0;
        Ability[] nextAbilities = new Ability[length + 1];
        if (length > 0)
        {
            Array.Copy(abilities, nextAbilities, length);
        }

        patrolAbility = new LitSimpleUccNpcPatrolAbility();
        nextAbilities[length] = patrolAbility;
        locomotion.Abilities = nextAbilities;
        return patrolAbility != null;
    }

    private void ResolveReferences()
    {
        if (locomotion == null)
        {
            locomotion = GetComponent<UltimateCharacterLocomotion>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void EnsureInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            interactionTrigger = gameObject.AddComponent<SphereCollider>();
        }

        ConfigureInteractionTrigger();
    }

    private void ConfigureInteractionTrigger()
    {
        interactionTrigger.isTrigger = true;
        interactionTrigger.radius = Mathf.Max(0.05f, interactionRadius);
        interactionTrigger.center = interactionCenter;
    }

    private void PlayAnimatorCue(string stateName, string triggerName)
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(triggerName))
        {
            animator.SetTrigger(triggerName);
        }

        if (!string.IsNullOrWhiteSpace(stateName))
        {
            animator.CrossFadeInFixedTime(stateName, animatorCrossFadeDuration);
        }
    }

    private void SetAnimatorMoving(bool moving)
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(movingBoolParameter))
        {
            animator.SetBool(movingBoolParameter, moving);
        }

        if (!string.IsNullOrWhiteSpace(stoppedBoolParameter))
        {
            animator.SetBool(stoppedBoolParameter, !moving);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return;
        }

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            PatrolPoint point = patrolPoints[i];
            if (point == null || point.point == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(point.point.position, arrivalDistance);
            int nextIndex = i + 1;
            if (nextIndex >= patrolPoints.Length)
            {
                if (!loop)
                {
                    continue;
                }

                nextIndex = 0;
            }

            PatrolPoint nextPoint = patrolPoints[nextIndex];
            if (nextPoint != null && nextPoint.point != null)
            {
                Gizmos.DrawLine(point.point.position, nextPoint.point.position);
            }
        }
    }
}

[Serializable]
public sealed class LitSimpleUccNpcPatrolAbility : Ability
{
    private Vector3 worldMoveDirection;
    private Vector3 worldFacingDirection;
    private bool hasFacingDirection;
    private float inputMagnitude;
    private float turnSpeed = 540f;

    public LitSimpleUccNpcPatrolAbility()
    {
        m_StartType = AbilityStartType.Manual;
        m_StopType = AbilityStopType.Manual;
        m_AbilityIndexParameter = -1;
    }

    public override bool IsConcurrent => true;

    public void SetCommand(Vector3 moveDirection, float moveInput, bool useFacingDirection, Vector3 facingDirection, float newTurnSpeed)
    {
        worldMoveDirection = moveDirection;
        if (worldMoveDirection.sqrMagnitude > 1f)
        {
            worldMoveDirection.Normalize();
        }

        hasFacingDirection = useFacingDirection;
        worldFacingDirection = facingDirection;
        if (worldFacingDirection.sqrMagnitude > 1f)
        {
            worldFacingDirection.Normalize();
        }

        inputMagnitude = Mathf.Clamp01(moveInput);
        turnSpeed = Mathf.Max(0f, newTurnSpeed);
    }

    public override void Update()
    {
        base.Update();

        if (m_CharacterLocomotion == null || m_Transform == null)
        {
            return;
        }

        Vector2 inputVector = ResolveLocalInputVector();
        m_CharacterLocomotion.InputVector = inputVector;
        m_CharacterLocomotion.RawInputVector = inputVector;
        m_CharacterLocomotion.DeltaRotation = ResolveDeltaRotation();
    }

    protected override void AbilityStopped(bool force)
    {
        worldMoveDirection = Vector3.zero;
        worldFacingDirection = Vector3.zero;
        hasFacingDirection = false;
        inputMagnitude = 0f;

        if (m_CharacterLocomotion != null)
        {
            m_CharacterLocomotion.InputVector = Vector2.zero;
            m_CharacterLocomotion.RawInputVector = Vector2.zero;
            m_CharacterLocomotion.DeltaRotation = Vector3.zero;
        }

        base.AbilityStopped(force);
    }

    private Vector2 ResolveLocalInputVector()
    {
        if (worldMoveDirection.sqrMagnitude <= 0.0001f || inputMagnitude <= 0f)
        {
            return Vector2.zero;
        }

        Vector3 up = ResolveUp();
        Vector3 planarDirection = Vector3.ProjectOnPlane(worldMoveDirection, up);
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        Vector3 localDirection = m_Transform.InverseTransformDirection(planarDirection.normalized);
        Vector2 input = new Vector2(localDirection.x, localDirection.z);
        return Vector2.ClampMagnitude(input, 1f) * inputMagnitude;
    }

    private Vector3 ResolveDeltaRotation()
    {
        Vector3 targetDirection = hasFacingDirection ? worldFacingDirection : worldMoveDirection;
        Vector3 up = ResolveUp();
        targetDirection = Vector3.ProjectOnPlane(targetDirection, up);
        if (targetDirection.sqrMagnitude <= 0.0001f || turnSpeed <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 currentForward = Vector3.ProjectOnPlane(m_Transform.forward, up);
        if (currentForward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        float signedYaw = Vector3.SignedAngle(currentForward.normalized, targetDirection.normalized, up);
        float maxYaw = turnSpeed * TimeUtility.DeltaTime;
        return new Vector3(0f, Mathf.Clamp(signedYaw, -maxYaw, maxYaw), 0f);
    }

    private Vector3 ResolveUp()
    {
        if (m_CharacterLocomotion != null && m_CharacterLocomotion.Up.sqrMagnitude > 0.0001f)
        {
            return m_CharacterLocomotion.Up.normalized;
        }

        return Vector3.up;
    }
}
