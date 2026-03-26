using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Objet de monde destructible via une capacite accordee par un item equipe.
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class DestructibleObject : NetworkBehaviour
{
    [Header("Destruction")]
    [Tooltip("Si false, l'objet ne peut pas etre detruit.")]
    public bool destructible = true;
    [Tooltip("Capacite requise pour detruire cet objet.")]
    public InteractionCapability requiredCapability = InteractionCapability.None;
    [Tooltip("Feedback si le personnage n'a pas l'equipement requis.")]
    public string cannotDestroyMessage = "Impossible de detruire cet objet.";
    [Tooltip("Feedback optionnel si la destruction reussit.")]
    public string destroySuccessMessage = "Objet detruit.";
    [Tooltip("Retard avant destruction definitive.")]
    public float destroyDelay = 0f;
    [Tooltip("Desactive les colliders pendant la destruction.")]
    public bool disableCollidersOnDestroy = true;
    [Tooltip("Desactive les renderers si aucun root visuel n'est defini.")]
    public bool disableRenderersOnDestroy = true;
    [Tooltip("Root visuel a masquer immediatement lors de la destruction.")]
    public GameObject visualRootToDisable;
    [Tooltip("Effet instancie lors de la destruction.")]
    public GameObject destroyEffectPrefab;
    [Tooltip("Son joue lors de la destruction.")]
    public AudioClip destroySound;
    [Tooltip("Active des logs de debug.")]
    public bool logDestroyDebug = false;

    [Header("Interaction")]
    [Tooltip("Trigger d'interaction. Laisse vide pour auto-detecter.")]
    public Collider interactionTrigger;

    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private readonly NetworkVariable<bool> netDestroyed = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private GameObject currentCharacter;
    private bool useSelfTriggerEvents;
    private bool isDestroyed;
    private bool destroyedStateApplied;
    private bool finalDestroyIssued;

    private void Awake()
    {
        InitializeInteractionTrigger();
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
    }

    public override void OnNetworkSpawn()
    {
        netDestroyed.OnValueChanged += OnNetDestroyedChanged;
        if (netDestroyed.Value)
        {
            isDestroyed = true;
            ApplyDestroyedState();
        }
    }

    public override void OnNetworkDespawn()
    {
        netDestroyed.OnValueChanged -= OnNetDestroyedChanged;
    }

    private void OnNetDestroyedChanged(bool previousValue, bool newValue)
    {
        if (!newValue)
        {
            return;
        }

        isDestroyed = true;
        ApplyDestroyedState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useSelfTriggerEvents)
        {
            return;
        }

        HandleCharacterEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useSelfTriggerEvents)
        {
            return;
        }

        HandleCharacterExit(other);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isDestroyed || InputFocusStack.HasAnyFocus())
        {
            return;
        }

        UpdateCurrentCharacter();
        if (currentCharacter == null)
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();
        HandleInteract();
    }

    private void HandleInteract()
    {
        if (isDestroyed)
        {
            return;
        }

        if (IsNetworkedRuntime() && !IsServer)
        {
            RequestDestroyServerRpc();
            return;
        }

        SquadCharacterController controller = GetCurrentCharacterController();
        if (!TryDestroy(controller, out string feedback))
        {
            ShowFeedback(feedback);
            return;
        }

        ShowFeedback(feedback);
    }

    public bool CanBeDestroyedBy(SquadCharacterController controller)
    {
        return CanBeDestroyedBy(controller, out _);
    }

    public bool CanBeDestroyedBy(SquadCharacterController controller, out string reason)
    {
        if (isDestroyed)
        {
            reason = string.Empty;
            return false;
        }

        if (!destructible)
        {
            reason = GetCannotDestroyFeedback();
            return false;
        }

        if (controller == null)
        {
            reason = GetCannotDestroyFeedback();
            return false;
        }

        if (requiredCapability != InteractionCapability.None &&
            !controller.HasEquippedInteractionCapability(requiredCapability))
        {
            reason = GetCannotDestroyFeedback();
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryDestroy(SquadCharacterController controller)
    {
        return TryDestroy(controller, out _);
    }

    public bool TryDestroy(SquadCharacterController controller, out string feedback)
    {
        if (!CanBeDestroyedBy(controller, out feedback))
        {
            return false;
        }

        BeginDestroySequence();
        feedback = GetDestroySuccessFeedback();
        return true;
    }

    private void BeginDestroySequence()
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;
        if (IsServer)
        {
            netDestroyed.Value = true;
        }

        ApplyDestroyedState();
        if (destroyDelay <= 0f)
        {
            FinalizeDestroy();
            return;
        }

        StartCoroutine(FinalizeDestroyAfterDelay());
    }

    private IEnumerator FinalizeDestroyAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, destroyDelay));
        FinalizeDestroy();
    }

    private void FinalizeDestroy()
    {
        if (finalDestroyIssued)
        {
            return;
        }

        finalDestroyIssued = true;
        if (IsNetworkedRuntime() && IsServer)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
                return;
            }
        }

        Destroy(gameObject);
    }

    private void ApplyDestroyedState()
    {
        if (destroyedStateApplied)
        {
            return;
        }

        destroyedStateApplied = true;
        if (logDestroyDebug)
        {
            Debug.Log($"DestructibleObject: destruction appliquee sur '{name}'.", this);
        }

        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, transform.position, transform.rotation);
        }

        if (destroySound != null)
        {
            AudioSource.PlayClipAtPoint(destroySound, transform.position);
        }

        if (disableCollidersOnDestroy)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        if (visualRootToDisable != null)
        {
            visualRootToDisable.SetActive(false);
            return;
        }

        if (!disableRenderersOnDestroy)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }
    }

    private string GetCannotDestroyFeedback()
    {
        if (!string.IsNullOrWhiteSpace(cannotDestroyMessage))
        {
            return cannotDestroyMessage;
        }

        return "Impossible de detruire cet objet.";
    }

    private string GetDestroySuccessFeedback()
    {
        if (!string.IsNullOrWhiteSpace(destroySuccessMessage))
        {
            return destroySuccessMessage;
        }

        return "Objet detruit.";
    }

    private bool IsNetworkedRuntime()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private void ShowFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
    }

    private void UpdateCurrentCharacter()
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

        UpdateCurrentCharacter();
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
        UpdateCurrentCharacter();
    }

    public void NotifyTriggerEnter(Collider other)
    {
        HandleCharacterEnter(other);
    }

    public void NotifyTriggerExit(Collider other)
    {
        HandleCharacterExit(other);
    }

    private void InitializeInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                {
                    interactionTrigger = colliders[i];
                    break;
                }
            }

            if (interactionTrigger == null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        interactionTrigger = colliders[i];
                        break;
                    }
                }
            }
        }

        if (interactionTrigger == null)
        {
            Debug.LogWarning("DestructibleObject: aucun collider trouve pour l'interaction.", this);
            useSelfTriggerEvents = false;
            return;
        }

        if (!interactionTrigger.isTrigger || IsConcaveMeshCollider(interactionTrigger))
        {
            Collider fallback = CreateBoxTrigger(interactionTrigger);
            if (fallback != null)
            {
                interactionTrigger = fallback;
            }
        }

        useSelfTriggerEvents = interactionTrigger.gameObject == gameObject;
        if (!useSelfTriggerEvents)
        {
            DestructibleObjectTriggerProxy proxy = interactionTrigger.GetComponent<DestructibleObjectTriggerProxy>();
            if (proxy == null)
            {
                proxy = interactionTrigger.gameObject.AddComponent<DestructibleObjectTriggerProxy>();
            }

            proxy.Owner = this;
        }
    }

    private static bool IsConcaveMeshCollider(Collider collider)
    {
        MeshCollider meshCollider = collider as MeshCollider;
        return meshCollider != null && !meshCollider.convex;
    }

    private Collider CreateBoxTrigger(Collider reference)
    {
        if (reference == null)
        {
            return null;
        }

        BoxCollider box = reference.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        FitBoxToCollider(box, reference);
        return box;
    }

    private void FitBoxToCollider(BoxCollider box, Collider reference)
    {
        if (box == null)
        {
            return;
        }

        if (reference == null)
        {
            box.center = Vector3.zero;
            box.size = Vector3.one;
            return;
        }

        if (reference is BoxCollider boxCollider)
        {
            box.center = boxCollider.center;
            box.size = boxCollider.size;
            return;
        }

        if (reference is SphereCollider sphereCollider)
        {
            float diameter = sphereCollider.radius * 2f;
            box.center = sphereCollider.center;
            box.size = new Vector3(diameter, diameter, diameter);
            return;
        }

        if (reference is CapsuleCollider capsuleCollider)
        {
            float diameter = capsuleCollider.radius * 2f;
            box.center = capsuleCollider.center;
            box.size = new Vector3(diameter, capsuleCollider.height, diameter);
            return;
        }

        Bounds bounds = reference.bounds;
        box.center = reference.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = reference.transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
    }

    private SquadCharacterController GetCurrentCharacterController()
    {
        if (currentCharacter == null)
        {
            return null;
        }

        return currentCharacter.GetComponent<SquadCharacterController>();
    }

    private SquadCharacterController GetControllerFromRoot(Transform playerRoot)
    {
        if (playerRoot == null)
        {
            return null;
        }

        SquadCharacterController controller = playerRoot.GetComponent<SquadCharacterController>();
        if (controller != null)
        {
            return controller;
        }

        return playerRoot.GetComponentInChildren<SquadCharacterController>(true);
    }

    private bool IsCharacterInRange(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return false;
        }

        Collider col = interactionTrigger != null ? interactionTrigger : GetComponent<Collider>();
        if (col == null)
        {
            return true;
        }

        Vector3 closest = col.ClosestPoint(characterRoot.position);
        float distanceSqr = (closest - characterRoot.position).sqrMagnitude;
        return distanceSqr <= 0.25f;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDestroyServerRpc(ServerRpcParams rpcParams = default)
    {
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        if (!IsCharacterInRange(playerRoot))
        {
            return;
        }

        SquadCharacterController controller = GetControllerFromRoot(playerRoot);
        if (controller == null)
        {
            return;
        }

        if (!TryDestroy(controller, out string feedback))
        {
            ShowFeedbackClientRpc(feedback, BuildClientRpcParams(rpcParams));
        }
    }

    [ClientRpc]
    private void ShowFeedbackClientRpc(string message, ClientRpcParams rpcParams = default)
    {
        ShowFeedback(message);
    }

    private static ClientRpcParams BuildClientRpcParams(ServerRpcParams rpcParams)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
            }
        };
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
}

public class DestructibleObjectTriggerProxy : MonoBehaviour
{
    public DestructibleObject Owner { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        if (Owner != null)
        {
            Owner.NotifyTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (Owner != null)
        {
            Owner.NotifyTriggerExit(other);
        }
    }
}
