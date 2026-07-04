using System.Collections;
using UnityEngine;

// Role: expose des hooks Animation Event pour la presentation des attaques de combat.
// Usage: attacher sur l'objet qui recoit les Animation Events; le parent est deplace par defaut.
// Responsibilities: ralentir localement la presentation, afficher l'UI defensive et deplacer l'attaquant vers sa victime.
// Precautions: presentation locale uniquement; ne resout aucun degat et ne modifie pas Time.timeScale.
public sealed class CombatAnimationEvents : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform actorRoot;
    [SerializeField] private Transform victimOverride;

    [Header("Time")]
    [SerializeField, Range(0.01f, 1f)] private float slowedTimeScale = 0.1f;
    [SerializeField, Min(0.01f)] private float slowBlendSeconds = 0.15f;
    [SerializeField] private AnimationCurve slowBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool includeVictimInTimeScale = true;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float victimStopDistance = 1.15f;
    [SerializeField, Min(0.01f)] private float moveToVictimSeconds = 0.2f;
    [SerializeField, Min(0.01f)] private float returnSeconds = 0.2f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool keepActorHeight = true;

    private Coroutine timeRoutine;
    private Coroutine moveRoutine;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool hasInitialPose;

    public void SlowCombatTime()
    {
        ShowCombatDefensePanel();
        StartSlowCombatTime(slowedTimeScale, slowBlendSeconds);
    }

    public void SlowCombatTimeTo(float targetTimeScale)
    {
        ShowCombatDefensePanel();
        StartSlowCombatTime(targetTimeScale, slowBlendSeconds);
    }

    public void SlowCombatTimeInstant()
    {
        ShowCombatDefensePanel();
        StartSlowCombatTime(slowedTimeScale, 0.01f);
    }

    private void StartSlowCombatTime(float targetTimeScale, float blendSeconds)
    {
        StopTimeRoutine();
        timeRoutine = StartCoroutine(SlowCombatTimeRoutine(targetTimeScale, blendSeconds));
    }

    public void RestoreCombatTime()
    {
        StopTimeRoutine();
        TimeManager.Instance?.SetCombatPresentationTimeScale(null, 1f, active: false);
        HideCombatDefensePanel();
    }

    public void ShowCombatDefensePanel()
    {
        CombatHudController.SetCombatDefensePanelVisibleFromAnimationEvent(true);
    }

    public void HideCombatDefensePanel()
    {
        CombatHudController.SetCombatDefensePanelVisibleFromAnimationEvent(false);
    }

    public void MoveParentToCombatVictim()
    {
        Transform actor = ResolveActorRoot();
        if (actor == null)
        {
            return;
        }

        if (!TryResolveCombatVictim(actor, out Transform victim))
        {
            return;
        }

        CaptureInitialPose(actor);
        Vector3 destination = ResolveVictimApproachPosition(actor.position, victim);
        Quaternion rotation = ResolveFacingRotation(destination, victim.position, actor.rotation);
        StartMove(actor, destination, rotation, moveToVictimSeconds, clearInitialPose: false);
    }

    public void ReturnParentToInitialPosition()
    {
        Transform actor = ResolveActorRoot();
        if (actor == null || !hasInitialPose)
        {
            return;
        }

        StartMove(actor, initialPosition, initialRotation, returnSeconds, clearInitialPose: true);
    }

    public void NotifyCombatImpact()
    {
        CombatSessionManager.Instance?.NotifyLocalCombatAnimationImpact(ResolveActorRoot());
    }

    private void OnDisable()
    {
        StopTimeRoutine();
        StopMoveRoutine();
        TimeManager.Instance?.SetCombatPresentationTimeScale(null, 1f, active: false);
        HideCombatDefensePanel();
    }

    private IEnumerator SlowCombatTimeRoutine(float targetTimeScale, float blendSeconds)
    {
        Transform actor = ResolveActorRoot();
        if (actor == null)
        {
            yield break;
        }

        TimeManager timeManager = TimeManager.EnsureInstance();
        Transform victim = includeVictimInTimeScale && TryResolveCombatVictim(actor, out Transform resolvedVictim)
            ? resolvedVictim
            : null;
        float targetScale = Mathf.Clamp(targetTimeScale, 0.01f, 1f);
        float duration = Mathf.Max(0.01f, blendSeconds);
        float elapsed = 0f;

        while (actor != null && elapsed < duration)
        {
            float t = EvaluateCurve(slowBlendCurve, elapsed / duration);
            float timeScale = Mathf.Lerp(1f, targetScale, t);
            ApplyPresentationTimeScale(timeManager, actor, victim, timeScale);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (actor != null)
        {
            ApplyPresentationTimeScale(timeManager, actor, victim, targetScale);
        }

        timeRoutine = null;
    }

    private void ApplyPresentationTimeScale(TimeManager timeManager, Transform actor, Transform victim, float timeScale)
    {
        if (timeManager == null || actor == null)
        {
            return;
        }

        timeManager.SetCombatPresentationTimeScale(actor, timeScale, active: true);
        if (includeVictimInTimeScale && victim != null && !BelongsTo(victim, actor))
        {
            timeManager.SetCombatPresentationTimeScale(victim, timeScale, active: true);
        }
    }

    private void StartMove(
        Transform actor,
        Vector3 destination,
        Quaternion rotation,
        float duration,
        bool clearInitialPose)
    {
        StopMoveRoutine();
        moveRoutine = StartCoroutine(MoveRoutine(actor, destination, rotation, duration, clearInitialPose));
    }

    private IEnumerator MoveRoutine(
        Transform actor,
        Vector3 destination,
        Quaternion rotation,
        float duration,
        bool clearInitialPose)
    {
        Vector3 startPosition = actor.position;
        Quaternion startRotation = actor.rotation;
        float resolvedDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (actor != null && elapsed < resolvedDuration)
        {
            float t = EvaluateCurve(movementCurve, elapsed / resolvedDuration);
            MoveActor(
                actor,
                Vector3.Lerp(startPosition, destination, t),
                Quaternion.Slerp(startRotation, rotation, t));
            elapsed += Mathf.Max(0f, TimeManager.GetCombatPresentationDeltaTime());
            yield return null;
        }

        if (actor != null)
        {
            MoveActor(actor, destination, rotation);
        }

        if (clearInitialPose)
        {
            hasInitialPose = false;
        }

        moveRoutine = null;
    }

    private Transform ResolveActorRoot()
    {
        if (actorRoot != null)
        {
            return actorRoot;
        }

        return transform.parent != null ? transform.parent : transform;
    }

    private bool TryResolveCombatVictim(Transform actor, out Transform victim)
    {
        victim = null;

        CombatSessionManager manager = CombatSessionManager.Instance;
        if (manager != null &&
            manager.TryGetLocalCombatCameraContext(
                out Transform player,
                out Transform enemy,
                out bool playerTurn,
                out _))
        {
            if (BelongsTo(actor, enemy))
            {
                victim = player;
                return victim != null;
            }

            if (BelongsTo(actor, player))
            {
                victim = enemy;
                return victim != null;
            }

            victim = playerTurn ? enemy : player;
            return victim != null;
        }

        victim = victimOverride;
        return victim != null;
    }

    private void CaptureInitialPose(Transform actor)
    {
        initialPosition = actor.position;
        initialRotation = actor.rotation;
        hasInitialPose = true;
    }

    private Vector3 ResolveVictimApproachPosition(Vector3 actorPosition, Transform victim)
    {
        Vector3 victimPosition = victim.position;
        Vector3 directionFromVictim = Vector3.ProjectOnPlane(actorPosition - victimPosition, Vector3.up);
        if (directionFromVictim.sqrMagnitude <= 0.0001f)
        {
            directionFromVictim = Vector3.ProjectOnPlane(-victim.forward, Vector3.up);
        }

        if (directionFromVictim.sqrMagnitude <= 0.0001f)
        {
            directionFromVictim = Vector3.back;
        }

        Vector3 destination = victimPosition + directionFromVictim.normalized * victimStopDistance;
        if (keepActorHeight)
        {
            destination.y = actorPosition.y;
        }

        return destination;
    }

    private static Quaternion ResolveFacingRotation(Vector3 origin, Vector3 target, Quaternion fallback)
    {
        Vector3 direction = Vector3.ProjectOnPlane(target - origin, Vector3.up);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return fallback;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static bool BelongsTo(Transform actor, Transform candidate)
    {
        return actor != null &&
               candidate != null &&
               (actor == candidate || actor.IsChildOf(candidate) || candidate.IsChildOf(actor));
    }

    private static float EvaluateCurve(AnimationCurve curve, float t)
    {
        float clamped = Mathf.Clamp01(t);
        if (curve == null || curve.length == 0)
        {
            return clamped;
        }

        return Mathf.Clamp01(curve.Evaluate(clamped));
    }

    private static void MoveActor(Transform actor, Vector3 position, Quaternion rotation)
    {
        Rigidbody body = actor.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
        }

        actor.SetPositionAndRotation(position, rotation);
    }

    private void StopTimeRoutine()
    {
        if (timeRoutine == null)
        {
            return;
        }

        StopCoroutine(timeRoutine);
        timeRoutine = null;
    }

    private void StopMoveRoutine()
    {
        if (moveRoutine == null)
        {
            return;
        }

        StopCoroutine(moveRoutine);
        moveRoutine = null;
    }
}
