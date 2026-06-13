using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Centralise les comportements runtime de Munin.
[DisallowMultipleComponent]
public class MuninController : MonoBehaviour
{
    public const string DefaultTag = "Munin";

    public enum FollowState
    {
        Following,
        MovingToTarget,
        Returning,
        Disabled
    }

    [SerializeField, Tooltip("Tag utilise pour identifier Munin dans le personnage.")]
    private string muninTag = DefaultTag;
    [SerializeField, Tooltip("Collecte automatiquement les scripts de follow sous Munin.")]
    private bool autoCollectFollowTargets = true;
    [SerializeField, Tooltip("FollowTarget a suspendre pendant les mouvements manuels.")]
    private FollowTarget[] followTargets = Array.Empty<FollowTarget>();

    [Header("Independent Follow")]
    [SerializeField, Tooltip("Transform du joueur autour duquel Munin doit rester. Laisse vide pour utiliser le SquadCharacterController parent, puis le parent direct.")]
    private Transform targetPlayer;
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

    [Header("Follow Drift")]
    [SerializeField, Min(0f), Tooltip("Amplitude de la derive organique autour de la position cible.")]
    private float driftAmplitude = 0.08f;
    [SerializeField, Min(0f), Tooltip("Frequence du bruit de derive. Plus haut = variations plus rapides.")]
    private float driftFrequency = 0.75f;
    [SerializeField, Min(0f), Tooltip("Vitesse globale de lecture de la derive.")]
    private float driftSpeed = 1f;
    [SerializeField, Tooltip("Multiplicateur par axe pour orienter la derive. Mettre un axe a 0 pour le bloquer.")]
    private Vector3 driftAxisMultiplier = Vector3.one;

    [Header("Follow Spasms")]
    [SerializeField, Min(0f), Tooltip("Amplitude maximale des micro-spasmes du suivi. Garder faible pour eviter un rendu jitter.")]
    private float followSpasmAmplitude = 0.035f;
    [SerializeField, Min(0f), Tooltip("Vibrations internes pendant un spasme de suivi. Le cooldown controle leur rarete.")]
    private float followSpasmFrequency = 1.25f;
    [SerializeField, Min(0.01f), Tooltip("Duree d'une impulsion de spasme de suivi.")]
    private float followSpasmDuration = 0.12f;
    [SerializeField, Range(0f, 1f), Tooltip("Variation aleatoire de direction, amplitude et duree des spasmes de suivi.")]
    private float followSpasmRandomness = 0.45f;
    [SerializeField, Min(0f), Tooltip("Cooldown minimum entre deux spasmes de suivi.")]
    private float followSpasmCooldownMin = 1.6f;
    [SerializeField, Min(0f), Tooltip("Cooldown maximum entre deux spasmes de suivi.")]
    private float followSpasmCooldownMax = 4.5f;

    [Header("Follow Movement")]
    [SerializeField, Tooltip("Ignore la rotation du joueur pour calculer l'offset cible.")]
    private bool ignoreTargetRotation = true;
    [SerializeField, Tooltip("Applique baseOffset en world space. Si faux, l'offset peut suivre la rotation du joueur.")]
    private bool useWorldSpaceOffset = true;
    [SerializeField, Tooltip("Conserve la rotation monde initiale de Munin pour compenser la rotation du parent.")]
    private bool keepWorldRotation = true;
    [SerializeField, Tooltip("Dessine les limites de distance et la position cible dans la Scene view.")]
    private bool drawFollowGizmos;

    [Header("Manual Movement")]
    [Min(0.01f), Tooltip("Vitesse de deplacement manuel de Munin vers un brasero ou une torche, en unites Unity par seconde.")]
    public float movementSpeed = 5f;

    [Header("Life Motion")]
    [SerializeField, Tooltip("Anime un enfant de Munin pour lui donner un mouvement vivant.")]
    private bool lifeMotionEnabled = true;
    [SerializeField, Tooltip("Cible animee par le mouvement vivant. Laisse vide pour utiliser le premier enfant.")]
    private Transform lifeMotionTarget;
    [SerializeField, Tooltip("Utilise automatiquement le premier enfant si aucune cible n'est renseignee.")]
    private bool autoResolveLifeMotionTarget = true;
    [SerializeField, Min(0.01f), Tooltip("Cycles de respiration par seconde.")]
    private float breathingFrequency = 0.65f;
    [SerializeField, Range(0f, 1f), Tooltip("Decalage de phase de la respiration.")]
    private float breathingPhaseOffset;
    [SerializeField, Tooltip("Amplitude locale de position de la respiration.")]
    private Vector3 breathingPositionAmplitude = new Vector3(0.01f, 0.035f, 0.01f);
    [SerializeField, Tooltip("Amplitude locale de rotation de la respiration en degres.")]
    private Vector3 breathingRotationAmplitude = new Vector3(1.25f, 0.45f, 1f);
    [SerializeField, Tooltip("Amplitude relative de scale de la respiration.")]
    private Vector3 breathingScaleAmplitude = new Vector3(0.015f, 0.025f, 0.015f);
    [SerializeField, Min(0f), Tooltip("Probabilite par seconde de declencher un spasme.")]
    private float spasmChancePerSecond = 0.35f;
    [SerializeField, Tooltip("Amplitude locale maximale de position d'un spasme.")]
    private Vector3 spasmPositionAmplitude = new Vector3(0.025f, 0.04f, 0.025f);
    [SerializeField, Tooltip("Amplitude maximale de rotation d'un spasme en degres.")]
    private Vector3 spasmRotationAmplitude = new Vector3(7f, 5f, 7f);
    [SerializeField, Min(0.01f), Tooltip("Duree minimale d'un spasme.")]
    private float minSpasmDuration = 0.08f;
    [SerializeField, Min(0.01f), Tooltip("Duree maximale d'un spasme.")]
    private float maxSpasmDuration = 0.22f;

    [Header("Reaction")]
    [SerializeField, Tooltip("Amplifie le mouvement vivant quand Munin detecte une source allumable/eteignable proche.")]
    private bool reactionEnabled = true;
    [SerializeField, Min(0f), Tooltip("Vitesse de fondu de la reaction.")]
    private float reactionBlendSpeed = 8f;
    [SerializeField, Min(0.01f), Tooltip("Frequence du tremblement de reaction.")]
    private float reactionFrequency = 1.8f;
    [SerializeField, Tooltip("Amplitude locale ajoutee par la reaction.")]
    private Vector3 reactionPositionAmplitude = new Vector3(0.02f, 0.07f, 0.02f);
    [SerializeField, Tooltip("Rotation locale ajoutee par la reaction, en degres.")]
    private Vector3 reactionRotationAmplitude = new Vector3(6f, 3f, 6f);
    [SerializeField, Tooltip("Scale locale ajoutee par la reaction.")]
    private Vector3 reactionScaleAmplitude = new Vector3(0.025f, 0.06f, 0.025f);
    [SerializeField, Min(0f), Tooltip("Multiplicateur d'amplitude de respiration a reaction maximale.")]
    private float reactionBreathingAmplitudeMultiplier = 1.35f;
    [SerializeField, Min(0f), Tooltip("Multiplicateur de chance de spasme a reaction maximale.")]
    private float reactionSpasmChanceMultiplier = 3f;
    [SerializeField, Min(0.01f), Tooltip("Duree utilisee pour un pulse de reaction sans duree explicite.")]
    private float defaultReactionPulseDuration = 0.45f;

    [Header("Light Source Detection")]
    [SerializeField, Tooltip("Si actif, Munin remplace la portee d'Outline/action des torches et braseros du personnage.")]
    private bool overrideLightSourceDetectionDistance = true;
    [SerializeField, Min(0.1f), Tooltip("Distance d'Outline et d'action TriggerMunin pour les torches.")]
    private float torchDetectionDistance = 4f;
    [SerializeField, Min(0.1f), Tooltip("Distance d'Outline et d'action TriggerMunin pour les braseros.")]
    private float braseroDetectionDistance = 4.5f;
    [SerializeField, Tooltip("Dessine les distances de detection torche/brasero autour de la cible suivie.")]
    private bool drawLightDetectionGizmos = true;

    [Header("Charges")]
    [SerializeField, Tooltip("Active la consommation de charges quand Munin allume ou eteint une source.")]
    private bool chargesEnabled = true;
    [SerializeField, Min(0), Tooltip("Nombre maximum de charges disponibles pour Munin.")]
    private int maxCharges = 3;
    [SerializeField, Min(0), Tooltip("Charges disponibles au demarrage.")]
    private int currentCharges = 3;
    [SerializeField, Min(0.01f), Tooltip("Duree du pulse visuel quand une action est refusee faute de charge.")]
    private float noChargeReactionPulseDuration = 0.35f;

    private readonly List<FollowTarget> disabledFollowTargets = new List<FollowTarget>();
    private Transform resolvedLifeMotionTarget;
    private Vector3 baseLifeLocalPosition;
    private Quaternion baseLifeLocalRotation = Quaternion.identity;
    private Vector3 baseLifeLocalScale = Vector3.one;
    private bool hasLifeMotionBase;
    private float spasmElapsed;
    private float spasmDuration;
    private Vector3 spasmPositionOffset;
    private Vector3 spasmRotationOffset;
    private float targetReactionIntensity;
    private float currentReactionIntensity;
    private float reactionPulseIntensity;
    private float reactionPulseElapsed;
    private float reactionPulseDuration;
    private bool suspendedIndependentFollow;
    private FollowState followState = FollowState.Following;
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
    private float followSpasmCooldownRemaining;
    private bool followSpasmActive;
    private float followSpasmElapsed;
    private float currentFollowSpasmDuration;
    private float currentFollowSpasmMagnitude;
    private Vector3 currentFollowSpasmDirection = Vector3.up;
    private Vector3 currentFollowSpasmOffset;

    private struct ManualReturnPose
    {
        public Transform parent;
        public Vector3 localPosition;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
    }

    public bool IsMoving { get; private set; }
    public string MuninTag => muninTag;
    public bool ChargesEnabled => chargesEnabled;
    public int MaxCharges => Mathf.Max(0, maxCharges);
    public int ChargesRemaining => chargesEnabled ? Mathf.Clamp(currentCharges, 0, MaxCharges) : MaxCharges;
    public bool HasAvailableCharge => !chargesEnabled || ChargesRemaining > 0;
    public Transform TargetPlayer => targetPlayer;
    public FollowState State => followState;
    public bool IsFollowing => followState == FollowState.Following && enabled && isActiveAndEnabled;
    public bool OverridesLightSourceDetectionDistance => overrideLightSourceDetectionDistance;
    public float TorchDetectionDistance => Mathf.Max(0.1f, torchDetectionDistance);
    public float BraseroDetectionDistance => Mathf.Max(0.1f, braseroDetectionDistance);
    public float MaxLightSourceDetectionDistance => overrideLightSourceDetectionDistance
        ? Mathf.Max(TorchDetectionDistance, BraseroDetectionDistance)
        : 0f;

    public event Action<MuninController, int, int> ChargesChanged;
    public event Action<MuninController> ChargeUseRejected;

    private void Reset()
    {
        RefreshFollowTargets();
        ResolveDefaultTarget();
        CaptureWorldPose();
    }

    private void Awake()
    {
        ValidateSettings();
        RefreshFollowTargets();
        ResolveDefaultTarget();
        InitializeRandomSeeds();
        CaptureWorldPose();
        ResetTargetVelocityTracking();
        ScheduleNextFollowSpasm();
        ResolveLifeMotionTarget();
    }

    private void OnEnable()
    {
        ResolveDefaultTarget();
        CaptureWorldPose();
        ResetTargetVelocityTracking();
        if (followSpasmCooldownRemaining <= 0f)
        {
            ScheduleNextFollowSpasm();
        }
    }

    private void LateUpdate()
    {
        ApplyIndependentFollow();
        ApplyLifeMotion();
    }

    private void OnDisable()
    {
        CancelManualMotion();
        RestoreLifeMotionBase();
    }

    public static MuninController FindForCharacter(GameObject character, string tag)
    {
        if (character == null || string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        Transform taggedTransform = FindChildByTag(character.transform, tag);
        if (taggedTransform == null)
        {
            return null;
        }

        if (taggedTransform.TryGetComponent(out MuninController controller))
        {
            return controller;
        }

        controller = taggedTransform.GetComponentInChildren<MuninController>(true);
        if (controller != null)
        {
            return controller;
        }

        return taggedTransform.GetComponentInParent<MuninController>();
    }

    public static float GetRoundTripDuration(Vector3 fromPosition, Vector3 toPosition, float speed)
    {
        float safeSpeed = Mathf.Max(0.01f, speed);
        return Vector3.Distance(fromPosition, toPosition) * 2f / safeSpeed;
    }

    public float GetRoundTripDurationTo(Vector3 targetPosition)
    {
        return GetRoundTripDuration(transform.position, targetPosition, movementSpeed);
    }

    public IEnumerator MoveToWorldAndBack(Vector3 targetPosition, Action onArrived = null)
    {
        IsMoving = true;
        SuspendFollowTargets();

        ManualReturnPose returnPose = CaptureManualReturnPose();
        Vector3 originalPosition = returnPose.worldPosition;
        Quaternion originalRotation = returnPose.worldRotation;
        Quaternion targetRotation = originalRotation;
        float outboundDuration = GetMoveDuration(originalPosition, targetPosition);

        yield return LerpTo(originalPosition, originalRotation, targetPosition, targetRotation, outboundDuration);
        onArrived?.Invoke();
        SetFollowState(FollowState.Returning);
        ResolveManualReturnPose(returnPose, out Vector3 returnPosition, out _);
        float returnDuration = GetMoveDuration(targetPosition, returnPosition);
        yield return LerpToDynamicReturn(targetPosition, targetRotation, returnPose, returnDuration);

        RestoreFollowTargets();
        IsMoving = false;
    }

    public void SetProximityReaction(float intensity)
    {
        if (!reactionEnabled)
        {
            ClearProximityReaction();
            return;
        }

        targetReactionIntensity = Mathf.Clamp01(intensity);
    }

    public void ClearProximityReaction()
    {
        targetReactionIntensity = 0f;
    }

    public void PulseReaction(float intensity, float duration = 0f)
    {
        if (!reactionEnabled)
        {
            return;
        }

        reactionPulseIntensity = Mathf.Max(reactionPulseIntensity, Mathf.Clamp01(intensity));
        reactionPulseDuration = Mathf.Max(0.01f, duration > 0f ? duration : defaultReactionPulseDuration);
        reactionPulseElapsed = 0f;
    }

    public bool TryConsumeCharge(int amount = 1)
    {
        if (!chargesEnabled)
        {
            return true;
        }

        int safeAmount = Mathf.Max(1, amount);
        if (ChargesRemaining < safeAmount)
        {
            ChargeUseRejected?.Invoke(this);
            PulseReaction(1f, noChargeReactionPulseDuration);
            return false;
        }

        SetCharges(currentCharges - safeAmount);
        return true;
    }

    public void SetCharges(int charges)
    {
        int previous = currentCharges;
        currentCharges = Mathf.Clamp(charges, 0, MaxCharges);
        if (previous != currentCharges)
        {
            NotifyChargesChanged();
        }
    }

    public void AddCharges(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetCharges(currentCharges + amount);
    }

    public void RefillCharges()
    {
        SetCharges(MaxCharges);
    }

    public void SetMaxCharges(int charges, bool refill)
    {
        int previousMax = maxCharges;
        int previousCurrent = currentCharges;
        maxCharges = Mathf.Max(0, charges);
        currentCharges = refill ? MaxCharges : Mathf.Clamp(currentCharges, 0, MaxCharges);
        if (previousMax != maxCharges || previousCurrent != currentCharges)
        {
            NotifyChargesChanged();
        }
    }

    private void NotifyChargesChanged()
    {
        ChargesChanged?.Invoke(this, ChargesRemaining, MaxCharges);
    }

    public void CancelManualMotion()
    {
        RestoreFollowTargets();
        IsMoving = false;
    }

    public void RefreshFollowTargets()
    {
        if (!autoCollectFollowTargets)
        {
            return;
        }

        followTargets = GetComponentsInChildren<FollowTarget>(true);
    }

    private void SuspendFollowTargets()
    {
        RestoreFollowTargets();

        for (int i = 0; i < followTargets.Length; i++)
        {
            FollowTarget followTarget = followTargets[i];
            if (followTarget == null || !followTarget.enabled)
            {
                continue;
            }

            followTarget.enabled = false;
            disabledFollowTargets.Add(followTarget);
        }

        if (IsFollowing)
        {
            BeginExternalMotion(FollowState.MovingToTarget);
            suspendedIndependentFollow = true;
        }
    }

    private void RestoreFollowTargets()
    {
        for (int i = 0; i < disabledFollowTargets.Count; i++)
        {
            FollowTarget followTarget = disabledFollowTargets[i];
            if (followTarget != null)
            {
                followTarget.enabled = true;
            }
        }

        disabledFollowTargets.Clear();

        if (suspendedIndependentFollow)
        {
            EndExternalMotion();
        }

        suspendedIndependentFollow = false;
    }

    public bool TryGetLightSourceDetectionDistance(ICharacterDetectedInteractable target, out float distance)
    {
        distance = 0f;
        if (!overrideLightSourceDetectionDistance || target == null)
        {
            return false;
        }

        if (target is Torch)
        {
            distance = TorchDetectionDistance;
            return true;
        }

        if (target is Brasero)
        {
            distance = BraseroDetectionDistance;
            return true;
        }

        return false;
    }

    public static MuninController FindForCharacter(GameObject character)
    {
        if (character == null)
        {
            return null;
        }

        MuninController controller = character.GetComponentInChildren<MuninController>(true);
        if (controller != null)
        {
            return controller;
        }

        return character.GetComponentInParent<MuninController>(true);
    }

    public void SetTargetPlayer(Transform target)
    {
        targetPlayer = target;
        CaptureWorldPose();
        ResetTargetVelocityTracking();
    }

    public void SetFollowState(FollowState newState)
    {
        followState = newState;
        if (followState == FollowState.Following)
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
        SetFollowState(followingEnabled ? FollowState.Following : FollowState.Disabled);
    }

    public void BeginExternalMotion(FollowState externalState = FollowState.MovingToTarget)
    {
        if (externalState == FollowState.Following)
        {
            externalState = FollowState.MovingToTarget;
        }

        SetFollowState(externalState);
    }

    public void EndExternalMotion(bool resumeFollowing = true)
    {
        CaptureWorldPose();
        SetFollowState(resumeFollowing ? FollowState.Following : FollowState.Disabled);
    }

    public void RecaptureWorldPose()
    {
        CaptureWorldPose();
    }

    private void ApplyIndependentFollow()
    {
        if (followState != FollowState.Following)
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
            UnityEngine.Random.Range(10f, 1000f),
            UnityEngine.Random.Range(10f, 1000f),
            UnityEngine.Random.Range(10f, 1000f));
    }

    private Vector3 ResolveFollowTargetPosition(float deltaTime)
    {
        Vector3 targetPosition = ResolveBaseTargetPosition();
        if (targetVelocityLeadTime > 0f)
        {
            Vector3 targetVelocity = Vector3.ProjectOnPlane(ResolveTargetVelocity(deltaTime), Vector3.up);
            targetPosition += targetVelocity * targetVelocityLeadTime;
        }

        return targetPosition + EvaluateDrift() + EvaluateFollowSpasm(deltaTime);
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

    private Vector3 EvaluateFollowSpasm(float deltaTime)
    {
        if (followSpasmAmplitude <= 0f)
        {
            currentFollowSpasmOffset = Vector3.zero;
            return currentFollowSpasmOffset;
        }

        if (!followSpasmActive)
        {
            followSpasmCooldownRemaining -= deltaTime;
            if (followSpasmCooldownRemaining <= 0f)
            {
                StartFollowSpasm();
            }
        }

        if (!followSpasmActive)
        {
            currentFollowSpasmOffset = Vector3.zero;
            return currentFollowSpasmOffset;
        }

        followSpasmElapsed += deltaTime;
        float normalized = currentFollowSpasmDuration > 0f ? Mathf.Clamp01(followSpasmElapsed / currentFollowSpasmDuration) : 1f;
        float envelope = Mathf.Sin(normalized * Mathf.PI);
        float vibration = Mathf.Lerp(
            0.65f,
            1f,
            Mathf.Abs(Mathf.Sin(normalized * Mathf.PI * 2f * Mathf.Max(0.1f, followSpasmFrequency))));

        currentFollowSpasmOffset = currentFollowSpasmDirection * currentFollowSpasmMagnitude * envelope * vibration;

        if (normalized >= 1f)
        {
            followSpasmActive = false;
            currentFollowSpasmOffset = Vector3.zero;
            ScheduleNextFollowSpasm();
        }

        return currentFollowSpasmOffset;
    }

    private void StartFollowSpasm()
    {
        Vector3 direction = UnityEngine.Random.insideUnitSphere;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.up;
        }

        float randomAmplitude = Mathf.Lerp(1f, UnityEngine.Random.Range(0.45f, 1.25f), followSpasmRandomness);
        float randomDuration = Mathf.Lerp(1f, UnityEngine.Random.Range(0.75f, 1.35f), followSpasmRandomness);

        currentFollowSpasmDirection = direction.normalized;
        currentFollowSpasmMagnitude = followSpasmAmplitude * randomAmplitude;
        currentFollowSpasmDuration = Mathf.Max(0.01f, followSpasmDuration * randomDuration);
        followSpasmElapsed = 0f;
        followSpasmActive = true;
    }

    private void ScheduleNextFollowSpasm()
    {
        float min = Mathf.Min(followSpasmCooldownMin, followSpasmCooldownMax);
        float max = Mathf.Max(followSpasmCooldownMin, followSpasmCooldownMax);
        followSpasmCooldownRemaining = max > min ? UnityEngine.Random.Range(min, max) : min;
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

    private void ApplyLifeMotion()
    {
        Transform target = ResolveLifeMotionTarget();
        if (target == null)
        {
            ClearLifeMotionBase();
            return;
        }

        if (!lifeMotionEnabled)
        {
            RestoreLifeMotionBase();
            return;
        }

        EnsureLifeMotionBase(target);

        float deltaTime = Time.deltaTime;
        float reactionIntensity = EvaluateReactionIntensity(deltaTime);
        float breathingMultiplier = 1f + reactionIntensity * reactionBreathingAmplitudeMultiplier;

        float phase = (Time.time * breathingFrequency + breathingPhaseOffset) * Mathf.PI * 2f;
        float breath = Mathf.Sin(phase);
        Vector3 breathingPosition = new Vector3(
            Mathf.Sin(phase * 0.5f) * breathingPositionAmplitude.x * breathingMultiplier,
            breath * breathingPositionAmplitude.y * breathingMultiplier,
            Mathf.Cos(phase * 0.75f) * breathingPositionAmplitude.z * breathingMultiplier);
        Vector3 breathingRotation = new Vector3(
            breath * breathingRotationAmplitude.x * breathingMultiplier,
            Mathf.Sin(phase * 0.5f) * breathingRotationAmplitude.y * breathingMultiplier,
            Mathf.Cos(phase * 0.75f) * breathingRotationAmplitude.z * breathingMultiplier);
        Vector3 breathingScale = new Vector3(
            1f + breath * breathingScaleAmplitude.x * breathingMultiplier,
            1f + breath * breathingScaleAmplitude.y * breathingMultiplier,
            1f + breath * breathingScaleAmplitude.z * breathingMultiplier);

        Vector3 reactionPosition = Vector3.zero;
        Vector3 reactionRotation = Vector3.zero;
        Vector3 reactionScale = Vector3.one;
        if (reactionIntensity > 0.001f)
        {
            float reactionPhase = Time.time * reactionFrequency * Mathf.PI * 2f;
            float pulse = Mathf.Sin(reactionPhase);
            float lift = Mathf.Abs(Mathf.Sin(reactionPhase * 0.5f));
            reactionPosition = new Vector3(
                Mathf.Sin(reactionPhase * 0.7f) * reactionPositionAmplitude.x,
                lift * reactionPositionAmplitude.y,
                Mathf.Cos(reactionPhase * 0.9f) * reactionPositionAmplitude.z) * reactionIntensity;
            reactionRotation = new Vector3(
                pulse * reactionRotationAmplitude.x,
                Mathf.Sin(reactionPhase * 0.45f) * reactionRotationAmplitude.y,
                Mathf.Cos(reactionPhase * 0.8f) * reactionRotationAmplitude.z) * reactionIntensity;
            reactionScale = new Vector3(
                1f + pulse * reactionScaleAmplitude.x * reactionIntensity,
                1f + lift * reactionScaleAmplitude.y * reactionIntensity,
                1f + Mathf.Cos(reactionPhase * 0.6f) * reactionScaleAmplitude.z * reactionIntensity);
        }

        float spasmStrength = EvaluateSpasm(deltaTime, reactionIntensity);
        target.localPosition = baseLifeLocalPosition
            + breathingPosition
            + reactionPosition
            + spasmPositionOffset * spasmStrength;
        target.localRotation = baseLifeLocalRotation
            * Quaternion.Euler(breathingRotation + reactionRotation + spasmRotationOffset * spasmStrength);
        target.localScale = Vector3.Scale(baseLifeLocalScale, Vector3.Scale(breathingScale, reactionScale));
    }

    private float EvaluateReactionIntensity(float deltaTime)
    {
        float desiredIntensity = reactionEnabled ? Mathf.Clamp01(targetReactionIntensity) : 0f;
        if (reactionPulseDuration > 0f && reactionPulseElapsed < reactionPulseDuration)
        {
            reactionPulseElapsed += Mathf.Max(0f, deltaTime);
            float normalizedPulse = Mathf.Clamp01(reactionPulseElapsed / reactionPulseDuration);
            float pulse = Mathf.Sin((1f - normalizedPulse) * Mathf.PI * 0.5f);
            desiredIntensity = Mathf.Max(desiredIntensity, reactionPulseIntensity * pulse);
        }
        else
        {
            reactionPulseIntensity = 0f;
            reactionPulseDuration = 0f;
            reactionPulseElapsed = 0f;
        }

        currentReactionIntensity = Mathf.MoveTowards(
            currentReactionIntensity,
            Mathf.Clamp01(desiredIntensity),
            Mathf.Max(0f, reactionBlendSpeed) * Mathf.Max(0f, deltaTime));
        return currentReactionIntensity;
    }

    private float EvaluateSpasm(float deltaTime, float reactionIntensity)
    {
        if (spasmElapsed >= spasmDuration)
        {
            float chance = spasmChancePerSecond * (1f + Mathf.Clamp01(reactionIntensity) * reactionSpasmChanceMultiplier);
            if (chance <= 0f || UnityEngine.Random.value > chance * deltaTime)
            {
                return 0f;
            }

            spasmDuration = UnityEngine.Random.Range(minSpasmDuration, maxSpasmDuration);
            spasmElapsed = 0f;
            float spasmMultiplier = 1f + Mathf.Clamp01(reactionIntensity);
            spasmPositionOffset = RandomRange(spasmPositionAmplitude * spasmMultiplier);
            spasmRotationOffset = RandomRange(spasmRotationAmplitude * spasmMultiplier);
        }

        spasmElapsed += deltaTime;
        float normalized = spasmDuration > 0f ? Mathf.Clamp01(spasmElapsed / spasmDuration) : 1f;
        return Mathf.Sin(normalized * Mathf.PI);
    }

    private Transform ResolveLifeMotionTarget()
    {
        Transform target = lifeMotionTarget;
        if (target == null && autoResolveLifeMotionTarget && transform.childCount > 0)
        {
            target = transform.GetChild(0);
        }

        if (target == resolvedLifeMotionTarget)
        {
            return resolvedLifeMotionTarget;
        }

        RestoreLifeMotionBase();
        resolvedLifeMotionTarget = target;
        hasLifeMotionBase = false;
        return resolvedLifeMotionTarget;
    }

    private void EnsureLifeMotionBase(Transform target)
    {
        if (hasLifeMotionBase && target == resolvedLifeMotionTarget)
        {
            return;
        }

        resolvedLifeMotionTarget = target;
        baseLifeLocalPosition = target.localPosition;
        baseLifeLocalRotation = target.localRotation;
        baseLifeLocalScale = target.localScale;
        hasLifeMotionBase = true;
        spasmElapsed = 0f;
        spasmDuration = 0f;
        spasmPositionOffset = Vector3.zero;
        spasmRotationOffset = Vector3.zero;
    }

    private void RestoreLifeMotionBase()
    {
        if (!hasLifeMotionBase || resolvedLifeMotionTarget == null)
        {
            ClearLifeMotionBase();
            return;
        }

        resolvedLifeMotionTarget.localPosition = baseLifeLocalPosition;
        resolvedLifeMotionTarget.localRotation = baseLifeLocalRotation;
        resolvedLifeMotionTarget.localScale = baseLifeLocalScale;
        ClearLifeMotionBase();
    }

    private void ClearLifeMotionBase()
    {
        hasLifeMotionBase = false;
        resolvedLifeMotionTarget = null;
        spasmElapsed = 0f;
        spasmDuration = 0f;
        spasmPositionOffset = Vector3.zero;
        spasmRotationOffset = Vector3.zero;
    }

    private IEnumerator LerpTo(
        Vector3 fromPosition,
        Quaternion fromRotation,
        Vector3 toPosition,
        Quaternion toRotation,
        float duration)
    {
        if (duration <= 0f)
        {
            transform.SetPositionAndRotation(toPosition, toRotation);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            transform.SetPositionAndRotation(
                Vector3.Lerp(fromPosition, toPosition, t),
                Quaternion.Slerp(fromRotation, toRotation, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.SetPositionAndRotation(toPosition, toRotation);
    }

    private IEnumerator LerpToDynamicReturn(
        Vector3 fromPosition,
        Quaternion fromRotation,
        ManualReturnPose returnPose,
        float duration)
    {
        if (duration <= 0f)
        {
            ResolveManualReturnPose(returnPose, out Vector3 immediatePosition, out Quaternion immediateRotation);
            transform.SetPositionAndRotation(immediatePosition, immediateRotation);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            ResolveManualReturnPose(returnPose, out Vector3 targetPosition, out Quaternion targetRotation);
            float t = Mathf.Clamp01(elapsed / duration);
            transform.SetPositionAndRotation(
                Vector3.Lerp(fromPosition, targetPosition, t),
                Quaternion.Slerp(fromRotation, targetRotation, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResolveManualReturnPose(returnPose, out Vector3 finalPosition, out Quaternion finalRotation);
        transform.SetPositionAndRotation(finalPosition, finalRotation);
    }

    private ManualReturnPose CaptureManualReturnPose()
    {
        return new ManualReturnPose
        {
            parent = transform.parent,
            localPosition = transform.localPosition,
            worldPosition = transform.position,
            worldRotation = transform.rotation
        };
    }

    private static void ResolveManualReturnPose(
        ManualReturnPose returnPose,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (returnPose.parent != null)
        {
            position = returnPose.parent.TransformPoint(returnPose.localPosition);
            rotation = returnPose.worldRotation;
            return;
        }

        position = returnPose.worldPosition;
        rotation = returnPose.worldRotation;
    }

    private float GetMoveDuration(Vector3 fromPosition, Vector3 toPosition)
    {
        return Vector3.Distance(fromPosition, toPosition) / Mathf.Max(0.01f, movementSpeed);
    }

    private static Transform FindChildByTag(Transform root, string tag)
    {
        if (root == null)
        {
            return null;
        }

        if (HasTag(root, tag))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByTag(root.GetChild(i), tag);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool HasTag(Transform target, string tag)
    {
        try
        {
            return target.CompareTag(tag);
        }
        catch (UnityException)
        {
            return false;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ValidateSettings();
        RefreshFollowTargets();
        ResolveDefaultTarget();
    }

    private void OnDrawGizmosSelected()
    {
        Transform target = targetPlayer != null ? targetPlayer : transform.parent;
        Vector3 detectionCenter = target != null ? target.position : transform.position;

        if (drawFollowGizmos && target != null)
        {
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

        if (!drawLightDetectionGizmos || !overrideLightSourceDetectionDistance)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.35f);
        Gizmos.DrawWireSphere(detectionCenter, TorchDetectionDistance);

        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(detectionCenter, BraseroDetectionDistance);
    }
#endif

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(muninTag))
        {
            muninTag = DefaultTag;
        }

        breathingFrequency = Mathf.Max(0.01f, breathingFrequency);
        breathingPhaseOffset = Mathf.Repeat(breathingPhaseOffset, 1f);
        movementSpeed = Mathf.Max(0.01f, movementSpeed);
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
        followSpasmAmplitude = Mathf.Max(0f, followSpasmAmplitude);
        followSpasmFrequency = Mathf.Max(0f, followSpasmFrequency);
        followSpasmDuration = Mathf.Max(0.01f, followSpasmDuration);
        followSpasmRandomness = Mathf.Clamp01(followSpasmRandomness);
        followSpasmCooldownMin = Mathf.Max(0f, followSpasmCooldownMin);
        followSpasmCooldownMax = Mathf.Max(followSpasmCooldownMin, followSpasmCooldownMax);
        spasmChancePerSecond = Mathf.Max(0f, spasmChancePerSecond);
        minSpasmDuration = Mathf.Max(0.01f, minSpasmDuration);
        maxSpasmDuration = Mathf.Max(minSpasmDuration, maxSpasmDuration);
        reactionBlendSpeed = Mathf.Max(0f, reactionBlendSpeed);
        reactionFrequency = Mathf.Max(0.01f, reactionFrequency);
        reactionBreathingAmplitudeMultiplier = Mathf.Max(0f, reactionBreathingAmplitudeMultiplier);
        reactionSpasmChanceMultiplier = Mathf.Max(0f, reactionSpasmChanceMultiplier);
        defaultReactionPulseDuration = Mathf.Max(0.01f, defaultReactionPulseDuration);
        torchDetectionDistance = Mathf.Max(0.1f, torchDetectionDistance);
        braseroDetectionDistance = Mathf.Max(0.1f, braseroDetectionDistance);
        maxCharges = Mathf.Max(0, maxCharges);
        currentCharges = Mathf.Clamp(currentCharges, 0, maxCharges);
        noChargeReactionPulseDuration = Mathf.Max(0.01f, noChargeReactionPulseDuration);
    }

    private static Vector3 RandomRange(Vector3 amplitude)
    {
        return new Vector3(
            UnityEngine.Random.Range(-amplitude.x, amplitude.x),
            UnityEngine.Random.Range(-amplitude.y, amplitude.y),
            UnityEngine.Random.Range(-amplitude.z, amplitude.z));
    }
}
