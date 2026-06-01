using UnityEngine;

// Donne a Munin un suivi visuellement independant, meme quand il reste enfant du joueur.
[DisallowMultipleComponent]
public class MuninIndependentFollower : MonoBehaviour
{
    public enum FollowState
    {
        Following,
        MovingToTarget,
        Returning,
        Disabled
    }

    [Header("Target")]
    [SerializeField, Tooltip("Transform du joueur autour duquel Munin doit rester. Laisse vide pour utiliser le SquadCharacterController parent, puis le parent direct.")]
    private Transform targetPlayer;

    [Header("Position")]
    [SerializeField, Tooltip("Offset de base autour du joueur. En world space par defaut pour eviter que Munin tourne avec le joueur.")]
    private Vector3 baseOffset = new Vector3(0.7f, 2.3f, 0f);
    [SerializeField, Min(0.01f), Tooltip("Temps d'amortissement du suivi principal.")]
    private float followSmoothTime = 0.22f;
    [SerializeField, Min(0f), Tooltip("Anticipe legerement la vitesse horizontale du joueur pour eviter le retard visible en sprint.")]
    private float targetVelocityLeadTime = 0.08f;
    [SerializeField, Range(0.1f, 0.98f), Tooltip("Ratio de la distance max a partir duquel Munin accelere son rattrapage.")]
    private float catchUpDistanceRatio = 0.88f;
    [SerializeField, Range(0.05f, 1f), Tooltip("Multiplicateur du smooth time pendant le rattrapage. Plus bas = rattrapage plus rapide.")]
    private float catchUpSmoothTimeMultiplier = 0.45f;
    [SerializeField, Min(0f), Tooltip("Lissage de la vitesse cible utilisee pour l'anticipation.")]
    private float targetVelocitySharpness = 12f;
    [SerializeField, Min(0f), Tooltip("Distance maximale autorisee entre Munin et le joueur. 0 desactive la limite.")]
    private float maxDistanceFromTarget = 3f;
    [SerializeField, Min(0f), Tooltip("Distance minimale optionnelle pour eviter que Munin traverse le centre du joueur.")]
    private float minDistanceFromTarget = 0.45f;

    [Header("Drift")]
    [SerializeField, Min(0f), Tooltip("Amplitude de la derive organique autour de la position cible.")]
    private float driftAmplitude = 0.08f;
    [SerializeField, Min(0f), Tooltip("Frequence du bruit de derive. Plus haut = variations plus rapides.")]
    private float driftFrequency = 0.75f;
    [SerializeField, Min(0f), Tooltip("Vitesse globale de lecture de la derive.")]
    private float driftSpeed = 1f;
    [SerializeField, Tooltip("Multiplicateur par axe pour orienter la derive. Mettre un axe a 0 pour le bloquer.")]
    private Vector3 driftAxisMultiplier = Vector3.one;

    [Header("Spasms")]
    [SerializeField, Min(0f), Tooltip("Amplitude maximale des micro-spasmes. Garder faible pour eviter un rendu jitter.")]
    private float spasmAmplitude = 0.035f;
    [SerializeField, Min(0f), Tooltip("Vibrations internes pendant un spasme. Le cooldown controle leur rarete.")]
    private float spasmFrequency = 1.25f;
    [SerializeField, Min(0.01f), Tooltip("Duree d'une impulsion de spasme.")]
    private float spasmDuration = 0.12f;
    [SerializeField, Range(0f, 1f), Tooltip("Variation aleatoire de direction, amplitude et duree des spasmes.")]
    private float spasmRandomness = 0.45f;
    [SerializeField, Min(0f), Tooltip("Cooldown minimum entre deux spasmes.")]
    private float spasmCooldownMin = 1.6f;
    [SerializeField, Min(0f), Tooltip("Cooldown maximum entre deux spasmes.")]
    private float spasmCooldownMax = 4.5f;

    [Header("Movement")]
    [SerializeField, Tooltip("Ignore la rotation du joueur pour calculer l'offset cible.")]
    private bool ignoreTargetRotation = true;
    [SerializeField, Tooltip("Applique baseOffset en world space. Si faux, l'offset peut suivre la rotation du joueur.")]
    private bool useWorldSpaceOffset = true;
    [SerializeField, Tooltip("Conserve la rotation monde initiale de Munin pour compenser la rotation du parent.")]
    private bool keepWorldRotation = true;

    [Header("Debug")]
    [SerializeField, Tooltip("Dessine les limites de distance et la position cible dans la Scene view.")]
    private bool drawDebugGizmos;

    [SerializeField, HideInInspector]
    private FollowState state = FollowState.Following;

    private Vector3 smoothVelocity;
    private Vector3 currentWorldPosition;
    private bool hasCurrentWorldPosition;
    private Transform velocityTarget;
    private Rigidbody targetRigidbody;
    private Vector3 lastTargetPosition;
    private Vector3 smoothedTargetVelocity;
    private bool hasLastTargetPosition;
    private Quaternion keptWorldRotation = Quaternion.identity;
    private bool hasKeptWorldRotation;
    private Vector3 driftSeed;
    private float spasmCooldownRemaining;
    private bool spasmActive;
    private float spasmElapsed;
    private float currentSpasmDuration;
    private float currentSpasmMagnitude;
    private Vector3 currentSpasmDirection = Vector3.up;
    private Vector3 currentSpasmOffset;

    public Transform TargetPlayer => targetPlayer;
    public FollowState State => state;
    public bool IsFollowing => state == FollowState.Following && enabled && isActiveAndEnabled;

    private void Reset()
    {
        ResolveDefaultTarget();
        CaptureWorldPose();
    }

    private void Awake()
    {
        ValidateSettings();
        ResolveDefaultTarget();
        InitializeRandomSeeds();
        CaptureWorldPose();
        ResetTargetVelocityTracking();
        ScheduleNextSpasm();
    }

    private void OnEnable()
    {
        ResolveDefaultTarget();
        CaptureWorldPose();
        ResetTargetVelocityTracking();
        if (spasmCooldownRemaining <= 0f)
        {
            ScheduleNextSpasm();
        }
    }

    private void LateUpdate()
    {
        if (state != FollowState.Following)
        {
            return;
        }

        if (targetPlayer == null)
        {
            ResolveDefaultTarget();
            if (targetPlayer == null)
            {
                return;
            }
        }

        if (!hasCurrentWorldPosition)
        {
            CaptureWorldPose();
        }

        float deltaTime = Mathf.Max(0f, Time.deltaTime);
        Vector3 targetPosition = ResolveFollowTargetPosition(deltaTime);
        float effectiveSmoothTime = ResolveEffectiveFollowSmoothTime();

        // Le calcul reste base sur la derniere position monde controlee par ce script.
        // Ainsi, la rotation du parent ne pollue pas le SmoothDamp entre deux frames.
        Vector3 nextPosition = Vector3.SmoothDamp(
            currentWorldPosition,
            targetPosition,
            ref smoothVelocity,
            effectiveSmoothTime,
            Mathf.Infinity,
            deltaTime);

        Vector3 constrainedPosition = ConstrainDistanceFromTarget(nextPosition);
        if ((constrainedPosition - nextPosition).sqrMagnitude > 0.000001f)
        {
            Vector3 radial = constrainedPosition - targetPlayer.position;
            if (radial.sqrMagnitude > 0.0001f)
            {
                smoothVelocity = Vector3.ProjectOnPlane(smoothVelocity, radial.normalized);
            }
            else
            {
                smoothVelocity = Vector3.zero;
            }

            nextPosition = constrainedPosition;
        }

        transform.position = nextPosition;
        currentWorldPosition = nextPosition;
        hasCurrentWorldPosition = true;

        if (keepWorldRotation)
        {
            if (!hasKeptWorldRotation)
            {
                keptWorldRotation = transform.rotation;
                hasKeptWorldRotation = true;
            }

            transform.rotation = keptWorldRotation;
        }
    }

    public void SetTargetPlayer(Transform target)
    {
        targetPlayer = target;
        CaptureWorldPose();
        ResetTargetVelocityTracking();
    }

    public void SetState(FollowState newState)
    {
        state = newState;
        if (state == FollowState.Following)
        {
            CaptureWorldPose();
        }
        else
        {
            smoothVelocity = Vector3.zero;
        }
    }

    public void SetFollowingEnabled(bool followingEnabled)
    {
        SetState(followingEnabled ? FollowState.Following : FollowState.Disabled);
    }

    public void BeginExternalMotion(FollowState externalState = FollowState.MovingToTarget)
    {
        if (externalState == FollowState.Following)
        {
            externalState = FollowState.MovingToTarget;
        }

        SetState(externalState);
    }

    public void EndExternalMotion(bool resumeFollowing = true)
    {
        CaptureWorldPose();
        SetState(resumeFollowing ? FollowState.Following : FollowState.Disabled);
    }

    public void RecaptureWorldPose()
    {
        CaptureWorldPose();
    }

    private void CaptureWorldPose()
    {
        currentWorldPosition = transform.position;
        hasCurrentWorldPosition = true;
        smoothVelocity = Vector3.zero;

        if (keepWorldRotation)
        {
            keptWorldRotation = transform.rotation;
            hasKeptWorldRotation = true;
        }
    }

    private void ResolveDefaultTarget()
    {
        if (targetPlayer != null)
        {
            return;
        }

        SquadCharacterController controller = GetComponentInParent<SquadCharacterController>();
        if (controller != null)
        {
            targetPlayer = controller.transform;
            return;
        }

        if (transform.parent != null)
        {
            targetPlayer = transform.parent;
        }
    }

    private void InitializeRandomSeeds()
    {
        if (driftSeed.sqrMagnitude > 0.0001f)
        {
            return;
        }

        driftSeed = new Vector3(
            Random.Range(10f, 1000f),
            Random.Range(10f, 1000f),
            Random.Range(10f, 1000f));
    }

    private Vector3 ResolveFollowTargetPosition(float deltaTime)
    {
        Vector3 targetPosition = ResolveBaseTargetPosition();
        if (targetVelocityLeadTime > 0f)
        {
            Vector3 targetVelocity = Vector3.ProjectOnPlane(ResolveTargetVelocity(deltaTime), Vector3.up);
            targetPosition += targetVelocity * targetVelocityLeadTime;
        }

        return targetPosition + EvaluateDrift() + EvaluateSpasm(deltaTime);
    }

    private Vector3 ResolveTargetVelocity(float deltaTime)
    {
        if (targetPlayer == null)
        {
            ResetTargetVelocityTracking();
            return Vector3.zero;
        }

        if (velocityTarget != targetPlayer)
        {
            velocityTarget = targetPlayer;
            targetRigidbody = targetPlayer.GetComponent<Rigidbody>();
            lastTargetPosition = targetPlayer.position;
            hasLastTargetPosition = true;
            smoothedTargetVelocity = targetRigidbody != null ? targetRigidbody.linearVelocity : Vector3.zero;
            return smoothedTargetVelocity;
        }

        Vector3 rawVelocity = Vector3.zero;
        if (targetRigidbody != null)
        {
            rawVelocity = targetRigidbody.linearVelocity;
        }
        else if (hasLastTargetPosition && deltaTime > 0.0001f)
        {
            rawVelocity = (targetPlayer.position - lastTargetPosition) / deltaTime;
        }

        lastTargetPosition = targetPlayer.position;
        hasLastTargetPosition = true;

        if (targetVelocitySharpness <= 0f || deltaTime <= 0f)
        {
            smoothedTargetVelocity = rawVelocity;
        }
        else
        {
            float t = 1f - Mathf.Exp(-targetVelocitySharpness * deltaTime);
            smoothedTargetVelocity = Vector3.Lerp(smoothedTargetVelocity, rawVelocity, t);
        }

        return smoothedTargetVelocity;
    }

    private void ResetTargetVelocityTracking()
    {
        velocityTarget = null;
        targetRigidbody = null;
        lastTargetPosition = Vector3.zero;
        smoothedTargetVelocity = Vector3.zero;
        hasLastTargetPosition = false;
    }

    private float ResolveEffectiveFollowSmoothTime()
    {
        if (maxDistanceFromTarget <= 0f || targetPlayer == null || !hasCurrentWorldPosition)
        {
            return followSmoothTime;
        }

        float distance = (currentWorldPosition - targetPlayer.position).magnitude;
        float catchUpStart = maxDistanceFromTarget * catchUpDistanceRatio;
        if (distance <= catchUpStart)
        {
            return followSmoothTime;
        }

        float catchUpAmount = Mathf.InverseLerp(catchUpStart, maxDistanceFromTarget, distance);
        float catchUpSmoothTime = followSmoothTime * catchUpSmoothTimeMultiplier;
        return Mathf.Lerp(followSmoothTime, catchUpSmoothTime, catchUpAmount);
    }

    private Vector3 ResolveBaseTargetPosition()
    {
        return targetPlayer.position + ResolveBaseOffset(targetPlayer);
    }

    private Vector3 ResolveBaseOffset(Transform target)
    {
        if (target == null)
        {
            return baseOffset;
        }

        // Par defaut Munin utilise un offset monde pour ne pas orbiter rigidement quand le joueur pivote.
        if (useWorldSpaceOffset || ignoreTargetRotation)
        {
            return baseOffset;
        }

        return target.rotation * baseOffset;
    }

    private Vector3 EvaluateDrift()
    {
        if (driftAmplitude <= 0f || driftFrequency <= 0f || driftSpeed <= 0f)
        {
            return Vector3.zero;
        }

        float t = Time.time * driftSpeed * driftFrequency;
        Vector3 noise = new Vector3(
            Mathf.PerlinNoise(driftSeed.x, t) * 2f - 1f,
            Mathf.PerlinNoise(driftSeed.y, t + 17.31f) * 2f - 1f,
            Mathf.PerlinNoise(driftSeed.z, t + 43.73f) * 2f - 1f);

        return Vector3.Scale(noise, driftAxisMultiplier) * driftAmplitude;
    }

    private Vector3 EvaluateSpasm(float deltaTime)
    {
        if (spasmAmplitude <= 0f)
        {
            currentSpasmOffset = Vector3.zero;
            return currentSpasmOffset;
        }

        if (!spasmActive)
        {
            spasmCooldownRemaining -= deltaTime;
            if (spasmCooldownRemaining <= 0f)
            {
                StartSpasm();
            }
        }

        if (!spasmActive)
        {
            currentSpasmOffset = Vector3.zero;
            return currentSpasmOffset;
        }

        spasmElapsed += deltaTime;
        float normalized = currentSpasmDuration > 0f ? Mathf.Clamp01(spasmElapsed / currentSpasmDuration) : 1f;
        float envelope = Mathf.Sin(normalized * Mathf.PI);
        float vibration = Mathf.Lerp(
            0.65f,
            1f,
            Mathf.Abs(Mathf.Sin(normalized * Mathf.PI * 2f * Mathf.Max(0.1f, spasmFrequency))));

        currentSpasmOffset = currentSpasmDirection * currentSpasmMagnitude * envelope * vibration;

        if (normalized >= 1f)
        {
            spasmActive = false;
            currentSpasmOffset = Vector3.zero;
            ScheduleNextSpasm();
        }

        return currentSpasmOffset;
    }

    private void StartSpasm()
    {
        Vector3 direction = Random.insideUnitSphere;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.up;
        }

        float randomAmplitude = Mathf.Lerp(1f, Random.Range(0.45f, 1.25f), spasmRandomness);
        float randomDuration = Mathf.Lerp(1f, Random.Range(0.75f, 1.35f), spasmRandomness);

        currentSpasmDirection = direction.normalized;
        currentSpasmMagnitude = spasmAmplitude * randomAmplitude;
        currentSpasmDuration = Mathf.Max(0.01f, spasmDuration * randomDuration);
        spasmElapsed = 0f;
        spasmActive = true;
    }

    private void ScheduleNextSpasm()
    {
        float min = Mathf.Min(spasmCooldownMin, spasmCooldownMax);
        float max = Mathf.Max(spasmCooldownMin, spasmCooldownMax);
        spasmCooldownRemaining = max > min ? Random.Range(min, max) : min;
    }

    private Vector3 ConstrainDistanceFromTarget(Vector3 position)
    {
        if (targetPlayer == null)
        {
            return position;
        }

        Vector3 fromTarget = position - targetPlayer.position;
        float distance = fromTarget.magnitude;
        if (maxDistanceFromTarget > 0f && distance > maxDistanceFromTarget)
        {
            fromTarget = fromTarget / distance * maxDistanceFromTarget;
            position = targetPlayer.position + fromTarget;
            distance = maxDistanceFromTarget;
        }

        if (minDistanceFromTarget > 0f && distance < minDistanceFromTarget)
        {
            Vector3 direction = distance > 0.0001f ? fromTarget / distance : GetFallbackOffsetDirection();
            position = targetPlayer.position + direction * minDistanceFromTarget;
        }

        return position;
    }

    private Vector3 GetFallbackOffsetDirection()
    {
        Vector3 offset = ResolveBaseOffset(targetPlayer);
        if (offset.sqrMagnitude > 0.0001f)
        {
            return offset.normalized;
        }

        return Vector3.up;
    }

    private void ValidateSettings()
    {
        followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
        targetVelocityLeadTime = Mathf.Max(0f, targetVelocityLeadTime);
        catchUpDistanceRatio = Mathf.Clamp(catchUpDistanceRatio, 0.1f, 0.98f);
        catchUpSmoothTimeMultiplier = Mathf.Clamp(catchUpSmoothTimeMultiplier, 0.05f, 1f);
        targetVelocitySharpness = Mathf.Max(0f, targetVelocitySharpness);
        maxDistanceFromTarget = Mathf.Max(0f, maxDistanceFromTarget);
        minDistanceFromTarget = Mathf.Max(0f, minDistanceFromTarget);
        driftAmplitude = Mathf.Max(0f, driftAmplitude);
        driftFrequency = Mathf.Max(0f, driftFrequency);
        driftSpeed = Mathf.Max(0f, driftSpeed);
        spasmAmplitude = Mathf.Max(0f, spasmAmplitude);
        spasmFrequency = Mathf.Max(0f, spasmFrequency);
        spasmDuration = Mathf.Max(0.01f, spasmDuration);
        spasmRandomness = Mathf.Clamp01(spasmRandomness);
        spasmCooldownMin = Mathf.Max(0f, spasmCooldownMin);
        spasmCooldownMax = Mathf.Max(spasmCooldownMin, spasmCooldownMax);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ValidateSettings();
        ResolveDefaultTarget();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        Transform target = targetPlayer != null ? targetPlayer : transform.parent;
        if (target == null)
        {
            return;
        }

        Vector3 baseTargetPosition = target.position + ResolveBaseOffset(target);

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.85f);
        Gizmos.DrawWireSphere(baseTargetPosition, 0.08f);

        if (maxDistanceFromTarget > 0f)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(target.position, maxDistanceFromTarget);
        }

        if (minDistanceFromTarget > 0f)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(target.position, minDistanceFromTarget);
        }
    }
#endif
}
