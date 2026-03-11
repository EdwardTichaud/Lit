using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

// Levier interactif avec timer de desactivation.
[RequireComponent(typeof(NetworkObject))]
public class Lever : NetworkBehaviour
{
    [Header("Interact")]
    [Tooltip("Ecoute l'input Interact pour activer le levier.")]
    public bool useInteractInput = true;
    [Tooltip("Exige un tag Player si aucun personnage de squad n'est trouve.")]
    public bool requirePlayerTag = true;
    [Tooltip("Pilote le bool de l'Animator lors de l'activation.")]
    public bool setAnimatorBoolOnInteract = true;

    [Header("Interact Range")]
    [Tooltip("Utilise le bounds du collider pour estimer le rayon.")]
    public bool useColliderBounds = true;
    [Tooltip("Rayon manuel d'interaction.")]
    public float interactionRadius = 1.25f;
    [Tooltip("Padding ajoute au rayon du collider.")]
    public float colliderRadiusPadding = 0.1f;

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
    [Tooltip("Temps avant desactivation si aucun personnage ne reste proche.")]
    public float activeDuration = 1f;

    [Header("State")]
    [SerializeField, Tooltip("Etat courant du levier (debug).")]
    private bool isActive;

    public bool IsActive => isActive;

    public event Action<Lever, bool> StateChanged;

    private Collider leverCollider;
    private Coroutine deactivateRoutine;
    private NetworkVariable<bool> netIsActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (leverAnimator == null)
        {
            leverAnimator = GetComponent<Animator>();
        }

        leverCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (!useInteractInput)
        {
            return;
        }

        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
    }

    private void OnDisable()
    {
        if (!useInteractInput)
        {
            return;
        }

        LocalInputRouter.Interact -= OnInteractPerformed;

        StopDeactivateTimer();
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
            ApplyState(netIsActive.Value, false);
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

            SetActiveServer(active);
            return;
        }

        SetActiveInternal(active, true);
    }

    public void ApplySnapshotState(bool active)
    {
        ApplyState(active, false);
    }

    private void SetActiveServer(bool active)
    {
        SetActiveInternal(active, true);
        netIsActive.Value = active;
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

        // Active le levier uniquement si un personnage local est a portee.
        if (!IsLocalCharacterInRange())
        {
            return;
        }

        if (IsNetworked())
        {
            RequestInteractServerRpc();
            return;
        }

        SetActiveInternal(true, true);
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

    private bool IsLocalCharacterInRange()
    {
        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot == null)
        {
            return FindClosestCharacter() != null;
        }

        return IsCharacterInRange(localRoot);
    }

    private bool IsCharacterInRange(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return false;
        }

        Vector3 center = transform.position;
        float radius = interactionRadius;

        if (leverCollider != null && useColliderBounds)
        {
            Bounds bounds = leverCollider.bounds;
            center = bounds.center;
            Vector3 extents = bounds.extents;
            radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) + colliderRadiusPadding;
        }

        float distanceSqr = (characterRoot.position - center).sqrMagnitude;
        return distanceSqr <= radius * radius;
    }

    private bool IsAnyCharacterInRange()
    {
        return FindClosestCharacter() != null;
    }

    private GameObject FindClosestCharacter()
    {
        Vector3 center = transform.position;
        float radius = interactionRadius;

        if (leverCollider != null && useColliderBounds)
        {
            Bounds bounds = leverCollider.bounds;
            center = bounds.center;
            Vector3 extents = bounds.extents;
            radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) + colliderRadiusPadding;
        }

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

    private void OnNetStateChanged(bool previous, bool current)
    {
        ApplyState(current, false);
    }

    private void SetActiveInternal(bool active, bool updateTimer)
    {
        ApplyState(active, updateTimer);
    }

    private void ApplyState(bool active, bool updateTimer)
    {
        if (isActive == active)
        {
            if (updateTimer && active)
            {
                RestartDeactivateTimer();
            }

            return;
        }

        isActive = active;
        if (setAnimatorBoolOnInteract)
        {
            SetLeverAnimatorBool(isActive);
        }

        StateChanged?.Invoke(this, isActive);
        PlaySfx(isActive ? activateSfx : deactivateSfx);

        if (!updateTimer)
        {
            return;
        }

        if (isActive)
        {
            RestartDeactivateTimer();
        }
        else
        {
            StopDeactivateTimer();
        }
    }

    private bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInteractServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!NetcodeServerRpcValidation.TryResolvePlayerContext(
                this,
                rpcParams,
                out NetcodeServerRpcValidation.PlayerContext context,
                out string reason,
                requireController: false,
                requireInventory: false))
        {
            ShowFeedbackClientRpc(reason, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
            return;
        }

        Vector3 center = transform.position;
        float radius = interactionRadius;
        if (leverCollider != null && useColliderBounds)
        {
            Bounds bounds = leverCollider.bounds;
            center = bounds.center;
            Vector3 extents = bounds.extents;
            radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) + colliderRadiusPadding;
        }

        if (!NetcodeServerRpcValidation.TryValidateRange(this, context, center, radius, "activer le levier", out reason))
        {
            ShowFeedbackClientRpc(reason, NetcodeServerRpcValidation.BuildClientRpcParams(rpcParams));
            return;
        }

        SetActiveServer(true);
    }

    [ClientRpc]
    private void ShowFeedbackClientRpc(string message, ClientRpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
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

    private void RestartDeactivateTimer()
    {
        StopDeactivateTimer();
        deactivateRoutine = StartCoroutine(DeactivateWhenEmpty());
    }

    private void StopDeactivateTimer()
    {
        if (deactivateRoutine != null)
        {
            StopCoroutine(deactivateRoutine);
            deactivateRoutine = null;
        }
    }

    private System.Collections.IEnumerator DeactivateWhenEmpty()
    {
        // Attend que la zone soit vide, puis lance le timer avant extinction.
        while (isActive)
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
}
