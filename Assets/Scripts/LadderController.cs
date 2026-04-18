using System.Collections;
using System.Collections.Generic;
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
    [SerializeField, Tooltip("Duree minimale de la phase Ladder_Loop apres Ladder_Start.")]
    private float minimumLoopDuration = 0.1f;
    [SerializeField, Tooltip("Duree du deplacement vers le point de sortie pendant Ladder_End.")]
    private float exitDuration = 0.45f;

    [Header("Root Alignment")]
    [SerializeField, Tooltip("Compense le root aux pieds: en sortie haute, Ladder_End commence avant que les pieds atteignent le H_Trigger.")]
    private bool offsetHighExitStartByCharacterHeight = true;
    [SerializeField, Tooltip("Part de la hauteur du personnage utilisee pour reculer le debut de Ladder_End sur l'echelle.")]
    private float highExitStartHeightMultiplier = 1f;
    [SerializeField, Tooltip("Offset manuel ajoute au recul du debut de Ladder_End.")]
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

    private Coroutine activeRoutine;

    public bool IsBusy => activeRoutine != null;

    private void Awake()
    {
        ResolvePointReferencesIfNeeded();
    }

    private void OnValidate()
    {
        approachDuration = Mathf.Max(0f, approachDuration);
        climbSpeed = Mathf.Max(0.01f, climbSpeed);
        minimumLoopDuration = Mathf.Max(0f, minimumLoopDuration);
        exitDuration = Mathf.Max(0f, exitDuration);
        highExitStartHeightMultiplier = Mathf.Max(0f, highExitStartHeightMultiplier);
        highExitStartExtraOffset = Mathf.Max(0f, highExitStartExtraOffset);
        animationLayer = Mathf.Max(0, animationLayer);
        ladderStartFallbackDuration = Mathf.Max(0f, ladderStartFallbackDuration);
        ladderEndFallbackDuration = Mathf.Max(0f, ladderEndFallbackDuration);
        crossFadeDuration = Mathf.Max(0f, crossFadeDuration);
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
        Animator animator = ResolveCharacterAnimator(character, controller);
        Rigidbody body = ResolveCharacterRigidbody(character, controller);
        Transform motionRoot = ResolveCharacterMotionRoot(character, controller, body);
        if (motionRoot == null)
        {
            return false;
        }

        Vector3 ladderEndStartPosition = ResolveLadderEndStartPosition(
            character,
            controller,
            motionRoot,
            route.EntryPoint.position,
            route.TargetPoint.position,
            route.ExitsAtTop);
        LadderAnimationSet animationSet = ResolveLadderAnimationSet(route.ExitsAtTop);

        activeRoutine = StartCoroutine(UseLadderRoutine(
            controller,
            animator,
            body,
            motionRoot,
            route.EntryPoint,
            ladderEndStartPosition,
            route.TargetPoint.rotation,
            route.ExitPoint,
            animationSet,
            driveMotion));
        return true;
    }

    private IEnumerator UseLadderRoutine(
        SquadCharacterController controller,
        Animator animator,
        Rigidbody body,
        Transform motionRoot,
        Transform entryPoint,
        Vector3 ladderEndStartPosition,
        Quaternion ladderEndStartRotation,
        Transform exitPoint,
        LadderAnimationSet animationSet,
        bool driveMotion)
    {
        bool inputSuppressed = false;
        bool bodyPrepared = false;
        RigidbodyState bodyState = default;

        try
        {
            if (controller != null)
            {
                controller.PushScriptedMovementSuppression();
                inputSuppressed = true;
            }

            if (driveMotion && body != null)
            {
                bodyState = new RigidbodyState(body);
                PrepareBodyForScriptedMotion(body);
                bodyPrepared = true;
            }

            if (driveMotion)
            {
                yield return MoveToPoint(motionRoot, body, entryPoint.position, entryPoint.rotation, approachDuration);
            }
            else
            {
                yield return WaitForFixedDuration(approachDuration);
            }

            TriggerOneShotAnimation(animator, animationSet.StartName);

            float startDuration = ResolveAnimationDuration(animator, animationSet.StartName, ladderStartFallbackDuration);
            float climbDuration = ResolveClimbDuration(entryPoint.position, ladderEndStartPosition, startDuration);
            yield return RunClimbPhase(
                animator,
                animationSet.StartName,
                animationSet.LoopName,
                motionRoot,
                body,
                entryPoint.position,
                entryPoint.rotation,
                ladderEndStartPosition,
                ladderEndStartRotation,
                startDuration,
                climbDuration,
                driveMotion);

            SetLoopAnimation(animator, animationSet.LoopName, false);
            TriggerOneShotAnimation(animator, animationSet.EndName);

            float endDuration = ResolveAnimationDuration(animator, animationSet.EndName, ladderEndFallbackDuration);
            float exitMoveDuration = Mathf.Max(0f, exitDuration);
            if (driveMotion)
            {
                yield return MoveToPoint(motionRoot, body, exitPoint.position, exitPoint.rotation, exitMoveDuration);
            }
            else
            {
                yield return WaitForFixedDuration(exitMoveDuration);
            }

            if (bodyPrepared)
            {
                bodyState.Restore(body);
                bodyPrepared = false;
            }

            if (inputSuppressed && controller != null)
            {
                controller.PopScriptedMovementSuppression();
                inputSuppressed = false;
            }

            endDuration = ResolveAnimationDuration(animator, animationSet.EndName, endDuration);
            float remainingEndDuration = Mathf.Max(0f, endDuration - exitMoveDuration);
            if (remainingEndDuration > 0f)
            {
                yield return WaitForFixedDuration(remainingEndDuration);
            }
        }
        finally
        {
            SetLoopAnimation(animator, animationSet.LoopName, false);

            if (bodyPrepared)
            {
                bodyState.Restore(body);
            }

            if (inputSuppressed && controller != null)
            {
                controller.PopScriptedMovementSuppression();
            }

            activeRoutine = null;
        }
    }

    private IEnumerator RunClimbPhase(
        Animator animator,
        string ladderStartName,
        string ladderLoopName,
        Transform motionRoot,
        Rigidbody body,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        float startDuration,
        float climbDuration,
        bool driveMotion)
    {
        bool loopStarted = false;
        float duration = Mathf.Max(0.001f, climbDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!loopStarted && ShouldStartLoopAnimation(animator, ladderStartName, elapsed, startDuration))
            {
                SetLoopAnimation(animator, ladderLoopName, true);
                loopStarted = true;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            if (driveMotion)
            {
                ApplyPose(
                    motionRoot,
                    body,
                    Vector3.Lerp(startPosition, endPosition, t),
                    Quaternion.Slerp(startRotation, endRotation, t));
            }

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        if (!loopStarted)
        {
            SetLoopAnimation(animator, ladderLoopName, true);
        }

        if (driveMotion)
        {
            ApplyPose(motionRoot, body, endPosition, endRotation);
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
            float normalizedThreshold = Mathf.Clamp01(1f - (leadTime / stateDuration));
            return stateInfo.normalizedTime >= normalizedThreshold ||
                   elapsed >= Mathf.Max(0f, stateDuration - leadTime);
        }

        return elapsed >= Mathf.Max(0f, fallbackStartDuration - crossFadeDuration);
    }

    private IEnumerator MoveToPoint(Transform motionRoot, Rigidbody body, Vector3 targetPosition, Quaternion targetRotation, float duration)
    {
        Vector3 startPosition = body != null ? body.position : motionRoot.position;
        Quaternion startRotation = body != null ? body.rotation : motionRoot.rotation;
        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f)
        {
            ApplyPose(motionRoot, body, targetPosition, targetRotation);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            float t = Mathf.Clamp01(elapsed / clampedDuration);
            ApplyPose(
                motionRoot,
                body,
                Vector3.Lerp(startPosition, targetPosition, t),
                Quaternion.Slerp(startRotation, targetRotation, t));

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        ApplyPose(motionRoot, body, targetPosition, targetRotation);
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

    private void ApplyPose(Transform motionRoot, Rigidbody body, Vector3 position, Quaternion rotation)
    {
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
            return;
        }

        motionRoot.SetPositionAndRotation(position, rotation);
    }

    private float ResolveClimbDuration(Vector3 basePosition, Vector3 topPosition, float startDuration)
    {
        float distanceDuration = Vector3.Distance(basePosition, topPosition) / Mathf.Max(0.01f, climbSpeed);
        return Mathf.Max(0.001f, distanceDuration, startDuration + minimumLoopDuration);
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

        float characterHeight = ResolveCharacterRootTopHeight(character, controller, motionRoot);
        float heightOffset = (characterHeight * highExitStartHeightMultiplier) + highExitStartExtraOffset;
        float clampedOffset = Mathf.Clamp(heightOffset, 0f, Mathf.Max(0f, climbDistance - 0.001f));
        return targetPosition - (climbDirection / climbDistance) * clampedOffset;
    }

    private LadderAnimationSet ResolveLadderAnimationSet(bool exitsAtTop)
    {
        return exitsAtTop
            ? new LadderAnimationSet(ladderUpStartName, ladderUpLoopName, ladderUpEndName)
            : new LadderAnimationSet(ladderDownStartName, ladderDownLoopName, ladderDownEndName);
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

    private static Transform ResolveCharacterMotionRoot(GameObject character, SquadCharacterController controller, Rigidbody body)
    {
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

    private static void PrepareBodyForScriptedMotion(Rigidbody body)
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
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
        public LadderAnimationSet(string startName, string loopName, string endName)
        {
            StartName = startName;
            LoopName = loopName;
            EndName = endName;
        }

        public string StartName { get; }
        public string LoopName { get; }
        public string EndName { get; }
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
