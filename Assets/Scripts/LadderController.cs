using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class LadderController : MonoBehaviour
{
    private const float MaxStartToLoopLeadRatio = 0.4f;
    private const string AutoAnchorRootName = "__LadderAutoRoute";

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

    [Header("Auto Geometry")]
    [SerializeField, Tooltip("Construit automatiquement les points bas/haut depuis les colliders/renderers enfants quand les points explicites manquent.")]
    private bool autoBuildRouteFromGeometry = true;
    [SerializeField, Tooltip("Axe local de l'echelle. Laisse Vector3.up pour les echelles verticales classiques.")]
    private Vector3 localClimbAxis = Vector3.up;
    [SerializeField, Tooltip("Direction locale du cote accessible de l'echelle.")]
    private Vector3 localApproachNormal = Vector3.back;
    [SerializeField, Min(0f), Tooltip("Distance du personnage devant la surface de l'echelle pendant la grimpe.")]
    private float climbSurfaceOffset = 0.35f;
    [SerializeField, Min(0f), Tooltip("Petit retrait applique aux extremites automatiques pour eviter de placer le personnage exactement au bord du mesh.")]
    private float endpointInset = 0.08f;
    [SerializeField, Min(0.1f), Tooltip("Hauteur minimale necessaire pour accepter une echelle auto-detectee.")]
    private float minimumAutoHeight = 0.75f;
    [SerializeField, Min(0f), Tooltip("Distance de sortie en haut, dans la direction accessible.")]
    private float topExitForwardOffset = 0.85f;
    [SerializeField, Min(0f), Tooltip("Offset vertical ajoute a la sortie haute.")]
    private float topExitUpOffset = 0.05f;
    [SerializeField, Min(0f), Tooltip("Distance de sortie en bas, dans la direction accessible.")]
    private float bottomExitForwardOffset = 0.65f;
    [SerializeField, Tooltip("Inclut les colliders trigger dans le calcul automatique de l'echelle.")]
    private bool includeTriggerCollidersInAutoGeometry;

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
    private Transform autoAnchorRoot;
    private Transform autoBottomPoint;
    private Transform autoTopPoint;
    private Transform autoTopExitPoint;
    private Transform autoBottomExitPoint;

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
        if (localClimbAxis.sqrMagnitude <= 0.0001f)
        {
            localClimbAxis = Vector3.up;
        }

        if (localApproachNormal.sqrMagnitude <= 0.0001f)
        {
            localApproachNormal = Vector3.back;
        }

        climbSurfaceOffset = Mathf.Max(0f, climbSurfaceOffset);
        endpointInset = Mathf.Max(0f, endpointInset);
        minimumAutoHeight = Mathf.Max(0.1f, minimumAutoHeight);
        topExitForwardOffset = Mathf.Max(0f, topExitForwardOffset);
        topExitUpOffset = Mathf.Max(0f, topExitUpOffset);
        bottomExitForwardOffset = Mathf.Max(0f, bottomExitForwardOffset);
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
        if (controller != null && uccBridge == null)
        {
            return false;
        }

        Animator animator = ResolveCharacterAnimator(character, controller);
        Rigidbody body = uccBridge != null || controller != null ? null : ResolveCharacterRigidbody(character, controller);
        Transform motionRoot = ResolveCharacterMotionRoot(character, uccBridge, controller, body);
        if (motionRoot == null)
        {
            return false;
        }
        ScriptedMotionTarget motionTarget = new ScriptedMotionTarget(uccBridge, motionRoot, body);

        LadderAnimationSet animationSet = ResolveLadderAnimationSet(route.ExitsAtTop);
        Vector3 ladderEndStartPosition = ResolveLadderEndStartPosition(
            character,
            controller,
            motionRoot,
            route.EntryPosition,
            route.TargetPosition,
            route.ExitsAtTop);
        Vector3 ladderLoopEndPosition = ResolveLadderLoopEndPosition(
            animator,
            animationSet,
            route.EntryPosition,
            ladderEndStartPosition,
            route.ExitsAtTop);
        Quaternion climbRotation = keepEntryRotationDuringClimb
            ? route.EntryRotation
            : route.TargetRotation;

        activeRoutine = StartCoroutine(UseLadderRoutine(
            controller,
            animator,
            motionTarget,
            route.EntryPosition,
            route.EntryRotation,
            ladderLoopEndPosition,
            ladderEndStartPosition,
            climbRotation,
            route.ExitPosition,
            route.ExitRotation,
            animationSet,
            driveMotion));
        return true;
    }

    private IEnumerator UseLadderRoutine(
        SquadCharacterController controller,
        Animator animator,
        ScriptedMotionTarget motionTarget,
        Vector3 entryPosition,
        Quaternion entryRotation,
        Vector3 ladderLoopEndPosition,
        Vector3 ladderEndStartPosition,
        Quaternion ladderEndStartRotation,
        Vector3 exitPosition,
        Quaternion exitRotation,
        LadderAnimationSet animationSet,
        bool driveMotion)
    {
        bool inputSuppressed = false;
        bool uccPrepared = false;
        bool bodyPrepared = false;
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

            if (controller != null)
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
                yield return MoveToPoint(motionTarget, entryPosition, entryRotation, approachDuration);
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
                    Vector3.Distance(entryPosition, ladderLoopEndPosition),
                    climbSpeed * ladderEndToClimbLerpEndTime);
            }

            yield return RunClimbPhase(
                animator,
                animationSet.StartName,
                animationSet.LoopName,
                motionTarget,
                entryPosition,
                entryRotation,
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
                    exitPosition,
                    exitRotation,
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
                    yield return MoveToPoint(motionTarget, exitPosition, exitRotation, exitMoveDuration);
                }
                else
                {
                    yield return WaitForFixedDuration(exitMoveDuration);
                }

                exitMoveEndTime = endDuration + exitMoveDuration;
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
        if (!TryResolveLadderEndpoints(out LadderEndpoints endpoints))
        {
            return null;
        }

        return endpoints.TopExitPoint;
    }

    private List<Transform> CollectNavMeshBaseEndpoints()
    {
        List<Transform> endpoints = new List<Transform>();
        if (TryResolveLadderEndpoints(out LadderEndpoints ladderEndpoints) && ladderEndpoints.BottomExitPoint != null)
        {
            endpoints.Add(ladderEndpoints.BottomExitPoint);
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
        if (!TryResolveLadderEndpoints(characterPosition, out LadderEndpoints endpoints))
        {
            Debug.LogWarning("LadderController: aucun trajet d'echelle valide. Ajoute des points B_Trigger/H_Trigger ou des colliders/renderers d'echelle.", this);
            return false;
        }

        Transform routeBottomPoint = ResolveExplicitBottomPoint(characterPosition, useNearest: true);
        if (routeBottomPoint == null)
        {
            routeBottomPoint = endpoints.BottomPoint;
        }

        Vector3 climbAxis = ResolveRouteClimbAxis(routeBottomPoint, endpoints.TopPoint);
        Vector3 approachNormal = ResolveRouteApproachNormal(
            climbAxis,
            routeBottomPoint,
            endpoints.TopPoint,
            endpoints.BottomExitPoint,
            endpoints.TopExitPoint,
            characterPosition);
        Quaternion climbRotation = ResolveSafeLookRotation(-approachNormal, climbAxis, transform.rotation);
        Quaternion bottomExitRotation = ResolveExitRotation(endpoints.BottomExitPoint, routeBottomPoint, approachNormal, climbAxis, climbRotation);
        Quaternion topExitRotation = ResolveExitRotation(endpoints.TopExitPoint, endpoints.TopPoint, approachNormal, climbAxis, climbRotation);

        bool useTopEntry = ShouldUseTopEntry(characterPosition, routeBottomPoint, endpoints.TopPoint);
        if (useTopEntry)
        {
            route = new LadderRoute(
                endpoints.TopPoint.position,
                climbRotation,
                routeBottomPoint.position,
                climbRotation,
                endpoints.BottomExitPoint.position,
                bottomExitRotation,
                false);
            return true;
        }

        route = new LadderRoute(
            routeBottomPoint.position,
            climbRotation,
            endpoints.TopPoint.position,
            climbRotation,
            endpoints.TopExitPoint.position,
            topExitRotation,
            true);
        return true;
    }

    private bool TryResolveLadderEndpoints(out LadderEndpoints endpoints)
    {
        return TryResolveLadderEndpoints(null, out endpoints);
    }

    private bool TryResolveLadderEndpoints(Vector3? characterPosition, out LadderEndpoints endpoints)
    {
        endpoints = default;
        ResolvePointReferencesIfNeeded();

        AutoLadderGeometry geometry = default;
        bool hasAutoGeometry = autoBuildRouteFromGeometry && TryResolveAutoLadderGeometry(characterPosition, out geometry);
        Transform bottomPoint = ResolveExplicitBottomPoint(Vector3.zero, useNearest: false);
        Transform topPoint = ladderTop;
        Transform topExitPoint = ladderExit;
        Transform bottomExitPoint = bottomExit;

        if (hasAutoGeometry)
        {
            UpdateAutoRouteAnchors(geometry);
            if (bottomPoint == null)
            {
                bottomPoint = autoBottomPoint;
            }

            if (topPoint == null)
            {
                topPoint = autoTopPoint;
            }

            if (topExitPoint == null)
            {
                topExitPoint = autoTopExitPoint;
            }

            if (bottomExitPoint == null)
            {
                bottomExitPoint = autoBottomExitPoint;
            }
        }

        if (bottomPoint == null)
        {
            bottomPoint = transform;
        }

        if (topPoint == null)
        {
            return false;
        }

        if (topExitPoint == null)
        {
            topExitPoint = topPoint;
        }

        if (bottomExitPoint == null)
        {
            bottomExitPoint = bottomPoint;
        }

        endpoints = new LadderEndpoints(bottomPoint, topPoint, bottomExitPoint, topExitPoint);
        return true;
    }

    private Transform ResolveExplicitBottomPoint(Vector3 characterPosition, bool useNearest)
    {
        if (ladderBases == null || ladderBases.Length == 0)
        {
            return null;
        }

        Transform fallback = null;
        Transform nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < ladderBases.Length; i++)
        {
            Transform candidate = ladderBases[i];
            if (candidate == null)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }

            if (!useNearest)
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

        return useNearest && nearest != null ? nearest : fallback;
    }

    private bool TryResolveAutoLadderGeometry(Vector3? characterPosition, out AutoLadderGeometry geometry)
    {
        geometry = default;

        Vector3 axis = ResolveWorldClimbAxis();
        Vector3 normal = ResolveWorldApproachNormal(axis);
        Vector3 side = Vector3.Cross(axis, normal);
        if (side.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        side.Normalize();
        normal = Vector3.Cross(side, axis).normalized;

        float minAxis = float.PositiveInfinity;
        float maxAxis = float.NegativeInfinity;
        float minSide = float.PositiveInfinity;
        float maxSide = float.NegativeInfinity;
        float minNormal = float.PositiveInfinity;
        float maxNormal = float.NegativeInfinity;
        bool hasBounds = false;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate == null || !candidate.enabled)
            {
                continue;
            }

            if (candidate.isTrigger && !includeTriggerCollidersInAutoGeometry)
            {
                continue;
            }

            if (IsRuntimeGeneratedTransform(candidate.transform))
            {
                continue;
            }

            EncapsulateProjectedBounds(
                candidate.bounds,
                axis,
                side,
                normal,
                ref minAxis,
                ref maxAxis,
                ref minSide,
                ref maxSide,
                ref minNormal,
                ref maxNormal,
                ref hasBounds);
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer candidate = renderers[i];
            if (candidate == null || !candidate.enabled || IsRuntimeGeneratedTransform(candidate.transform))
            {
                continue;
            }

            EncapsulateProjectedBounds(
                candidate.bounds,
                axis,
                side,
                normal,
                ref minAxis,
                ref maxAxis,
                ref minSide,
                ref maxSide,
                ref minNormal,
                ref maxNormal,
                ref hasBounds);
        }

        if (!hasBounds)
        {
            return false;
        }

        float height = maxAxis - minAxis;
        if (height < minimumAutoHeight)
        {
            return false;
        }

        float inset = Mathf.Min(endpointInset, Mathf.Max(0f, height * 0.45f));
        float centerSide = (minSide + maxSide) * 0.5f;
        float centerNormal = (minNormal + maxNormal) * 0.5f;
        bool useNegativeNormalSide = characterPosition.HasValue &&
            Vector3.Dot(characterPosition.Value, normal) < centerNormal;
        float frontNormal = useNegativeNormalSide
            ? minNormal - climbSurfaceOffset
            : maxNormal + climbSurfaceOffset;
        Vector3 approachNormal = useNegativeNormalSide ? -normal : normal;
        Vector3 basePoint = side * centerSide + normal * frontNormal;
        Vector3 bottomPosition = basePoint + axis * (minAxis + inset);
        Vector3 topPosition = basePoint + axis * (maxAxis - inset);
        Quaternion climbRotation = ResolveSafeLookRotation(-approachNormal, axis, transform.rotation);

        geometry = new AutoLadderGeometry(
            bottomPosition,
            topPosition,
            bottomPosition + approachNormal * bottomExitForwardOffset,
            topPosition + approachNormal * topExitForwardOffset + axis * topExitUpOffset,
            climbRotation);
        return true;
    }

    private Vector3 ResolveWorldClimbAxis()
    {
        Vector3 axis = transform.TransformDirection(localClimbAxis);
        if (axis.sqrMagnitude <= 0.0001f)
        {
            axis = Vector3.up;
        }

        return axis.normalized;
    }

    private Vector3 ResolveWorldApproachNormal(Vector3 axis)
    {
        Vector3 normal = transform.TransformDirection(localApproachNormal);
        normal = Vector3.ProjectOnPlane(normal, axis);
        if (normal.sqrMagnitude > 0.0001f)
        {
            return normal.normalized;
        }

        normal = Vector3.ProjectOnPlane(transform.forward, axis);
        if (normal.sqrMagnitude > 0.0001f)
        {
            return normal.normalized;
        }

        normal = Vector3.Cross(axis, transform.right);
        if (normal.sqrMagnitude > 0.0001f)
        {
            return normal.normalized;
        }

        return Vector3.forward;
    }

    private static void EncapsulateProjectedBounds(
        Bounds bounds,
        Vector3 axis,
        Vector3 side,
        Vector3 normal,
        ref float minAxis,
        ref float maxAxis,
        ref float minSide,
        ref float maxSide,
        ref float minNormal,
        ref float maxNormal,
        ref bool hasBounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 point = center + new Vector3(extents.x * x, extents.y * y, extents.z * z);
                    float axisProjection = Vector3.Dot(point, axis);
                    float sideProjection = Vector3.Dot(point, side);
                    float normalProjection = Vector3.Dot(point, normal);
                    minAxis = Mathf.Min(minAxis, axisProjection);
                    maxAxis = Mathf.Max(maxAxis, axisProjection);
                    minSide = Mathf.Min(minSide, sideProjection);
                    maxSide = Mathf.Max(maxSide, sideProjection);
                    minNormal = Mathf.Min(minNormal, normalProjection);
                    maxNormal = Mathf.Max(maxNormal, normalProjection);
                    hasBounds = true;
                }
            }
        }
    }

    private void UpdateAutoRouteAnchors(AutoLadderGeometry geometry)
    {
        autoBottomPoint = GetOrCreateAutoRouteAnchor(autoBottomPoint, "Auto_B_Trigger");
        autoTopPoint = GetOrCreateAutoRouteAnchor(autoTopPoint, "Auto_H_Trigger");
        autoBottomExitPoint = GetOrCreateAutoRouteAnchor(autoBottomExitPoint, "Auto_B_Exit");
        autoTopExitPoint = GetOrCreateAutoRouteAnchor(autoTopExitPoint, "Auto_H_Exit");

        ApplyAutoRouteAnchor(autoBottomPoint, geometry.BottomPoint, geometry.ClimbRotation);
        ApplyAutoRouteAnchor(autoTopPoint, geometry.TopPoint, geometry.ClimbRotation);
        ApplyAutoRouteAnchor(autoBottomExitPoint, geometry.BottomExitPoint, geometry.ClimbRotation);
        ApplyAutoRouteAnchor(autoTopExitPoint, geometry.TopExitPoint, geometry.ClimbRotation);
    }

    private Transform GetOrCreateAutoRouteAnchor(Transform current, string anchorName)
    {
        if (current != null)
        {
            return current;
        }

        Transform root = GetOrCreateAutoRouteAnchorRoot();
        Transform existing = root.Find(anchorName);
        if (existing != null)
        {
            return existing;
        }

        GameObject anchorObject = new GameObject(anchorName);
        Transform anchor = anchorObject.transform;
        anchor.SetParent(root, false);
        anchor.localPosition = Vector3.zero;
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
        return anchor;
    }

    private Transform GetOrCreateAutoRouteAnchorRoot()
    {
        if (autoAnchorRoot != null)
        {
            return autoAnchorRoot;
        }

        Transform existing = transform.Find(AutoAnchorRootName);
        if (existing != null)
        {
            autoAnchorRoot = existing;
            return autoAnchorRoot;
        }

        GameObject rootObject = new GameObject(AutoAnchorRootName);
        autoAnchorRoot = rootObject.transform;
        autoAnchorRoot.SetParent(transform, false);
        autoAnchorRoot.localPosition = Vector3.zero;
        autoAnchorRoot.localRotation = Quaternion.identity;
        autoAnchorRoot.localScale = Vector3.one;
        return autoAnchorRoot;
    }

    private static void ApplyAutoRouteAnchor(Transform anchor, Vector3 position, Quaternion rotation)
    {
        if (anchor != null)
        {
            anchor.SetPositionAndRotation(position, rotation);
        }
    }

    private bool IsRuntimeGeneratedTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform current = candidate;
        while (current != null && current != transform)
        {
            if (current == autoAnchorRoot || current.name == AutoAnchorRootName || current.name == "__LadderNavMeshLinks")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private Vector3 ResolveRouteClimbAxis(Transform bottomPoint, Transform topPoint)
    {
        if (bottomPoint != null && topPoint != null)
        {
            Vector3 routeAxis = topPoint.position - bottomPoint.position;
            if (routeAxis.sqrMagnitude > 0.0001f)
            {
                return routeAxis.normalized;
            }
        }

        return ResolveWorldClimbAxis();
    }

    private Vector3 ResolveRouteApproachNormal(
        Vector3 climbAxis,
        Transform bottomPoint,
        Transform topPoint,
        Transform bottomExitPoint,
        Transform topExitPoint,
        Vector3 characterPosition)
    {
        Vector3 approachNormal = Vector3.zero;
        if (TryResolveExitOffsetNormal(bottomPoint, bottomExitPoint, climbAxis, out Vector3 bottomNormal))
        {
            approachNormal += bottomNormal;
        }

        if (TryResolveExitOffsetNormal(topPoint, topExitPoint, climbAxis, out Vector3 topNormal))
        {
            approachNormal += topNormal;
        }

        if (approachNormal.sqrMagnitude > 0.0001f)
        {
            return approachNormal.normalized;
        }

        approachNormal = ResolveWorldApproachNormal(climbAxis);
        Vector3 center = ResolveRouteCenter(bottomPoint, topPoint);
        Vector3 characterOffset = Vector3.ProjectOnPlane(characterPosition - center, climbAxis);
        if (characterOffset.sqrMagnitude > 0.0001f && Vector3.Dot(characterOffset, approachNormal) < 0f)
        {
            approachNormal = -approachNormal;
        }

        return approachNormal;
    }

    private static bool TryResolveExitOffsetNormal(
        Transform ladderPoint,
        Transform exitPoint,
        Vector3 climbAxis,
        out Vector3 normal)
    {
        normal = Vector3.zero;
        if (ladderPoint == null || exitPoint == null)
        {
            return false;
        }

        Vector3 offset = Vector3.ProjectOnPlane(exitPoint.position - ladderPoint.position, climbAxis);
        if (offset.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        normal = offset.normalized;
        return true;
    }

    private static Vector3 ResolveRouteCenter(Transform bottomPoint, Transform topPoint)
    {
        if (bottomPoint != null && topPoint != null)
        {
            return (bottomPoint.position + topPoint.position) * 0.5f;
        }

        if (topPoint != null)
        {
            return topPoint.position;
        }

        return bottomPoint != null ? bottomPoint.position : Vector3.zero;
    }

    private static Quaternion ResolveExitRotation(
        Transform exitPoint,
        Transform ladderPoint,
        Vector3 approachNormal,
        Vector3 climbAxis,
        Quaternion fallback)
    {
        if (TryResolveExitOffsetNormal(ladderPoint, exitPoint, climbAxis, out Vector3 exitDirection))
        {
            return ResolveSafeLookRotation(exitDirection, climbAxis, fallback);
        }

        return ResolveSafeLookRotation(-approachNormal, climbAxis, fallback);
    }

    private static Quaternion ResolveSafeLookRotation(Vector3 forward, Vector3 up, Quaternion fallback)
    {
        if (up.sqrMagnitude <= 0.0001f)
        {
            up = Vector3.up;
        }

        up.Normalize();
        forward = Vector3.ProjectOnPlane(forward, up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(fallback * Vector3.forward, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            return fallback;
        }

        return Quaternion.LookRotation(forward.normalized, up);
    }

    private static bool ShouldUseTopEntry(Vector3 characterPosition, Transform bottomPoint, Transform topPoint)
    {
        if (topPoint == null)
        {
            return false;
        }

        if (bottomPoint == null)
        {
            return true;
        }

        Vector3 axis = topPoint.position - bottomPoint.position;
        float height = axis.magnitude;
        if (height <= 0.001f)
        {
            return IsTopEntryCloser(characterPosition, bottomPoint, topPoint);
        }

        Vector3 axisDirection = axis / height;
        float characterHeight = Vector3.Dot(characterPosition - bottomPoint.position, axisDirection);
        float midpoint = height * 0.5f;
        float deadZone = Mathf.Min(0.25f, height * 0.1f);
        if (Mathf.Abs(characterHeight - midpoint) > deadZone)
        {
            return characterHeight > midpoint;
        }

        return IsTopEntryCloser(characterPosition, bottomPoint, topPoint);
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
        SquadCharacterController controller,
        Rigidbody body)
    {
        if (uccBridge != null)
        {
            return uccBridge.transform;
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
            Transform motionRoot,
            Rigidbody body)
        {
            UccBridge = uccBridge;
            MotionRoot = motionRoot;
            Body = body;
        }

        public LitOpsiveLocomotionBridge UccBridge { get; }
        public Transform MotionRoot { get; }
        public Rigidbody Body { get; }
    }

    private readonly struct LadderEndpoints
    {
        public LadderEndpoints(Transform bottomPoint, Transform topPoint, Transform bottomExitPoint, Transform topExitPoint)
        {
            BottomPoint = bottomPoint;
            TopPoint = topPoint;
            BottomExitPoint = bottomExitPoint;
            TopExitPoint = topExitPoint;
        }

        public Transform BottomPoint { get; }
        public Transform TopPoint { get; }
        public Transform BottomExitPoint { get; }
        public Transform TopExitPoint { get; }
    }

    private readonly struct AutoLadderGeometry
    {
        public AutoLadderGeometry(
            Vector3 bottomPoint,
            Vector3 topPoint,
            Vector3 bottomExitPoint,
            Vector3 topExitPoint,
            Quaternion climbRotation)
        {
            BottomPoint = bottomPoint;
            TopPoint = topPoint;
            BottomExitPoint = bottomExitPoint;
            TopExitPoint = topExitPoint;
            ClimbRotation = climbRotation;
        }

        public Vector3 BottomPoint { get; }
        public Vector3 TopPoint { get; }
        public Vector3 BottomExitPoint { get; }
        public Vector3 TopExitPoint { get; }
        public Quaternion ClimbRotation { get; }
    }

    private readonly struct LadderRoute
    {
        public LadderRoute(
            Vector3 entryPosition,
            Quaternion entryRotation,
            Vector3 targetPosition,
            Quaternion targetRotation,
            Vector3 exitPosition,
            Quaternion exitRotation,
            bool exitsAtTop)
        {
            EntryPosition = entryPosition;
            EntryRotation = entryRotation;
            TargetPosition = targetPosition;
            TargetRotation = targetRotation;
            ExitPosition = exitPosition;
            ExitRotation = exitRotation;
            ExitsAtTop = exitsAtTop;
        }

        public Vector3 EntryPosition { get; }
        public Quaternion EntryRotation { get; }
        public Vector3 TargetPosition { get; }
        public Quaternion TargetRotation { get; }
        public Vector3 ExitPosition { get; }
        public Quaternion ExitRotation { get; }
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
