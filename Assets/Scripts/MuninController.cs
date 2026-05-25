using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Centralise les comportements runtime de Munin.
[DisallowMultipleComponent]
public class MuninController : MonoBehaviour
{
    public const string DefaultTag = "Munin";

    [SerializeField, Tooltip("Tag utilise pour identifier Munin dans le personnage.")]
    private string muninTag = DefaultTag;
    [SerializeField, Tooltip("Collecte automatiquement les scripts de follow sous Munin.")]
    private bool autoCollectFollowTargets = true;
    [SerializeField, Tooltip("FollowTarget a suspendre pendant les mouvements manuels.")]
    private FollowTarget[] followTargets = Array.Empty<FollowTarget>();
    [SerializeField, Tooltip("Follow independant de Munin a suspendre pendant les mouvements manuels.")]
    private MuninIndependentFollower[] independentFollowers = Array.Empty<MuninIndependentFollower>();

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
    private readonly List<MuninIndependentFollower> suspendedIndependentFollowers = new List<MuninIndependentFollower>();
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

    public bool IsMoving { get; private set; }
    public string MuninTag => muninTag;
    public bool ChargesEnabled => chargesEnabled;
    public int MaxCharges => Mathf.Max(0, maxCharges);
    public int ChargesRemaining => chargesEnabled ? Mathf.Clamp(currentCharges, 0, MaxCharges) : MaxCharges;
    public bool HasAvailableCharge => !chargesEnabled || ChargesRemaining > 0;

    public event Action<MuninController, int, int> ChargesChanged;
    public event Action<MuninController> ChargeUseRejected;

    private void Reset()
    {
        RefreshFollowTargets();
    }

    private void Awake()
    {
        ValidateSettings();
        RefreshFollowTargets();
        ResolveLifeMotionTarget();
    }

    private void LateUpdate()
    {
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

        Vector3 originalPosition = transform.position;
        Quaternion originalRotation = transform.rotation;
        Quaternion targetRotation = originalRotation;
        float outboundDuration = GetMoveDuration(originalPosition, targetPosition);
        float returnDuration = GetMoveDuration(targetPosition, originalPosition);

        yield return LerpTo(originalPosition, originalRotation, targetPosition, targetRotation, outboundDuration);
        onArrived?.Invoke();
        SetSuspendedIndependentFollowersState(MuninIndependentFollower.FollowState.Returning);
        yield return LerpTo(targetPosition, targetRotation, originalPosition, originalRotation, returnDuration);

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
        independentFollowers = GetComponentsInChildren<MuninIndependentFollower>(true);
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

        for (int i = 0; i < independentFollowers.Length; i++)
        {
            MuninIndependentFollower follower = independentFollowers[i];
            if (follower == null || !follower.IsFollowing)
            {
                continue;
            }

            follower.BeginExternalMotion(MuninIndependentFollower.FollowState.MovingToTarget);
            suspendedIndependentFollowers.Add(follower);
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

        for (int i = 0; i < suspendedIndependentFollowers.Count; i++)
        {
            MuninIndependentFollower follower = suspendedIndependentFollowers[i];
            if (follower != null)
            {
                follower.EndExternalMotion();
            }
        }

        suspendedIndependentFollowers.Clear();
    }

    private void SetSuspendedIndependentFollowersState(MuninIndependentFollower.FollowState state)
    {
        for (int i = 0; i < suspendedIndependentFollowers.Count; i++)
        {
            MuninIndependentFollower follower = suspendedIndependentFollowers[i];
            if (follower != null)
            {
                follower.BeginExternalMotion(state);
            }
        }
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
        spasmChancePerSecond = Mathf.Max(0f, spasmChancePerSecond);
        minSpasmDuration = Mathf.Max(0.01f, minSpasmDuration);
        maxSpasmDuration = Mathf.Max(minSpasmDuration, maxSpasmDuration);
        reactionBlendSpeed = Mathf.Max(0f, reactionBlendSpeed);
        reactionFrequency = Mathf.Max(0.01f, reactionFrequency);
        reactionBreathingAmplitudeMultiplier = Mathf.Max(0f, reactionBreathingAmplitudeMultiplier);
        reactionSpasmChanceMultiplier = Mathf.Max(0f, reactionSpasmChanceMultiplier);
        defaultReactionPulseDuration = Mathf.Max(0.01f, defaultReactionPulseDuration);
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
