using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Levier interactif avec suivi de portee, mode d'activation et dispatch vers des cibles.
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class Lever : NetworkBehaviour
{
    public enum LeverActivationMode
    {
        Timed,
        Toggle,
        OneShot
    }

    [Serializable]
    public sealed class LeverTargetBinding
    {
        [Tooltip("Composant cible devant implementer ILeverTarget.")]
        public MonoBehaviour target;
        [Min(0f)]
        [Tooltip("Delai avant d'envoyer l'etat du levier a la cible.")]
        public float delay;
        [Tooltip("Notifie la cible lors de l'activation.")]
        public bool notifyOnActivate = true;
        [Tooltip("Notifie la cible lors de la desactivation.")]
        public bool notifyOnDeactivate;
    }

    [Header("Interact")]
    [Tooltip("Ecoute l'input Interact pour activer le levier.")]
    public bool useInteractInput = true;
    [Tooltip("Exige un tag Player si aucun personnage de squad n'est trouve.")]
    public bool requirePlayerTag = true;
    [Tooltip("Pilote le bool de l'Animator lors d'un changement d'etat.")]
    public bool setAnimatorBoolOnInteract = true;
    [Tooltip("Cooldown minimal entre deux interactions locales.")]
    public float interactCooldown = 0.15f;
    [SerializeField, Tooltip("Active des logs utiles pour diagnostiquer le flux du levier.")]
    private bool logDebug;

    [Header("Interaction Range")]
    [Tooltip("Utilise le bounds du trigger/collider pour estimer le rayon.")]
    public bool useColliderBounds = true;
    [Tooltip("Rayon manuel d'interaction si aucun collider n'est exploitable.")]
    public float interactionRadius = 1.25f;
    [Tooltip("Padding ajoute au rayon du collider.")]
    public float colliderRadiusPadding = 0.1f;
    [SerializeField, Tooltip("Trigger d'interaction utilise pour suivre les personnages a portee.")]
    private SphereCollider interactionTrigger;

    [Header("Behavior")]
    [Tooltip("Mode d'activation du levier.")]
    public LeverActivationMode activationMode = LeverActivationMode.Timed;
    [Tooltip("En mode Timed, une interaction sur un levier deja actif redemarre le timer.")]
    public bool allowRetriggerWhileActive = true;
    [Tooltip("Bloque toute interaction apres la premiere activation reussie.")]
    public bool singleUse;

    [Header("Targets")]
    [SerializeField, Tooltip("Cibles notifiees directement par le levier.")]
    private LeverTargetBinding[] targetBindings = Array.Empty<LeverTargetBinding>();
    [SerializeField, Tooltip("Evenement Inspector appele a l'activation.")]
    private UnityEvent onActivated;
    [SerializeField, Tooltip("Evenement Inspector appele a la desactivation.")]
    private UnityEvent onDeactivated;

    [Header("Animation")]
    [Tooltip("Animator du levier (auto-find si null).")]
    public Animator leverAnimator;
    [Tooltip("Parametre bool de l'Animator a modifier.")]
    public string leverBoolParam = "Triggered";

    [Header("Audio")]
    [Tooltip("SFX a l'activation.")]
    public AudioClipSO activateSfx;
    [Tooltip("SFX a la desactivation.")]
    public AudioClipSO deactivateSfx;

    [Header("Timing")]
    [Tooltip("Temps avant desactivation si aucun personnage ne reste proche en mode Timed.")]
    public float activeDuration = 1f;

    [Header("State")]
    [SerializeField, Tooltip("Etat courant du levier (debug).")]
    private bool isActive;
    [SerializeField, Tooltip("Indique si le levier a deja ete active au moins une fois.")]
    private bool wasActivatedOnce;

    public bool IsActive => isActive;
    public bool WasActivatedOnce => wasActivatedOnce;

    public event Action<Lever, bool> StateChanged;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private readonly List<Coroutine> targetDispatchRoutines = new List<Coroutine>();
    private Collider leverCollider;
    private Coroutine deactivateRoutine;
    private GameObject currentCharacter;
    private float nextInteractAllowedTime;
    private readonly NetworkVariable<bool> netIsActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Reset()
    {
        EnsureInteractionTrigger();
    }

    private void Awake()
    {
        if (leverAnimator == null)
        {
            leverAnimator = GetComponent<Animator>();
        }

        EnsureInteractionTrigger();
        leverCollider = ResolveInteractionBoundsCollider();
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.05f, interactionRadius);
        colliderRadiusPadding = Mathf.Max(0f, colliderRadiusPadding);
        activeDuration = Mathf.Max(0f, activeDuration);
        interactCooldown = Mathf.Max(0f, interactCooldown);

        if (Application.isPlaying)
        {
            return;
        }

        if (leverAnimator == null)
        {
            leverAnimator = GetComponent<Animator>();
        }

        EnsureInteractionTrigger();
        leverCollider = ResolveInteractionBoundsCollider();
    }

    private void OnEnable()
    {
        if (useInteractInput)
        {
            LocalInputRouter.EnsureInitialized();
            LocalInputRouter.Interact += OnInteractPerformed;
        }

        RefreshCurrentCharacter();
    }

    private void OnDisable()
    {
        if (useInteractInput)
        {
            LocalInputRouter.Interact -= OnInteractPerformed;
        }

        StopDeactivateTimer();
        StopTargetDispatches();
        ResetInteractionTracking();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useInteractInput)
        {
            return;
        }

        HandleCharacterEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useInteractInput)
        {
            return;
        }

        HandleCharacterExit(other);
    }

    public override void OnNetworkSpawn()
    {
        netIsActive.OnValueChanged += OnNetStateChanged;
        if (IsServer)
        {
            netIsActive.Value = isActive;
        }
        else
        {
            ApplyState(netIsActive.Value, updateTimer: false, emitNotifications: true, playFeedback: false, reason: "network_spawn");
        }
    }

    public override void OnNetworkDespawn()
    {
        netIsActive.OnValueChanged -= OnNetStateChanged;
    }

    public void SetActive(bool active)
    {
        if (IsNetworked())
        {
            if (!IsServer)
            {
                return;
            }

            SetActiveServer(active, updateTimer: true, emitNotifications: true, playFeedback: true, reason: "external_set_active");
            return;
        }

        ApplyState(active, updateTimer: true, emitNotifications: true, playFeedback: true, reason: "external_set_active");
    }

    public void RestoreActiveState(bool active, bool activatedOnce = false)
    {
        if (IsNetworked())
        {
            if (!IsServer)
            {
                return;
            }

            wasActivatedOnce = wasActivatedOnce || activatedOnce || active;
            SetActiveServer(active, updateTimer: false, emitNotifications: false, playFeedback: false, reason: "restore_state");
            return;
        }

        wasActivatedOnce = wasActivatedOnce || activatedOnce || active;
        ApplyState(active, updateTimer: false, emitNotifications: false, playFeedback: false, reason: "restore_state");
    }

    public void ResetSingleUseState()
    {
        wasActivatedOnce = false;
        LogDebug("single_use_reset");
    }

    public void SetLeverAnimatorActive()
    {
        SetLeverAnimatorBool(true);
    }

    public void SetLeverAnimatorInactive()
    {
        SetLeverAnimatorBool(false);
    }

    public void SetLeverAnimatorBool(bool value)
    {
        if (leverAnimator == null)
        {
            leverAnimator = GetComponent<Animator>();
        }

        if (leverAnimator == null || string.IsNullOrWhiteSpace(leverBoolParam))
        {
            return;
        }

        leverAnimator.SetBool(leverBoolParam, value);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!useInteractInput)
        {
            return;
        }

        if (LocalInputRouter.IsInteractConsumed)
        {
            LogDebug("interact_skipped", "reason='already_consumed'");
            return;
        }

        if (InputFocusStack.HasAnyFocus())
        {
            LogDebug("interact_skipped", "reason='input_focus'");
            return;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked())
        {
            LogDebug("interact_skipped", "reason='squad_input_locked'");
            return;
        }

        if (singleUse && wasActivatedOnce)
        {
            LogDebug("interact_skipped", "reason='single_use_consumed'");
            return;
        }

        if (Time.unscaledTime < nextInteractAllowedTime)
        {
            LogDebug("interact_skipped", $"reason='cooldown' retryAt={nextInteractAllowedTime:0.###}");
            return;
        }

        RefreshCurrentCharacter();
        if (!IsLocalCharacterInRange())
        {
            LogDebug("interact_skipped", "reason='out_of_range'");
            return;
        }

        if (!LocalInputRouter.TryConsumeInteract())
        {
            LogDebug("interact_skipped", "reason='consume_failed'");
            return;
        }

        nextInteractAllowedTime = Time.unscaledTime + interactCooldown;
        LogDebug("interact_accepted", $"networked={IsNetworked()}");

        if (IsNetworked())
        {
            RequestInteractServerRpc();
            return;
        }

        ProcessInteractionRequest("local_input");
    }

    private void ProcessInteractionRequest(string source)
    {
        if (singleUse && wasActivatedOnce)
        {
            LogDebug("interaction_ignored", $"source='{source}' reason='single_use_consumed'");
            return;
        }

        switch (activationMode)
        {
            case LeverActivationMode.Timed:
                if (isActive && !allowRetriggerWhileActive)
                {
                    LogDebug("interaction_ignored", $"source='{source}' reason='already_active_no_retrigger'");
                    return;
                }

                ApplyState(true, updateTimer: true, emitNotifications: true, playFeedback: true, reason: source);
                return;

            case LeverActivationMode.Toggle:
                ApplyState(!isActive, updateTimer: true, emitNotifications: true, playFeedback: true, reason: source);
                return;

            case LeverActivationMode.OneShot:
                ApplyState(true, updateTimer: false, emitNotifications: true, playFeedback: true, reason: source);
                return;
        }
    }

    private void SetActiveServer(bool active, bool updateTimer, bool emitNotifications, bool playFeedback, string reason)
    {
        ApplyState(active, updateTimer, emitNotifications, playFeedback, reason);
        netIsActive.Value = isActive;
    }

    private void OnNetStateChanged(bool previous, bool current)
    {
        ApplyState(current, updateTimer: false, emitNotifications: true, playFeedback: true, reason: "network_sync");
    }

    private void HandleCharacterEnter(Collider other)
    {
        if (other == null || other.isTrigger)
        {
            return;
        }

        GameObject character = GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        bool firstCollider = RegisterCharacterCollider(character);
        if (firstCollider && !charactersInRange.Contains(character))
        {
            charactersInRange.Add(character);
            LogDebug("character_enter", $"character='{character.name}' trackedCount={charactersInRange.Count}");
        }

        RefreshCurrentCharacter();
    }

    private void HandleCharacterExit(Collider other)
    {
        if (other == null || other.isTrigger)
        {
            return;
        }

        GameObject character = GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        if (!UnregisterCharacterCollider(character))
        {
            return;
        }

        charactersInRange.Remove(character);
        LogDebug("character_exit", $"character='{character.name}' trackedCount={charactersInRange.Count}");
        RefreshCurrentCharacter();
    }

    private void RefreshCurrentCharacter()
    {
        PruneMissingCharacters();
        if (charactersInRange.Count == 0)
        {
            currentCharacter = null;
            return;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled != null)
        {
            currentCharacter = charactersInRange.Contains(controlled) ? controlled : null;
            return;
        }

        currentCharacter = charactersInRange[0];
    }

    private void PruneMissingCharacters()
    {
        for (int i = charactersInRange.Count - 1; i >= 0; i--)
        {
            if (charactersInRange[i] != null)
            {
                continue;
            }

            charactersInRange.RemoveAt(i);
        }
    }

    private bool IsLocalCharacterInRange()
    {
        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot != null)
        {
            return IsCharacterInRange(localRoot);
        }

        RefreshCurrentCharacter();
        if (currentCharacter != null)
        {
            return true;
        }

        return FindClosestCharacter() != null;
    }

    private bool IsCharacterInRange(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return false;
        }

        ResolveInteractionCenterAndRadius(out Vector3 center, out float radius);
        float distanceSqr = (characterRoot.position - center).sqrMagnitude;
        return distanceSqr <= radius * radius;
    }

    private bool IsAnyCharacterInRange()
    {
        RefreshCurrentCharacter();
        return currentCharacter != null || FindClosestCharacter() != null;
    }

    private GameObject FindClosestCharacter()
    {
        ResolveInteractionCenterAndRadius(out Vector3 center, out float radius);
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        float bestDistance = float.MaxValue;
        GameObject bestCharacter = null;
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject character = GetSquadCharacter(hits[i]);
            if (character == null)
            {
                continue;
            }

            float distance = (character.transform.position - center).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCharacter = character;
            }
        }

        return bestCharacter;
    }

    private void ResolveInteractionCenterAndRadius(out Vector3 center, out float radius)
    {
        Collider boundsCollider = ResolveInteractionBoundsCollider();
        if (boundsCollider != null && useColliderBounds)
        {
            Bounds bounds = boundsCollider.bounds;
            center = bounds.center;
            Vector3 extents = bounds.extents;
            radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) + colliderRadiusPadding;
            return;
        }

        if (interactionTrigger != null)
        {
            center = interactionTrigger.transform.TransformPoint(interactionTrigger.center);
            radius = Mathf.Max(0.05f, interactionTrigger.radius * GetMaxAxis(interactionTrigger.transform.lossyScale) + colliderRadiusPadding);
            return;
        }

        center = transform.position;
        radius = Mathf.Max(0.05f, interactionRadius);
    }

    private Collider ResolveInteractionBoundsCollider()
    {
        if (interactionTrigger != null)
        {
            return interactionTrigger;
        }

        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate == null || !candidate.enabled)
            {
                continue;
            }

            if (candidate is SphereCollider sphere && sphere == interactionTrigger)
            {
                return sphere;
            }

            if (!candidate.isTrigger)
            {
                return candidate;
            }
        }

        return GetComponent<Collider>();
    }

    private void EnsureInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            SphereCollider[] sphereColliders = GetComponents<SphereCollider>();
            for (int i = 0; i < sphereColliders.Length; i++)
            {
                if (sphereColliders[i] != null && sphereColliders[i].isTrigger)
                {
                    interactionTrigger = sphereColliders[i];
                    break;
                }
            }

            if (interactionTrigger == null && sphereColliders.Length > 0)
            {
                interactionTrigger = sphereColliders[0];
            }
        }

        bool created = false;
        if (interactionTrigger == null)
        {
            interactionTrigger = gameObject.AddComponent<SphereCollider>();
            created = true;
        }

        interactionTrigger.isTrigger = true;
        if (created && interactionTrigger.radius <= 0f)
        {
            interactionTrigger.radius = Mathf.Max(0.1f, interactionRadius);
        }

        if (created)
        {
            LogDebug("interaction_trigger_created", $"radius={interactionTrigger.radius:0.###}");
        }
    }

    private bool RegisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            characterColliderCounts[character] = 1;
            return true;
        }

        characterColliderCounts[character] = count + 1;
        return false;
    }

    private bool UnregisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            return false;
        }

        count -= 1;
        if (count > 0)
        {
            characterColliderCounts[character] = count;
            return false;
        }

        characterColliderCounts.Remove(character);
        return true;
    }

    private void ResetInteractionTracking()
    {
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
    }

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        SquadManager manager = SquadManager.Instance;
        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject taggedRoot = null;
        GameObject squadRoot = null;

        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
                taggedRoot = current.gameObject;
            }

            if (manager != null && manager.squadCharacters != null && manager.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null && manager != null && manager.squadCharacters != null)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                    taggedRoot = root.gameObject;
                }

                for (int i = 0; i < manager.squadCharacters.Count; i++)
                {
                    GameObject candidate = manager.squadCharacters[i];
                    if (candidate != null && candidate.transform.IsChildOf(root))
                    {
                        squadRoot = candidate;
                        break;
                    }
                }
            }
        }

        if (squadRoot != null)
        {
            return squadRoot;
        }

        if (requirePlayerTag && hasPlayerTag)
        {
            return taggedRoot;
        }

        return null;
    }

    private bool ApplyState(bool active, bool updateTimer, bool emitNotifications, bool playFeedback, string reason)
    {
        if (isActive == active)
        {
            if (updateTimer && active && activationMode == LeverActivationMode.Timed && allowRetriggerWhileActive)
            {
                RestartDeactivateTimer();
                LogDebug("state_timer_restarted", $"reason='{reason}'");
            }
            else
            {
                LogDebug("state_unchanged", $"reason='{reason}'");
            }

            return false;
        }

        bool previous = isActive;
        isActive = active;
        if (active)
        {
            wasActivatedOnce = true;
        }

        if (setAnimatorBoolOnInteract)
        {
            SetLeverAnimatorBool(isActive);
        }

        if (emitNotifications)
        {
            StateChanged?.Invoke(this, isActive);
            DispatchTargets(isActive);
            InvokeUnityEvents(isActive);
        }

        if (playFeedback)
        {
            PlaySfx(isActive ? activateSfx : deactivateSfx);
        }

        if (updateTimer && activationMode == LeverActivationMode.Timed && isActive)
        {
            RestartDeactivateTimer();
        }
        else
        {
            StopDeactivateTimer();
        }

        LogDebug("state_changed", $"reason='{reason}' previous={previous} current={isActive} mode='{activationMode}' notified={emitNotifications} feedback={playFeedback}");
        return true;
    }

    private void DispatchTargets(bool active)
    {
        StopTargetDispatches();
        if (targetBindings == null || targetBindings.Length == 0)
        {
            return;
        }

        for (int i = 0; i < targetBindings.Length; i++)
        {
            LeverTargetBinding binding = targetBindings[i];
            if (!ShouldNotifyBinding(binding, active))
            {
                continue;
            }

            if (binding.target == null)
            {
                Debug.LogWarning($"[Lever] event='target_missing' lever='{name}' index={i}", this);
                continue;
            }

            if (!(binding.target is ILeverTarget))
            {
                Debug.LogWarning($"[Lever] event='target_invalid' lever='{name}' index={i} target='{binding.target.name}' required='ILeverTarget'", binding.target);
                continue;
            }

            if (binding.delay <= 0f)
            {
                NotifyTarget(binding.target, active);
                continue;
            }

            Coroutine routine = StartCoroutine(NotifyTargetAfterDelay(binding.target, active, binding.delay));
            targetDispatchRoutines.Add(routine);
        }
    }

    private static bool ShouldNotifyBinding(LeverTargetBinding binding, bool active)
    {
        if (binding == null)
        {
            return false;
        }

        return active ? binding.notifyOnActivate : binding.notifyOnDeactivate;
    }

    private IEnumerator NotifyTargetAfterDelay(MonoBehaviour target, bool active, float delay)
    {
        yield return new WaitForSeconds(delay);
        NotifyTarget(target, active);
    }

    private void NotifyTarget(MonoBehaviour target, bool active)
    {
        if (target == null)
        {
            return;
        }

        if (!(target is ILeverTarget leverTarget))
        {
            return;
        }

        leverTarget.HandleLeverStateChanged(this, active);
        LogDebug("target_notified", $"target='{target.name}' active={active}");
    }

    private void StopTargetDispatches()
    {
        for (int i = 0; i < targetDispatchRoutines.Count; i++)
        {
            if (targetDispatchRoutines[i] != null)
            {
                StopCoroutine(targetDispatchRoutines[i]);
            }
        }

        targetDispatchRoutines.Clear();
    }

    private void InvokeUnityEvents(bool active)
    {
        if (active)
        {
            onActivated?.Invoke();
            return;
        }

        onDeactivated?.Invoke();
    }

    private void RestartDeactivateTimer()
    {
        StopDeactivateTimer();
        deactivateRoutine = StartCoroutine(DeactivateWhenEmpty());
    }

    private void StopDeactivateTimer()
    {
        if (deactivateRoutine == null)
        {
            return;
        }

        StopCoroutine(deactivateRoutine);
        deactivateRoutine = null;
    }

    private IEnumerator DeactivateWhenEmpty()
    {
        while (isActive && activationMode == LeverActivationMode.Timed)
        {
            while (isActive && IsAnyCharacterInRange())
            {
                yield return null;
            }

            if (!isActive)
            {
                yield break;
            }

            if (activeDuration <= 0f)
            {
                SetActive(false);
                yield break;
            }

            float elapsed = 0f;
            while (isActive && elapsed < activeDuration)
            {
                if (IsAnyCharacterInRange())
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!isActive)
            {
                yield break;
            }

            if (elapsed >= activeDuration && !IsAnyCharacterInRange())
            {
                SetActive(false);
                yield break;
            }
        }
    }

    private bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInteractServerRpc(ServerRpcParams rpcParams = default)
    {
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        if (!IsCharacterInRange(playerRoot))
        {
            LogDebug("server_interact_rejected", $"reason='out_of_range' clientId={rpcParams.Receive.SenderClientId}");
            return;
        }

        if (singleUse && wasActivatedOnce)
        {
            LogDebug("server_interact_rejected", $"reason='single_use_consumed' clientId={rpcParams.Receive.SenderClientId}");
            return;
        }

        ProcessInteractionRequest($"server_rpc:{rpcParams.Receive.SenderClientId}");
        netIsActive.Value = isActive;
    }

    private void PlaySfx(AudioClipSO clip)
    {
        if (clip == null || clip.audioClip == null)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClip(clip, transform.position);
            return;
        }

        AudioSource.PlayClipAtPoint(clip.audioClip, transform.position, Mathf.Clamp01(clip.volume));
    }

    private void LogDebug(string eventName, string extra = "")
    {
        if (!logDebug)
        {
            return;
        }

        string suffix = string.IsNullOrWhiteSpace(extra) ? string.Empty : $" {extra}";
        Debug.Log(
            $"[Lever] event='{eventName}' lever='{name}' active={isActive} mode='{activationMode}' trackedCharacters={charactersInRange.Count} usedOnce={wasActivatedOnce}{suffix}",
            this);
    }

    private static float GetMaxAxis(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
    }
}
