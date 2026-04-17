using UnityEngine;

public partial class SquadCharacterController
{
    private enum LadderTraversalPhase
    {
        None = 0,
        AlignToStart = 1,
        StartAnimation = 2,
        ClimbLoop = 3,
        EndAnimation = 4,
    }

    [Header("Ladder Traversal")]
    [SerializeField, Tooltip("Autorise l'utilisation des echelles interactives.")]
    private bool enableLadderTraversal = true;
    [SerializeField, Tooltip("Vitesse d'approche vers la base haute/basse avant de commencer l'echelle.")]
    private float ladderApproachSpeed = 2.5f;
    [SerializeField, Tooltip("Vitesse de rotation pendant l'approche de l'echelle (deg/s).")]
    private float ladderApproachRotationSpeed = 720f;
    [SerializeField, Tooltip("Tolerance de position avant de lancer l'animation Start.")]
    private float ladderApproachPositionTolerance = 0.035f;
    [SerializeField, Tooltip("Tolerance d'orientation avant de lancer l'animation Start (deg).")]
    private float ladderApproachRotationTolerance = 2f;
    [SerializeField, Tooltip("Vitesse de progression le long de l'echelle (m/s).")]
    private float ladderClimbSpeed = 1.25f;
    [SerializeField, Tooltip("Duree minimale du loop de montee/descente.")]
    private float ladderMinimumClimbDuration = 0.25f;
    [SerializeField, Tooltip("Duree de la phase Start avant le loop.")]
    private float ladderStartDuration = 0.25f;
    [SerializeField, Tooltip("Duree de la phase End apres le loop.")]
    private float ladderEndDuration = 0.25f;

    [Header("Ladder Animation")]
    [SerializeField, Tooltip("Force un CrossFade vers les etats nommes ci-dessous quand ils existent.")]
    private bool forceLadderStateCrossFade;
    [SerializeField, Tooltip("Layer Animator utilise pour les etats d'echelle.")]
    private int ladderAnimationLayer;
    [SerializeField, Tooltip("Duree du CrossFade d'echelle.")]
    private float ladderAnimationCrossFadeDuration = 0.08f;
    [SerializeField, Tooltip("Trigger emis quand la phase Start commence.")]
    private string ladderStartTriggerParam = "LadderStartTrigger";
    [SerializeField, Tooltip("Trigger emis quand la phase End commence.")]
    private string ladderEndTriggerParam = "LadderEndTrigger";
    [SerializeField, Tooltip("Bool actif pendant toute l'utilisation de l'echelle.")]
    private string isClimbingLadderParam = "IsClimbingLadder";
    [SerializeField, Tooltip("Float de direction: 1 = montee, -1 = descente.")]
    private string ladderDirectionParam = "LadderDirection";
    [SerializeField, Tooltip("Int de phase: 0 none, 1 align, 2 start, 3 loop, 4 end.")]
    private string ladderPhaseParam = "LadderPhase";
    [SerializeField, Tooltip("Progression 0..1 pendant le loop.")]
    private string ladderProgressParam = "LadderProgress";
    [SerializeField, Tooltip("Nom de l'etat Animator Start.")]
    private string ladderStartStateName = "Ladder_Start";
    [SerializeField, Tooltip("Nom de l'etat Animator Loop.")]
    private string ladderLoopStateName = "Ladder_Loop";
    [SerializeField, Tooltip("Nom de l'etat Animator End.")]
    private string ladderEndStateName = "Ladder_End";

    private bool ladderTraversalActive;
    private bool ladderTraversalConsumedMovementThisStep;
    private LadderInteractable activeLadder;
    private LadderTraversalPhase ladderTraversalPhase;
    private Vector3 ladderStartPosition;
    private Quaternion ladderStartRotation;
    private Vector3 ladderEndPosition;
    private Quaternion ladderEndRotation;
    private bool ladderAscending;
    private float ladderPhaseTimer;
    private float ladderClimbDuration;

    public bool IsLadderTraversalActive => ladderTraversalActive;

    public bool TryStartLadderTraversal(
        LadderInteractable ladder,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        bool ascending)
    {
        if (!enableLadderTraversal || ladderTraversalActive || ladder == null)
        {
            return false;
        }

        if (!CanSimulateMovementLocally())
        {
            return false;
        }

        ValidateLadderTraversalSettings();

        float pathLength = Vector3.Distance(startPosition, endPosition);
        if (pathLength <= 0.001f)
        {
            return false;
        }

        Stop();
        ResetCommittedJumpRuntime();
        inputLockTimer = 0f;
        groundIgnoreUntilTime = 0f;

        activeLadder = ladder;
        ladderStartPosition = startPosition;
        ladderStartRotation = NormalizeRotation(startRotation, transform.rotation);
        ladderEndPosition = endPosition;
        ladderEndRotation = NormalizeRotation(endRotation, ladderStartRotation);
        ladderAscending = ascending;
        ladderClimbDuration = Mathf.Max(ladderMinimumClimbDuration, pathLength / Mathf.Max(0.01f, ladderClimbSpeed));

        ladderTraversalActive = true;
        BeginLadderPhase(LadderTraversalPhase.AlignToStart);
        SetAnimatorBoolIfValid(isClimbingLadderParam, true);
        SetAnimatorFloatIfValid(ladderDirectionParam, ladderAscending ? 1f : -1f);
        SetAnimatorFloatIfValid(ladderProgressParam, 0f);
        return true;
    }

    private void ValidateLadderTraversalSettings()
    {
        ladderApproachSpeed = Mathf.Max(0.01f, ladderApproachSpeed);
        ladderApproachRotationSpeed = Mathf.Max(1f, ladderApproachRotationSpeed);
        ladderApproachPositionTolerance = Mathf.Max(0.001f, ladderApproachPositionTolerance);
        ladderApproachRotationTolerance = Mathf.Max(0.01f, ladderApproachRotationTolerance);
        ladderClimbSpeed = Mathf.Max(0.01f, ladderClimbSpeed);
        ladderMinimumClimbDuration = Mathf.Max(0f, ladderMinimumClimbDuration);
        ladderStartDuration = Mathf.Max(0f, ladderStartDuration);
        ladderEndDuration = Mathf.Max(0f, ladderEndDuration);
        ladderAnimationLayer = Mathf.Max(0, ladderAnimationLayer);
        ladderAnimationCrossFadeDuration = Mathf.Max(0f, ladderAnimationCrossFadeDuration);
    }

    private void UpdateLadderTraversal(float deltaTime)
    {
        ladderTraversalConsumedMovementThisStep = false;
        if (!ladderTraversalActive)
        {
            return;
        }

        ladderTraversalConsumedMovementThisStep = true;
        if (!CanSimulateMovementLocally())
        {
            CancelLadderTraversal();
            return;
        }

        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        switch (ladderTraversalPhase)
        {
            case LadderTraversalPhase.AlignToStart:
                UpdateLadderAlignPhase(safeDeltaTime);
                break;
            case LadderTraversalPhase.StartAnimation:
                UpdateLadderTimedPhase(safeDeltaTime, ladderStartPosition, ladderStartRotation, ladderStartDuration, LadderTraversalPhase.ClimbLoop);
                break;
            case LadderTraversalPhase.ClimbLoop:
                UpdateLadderLoopPhase(safeDeltaTime);
                break;
            case LadderTraversalPhase.EndAnimation:
                UpdateLadderEndPhase(safeDeltaTime);
                break;
        }
    }

    private void UpdateLadderAlignPhase(float deltaTime)
    {
        Vector3 currentPosition = GetWorldPosition();
        Quaternion currentRotation = GetWorldRotation();
        Vector3 nextPosition = Vector3.MoveTowards(
            currentPosition,
            ladderStartPosition,
            ladderApproachSpeed * deltaTime);
        Quaternion nextRotation = Quaternion.RotateTowards(
            currentRotation,
            ladderStartRotation,
            ladderApproachRotationSpeed * deltaTime);

        ApplyLadderPose(nextPosition, nextRotation);

        bool positionReached = (nextPosition - ladderStartPosition).sqrMagnitude <= ladderApproachPositionTolerance * ladderApproachPositionTolerance;
        bool rotationReached = Quaternion.Angle(nextRotation, ladderStartRotation) <= ladderApproachRotationTolerance;
        if (positionReached && rotationReached)
        {
            ApplyLadderPose(ladderStartPosition, ladderStartRotation);
            BeginLadderPhase(LadderTraversalPhase.StartAnimation);
        }
    }

    private void UpdateLadderTimedPhase(
        float deltaTime,
        Vector3 position,
        Quaternion rotation,
        float duration,
        LadderTraversalPhase nextPhase)
    {
        ApplyLadderPose(position, rotation);
        ladderPhaseTimer += deltaTime;
        if (ladderPhaseTimer >= duration)
        {
            BeginLadderPhase(nextPhase);
        }
    }

    private void UpdateLadderLoopPhase(float deltaTime)
    {
        ladderPhaseTimer += deltaTime;
        float t = ladderClimbDuration <= 0f ? 1f : Mathf.Clamp01(ladderPhaseTimer / ladderClimbDuration);
        Vector3 position = Vector3.Lerp(ladderStartPosition, ladderEndPosition, t);
        Quaternion rotation = Quaternion.Slerp(ladderStartRotation, ladderEndRotation, t);

        ApplyLadderPose(position, rotation);
        SetAnimatorFloatIfValid(ladderProgressParam, t);

        if (t >= 1f)
        {
            ApplyLadderPose(ladderEndPosition, ladderEndRotation);
            BeginLadderPhase(LadderTraversalPhase.EndAnimation);
        }
    }

    private void UpdateLadderEndPhase(float deltaTime)
    {
        ApplyLadderPose(ladderEndPosition, ladderEndRotation);
        ladderPhaseTimer += deltaTime;
        if (ladderPhaseTimer >= ladderEndDuration)
        {
            CompleteLadderTraversal();
        }
    }

    private void BeginLadderPhase(LadderTraversalPhase phase)
    {
        ladderTraversalPhase = phase;
        ladderPhaseTimer = 0f;
        SetAnimatorIntIfValid(ladderPhaseParam, (int)phase);

        switch (phase)
        {
            case LadderTraversalPhase.StartAnimation:
                SetAnimatorTriggerIfValid(ladderStartTriggerParam);
                CrossFadeLadderStateIfRequested(ladderStartStateName);
                break;
            case LadderTraversalPhase.ClimbLoop:
                CrossFadeLadderStateIfRequested(ladderLoopStateName);
                break;
            case LadderTraversalPhase.EndAnimation:
                SetAnimatorTriggerIfValid(ladderEndTriggerParam);
                CrossFadeLadderStateIfRequested(ladderEndStateName);
                break;
        }
    }

    private void CompleteLadderTraversal()
    {
        ApplyLadderPose(ladderEndPosition, ladderEndRotation);
        ClearLadderTraversalState();
        Stop();
        hasObservedWorldPosition = false;
    }

    private void CancelLadderTraversal()
    {
        if (!ladderTraversalActive)
        {
            return;
        }

        ClearLadderTraversalState();
        StopHorizontalVelocity();
        hasObservedWorldPosition = false;
    }

    private void ClearLadderTraversalState()
    {
        ladderTraversalActive = false;
        activeLadder = null;
        ladderTraversalPhase = LadderTraversalPhase.None;
        ladderPhaseTimer = 0f;
        ladderClimbDuration = 0f;
        SetAnimatorBoolIfValid(isClimbingLadderParam, false);
        SetAnimatorIntIfValid(ladderPhaseParam, (int)LadderTraversalPhase.None);
        SetAnimatorFloatIfValid(ladderProgressParam, 0f);
    }

    private void ApplyLadderPose(Vector3 position, Quaternion rotation)
    {
        StopHorizontalVelocity();

        if (ShouldUseRigidbody() && rigidbodyTarget != null)
        {
            rigidbodyTarget.WakeUp();
            rigidbodyTarget.linearVelocity = Vector3.zero;
            rigidbodyTarget.angularVelocity = Vector3.zero;
            rigidbodyTarget.MovePosition(position);
            rigidbodyTarget.MoveRotation(rotation);
            return;
        }

        Transform target = motionRoot != null ? motionRoot : transform;
        target.SetPositionAndRotation(position, rotation);
    }

    private Quaternion GetWorldRotation()
    {
        if (ShouldUseRigidbody() && rigidbodyTarget != null)
        {
            return rigidbodyTarget.rotation;
        }

        Transform target = motionRoot != null ? motionRoot : transform;
        return target.rotation;
    }

    private Quaternion NormalizeRotation(Quaternion rotation, Quaternion fallback)
    {
        float magnitude = Mathf.Sqrt(
            rotation.x * rotation.x +
            rotation.y * rotation.y +
            rotation.z * rotation.z +
            rotation.w * rotation.w);
        return magnitude > 0.0001f ? rotation : fallback;
    }

    private void CrossFadeLadderStateIfRequested(string stateName)
    {
        if (!forceLadderStateCrossFade || animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int shortStateHash = Animator.StringToHash(stateName);
        string layerName = animator.GetLayerName(ladderAnimationLayer);
        int fullPathStateHash = string.IsNullOrWhiteSpace(layerName)
            ? shortStateHash
            : Animator.StringToHash(layerName + "." + stateName);
        if (!animator.HasState(ladderAnimationLayer, shortStateHash) &&
            !animator.HasState(ladderAnimationLayer, fullPathStateHash))
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateName, ladderAnimationCrossFadeDuration, ladderAnimationLayer, 0f);
    }
}
