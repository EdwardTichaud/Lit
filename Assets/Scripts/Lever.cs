using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Levier interactif avec timer de desactivation.
public class Lever : MonoBehaviour
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

    private PlayerInputs playerInputs;
    private Collider leverCollider;
    private Coroutine deactivateRoutine;

    private void Awake()
    {
        if (!useInteractInput)
        {
            return;
        }

        if (leverAnimator == null)
        {
            leverAnimator = GetComponent<Animator>();
        }

        leverCollider = GetComponent<Collider>();
        playerInputs = new PlayerInputs();
    }

    private void OnEnable()
    {
        if (!useInteractInput)
        {
            return;
        }

        if (playerInputs == null)
        {
            playerInputs = new PlayerInputs();
        }

        playerInputs.Enable();
        playerInputs.Player.Interact.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        if (!useInteractInput)
        {
            return;
        }

        if (playerInputs != null)
        {
            playerInputs.Player.Interact.performed -= OnInteractPerformed;
            playerInputs.Disable();
        }

        StopDeactivateTimer();
    }

    public void SetActive(bool active)
    {
        if (isActive == active)
        {
            if (active)
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

        if (isActive)
        {
            RestartDeactivateTimer();
        }
        else
        {
            StopDeactivateTimer();
        }
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

        // Active le levier uniquement si un personnage est a portee.
        if (!IsCharacterInRange())
        {
            return;
        }

        SetActive(true);
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

    private bool IsCharacterInRange()
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
            while (isActive && IsCharacterInRange())
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
                if (IsCharacterInRange())
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

            if (elapsed >= activeDuration && !IsCharacterInRange())
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
