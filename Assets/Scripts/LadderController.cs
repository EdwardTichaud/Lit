using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class LadderController : MonoBehaviour
{
    private const float MaxStartToLoopLeadRatio = 0.4f;

    private static readonly string[] BasePointNames =
    {
        "ladderbase",
        "base",
        "bottom",
        "btrigger"
    };

    private static readonly string[] TopPointNames =
    {
        "laddertop",
        "top",
        "htrigger",
        "hightrigger"
    };

    private static readonly string[] HighExitPointNames =
    {
        "ladderexit",
        "topexit",
        "highexit",
        "hexit"
    };

    private static readonly string[] BottomExitPointNames =
    {
        "ladderbottomexit",
        "bottomexit",
        "baseexit",
        "lowexit",
        "bexit"
    };

    [Header("Points")]
    [SerializeField, Tooltip("Points d'entree possibles. Si vide, le script cherche ladder_base/Base/Bottom/B_Trigger dans les enfants.")]
    private Transform[] ladderBases;
    [SerializeField, Tooltip("Point haut de l'echelle. Si vide, le script cherche ladder_top/Top/HTrigger dans les enfants.")]
    private Transform ladderTop;
    [SerializeField, Tooltip("Point de sortie haut. Si vide, le script cherche HExit/HighExit/TopExit dans les enfants.")]
    private Transform ladderExit;
    [SerializeField, Tooltip("Point de sortie bas. Si vide, le script cherche B_Exit/BottomExit/BaseExit dans les enfants.")]
    private Transform bottomExit;

    [Header("Motion")]
    [SerializeField, Tooltip("Duree de placement du personnage sur le point bas.")]
    private float approachDuration = 0.35f;
    [SerializeField, Tooltip("Vitesse de montee entre le point bas et le point haut.")]
    private float climbSpeed = 1.4f;
    [SerializeField, Tooltip("Multiplicateur de vitesse applique pendant Ladder_StartToClimb avant le loop.")]
    private float startClimbSpeedMultiplier = 0.5f;
    [SerializeField, Tooltip("Duree minimale de la phase Ladder_Loop apres Ladder_Start.")]
    private float minimumLoopDuration = 0.1f;
    [SerializeField, Tooltip("Duree du deplacement vers le point de sortie pendant Ladder_End.")]
    private float exitDuration = 0.45f;
    [SerializeField, Tooltip("Temps dans le clip Ladder_EndToClimb ou le Lerp vertical vers H_Trigger se termine.")]
    private float ladderEndToClimbLerpEndTime = 1.25f;
    [SerializeField, Tooltip("Temps dans le clip Ladder_EndToClimb avant de commencer la translation vers H_Exit.")]
    private float ladderEndToClimbExitMoveStartTime = 2f;
    [SerializeField, Tooltip("Temps dans le clip Ladder_EndToClimb ou la translation vers H_Exit se termine.")]
    private float ladderEndToClimbExitMoveEndTime = 3f;
    [SerializeField, Tooltip("Garde la rotation du point d'entree pendant les phases Ladder_Start et Ladder_Loop.")]
    private bool keepEntryRotationDuringClimb = true;

    [Header("Root Alignment")]
    [SerializeField, Tooltip("Autorise un leger recul manuel du debut de Ladder_EndToClimb avant H_Trigger.")]
    private bool offsetHighExitStartByCharacterHeight = true;
    [SerializeField, Tooltip("Compatibilite: ne plus utiliser pour eviter que Ladder_EndToClimb demarre trop bas.")]
    private float highExitStartHeightMultiplier;
    [SerializeField, Tooltip("Recul manuel en metres du debut de Ladder_EndToClimb avant H_Trigger.")]
    private float highExitStartExtraOffset;

    [Header("Animator")]
    [SerializeField, Tooltip("Layer Animator utilise pour les animations d'echelle.")]
    private int animationLayer;
    [SerializeField, Tooltip("Nom du trigger/state de debut quand le personnage monte.")]
    private string ladderUpStartName = "Ladder_StartToClimb";
    [SerializeField, Tooltip("Nom du bool/trigger/state de boucle quand le personnage monte.")]
    private string ladderUpLoopName = "Ladder_Up_Loop";
    [SerializeField, Tooltip("Nom du trigger/state de fin quand le personnage monte.")]
    private string ladderUpEndName = "Ladder_EndToClimb";
    [SerializeField, Tooltip("Nom du trigger/state de debut quand le personnage descend.")]
    private string ladderDownStartName = "Ladder_StartToDown";
    [SerializeField, Tooltip("Nom du bool/trigger/state de boucle quand le personnage descend.")]
    private string ladderDownLoopName = "Ladder_Down_Loop";
    [SerializeField, Tooltip("Nom du trigger/state de fin quand le personnage descend.")]
    private string ladderDownEndName = "Ladder_EndToDown";
    [SerializeField, Tooltip("Duree utilisee si le clip de debut d'echelle n'est pas trouve.")]
    private float ladderStartFallbackDuration = 0.45f;
    [SerializeField, Tooltip("Duree utilisee si le clip de fin d'echelle n'est pas trouve.")]
    private float ladderEndFallbackDuration = 0.45f;
    [SerializeField, Tooltip("Duree de transition si le script doit CrossFade vers un state.")]
    private float crossFadeDuration = 0.08f;

    [Header("NavMesh")]
    [SerializeField, Tooltip("Cree des NavMeshLink runtime pour que le NavMesh considere l'echelle comme un passage.")]
    private bool autoCreateNavMeshLinks = true;
    [SerializeField, Tooltip("Largeur des NavMeshLink generes pour l'echelle.")]
    private float navMeshLinkWidth = 0.8f;
    [SerializeField, Tooltip("Area NavMesh assignee aux NavMeshLink de l'echelle.")]
    private int navMeshLinkArea;
    [SerializeField, Tooltip("Cout NavMesh du lien. Une valeur negative laisse Unity utiliser le cout de l'area.")]
    private float navMeshLinkCostOverride = -1f;
    [SerializeField, Tooltip("Autorise les followers a utiliser le lien dans les deux sens.")]
    private bool navMeshLinkBidirectional = true;

    private Coroutine activeRoutine;
    private readonly List<NavMeshLink> managedNavMeshLinks = new List<NavMeshLink>();

    public bool IsBusy => activeRoutine != null;

    private void Awake()
    {
        ResolvePointReferencesIfNeeded();
        EnsureNavMeshLinks();
    }

    private void OnEnable()
    {
        ResolvePointReferencesIfNeeded();
        EnsureNavMeshLinks();
    }

    private void OnValidate()
    {
        approachDuration = Mathf.Max(0f, approachDuration);
        climbSpeed = Mathf.Max(0.01f, climbSpeed);
        startClimbSpeedMultiplier = Mathf.Max(0f, startClimbSpeedMultiplier);
        minimumLoopDuration = Mathf.Max(0f, minimumLoopDuration);
        exitDuration = Mathf.Max(0f, exitDuration);
        ladderEndToClimbLerpEndTime = Mathf.Max(0f, ladderEndToClimbLerpEndTime);
        ladderEndToClimbExitMoveStartTime = Mathf.Max(0f, ladderEndToClimbExitMoveStartTime);
        ladderEndToClimbExitMoveEndTime = Mathf.Max(ladderEndToClimbExitMoveStartTime, ladderEndToClimbExitMoveEndTime);
        highExitStartHeightMultiplier = Mathf.Max(0f, highExitStartHeightMultiplier);
        highExitStartExtraOffset = Mathf.Max(0f, highExitStartExtraOffset);
        animationLayer = Mathf.Max(0, animationLayer);
        ladderStartFallbackDuration = Mathf.Max(0f, ladderStartFallbackDuration);
        ladderEndFallbackDuration = Mathf.Max(0f, ladderEndFallbackDuration);
        crossFadeDuration = Mathf.Max(0f, crossFadeDuration);
        navMeshLinkWidth = Mathf.Max(0.05f, navMeshLinkWidth);
        navMeshLinkArea = Mathf.Max(0, navMeshLinkArea);
    }

    public void UseLadder()
    {
        UseLadder(ResolveFallbackCharacter(), true);
    }

    public bool UseLadder(GameObject character)
    {
        return UseLadder(character, true);
    }

    public bool UseLadder(GameObject character, bool driveMotion)
    {
        if (character == null || activeRoutine != null)
        {
            return false;
        }

        ResolvePointReferencesIfNeeded();

        if (!TryResolveLadderRoute(character.transform.position, out LadderRoute route))
        {
            return false;
        }

        SquadCharacterController controller = ResolveCharacterController(character);
        LitOpsiveLocomotionBridge uccBridge = ResolveUccBridge(character, controller);
        StarterInspiredThirdPersonMotor starterMotor = uccBridge != null ? null : ResolveStarterMotor(character);
        Animator animator = ResolveCharacterAnimator(character, controller);
        Rigidbody body = starterMotor != null || uccBridge != null ? null : ResolveCharacterRigidbody(character, controller);
        Transform motionRoot = ResolveCharacterMotionRoot(character, uccBridge, starterMotor, controller, body);
        if (motionRoot == null)
        {
            return false;
        }
        ScriptedMotionTarget motionTarget = new ScriptedMotionTarget(uccBridge, starterMotor, motionRoot, body);

        LadderAnimationSet animationSet = ResolveLadderAnimationSet(route.ExitsAtTop);
        Vector3 ladderEndStartPosition = ResolveLadderEndStartPosition(
            character,
            controller,
            motionRoot,
            route.EntryPoint.position,
            route.TargetPoint.position,
            route.ExitsAtTop);
        Vector3 ladderLoopEndPosition = ResolveLadderLoopEndPosition(
            animator,
            animationSet,
            route.EntryPoint.position,
            ladderEndStartPosition,
            route.ExitsAtTop);
        Quaternion climbRotation = keepEntryRotationDuringClimb
            ? route.EntryPoint.rotation
            : route.TargetPoint.rotation;

        activeRoutine = StartCoroutine(UseLadderRoutine(
            controller,
            animator,
            motionTarget,
            route.EntryPoint,
            ladderLoopEndPosition,
            ladderEndStartPosition,
            climbRotation,
            route.ExitPoint,
            animationSet,
            driveMotion));
        return true;
    }

    private IEnumerator UseLadderRoutine(
        SquadCharacterController controller,
        Animator animator,
        ScriptedMotionTarget motionTarget,
        Transform entryPoint,
        Vector3 ladderLoopEndPosition,
        Vector3 ladderEndStartPosition,
        Quaternion ladderEndStartRotation,
        Transform exitPoint,
        LadderAnimationSet animationSet,
        bool driveMotion)
    {
        bool inputSuppressed = false;
        bool uccPrepared = false;
        bool bodyPrepared = false;
        bool starterMotorPrepared = false;
        bool animatorRootMotionPrepared = false;
        bool previousApplyRootMotion = false;
        RigidbodyState bodyState = default;

        try
        {
            if (animator != null && animator.applyRootMotion)
            {
                previousApplyRootMotion = animator.applyRootMotion;
                animator.applyRootMotion = false;
                animatorRootMotionPrepared = true;
            }

            if (motionTarget.UccBridge != null)
            {
                if (!motionTarget.UccBridge.BeginScriptedTraversal())
                {
                    yield break;
                }

                uccPrepared = true;
            }

            if (motionTarget.StarterMotor != null)
            {
                motionTarget.StarterMotor.BeginLadderTraversal();
                starterMotorPrepared = true;
            }

            if (controller != null && motionTarget.StarterMotor == null)
            {
                controller.PushScriptedMovementSuppression();
                inputSuppressed = true;
            }

            if (driveMotion && motionTarget.Body != null)
            {
                bodyState = new RigidbodyState(motionTarget.Body);
                PrepareBodyForScriptedMotion(motionTarget.Body);
                bodyPrepared = true;
            }

            if (driveMotion)
            {
                yield return MoveToPoint(motionTarget, entryPoint.position, entryPoint.rotation, approachDuration);
            }
            else
            {
                yield return WaitForFixedDuration(approachDuration);
            }

            TriggerOneShotAnimation(animator, animationSet.StartName);
            yield return WaitUntilAnimationStateIsObserved(animator, animationSet.StartName, ladderStartFallbackDuration);

            float startDuration = ResolveAnimationDuration(animator, animationSet.StartName, ladderStartFallbackDuration);
            float startClipDuration = ResolveAnimationClipLength(animator, animationSet.StartClipReferenceName, startDuration);
            float startPhaseClimbSpeed = animationSet.ExitsAtTop
                ? climbSpeed * startClimbSpeedMultiplier
                : 0f;
            float startPhaseMoveStartTime = 0f;
            float startPhaseMoveEndTime = 0f;
            float startPhaseMoveDistance = 0f;
            if (!animationSet.ExitsAtTop)
            {
                startPhaseMoveStartTime = ResolveMirroredClipMarkerStateTime(
                    ladderEndToClimbLerpEndTime,
                    startDuration,
                    startClipDuration);
                startPhaseMoveEndTime = Mathf.Max(
                    startPhaseMoveStartTime,
                    startDuration - Mathf.Min(Mathf.Max(0f, crossFadeDuration), startDuration * MaxStartToLoopLeadRatio));
                startPhaseMoveDistance = Mathf.Min(
                    Vector3.Distance(entryPoint.position, ladderLoopEndPosition),
                    climbSpeed * ladderEndToClimbLerpEndTime);
            }

            yield return RunClimbPhase(
                animator,
                animationSet.StartName,
                animationSet.LoopName,
                motionTarget,
                entryPoint.position,
                entryPoint.rotation,
                ladderLoopEndPosition,
                ladderEndStartRotation,
                startDuration,
                startPhaseClimbSpeed,
                startPhaseMoveStartTime,
                startPhaseMoveEndTime,
                startPhaseMoveDistance,
                driveMotion);

            SetLoopAnimation(animator, animationSet.LoopName, false);
            TriggerOneShotAnimation(animator, animationSet.EndName);

            yield return WaitUntilAnimationStateIsObserved(animator, animationSet.EndName, ladderEndFallbackDuration);
            float endDuration = ResolveAnimationDuration(animator, animationSet.EndName, ladderEndFallbackDuration);
            float endClipDuration = ResolveAnimationClipLength(animator, animationSet.EndClipReferenceName, endDuration);
            float exitMoveEndTime = 0f;
            if (animationSet.ExitsAtTop)
            {
                float climbLerpEndTime = ResolveClipMarkerStateTime(
                    ladderEndToClimbLerpEndTime,
                    endDuration,
                    endClipDuration);
                yield return MoveToPointDuringAnimationStateTimeRange(
                    animator,
                    animationSet.EndName,
                    motionTarget,
                    ladderEndStartPosition,
                    ladderEndStartRotation,
                    0f,
                    climbLerpEndTime,
                    endDuration,
                    driveMotion);

                float exitMoveStartTime = ResolveClipMarkerStateTime(
                    ladderEndToClimbExitMoveStartTime,
                    endDuration,
                    endClipDuration);
                exitMoveEndTime = ResolveClipMarkerStateTime(
                    ladderEndToClimbExitMoveEndTime,
                    endDuration,
                    endClipDuration);
                yield return MoveToPointDuringAnimationStateTimeRange(
                    animator,
                    animationSet.EndName,
                    motionTarget,
                    exitPoint.position,
                    exitPoint.rotation,
                    exitMoveStartTime,
                    exitMoveEndTime,
                    endDuration,
                    driveMotion);
            }
            else
            {
                yield return MoveToPointDuringAnimationStateTimeRange(
                    animator,
                    animationSet.EndName,
                    motionTarget,
                    ladderEndStartPosition,
                    ladderEndStartRotation,
                    0f,
                    endDuration,
                    endDuration,
                    driveMotion);

                float exitMoveDuration = Mathf.Max(0f, exitDuration);
                if (driveMotion)
                {
                    yield return MoveToPoint(motionTarget, exitPoint.position, exitPoint.rotation, exitMoveDuration);
                }
                else
                {
                    yield return WaitForFixedDuration(exitMoveDuration);
                }

                exitMoveEndTime = endDuration + exitMoveDuration;
            }

            if (starterMotorPrepared && motionTarget.StarterMotor != null)
            {
                motionTarget.StarterMotor.EndLadderTraversal();
                starterMotorPrepared = false;
            }

            if (uccPrepared && motionTarget.UccBridge != null)
            {
                motionTarget.UccBridge.EndScriptedTraversal();
                uccPrepared = false;
            }

            if (bodyPrepared)
            {
                bodyState.Restore(motionTarget.Body);
                bodyPrepared = false;
            }

            if (inputSuppressed && controller != null)
            {
                controller.PopScriptedMovementSuppression();
                inputSuppressed = false;
            }

            float remainingEndDuration = Mathf.Max(0f, endDuration - exitMoveEndTime);
            if (remainingEndDuration > 0f)
            {
                yield return WaitForFixedDuration(remainingEndDuration);
            }
        }
        finally
        {
            SetLoopAnimation(animator, animationSet.LoopName, false);

            if (starterMotorPrepared && motionTarget.StarterMotor != null)
            {
                motionTarget.StarterMotor.EndLadderTraversal();
            }

            if (uccPrepared && motionTarget.UccBridge != null)
            {
                motionTarget.UccBridge.EndScriptedTraversal();
            }

            if (bodyPrepared)
            {
                bodyState.Restore(motionTarget.Body);
            }

            if (inputSuppressed && controller != null)
            {
                controller.PopScriptedMovementSuppression();
            }

            if (animatorRootMotionPrepared && animator != null)
            {
                animator.applyRootMotion = previousApplyRootMotion;
            }

            activeRoutine = null;
        }
    }

    private IEnumerator RunClimbPhase(
        Animator animator,
        string ladderStartName,
        string ladderLoopName,
        ScriptedMotionTarget motionTarget,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        float startDuration,
        float startPhaseClimbSpeed,
        float startPhaseMoveStartTime,
        float startPhaseMoveEndTime,
        float startPhaseMoveDistance,
        bool driveMotion)
    {
        Vector3 loopStartPosition = startPosition;
        Quaternion loopStartRotation = startRotation;
        float startElapsed = 0f;
        while (!ShouldStartLoopAnimation(animator, ladderStartName, startElapsed, startDuration))
        {
            if (driveMotion)
            {
                TryApplyStartPhaseMotion(
                    motionTarget,
                    startPosition,
                    startRotation,
                    endPosition,
                    endRotation,
                    startElapsed,
                    startPhaseClimbSpeed,
                    startPhaseMoveStartTime,
                    startPhaseMoveEndTime,
                    startPhaseMoveDistance,
                    out loopStartPosition,
                    out loopStartRotation);
            }

            yield return new WaitForFixedUpdate();
            startElapsed += Time.fixedDeltaTime;
        }

        if (driveMotion)
        {
            TryApplyStartPhaseMotion(
                motionTarget,
                startPosition,
                startRotation,
                endPosition,
                endRotation,
                startElapsed,
                startPhaseClimbSpeed,
                startPhaseMoveStartTime,
                startPhaseMoveEndTime,
                startPhaseMoveDistance,
                out loopStartPosition,
                out loopStartRotation);
        }

        SetLoopAnimation(animator, ladderLoopName, true);
        yield return WaitUntilLoopAnimationIsPlaying(animator, ladderLoopName);

        float duration = ResolveClimbDuration(loopStartPosition, endPosition);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            if (driveMotion)
            {
                ApplyPose(
                    motionTarget,
                    Vector3.Lerp(loopStartPosition, endPosition, t),
                    Quaternion.Slerp(loopStartRotation, endRotation, t));
            }

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        if (driveMotion)
        {
            ApplyPose(motionTarget, endPosition, endRotation);
        }
    }

    private IEnumerator WaitUntilLoopAnimationIsPlaying(Animator animator, string ladderLoopName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(ladderLoopName) || !IsLayerValid(animator))
        {
            yield break;
        }

        if (!HasAnimatorState(animator, ladderLoopName))
        {
            yield break;
        }

        if (IsCurrentAnimatorState(animator, ladderLoopName))
        {
            yield break;
        }

        float timeout = Mathf.Max(0.25f, crossFadeDuration * 3f);
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            if (IsCurrentAnimatorState(animator, ladderLoopName))
            {
                yield break;
            }
        }

        if (!CrossFadeStateIfAvailable(animator, ladderLoopName))
        {
            yield break;
        }

        while (animator != null && IsLayerValid(animator) && !IsCurrentAnimatorState(animator, ladderLoopName))
        {
            yield return new WaitForFixedUpdate();
        }
    }

    private IEnumerator WaitUntilAnimationStateIsObserved(Animator animator, string animationName, float fallbackDuration)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(animationName) ||
            !IsLayerValid(animator) ||
            !HasAnimatorState(animator, animationName))
        {
            yield break;
        }

        if (TryGetObservedAnimatorStateInfo(animator, animationName, out _, out _))
        {
            yield break;
        }

        float timeout = Mathf.Max(Time.fixedDeltaTime, crossFadeDuration * 3f, Mathf.Min(0.5f, fallbackDuration));
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            if (TryGetObservedAnimatorStateInfo(animator, animationName, out _, out _))
            {
                yield break;
            }
        }

        if (!CrossFadeStateIfAvailable(animator, animationName))
        {
            yield break;
        }

        float crossFadeTimeout = Mathf.Max(Time.fixedDeltaTime, crossFadeDuration * 3f, 0.25f);
        elapsed = 0f;
        while (elapsed < crossFadeTimeout &&
               animator != null &&
               IsLayerValid(animator) &&
               !TryGetObservedAnimatorStateInfo(animator, animationName, out _, out _))
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }
    }

    private IEnumerator WaitUntilAnimationStateTime(
        Animator animator,
        string animationName,
        float targetTime,
        float fallbackDuration)
    {
        float clampedTargetTime = Mathf.Max(0f, targetTime);
        if (clampedTargetTime <= 0f)
        {
            yield break;
        }

        float fallbackElapsed = 0f;
        while (fallbackElapsed < clampedTargetTime)
        {
            if (TryResolveAnimationStateElapsedTime(animator, animationName, fallbackDuration, out float stateElapsedTime))
            {
                if (stateElapsedTime >= clampedTargetTime)
                {
                    yield break;
                }
            }

            yield return new WaitForFixedUpdate();
            fallbackElapsed += Time.fixedDeltaTime;
        }
    }

    private bool ShouldStartLoopAnimation(Animator animator, string ladderStartName, float elapsed, float fallbackStartDuration)
    {
        if (TryGetObservedAnimatorStateInfo(animator, ladderStartName, out AnimatorStateInfo stateInfo, out bool isNextState))
        {
            if (isNextState)
            {
                return false;
            }

            float stateDuration = ResolveAnimatorStateDuration(stateInfo, fallbackStartDuration);
            if (stateDuration <= 0.0001f)
            {
                return elapsed >= Mathf.Max(0f, fallbackStartDuration);
            }

            float leadTime = Mathf.Min(Mathf.Max(0f, crossFadeDuration), stateDuration * MaxStartToLoopLeadRatio);
            if (!IsReverseAnimatorState(stateInfo))
            {
                float normalizedThreshold = Mathf.Clamp01(1f - (leadTime / stateDuration));
                if (stateInfo.normalizedTime >= normalizedThreshold)
                {
                    return true;
                }
            }

            return elapsed >= Mathf.Max(0f, stateDuration - leadTime);
        }

        return elapsed >= Mathf.Max(0f, fallbackStartDuration - crossFadeDuration);
    }

    private IEnumerator MoveToPoint(ScriptedMotionTarget motionTarget, Vector3 targetPosition, Quaternion targetRotation, float duration)
    {
        Vector3 startPosition = ResolveMotionPosition(motionTarget);
        Quaternion startRotation = ResolveMotionRotation(motionTarget);
        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f)
        {
            ApplyPose(motionTarget, targetPosition, targetRotation);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            float t = Mathf.Clamp01(elapsed / clampedDuration);
            ApplyPose(
                motionTarget,
                Vector3.Lerp(startPosition, targetPosition, t),
                Quaternion.Slerp(startRotation, targetRotation, t));

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        ApplyPose(motionTarget, targetPosition, targetRotation);
    }

    private IEnumerator MoveToPointDuringAnimationStateTimeRange(
        Animator animator,
        string animationName,
        ScriptedMotionTarget motionTarget,
        Vector3 targetPosition,
        Quaternion targetRotation,
        float startStateTime,
        float endStateTime,
        float fallbackDuration,
        bool driveMotion)
    {
        float clampedStartTime = Mathf.Max(0f, startStateTime);
        float clampedEndTime = Mathf.Max(0f, endStateTime);
        if (clampedStartTime > 0f)
        {
            yield return WaitUntilAnimationStateTime(animator, animationName, clampedStartTime, fallbackDuration);
        }

        if (!driveMotion)
        {
            yield return WaitUntilAnimationStateTime(animator, animationName, clampedEndTime, fallbackDuration);
            yield break;
        }

        if (clampedEndTime <= clampedStartTime)
        {
            ApplyPose(motionTarget, targetPosition, targetRotation);
            yield break;
        }

        Vector3 startPosition = ResolveMotionPosition(motionTarget);
        Quaternion startRotation = ResolveMotionRotation(motionTarget);
        float fallbackElapsed = clampedStartTime;
        while (fallbackElapsed < clampedEndTime)
        {
            float stateElapsedTime = fallbackElapsed;
            if (TryResolveAnimationStateElapsedTime(animator, animationName, fallbackDuration, out float observedElapsedTime))
            {
                stateElapsedTime = observedElapsedTime;
            }

            if (stateElapsedTime >= clampedEndTime)
            {
                break;
            }

            float t = Mathf.Clamp01((Mathf.Max(stateElapsedTime, clampedStartTime) - clampedStartTime) / (clampedEndTime - clampedStartTime));
            ApplyPose(
                motionTarget,
                Vector3.Lerp(startPosition, targetPosition, t),
                Quaternion.Slerp(startRotation, targetRotation, t));

            yield return new WaitForFixedUpdate();
            fallbackElapsed += Time.fixedDeltaTime;
        }

        ApplyPose(motionTarget, targetPosition, targetRotation);
    }

    private IEnumerator WaitForFixedDuration(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }
    }

    private void ApplyPose(ScriptedMotionTarget motionTarget, Vector3 position, Quaternion rotation)
    {
        if (motionTarget.UccBridge != null)
        {
            motionTarget.UccBridge.ApplyScriptedTraversalPose(position, rotation);
            return;
        }

        if (motionTarget.StarterMotor != null)
        {
            motionTarget.StarterMotor.ApplyLadderPose(position, rotation);
            return;
        }

        if (motionTarget.Body != null)
        {
            motionTarget.Body.position = position;
            motionTarget.Body.rotation = rotation;
            return;
        }

        if (motionTarget.MotionRoot != null)
        {
            motionTarget.MotionRoot.SetPositionAndRotation(position, rotation);
        }
    }

    private bool TryApplyStartPhaseMotion(
        ScriptedMotionTarget motionTarget,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        float elapsed,
        float startPhaseClimbSpeed,
        float startPhaseMoveStartTime,
        float startPhaseMoveEndTime,
        float startPhaseMoveDistance,
        out Vector3 currentPosition,
        out Quaternion currentRotation)
    {
        currentPosition = startPosition;
        currentRotation = startRotation;
        if (startPhaseMoveDistance > 0f && startPhaseMoveEndTime > startPhaseMoveStartTime)
        {
            float phaseTime = Mathf.Clamp(elapsed, startPhaseMoveStartTime, startPhaseMoveEndTime);
            float phaseProgress = Mathf.Clamp01((phaseTime - startPhaseMoveStartTime) / (startPhaseMoveEndTime - startPhaseMoveStartTime));
            ApplyClimbProgress(
                motionTarget,
                startPosition,
                startRotation,
                endPosition,
                endRotation,
                startPhaseMoveDistance * phaseProgress,
                out currentPosition,
                out currentRotation);
            return true;
        }

        if (startPhaseClimbSpeed <= 0f)
        {
            return false;
        }

        ApplyClimbProgress(
            motionTarget,
            startPosition,
            startRotation,
            endPosition,
            endRotation,
            elapsed * startPhaseClimbSpeed,
            out currentPosition,
            out currentRotation);
        return true;
    }

    private void ApplyClimbProgress(
        ScriptedMotionTarget motionTarget,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        float distance,
        out Vector3 currentPosition,
        out Quaternion currentRotation)
    {
        float climbDistance = Vector3.Distance(startPosition, endPosition);
        float progress = climbDistance <= 0.001f
            ? 1f
            : Mathf.Clamp01(Mathf.Max(0f, distance) / climbDistance);
        currentPosition = Vector3.Lerp(startPosition, endPosition, progress);
        currentRotation = Quaternion.Slerp(startRotation, endRotation, progress);
        ApplyPose(motionTarget, currentPosition, currentRotation);
    }

    private float ResolveClimbDuration(Vector3 basePosition, Vector3 topPosition)
    {
        float distanceDuration = Vector3.Distance(basePosition, topPosition) / Mathf.Max(0.01f, climbSpeed);
        return Mathf.Max(0.001f, distanceDuration, minimumLoopDuration);
    }

    private float ResolveClipMarkerStateTime(float clipTime, float stateDuration, float clipDuration)
    {
        float clampedStateDuration = Mathf.Max(0f, stateDuration);
        float requestedClipTime = Mathf.Max(0f, clipTime);
        if (clampedStateDuration <= 0f || requestedClipTime <= 0f)
        {
            return 0f;
        }

        float clampedClipDuration = Mathf.Max(0f, clipDuration);
        if (clampedClipDuration > 0.0001f)
        {
            float normalizedTime = Mathf.Clamp01(requestedClipTime / clampedClipDuration);
            return clampedStateDuration * normalizedTime;
        }

        return Mathf.Min(requestedClipTime, clampedStateDuration);
    }

    private float ResolveMirroredClipMarkerStateTime(float clipTime, float stateDuration, float clipDuration)
    {
        float clampedStateDuration = Mathf.Max(0f, stateDuration);
        if (clampedStateDuration <= 0f)
        {
            return 0f;
        }

        float requestedClipTime = Mathf.Max(0f, clipTime);
        float clampedClipDuration = Mathf.Max(0f, clipDuration);
        if (clampedClipDuration > 0.0001f)
        {
            float normalizedTime = Mathf.Clamp01(requestedClipTime / clampedClipDuration);
            return clampedStateDuration * (1f - normalizedTime);
        }

        return Mathf.Max(0f, clampedStateDuration - requestedClipTime);
    }

    private bool TryResolveAnimationStateElapsedTime(
        Animator animator,
        string animationName,
        float fallbackDuration,
        out float elapsedTime)
    {
        elapsedTime = 0f;
        if (!TryGetObservedAnimatorStateInfo(animator, animationName, out AnimatorStateInfo stateInfo, out bool isNextState) ||
            isNextState ||
            IsReverseAnimatorState(stateInfo))
        {
            return false;
        }

        float duration = ResolveAnimatorStateDuration(stateInfo, fallbackDuration);
        if (duration <= 0.0001f)
        {
            return false;
        }

        elapsedTime = Mathf.Clamp01(stateInfo.normalizedTime) * duration;
        return true;
    }

    private Vector3 ResolveLadderEndStartPosition(
        GameObject character,
        SquadCharacterController controller,
        Transform motionRoot,
        Vector3 entryPosition,
        Vector3 targetPosition,
        bool exitsAtTop)
    {
        if (!exitsAtTop || !offsetHighExitStartByCharacterHeight)
        {
            return targetPosition;
        }

        Vector3 climbDirection = targetPosition - entryPosition;
        float climbDistance = climbDirection.magnitude;
        if (climbDistance <= 0.001f)
        {
            return targetPosition;
        }

        float clampedOffset = Mathf.Clamp(highExitStartExtraOffset, 0f, Mathf.Max(0f, climbDistance - 0.001f));
        return targetPosition - (climbDirection / climbDistance) * clampedOffset;
    }

    private Vector3 ResolveLadderLoopEndPosition(
        Animator animator,
        LadderAnimationSet animationSet,
        Vector3 entryPosition,
        Vector3 targetPosition,
        bool exitsAtTop)
    {
        if (exitsAtTop)
        {
            return ResolvePositionBeforeTargetByDistance(
                entryPosition,
                targetPosition,
                climbSpeed * ladderEndToClimbLerpEndTime);
        }

        float endClipDuration = ResolveAnimationClipLength(
            animator,
            animationSet.EndClipReferenceName,
            ladderEndFallbackDuration);
        return ResolvePositionBeforeTargetByDistance(
            entryPosition,
            targetPosition,
            climbSpeed * startClimbSpeedMultiplier * endClipDuration);
    }

    private static Vector3 ResolvePositionBeforeTargetByDistance(Vector3 entryPosition, Vector3 targetPosition, float distanceBeforeTarget)
    {
        if (distanceBeforeTarget <= 0f)
        {
            return targetPosition;
        }

        Vector3 climbDirection = targetPosition - entryPosition;
        float climbDistance = climbDirection.magnitude;
        if (climbDistance <= 0.001f)
        {
            return targetPosition;
        }

        float clampedDistance = Mathf.Clamp(distanceBeforeTarget, 0f, Mathf.Max(0f, climbDistance - 0.001f));
        return targetPosition - (climbDirection / climbDistance) * clampedDistance;
    }

    private LadderAnimationSet ResolveLadderAnimationSet(bool exitsAtTop)
    {
        return exitsAtTop
            ? new LadderAnimationSet(
                ladderUpStartName,
                ladderUpLoopName,
                ladderUpEndName,
                true,
                ladderUpStartName,
                ladderUpEndName)
            : new LadderAnimationSet(
                ladderDownStartName,
                ladderDownLoopName,
                ladderDownEndName,
                false,
                ladderUpEndName,
                ladderUpStartName);
    }

    private void EnsureNavMeshLinks()
    {
        if (!autoCreateNavMeshLinks)
        {
            DisableManagedNavMeshLinks();
            return;
        }

        ResolvePointReferencesIfNeeded();

        Transform topEndpoint = ResolveNavMeshTopEndpoint();
        if (topEndpoint == null)
        {
            DisableManagedNavMeshLinks();
            return;
        }

        List<Transform> baseEndpoints = CollectNavMeshBaseEndpoints();
        if (baseEndpoints.Count == 0)
        {
            DisableManagedNavMeshLinks();
            return;
        }

        Transform linkRoot = GetOrCreateNavMeshLinkRoot();
        for (int i = 0; i < baseEndpoints.Count; i++)
        {
            NavMeshLink link = GetOrCreateManagedNavMeshLink(linkRoot, i);
            ConfigureNavMeshLink(link, baseEndpoints[i], topEndpoint);
        }

        for (int i = baseEndpoints.Count; i < managedNavMeshLinks.Count; i++)
        {
            if (managedNavMeshLinks[i] != null)
            {
                managedNavMeshLinks[i].activated = false;
            }
        }
    }

    private Transform ResolveNavMeshTopEndpoint()
    {
        if (ladderExit != null)
        {
            return ladderExit;
        }

        return ladderTop;
    }

    private List<Transform> CollectNavMeshBaseEndpoints()
    {
        List<Transform> endpoints = new List<Transform>();
        if (ladderBases != null)
        {
            for (int i = 0; i < ladderBases.Length; i++)
            {
                Transform ladderBase = ladderBases[i];
                if (ladderBase != null && !endpoints.Contains(ladderBase))
                {
                    endpoints.Add(ladderBase);
                }
            }
        }

        if (endpoints.Count == 0 && bottomExit != null)
        {
            endpoints.Add(bottomExit);
        }

        return endpoints;
    }

    private Transform GetOrCreateNavMeshLinkRoot()
    {
        const string rootName = "__LadderNavMeshLinks";
        Transform linkRoot = transform.Find(rootName);
        if (linkRoot != null)
        {
            return linkRoot;
        }

        GameObject rootObject = new GameObject(rootName);
        Transform rootTransform = rootObject.transform;
        rootTransform.SetParent(transform, false);
        rootTransform.localPosition = Vector3.zero;
        rootTransform.localRotation = Quaternion.identity;
        rootTransform.localScale = Vector3.one;
        return rootTransform;
    }

    private NavMeshLink GetOrCreateManagedNavMeshLink(Transform linkRoot, int index)
    {
        while (managedNavMeshLinks.Count <= index)
        {
            managedNavMeshLinks.Add(null);
        }

        NavMeshLink link = managedNavMeshLinks[index];
        if (link != null)
        {
            return link;
        }

        string linkName = $"LadderNavMeshLink_{index:00}";
        Transform existing = linkRoot != null ? linkRoot.Find(linkName) : null;
        if (existing == null)
        {
            GameObject linkObject = new GameObject(linkName);
            existing = linkObject.transform;
            existing.SetParent(linkRoot != null ? linkRoot : transform, false);
            existing.localPosition = Vector3.zero;
            existing.localRotation = Quaternion.identity;
            existing.localScale = Vector3.one;
        }

        link = existing.GetComponent<NavMeshLink>();
        if (link == null)
        {
            link = existing.gameObject.AddComponent<NavMeshLink>();
        }

        managedNavMeshLinks[index] = link;
        return link;
    }

    private void ConfigureNavMeshLink(NavMeshLink link, Transform start, Transform end)
    {
        if (link == null)
        {
            return;
        }

        if (start == null || end == null || start == end)
        {
            link.activated = false;
            return;
        }

        link.startTransform = start;
        link.endTransform = end;
        link.bidirectional = navMeshLinkBidirectional;
        link.width = navMeshLinkWidth;
        link.costModifier = navMeshLinkCostOverride;
        link.area = navMeshLinkArea;
        link.activated = true;
        link.UpdateLink();
    }

    private void DisableManagedNavMeshLinks()
    {
        for (int i = 0; i < managedNavMeshLinks.Count; i++)
        {
            if (managedNavMeshLinks[i] != null)
            {
                managedNavMeshLinks[i].activated = false;
            }
        }
    }

    private void TriggerOneShotAnimation(Animator animator, string animationName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(animationName))
        {
            return;
        }

        if (TrySetAnimatorTrigger(animator, animationName))
        {
            return;
        }

        CrossFadeStateIfAvailable(animator, animationName);
    }

    private void SetLoopAnimation(Animator animator, string ladderLoopName, bool active)
    {
        if (animator == null || string.IsNullOrWhiteSpace(ladderLoopName))
        {
            return;
        }

        if (HasAnimatorParameter(animator, ladderLoopName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(ladderLoopName, active);
            return;
        }

        if (!active)
        {
            return;
        }

        if (TrySetAnimatorTrigger(animator, ladderLoopName))
        {
            return;
        }

        CrossFadeStateIfAvailable(animator, ladderLoopName);
    }

    private bool TrySetAnimatorTrigger(Animator animator, string parameterName)
    {
        if (!HasAnimatorParameter(animator, parameterName, AnimatorControllerParameterType.Trigger))
        {
            return false;
        }

        animator.ResetTrigger(parameterName);
        animator.SetTrigger(parameterName);
        return true;
    }

    private bool CrossFadeStateIfAvailable(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName) || !IsLayerValid(animator))
        {
            return false;
        }

        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(animationLayer, shortHash))
        {
            animator.CrossFadeInFixedTime(stateName, crossFadeDuration, animationLayer, 0f);
            return true;
        }

        string layerPath = animator.GetLayerName(animationLayer) + "." + stateName;
        int fullHash = Animator.StringToHash(layerPath);
        if (!animator.HasState(animationLayer, fullHash))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(layerPath, crossFadeDuration, animationLayer, 0f);
        return true;
    }

    private bool HasAnimatorState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName) || !IsLayerValid(animator))
        {
            return false;
        }

        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(animationLayer, shortHash))
        {
            return true;
        }

        string layerPath = animator.GetLayerName(animationLayer) + "." + stateName;
        return animator.HasState(animationLayer, Animator.StringToHash(layerPath));
    }

    private bool IsCurrentAnimatorState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName) || !IsLayerValid(animator))
        {
            return false;
        }

        return MatchesAnimatorState(animator, animator.GetCurrentAnimatorStateInfo(animationLayer), stateName);
    }

    private float ResolveAnimationDuration(Animator animator, string animationName, float fallback)
    {
        float duration = Mathf.Max(0f, fallback);
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(animationName))
        {
            return duration;
        }

        if (TryResolveObservedAnimationDuration(animator, animationName, out float observedDuration))
        {
            return observedDuration;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null)
        {
            return duration;
        }

        string normalizedTarget = NormalizeName(animationName);
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            string normalizedClipName = NormalizeName(clip.name);
            if (!string.Equals(normalizedClipName, normalizedTarget, System.StringComparison.Ordinal) &&
                (normalizedTarget.Length <= 4 || !normalizedClipName.Contains(normalizedTarget)))
            {
                continue;
            }

            float animatorSpeed = Mathf.Abs(animator.speed) > 0.0001f ? Mathf.Abs(animator.speed) : 1f;
            return Mathf.Max(0f, clip.length / animatorSpeed);
        }

        return duration;
    }

    private float ResolveAnimationClipLength(Animator animator, string animationName, float fallback)
    {
        float duration = Mathf.Max(0f, fallback);
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(animationName))
        {
            return duration;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null)
        {
            return duration;
        }

        string normalizedTarget = NormalizeName(animationName);
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            string normalizedClipName = NormalizeName(clip.name);
            if (!string.Equals(normalizedClipName, normalizedTarget, System.StringComparison.Ordinal) &&
                (normalizedTarget.Length <= 4 || !normalizedClipName.Contains(normalizedTarget)))
            {
                continue;
            }

            return Mathf.Max(0f, clip.length);
        }

        return duration;
    }

    private bool TryResolveObservedAnimationDuration(Animator animator, string animationName, out float duration)
    {
        duration = 0f;
        if (!TryGetObservedAnimatorStateInfo(animator, animationName, out AnimatorStateInfo stateInfo, out _))
        {
            return false;
        }

        duration = ResolveAnimatorStateDuration(stateInfo, 0f);
        return duration > 0.0001f;
    }

    private float ResolveAnimatorStateDuration(AnimatorStateInfo stateInfo, float fallback)
    {
        if (stateInfo.length > 0.0001f)
        {
            return stateInfo.length;
        }

        return Mathf.Max(0f, fallback);
    }

    private static bool IsReverseAnimatorState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.speed < -0.0001f || stateInfo.speedMultiplier < -0.0001f;
    }

    private bool TryGetObservedAnimatorStateInfo(
        Animator animator,
        string stateName,
        out AnimatorStateInfo stateInfo,
        out bool isNextState)
    {
        stateInfo = default;
        isNextState = false;
        if (animator == null || string.IsNullOrWhiteSpace(stateName) || !IsLayerValid(animator))
        {
            return false;
        }

        if (animator.IsInTransition(animationLayer))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(animationLayer);
            if (MatchesAnimatorState(animator, nextState, stateName))
            {
                stateInfo = nextState;
                isNextState = true;
                return true;
            }
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(animationLayer);
        if (MatchesAnimatorState(animator, currentState, stateName))
        {
            stateInfo = currentState;
            return true;
        }

        return false;
    }

    private bool MatchesAnimatorState(Animator animator, AnimatorStateInfo stateInfo, string stateName)
    {
        if (stateInfo.shortNameHash == Animator.StringToHash(stateName))
        {
            return true;
        }

        if (!IsLayerValid(animator))
        {
            return false;
        }

        string layerPath = animator.GetLayerName(animationLayer) + "." + stateName;
        return stateInfo.fullPathHash == Animator.StringToHash(layerPath);
    }

    private bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter == null)
            {
                continue;
            }

            if (!string.Equals(parameter.name, parameterName, System.StringComparison.Ordinal))
            {
                continue;
            }

            return parameter.type == expectedType;
        }

        return false;
    }

    private bool IsLayerValid(Animator animator)
    {
        return animator != null && animationLayer >= 0 && animationLayer < animator.layerCount;
    }

    private void ResolvePointReferencesIfNeeded()
    {
        if (ladderBases == null || ladderBases.Length == 0)
        {
            ladderBases = FindMatchingChildren(BasePointNames);
        }

        if (ladderTop == null)
        {
            ladderTop = FindFirstMatchingChild(TopPointNames);
        }

        if (ladderExit == null)
        {
            ladderExit = FindFirstMatchingChild(HighExitPointNames);
        }

        if (bottomExit == null)
        {
            bottomExit = FindFirstMatchingChild(BottomExitPointNames);
        }
    }

    private bool TryResolveLadderRoute(Vector3 characterPosition, out LadderRoute route)
    {
        route = default;

        Transform bottomPoint = ResolveNearestBase(characterPosition);
        Transform topPoint = ladderTop;

        if (bottomPoint == null)
        {
            bottomPoint = transform;
        }

        if (topPoint == null)
        {
            Debug.LogWarning("LadderController: point haut ladder_top/H_Trigger introuvable.", this);
            return false;
        }

        bool useTopEntry = IsTopEntryCloser(characterPosition, bottomPoint, topPoint);
        if (useTopEntry)
        {
            route = new LadderRoute(
                topPoint,
                bottomPoint,
                bottomExit != null ? bottomExit : bottomPoint,
                false);
            return true;
        }

        route = new LadderRoute(
            bottomPoint,
            topPoint,
            ladderExit != null ? ladderExit : topPoint,
            true);
        return true;
    }

    private static bool IsTopEntryCloser(Vector3 characterPosition, Transform bottomPoint, Transform topPoint)
    {
        if (topPoint == null)
        {
            return false;
        }

        if (bottomPoint == null)
        {
            return true;
        }

        float bottomDistance = (bottomPoint.position - characterPosition).sqrMagnitude;
        float topDistance = (topPoint.position - characterPosition).sqrMagnitude;
        return topDistance < bottomDistance;
    }

    private Transform ResolveNearestBase(Vector3 characterPosition)
    {
        if (ladderBases == null || ladderBases.Length == 0)
        {
            return null;
        }

        Transform nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < ladderBases.Length; i++)
        {
            Transform candidate = ladderBases[i];
            if (candidate == null)
            {
                continue;
            }

            float distance = (candidate.position - characterPosition).sqrMagnitude;
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearest = candidate;
            nearestDistance = distance;
        }

        return nearest;
    }

    private Transform[] FindMatchingChildren(string[] aliases)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        List<Transform> matches = new List<Transform>();
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null || candidate == transform)
            {
                continue;
            }

            if (MatchesAlias(candidate.name, aliases))
            {
                matches.Add(candidate);
            }
        }

        return matches.ToArray();
    }

    private Transform FindFirstMatchingChild(string[] aliases)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null || candidate == transform)
            {
                continue;
            }

            if (MatchesAlias(candidate.name, aliases))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool MatchesAlias(string objectName, string[] aliases)
    {
        if (string.IsNullOrWhiteSpace(objectName) || aliases == null)
        {
            return false;
        }

        string normalizedName = NormalizeName(objectName);
        for (int i = 0; i < aliases.Length; i++)
        {
            string alias = aliases[i];
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            string normalizedAlias = NormalizeName(alias);
            if (normalizedName == normalizedAlias)
            {
                return true;
            }

            if (normalizedAlias.Length > 4 && normalizedName.Contains(normalizedAlias))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .ToLowerInvariant();
    }

    private static GameObject ResolveFallbackCharacter()
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled != null)
        {
            return controlled;
        }

        return SquadManager.Instance != null ? SquadManager.Instance.currentCharacter : null;
    }

    private static SquadCharacterController ResolveCharacterController(GameObject character)
    {
        if (character == null)
        {
            return null;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller != null)
        {
            return controller;
        }

        controller = character.GetComponentInChildren<SquadCharacterController>(true);
        if (controller != null)
        {
            return controller;
        }

        return character.GetComponentInParent<SquadCharacterController>();
    }

    private static LitOpsiveLocomotionBridge ResolveUccBridge(GameObject character, SquadCharacterController controller)
    {
        if (controller != null)
        {
            LitOpsiveLocomotionBridge bridge = controller.GetComponent<LitOpsiveLocomotionBridge>();
            if (bridge != null && bridge.CanDriveScriptedTraversal)
            {
                return bridge;
            }
        }

        if (character != null)
        {
            LitOpsiveLocomotionBridge bridge = character.GetComponent<LitOpsiveLocomotionBridge>();
            if (bridge != null && bridge.CanDriveScriptedTraversal)
            {
                return bridge;
            }

            bridge = character.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
            if (bridge != null && bridge.CanDriveScriptedTraversal)
            {
                return bridge;
            }

            bridge = character.GetComponentInParent<LitOpsiveLocomotionBridge>();
            if (bridge != null && bridge.CanDriveScriptedTraversal)
            {
                return bridge;
            }
        }

        return null;
    }

    private static StarterInspiredThirdPersonMotor ResolveStarterMotor(GameObject character)
    {
        if (character == null)
        {
            return null;
        }

        StarterInspiredThirdPersonMotor motor = character.GetComponent<StarterInspiredThirdPersonMotor>();
        if (motor != null)
        {
            return motor;
        }

        motor = character.GetComponentInChildren<StarterInspiredThirdPersonMotor>(true);
        if (motor != null)
        {
            return motor;
        }

        return character.GetComponentInParent<StarterInspiredThirdPersonMotor>();
    }

    private static Animator ResolveCharacterAnimator(GameObject character, SquadCharacterController controller)
    {
        Animator animator = controller != null ? controller.GetComponent<Animator>() : null;
        if (animator != null)
        {
            return animator;
        }

        animator = character != null ? character.GetComponent<Animator>() : null;
        if (animator != null)
        {
            return animator;
        }

        animator = character != null ? character.GetComponentInChildren<Animator>(true) : null;
        if (animator != null)
        {
            return animator;
        }

        return character != null ? character.GetComponentInParent<Animator>() : null;
    }

    private static Rigidbody ResolveCharacterRigidbody(GameObject character, SquadCharacterController controller)
    {
        Rigidbody body = controller != null ? controller.GetComponent<Rigidbody>() : null;
        if (body != null)
        {
            return body;
        }

        body = character != null ? character.GetComponent<Rigidbody>() : null;
        if (body != null)
        {
            return body;
        }

        body = character != null ? character.GetComponentInChildren<Rigidbody>(true) : null;
        if (body != null)
        {
            return body;
        }

        return character != null ? character.GetComponentInParent<Rigidbody>() : null;
    }

    private static float ResolveCharacterRootTopHeight(GameObject character, SquadCharacterController controller, Transform motionRoot)
    {
        if (motionRoot == null)
        {
            return 0f;
        }

        CapsuleCollider capsule = ResolveCharacterCapsule(character, controller, motionRoot);
        if (capsule != null)
        {
            return Mathf.Max(0f, capsule.bounds.max.y - motionRoot.position.y);
        }

        if (TryResolveCharacterBounds(character, controller, motionRoot, out Bounds bounds))
        {
            return Mathf.Max(0f, bounds.max.y - motionRoot.position.y);
        }

        return 0f;
    }

    private static CapsuleCollider ResolveCharacterCapsule(GameObject character, SquadCharacterController controller, Transform motionRoot)
    {
        CapsuleCollider capsule = controller != null ? controller.GetComponent<CapsuleCollider>() : null;
        if (IsUsableCharacterCollider(capsule))
        {
            return capsule;
        }

        capsule = motionRoot != null ? motionRoot.GetComponent<CapsuleCollider>() : null;
        if (IsUsableCharacterCollider(capsule))
        {
            return capsule;
        }

        capsule = character != null ? character.GetComponent<CapsuleCollider>() : null;
        if (IsUsableCharacterCollider(capsule))
        {
            return capsule;
        }

        CapsuleCollider[] capsules = character != null
            ? character.GetComponentsInChildren<CapsuleCollider>(true)
            : null;
        if (capsules == null)
        {
            return null;
        }

        for (int i = 0; i < capsules.Length; i++)
        {
            if (IsUsableCharacterCollider(capsules[i]))
            {
                return capsules[i];
            }
        }

        return null;
    }

    private static bool TryResolveCharacterBounds(
        GameObject character,
        SquadCharacterController controller,
        Transform motionRoot,
        out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Collider[] colliders = character != null ? character.GetComponentsInChildren<Collider>(true) : null;
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (!IsUsableCharacterCollider(collider))
                {
                    continue;
                }

                EncapsulateBounds(collider.bounds, ref bounds, ref hasBounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = character != null ? character.GetComponentsInChildren<Renderer>(true) : null;
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                EncapsulateBounds(renderer.bounds, ref bounds, ref hasBounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        if (controller != null)
        {
            bounds = new Bounds(controller.transform.position, Vector3.zero);
            return true;
        }

        if (motionRoot != null)
        {
            bounds = new Bounds(motionRoot.position, Vector3.zero);
            return true;
        }

        return false;
    }

    private static void EncapsulateBounds(Bounds candidate, ref Bounds bounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            bounds = candidate;
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(candidate);
    }

    private static bool IsUsableCharacterCollider(Collider collider)
    {
        return collider != null && collider.enabled && !collider.isTrigger;
    }

    private static Transform ResolveCharacterMotionRoot(
        GameObject character,
        LitOpsiveLocomotionBridge uccBridge,
        StarterInspiredThirdPersonMotor starterMotor,
        SquadCharacterController controller,
        Rigidbody body)
    {
        if (uccBridge != null)
        {
            return uccBridge.transform;
        }

        if (starterMotor != null)
        {
            return starterMotor.transform;
        }

        if (body != null)
        {
            return body.transform;
        }

        if (controller != null)
        {
            return controller.transform;
        }

        return character != null ? character.transform : null;
    }

    private static Vector3 ResolveMotionPosition(ScriptedMotionTarget motionTarget)
    {
        if (motionTarget.UccBridge != null)
        {
            return motionTarget.UccBridge.transform.position;
        }

        if (motionTarget.StarterMotor != null)
        {
            return motionTarget.StarterMotor.transform.position;
        }

        if (motionTarget.Body != null)
        {
            return motionTarget.Body.position;
        }

        return motionTarget.MotionRoot != null ? motionTarget.MotionRoot.position : Vector3.zero;
    }

    private static Quaternion ResolveMotionRotation(ScriptedMotionTarget motionTarget)
    {
        if (motionTarget.UccBridge != null)
        {
            return motionTarget.UccBridge.transform.rotation;
        }

        if (motionTarget.StarterMotor != null)
        {
            return motionTarget.StarterMotor.transform.rotation;
        }

        if (motionTarget.Body != null)
        {
            return motionTarget.Body.rotation;
        }

        return motionTarget.MotionRoot != null ? motionTarget.MotionRoot.rotation : Quaternion.identity;
    }

    private static void PrepareBodyForScriptedMotion(Rigidbody body)
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private readonly struct ScriptedMotionTarget
    {
        public ScriptedMotionTarget(
            LitOpsiveLocomotionBridge uccBridge,
            StarterInspiredThirdPersonMotor starterMotor,
            Transform motionRoot,
            Rigidbody body)
        {
            UccBridge = uccBridge;
            StarterMotor = starterMotor;
            MotionRoot = motionRoot;
            Body = body;
        }

        public LitOpsiveLocomotionBridge UccBridge { get; }
        public StarterInspiredThirdPersonMotor StarterMotor { get; }
        public Transform MotionRoot { get; }
        public Rigidbody Body { get; }
    }

    private readonly struct LadderRoute
    {
        public LadderRoute(Transform entryPoint, Transform targetPoint, Transform exitPoint, bool exitsAtTop)
        {
            EntryPoint = entryPoint;
            TargetPoint = targetPoint;
            ExitPoint = exitPoint;
            ExitsAtTop = exitsAtTop;
        }

        public Transform EntryPoint { get; }
        public Transform TargetPoint { get; }
        public Transform ExitPoint { get; }
        public bool ExitsAtTop { get; }
    }

    private readonly struct LadderAnimationSet
    {
        public LadderAnimationSet(
            string startName,
            string loopName,
            string endName,
            bool exitsAtTop,
            string startClipReferenceName,
            string endClipReferenceName)
        {
            StartName = startName;
            LoopName = loopName;
            EndName = endName;
            ExitsAtTop = exitsAtTop;
            StartClipReferenceName = startClipReferenceName;
            EndClipReferenceName = endClipReferenceName;
        }

        public string StartName { get; }
        public string LoopName { get; }
        public string EndName { get; }
        public bool ExitsAtTop { get; }
        public string StartClipReferenceName { get; }
        public string EndClipReferenceName { get; }
    }

    private readonly struct RigidbodyState
    {
        private readonly bool isKinematic;
        private readonly bool useGravity;
        private readonly CollisionDetectionMode collisionDetectionMode;
        private readonly RigidbodyInterpolation interpolation;

        public RigidbodyState(Rigidbody body)
        {
            isKinematic = body != null && body.isKinematic;
            useGravity = body != null && body.useGravity;
            collisionDetectionMode = body != null ? body.collisionDetectionMode : CollisionDetectionMode.Discrete;
            interpolation = body != null ? body.interpolation : RigidbodyInterpolation.None;
        }

        public void Restore(Rigidbody body)
        {
            if (body == null)
            {
                return;
            }

            body.isKinematic = isKinematic;
            body.useGravity = useGravity;
            body.collisionDetectionMode = collisionDetectionMode;
            body.interpolation = interpolation;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }
}
