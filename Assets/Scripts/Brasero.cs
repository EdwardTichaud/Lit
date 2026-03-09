using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Netcode;

// Brasero: source de lumiere qui peut etre allumee ou eteinte.
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(NetworkObject))]
[DisallowMultipleComponent]
public class Brasero : NetworkBehaviour
{
    [Header("State")]
    [SerializeField, Tooltip("Etat du brasero au demarrage.")]
    private bool isLit = false;
    [SerializeField, Tooltip("Identifiant unique utilise pour la sauvegarde.")]
    private string braseroId;

    public bool IsLit => isLit;
    public string BraseroId => braseroId;

    public event Action<Brasero, bool> StateChanged;

    [Header("Visuals")]
    [Tooltip("Racine activee quand le brasero est allume.")]
    public GameObject litRoot;
    [Tooltip("Racine activee quand le brasero est eteint.")]
    public GameObject unlitRoot;
    [Tooltip("Lumiere de flamme optionnelle.")]
    public Light flameLight;
    [Tooltip("Prefabs de flamme optionnels.")]
    [FormerlySerializedAs("flameParticles")]
    public GameObject[] flamePrefabs;
    [Tooltip("Offset local applique lors de l'instanciation des flammes.")]
    public Vector3 flamePrefabsOffset = Vector3.zero;
    [Tooltip("Burst de flamme optionnel instancie lors de l'allumage.")]
    public GameObject flameBurst;
    [Tooltip("Offset local applique lors de l'instanciation du burst de flamme.")]
    public Vector3 flameBurstOffset = Vector3.zero;

    [Header("Interaction")]
    [Tooltip("Ecoute l'input Interact pour allumer/eteindre.")]
    public bool useInteractInput = true;
    [Tooltip("Rayon du trigger d'interaction.")]
    public float interactionRadius = 2f;
    [Tooltip("Centre local du trigger d'interaction.")]
    public Vector3 interactionCenter = Vector3.zero;

    [SerializeField, Tooltip("Collider d'interaction (auto).")]
    private SphereCollider interactionTrigger;

    [Header("Animation")]
    [Tooltip("Animator d'interaction optionnel. Laisse vide pour utiliser celui du personnage.")]
    [FormerlySerializedAs("braseroAnimator")]
    public Animator interactionAnimatorOverride;
    [Tooltip("Nom du state joue lors de l'interaction.")]
    public string interactionStateName = "Brasero_Light";
    [Tooltip("Duree de lock si l'animation n'est pas resolue.")]
    public float interactionFallbackLock = 1f;

    [Header("Flame Emission")]
    [Tooltip("Duree du fondu d'emission (allumage/extinction).")]
    public float emissionFadeDuration = 1f;

    private GameObject[] flameInstances;
    private readonly List<ParticleSystem> flameParticleSystems = new List<ParticleSystem>();
    private readonly Dictionary<ParticleSystem, EmissionBase> emissionBases = new Dictionary<ParticleSystem, EmissionBase>();
    private Coroutine emissionRoutine;
    private float currentEmissionFactor = -1f;
    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private Coroutine interactionRoutine;
    private bool squadInputLocked;
    private bool interactionInProgress;
    private NetworkVariable<bool> netIsLit = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Reset()
    {
        interactionRadius = 2f;
        interactionCenter = Vector3.zero;
        EnsureInteractionTrigger();
        EnsureId();
    }

    private void Awake()
    {
        EnsureInteractionTrigger();
        EnsureId();

    }

    private void EnsureId()
    {
        if (!string.IsNullOrWhiteSpace(braseroId))
        {
            return;
        }

        braseroId = Guid.NewGuid().ToString("N");
    }

    private void OnEnable()
    {
        ApplyVisuals(true);

        if (!useInteractInput)
        {
            return;
        }

        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;

        StopEmissionRoutine();
        StopInteractionRoutine();
        ResetInteractionState();
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

    public void SetLit(bool lit)
    {
        if (IsNetworked())
        {
            if (!IsServer)
            {
                return;
            }

            SetLitServer(lit);
            return;
        }

        SetLitInternal(lit);
    }

    public void Toggle()
    {
        if (IsNetworked())
        {
            if (!IsServer)
            {
                return;
            }

            SetLitServer(!isLit);
            return;
        }

        SetLitInternal(!isLit);
    }

    private void ApplyVisuals(bool immediate)
    {
        if (litRoot != null)
        {
            litRoot.SetActive(isLit);
        }

        if (unlitRoot != null)
        {
            unlitRoot.SetActive(!isLit);
        }

        if (flameLight != null)
        {
            flameLight.enabled = isLit;
        }

        UpdateFlameVisuals(immediate);
    }

    private void UpdateFlameVisuals(bool immediate)
    {
        EnsureFlameInstances();
        CollectFlameParticleSystems();

        if (flameInstances == null || flameInstances.Length == 0)
        {
            return;
        }

        if (isLit)
        {
            SetFlameInstancesActive(true);
            EnsureFlameParticlesPlaying();
        }

        if (immediate || !Application.isPlaying)
        {
            StopEmissionRoutine();
            SetEmissionFactor(isLit ? 1f : 0f);
            if (!isLit)
            {
                SetFlameInstancesActive(false);
            }

            return;
        }

        StartEmissionTransition(isLit ? 1f : 0f, emissionFadeDuration, !isLit);
    }

    private void EnsureFlameInstances()
    {
        if (flamePrefabs == null || flamePrefabs.Length == 0)
        {
            flameInstances = null;
            return;
        }

        if (flameInstances == null || flameInstances.Length != flamePrefabs.Length)
        {
            flameInstances = new GameObject[flamePrefabs.Length];
        }

        for (int i = 0; i < flamePrefabs.Length; i++)
        {
            GameObject prefab = flamePrefabs[i];
            if (prefab == null)
            {
                flameInstances[i] = null;
                continue;
            }

            if (prefab.scene.IsValid() && prefab.scene.isLoaded)
            {
                flameInstances[i] = prefab;
                continue;
            }

            if (!Application.isPlaying || !isLit)
            {
                continue;
            }

            if (flameInstances[i] != null)
            {
                continue;
            }

            Transform parent = litRoot != null ? litRoot.transform : transform;
            Vector3 spawnPosition = parent.position + parent.rotation * flamePrefabsOffset;
            flameInstances[i] = Instantiate(prefab, spawnPosition, parent.rotation, parent);
        }
    }

    private void CollectFlameParticleSystems()
    {
        flameParticleSystems.Clear();
        if (flameInstances == null)
        {
            return;
        }

        for (int i = 0; i < flameInstances.Length; i++)
        {
            GameObject flame = flameInstances[i];
            if (flame == null)
            {
                continue;
            }

            flame.GetComponentsInChildren(true, flameParticleSystems);
        }

        for (int i = flameParticleSystems.Count - 1; i >= 0; i--)
        {
            ParticleSystem system = flameParticleSystems[i];
            if (system == null)
            {
                flameParticleSystems.RemoveAt(i);
                continue;
            }

            if (!emissionBases.ContainsKey(system))
            {
                CacheEmissionBase(system);
            }
        }
    }

    private void EnsureFlameParticlesPlaying()
    {
        for (int i = 0; i < flameParticleSystems.Count; i++)
        {
            ParticleSystem system = flameParticleSystems[i];
            if (system == null)
            {
                continue;
            }

            if (!system.isPlaying)
            {
                system.Play();
            }
        }
    }

    private void SetFlameInstancesActive(bool active)
    {
        if (flameInstances == null)
        {
            return;
        }

        for (int i = 0; i < flameInstances.Length; i++)
        {
            GameObject flame = flameInstances[i];
            if (flame == null)
            {
                continue;
            }

            if (flame.activeSelf != active)
            {
                flame.SetActive(active);
            }
        }
    }

    private void StartEmissionTransition(float target, float duration, bool deactivateAfter)
    {
        StopEmissionRoutine();

        if (flameParticleSystems.Count == 0)
        {
            if (deactivateAfter && target <= 0f)
            {
                SetFlameInstancesActive(false);
            }

            return;
        }

        float start = currentEmissionFactor;
        if (start < 0f)
        {
            start = target > 0f ? 0f : 1f;
            currentEmissionFactor = start;
        }

        if (duration <= 0f || Mathf.Approximately(start, target))
        {
            SetEmissionFactor(target);
            if (deactivateAfter && target <= 0f)
            {
                SetFlameInstancesActive(false);
            }

            return;
        }

        emissionRoutine = StartCoroutine(AnimateEmission(start, target, duration, deactivateAfter));
    }

    private IEnumerator AnimateEmission(float start, float target, float duration, bool deactivateAfter)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            SetEmissionFactor(Mathf.Lerp(start, target, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetEmissionFactor(target);

        if (deactivateAfter && target <= 0f)
        {
            SetFlameInstancesActive(false);
        }

        emissionRoutine = null;
    }

    private void StopEmissionRoutine()
    {
        if (emissionRoutine != null)
        {
            StopCoroutine(emissionRoutine);
            emissionRoutine = null;
        }
    }

    private void SetEmissionFactor(float factor)
    {
        currentEmissionFactor = factor;

        for (int i = 0; i < flameParticleSystems.Count; i++)
        {
            ParticleSystem system = flameParticleSystems[i];
            if (system == null)
            {
                continue;
            }

            if (!emissionBases.TryGetValue(system, out EmissionBase baseRates))
            {
                baseRates = CacheEmissionBase(system);
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = baseRates.rateOverTime * factor;
            emission.rateOverDistanceMultiplier = baseRates.rateOverDistance * factor;
        }
    }

    private EmissionBase CacheEmissionBase(ParticleSystem system)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        EmissionBase baseRates = new EmissionBase
        {
            rateOverTime = emission.rateOverTimeMultiplier,
            rateOverDistance = emission.rateOverDistanceMultiplier
        };
        emissionBases[system] = baseRates;
        return baseRates;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!useInteractInput)
        {
            return;
        }

        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        if (interactionInProgress)
        {
            return;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked())
        {
            return;
        }

        if (!IsLocalCharacterInRange())
        {
            return;
        }

        if (IsNetworked())
        {
            RequestInteractServerRpc();
            return;
        }

        RefreshCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        StartInteraction();
    }

    public override void OnNetworkSpawn()
    {
        netIsLit.OnValueChanged += OnNetLitChanged;
        if (IsServer)
        {
            netIsLit.Value = isLit;
        }
        else
        {
            ApplyNetState(netIsLit.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        netIsLit.OnValueChanged -= OnNetLitChanged;
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
        RefreshCurrentCharacter();
    }

    private void RefreshCurrentCharacter()
    {
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

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject taggedPlayerRoot = null;
        GameObject squadRoot = null;
        bool hasSquadList = SquadManager.Instance != null && SquadManager.Instance.squadCharacters != null;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
                taggedPlayerRoot = current.gameObject;
            }

            if (hasSquadList && SquadManager.Instance.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null && hasSquadList)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                    taggedPlayerRoot = root.gameObject;
                }

                for (int i = 0; i < SquadManager.Instance.squadCharacters.Count; i++)
                {
                    GameObject candidate = SquadManager.Instance.squadCharacters[i];
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

        if (hasPlayerTag && taggedPlayerRoot != null)
        {
            return taggedPlayerRoot;
        }

        return null;
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

    private void ResetInteractionState()
    {
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
    }

    private void StartInteraction()
    {
        if (interactionRoutine != null)
        {
            return;
        }

        interactionRoutine = StartCoroutine(HandleInteractionRoutine());
    }

    private IEnumerator HandleInteractionRoutine()
    {
        interactionInProgress = true;

        Toggle();

        bool playedAnimation = false;
        float lockDuration = 0f;
        Animator animator = ResolveInteractionAnimator();
        if (animator != null && !string.IsNullOrWhiteSpace(interactionStateName))
        {
            int stateHash = Animator.StringToHash(interactionStateName);
            if (animator.HasState(0, stateHash))
            {
                playedAnimation = true;
                SetSquadInputLock(true);
                animator.Play(stateHash, 0, 0f);
                yield return null;
                lockDuration = ResolveStateDuration(animator, stateHash);
            }
            else
            {
                Debug.LogWarning($"Brasero: Animator state '{interactionStateName}' introuvable.", this);
            }
        }

        if (playedAnimation)
        {
            if (lockDuration <= 0f)
            {
                lockDuration = Mathf.Max(0f, interactionFallbackLock);
            }

            if (lockDuration > 0f)
            {
                yield return new WaitForSeconds(lockDuration);
            }

            SetSquadInputLock(false);
        }

        interactionInProgress = false;
        interactionRoutine = null;
    }

    private bool IsLocalCharacterInRange()
    {
        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot != null)
        {
            return IsCharacterInRange(localRoot);
        }

        RefreshCurrentCharacter();
        return currentCharacter != null;
    }

    private bool IsCharacterInRange(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return false;
        }

        Vector3 center = transform.TransformPoint(interactionCenter);
        float radius = Mathf.Max(0f, interactionRadius);
        float distanceSqr = (characterRoot.position - center).sqrMagnitude;
        return distanceSqr <= radius * radius;
    }

    private void OnNetLitChanged(bool previous, bool current)
    {
        ApplyNetState(current);
    }

    private void ApplyNetState(bool lit)
    {
        isLit = lit;
        ApplyVisuals(!Application.isPlaying);
        if (isLit)
        {
            SpawnFlameBurst();
        }
        StateChanged?.Invoke(this, isLit);
    }

    private void SetLitServer(bool lit)
    {
        SetLitInternal(lit);
        netIsLit.Value = lit;
    }

    private void SetLitInternal(bool lit)
    {
        if (isLit == lit)
        {
            return;
        }

        bool wasLit = isLit;
        isLit = lit;
        ApplyVisuals(!Application.isPlaying);
        if (!wasLit && isLit)
        {
            SpawnFlameBurst();
        }
        StateChanged?.Invoke(this, isLit);
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
            return;
        }

        SetLitServer(!isLit);
    }

    private Animator ResolveInteractionAnimator()
    {
        Animator animator = null;

        if (currentCharacter != null)
        {
            animator = currentCharacter.GetComponent<Animator>();
            if (animator == null)
            {
                animator = currentCharacter.GetComponentInChildren<Animator>(true);
            }
        }

        if (animator != null)
        {
            return animator;
        }

        if (interactionAnimatorOverride != null)
        {
            return interactionAnimatorOverride;
        }

        return null;
    }

    private float ResolveStateDuration(Animator animator, int stateHash)
    {
        if (animator == null)
        {
            return 0f;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (!IsStateMatch(stateInfo, stateHash))
        {
            AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(0);
            if (IsStateMatch(nextInfo, stateHash))
            {
                stateInfo = nextInfo;
            }
        }

        if (!IsStateMatch(stateInfo, stateHash))
        {
            return 0f;
        }

        if (stateInfo.loop)
        {
            return 0f;
        }

        float speed = Mathf.Abs(stateInfo.speed * animator.speed);
        if (speed <= 0.0001f)
        {
            speed = 1f;
        }

        return Mathf.Max(0f, stateInfo.length / speed);
    }

    private static bool IsStateMatch(AnimatorStateInfo stateInfo, int stateHash)
    {
        return stateInfo.shortNameHash == stateHash || stateInfo.fullPathHash == stateHash;
    }

    private void SetSquadInputLock(bool locked)
    {
        if (SquadManager.Instance == null)
        {
            return;
        }

        if (locked)
        {
            if (squadInputLocked)
            {
                return;
            }

            SquadManager.Instance.SetInputLocked(true);
            squadInputLocked = true;
            return;
        }

        if (!squadInputLocked)
        {
            return;
        }

        SquadManager.Instance.SetInputLocked(false);
        squadInputLocked = false;
    }

    private void StopInteractionRoutine()
    {
        if (interactionRoutine != null)
        {
            StopCoroutine(interactionRoutine);
            interactionRoutine = null;
        }

        interactionInProgress = false;
        SetSquadInputLock(false);
    }

    private void EnsureInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            SphereCollider[] colliders = GetComponents<SphereCollider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                {
                    interactionTrigger = colliders[i];
                    break;
                }
            }
        }

        if (interactionTrigger == null)
        {
            interactionTrigger = gameObject.AddComponent<SphereCollider>();
        }

        if (interactionTrigger == null)
        {
            return;
        }

        interactionTrigger.isTrigger = true;
        interactionTrigger.radius = Mathf.Max(0f, interactionRadius);
        interactionTrigger.center = interactionCenter;
    }

    private void SpawnFlameBurst()
    {
        if (!Application.isPlaying || flameBurst == null)
        {
            return;
        }

        Transform spawn = transform;
        if (flameLight != null)
        {
            spawn = flameLight.transform;
        }
        else if (litRoot != null)
        {
            spawn = litRoot.transform;
        }
        Vector3 spawnPosition = spawn.position + spawn.rotation * flameBurstOffset;
        Instantiate(flameBurst, spawnPosition, spawn.rotation, spawn);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawOffsetGizmos();
    }

    private void OnValidate()
    {
        EnsureId();
        EnsureInteractionTrigger();
        if (!Application.isPlaying)
        {
            ApplyVisuals(true);
        }
    }

    private void DrawOffsetGizmos()
    {
        Transform flameParent = litRoot != null ? litRoot.transform : transform;
        Vector3 flameBase = flameParent.position;
        Vector3 flameOffsetWorld = flameParent.rotation * flamePrefabsOffset;
        Vector3 flamePosition = flameBase + flameOffsetWorld;

        Transform burstParent = transform;
        if (flameLight != null)
        {
            burstParent = flameLight.transform;
        }
        else if (litRoot != null)
        {
            burstParent = litRoot.transform;
        }

        Vector3 burstBase = burstParent.position;
        Vector3 burstOffsetWorld = burstParent.rotation * flameBurstOffset;
        Vector3 burstPosition = burstBase + burstOffsetWorld;

        const float markerSize = 0.08f;

        Gizmos.color = new Color(1f, 0.55f, 0.1f, 1f);
        Gizmos.DrawLine(flameBase, flamePosition);
        Gizmos.DrawSphere(flamePosition, markerSize);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 1f);
        Gizmos.DrawLine(burstBase, burstPosition);
        Gizmos.DrawSphere(burstPosition, markerSize);
    }
#endif

    private struct EmissionBase
    {
        public float rateOverTime;
        public float rateOverDistance;
    }
}
