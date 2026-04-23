using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class IustiaIdolPrayer : MonoBehaviour, ICharacterDetectedInteractable
{
    private static readonly List<IustiaIdolPrayer> activeIdols = new List<IustiaIdolPrayer>();

    [SerializeField, Tooltip("Collider utilise pour detecter l'interaction avec l'idole.")]
    private Collider interactionCollider;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale d'interaction avec l'idole.")]
    private float interactionMaxDistance = 2f;
    [SerializeField, Tooltip("Arrete la priere locale quand le joueur sort de la portee.")]
    private bool stopPrayerWhenOutOfRange = true;

    private GameObject currentCharacter;

    public static bool LocalPrayerActive { get; private set; }

    private void Awake()
    {
        ResolveInteractionCollider();
    }

    private void OnEnable()
    {
        if (!activeIdols.Contains(this))
        {
            activeIdols.Add(this);
        }

        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteract;
    }

    private void OnDisable()
    {
        activeIdols.Remove(this);
        LocalInputRouter.Interact -= OnInteract;

        if (LocalPrayerActive && CombatSessionManager.Instance != null)
        {
            CombatSessionManager.Instance.RequestStopPrayerFromLocal();
        }

        if (currentCharacter != null)
        {
            currentCharacter = null;
        }
    }

    private void Update()
    {
        if (!stopPrayerWhenOutOfRange || !LocalPrayerActive || currentCharacter == null)
        {
            return;
        }

        SquadCharacterController controller = currentCharacter.GetComponentInChildren<SquadCharacterController>(true);
        if (controller == null || !IsControllerInRange(controller))
        {
            CombatSessionManager.EnsureInstance()?.RequestStopPrayerFromLocal();
        }
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null && isActiveAndEnabled && !CombatSessionManager.IsCharacterInCombat(controller);
    }

    public Collider GetInteractionDetectionCollider()
    {
        return ResolveInteractionCollider();
    }

    public Transform GetInteractionAnchor()
    {
        return transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return Mathf.Max(0.1f, interactionMaxDistance);
    }

    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return 150;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        if (currentCharacter == character)
        {
            return;
        }

        if (currentCharacter != null && character == null && LocalPrayerActive)
        {
            CombatSessionManager.EnsureInstance()?.RequestStopPrayerFromLocal();
        }

        currentCharacter = character;
    }

    public static void SetLocalPrayerState(bool active)
    {
        LocalPrayerActive = active;
    }

    public static bool IsAnyIdolInRange(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return false;
        }

        for (int i = activeIdols.Count - 1; i >= 0; i--)
        {
            IustiaIdolPrayer idol = activeIdols[i];
            if (idol == null)
            {
                activeIdols.RemoveAt(i);
                continue;
            }

            if (idol.isActiveAndEnabled && idol.IsControllerInRange(controller))
            {
                return true;
            }
        }

        return false;
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (currentCharacter == null || InputFocusStack.HasAnyFocus())
        {
            return;
        }

        SquadCharacterController controller = currentCharacter.GetComponentInChildren<SquadCharacterController>(true);
        if (controller == null || !IsControllerInRange(controller))
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();
        CombatSessionManager.EnsureInstance()?.RequestTogglePrayerFromLocal(controller, this);
    }

    private bool IsControllerInRange(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return false;
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            controller.transform,
            ResolveInteractionCollider(),
            transform,
            interactionMaxDistance);
    }

    private Collider ResolveInteractionCollider()
    {
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        return interactionCollider;
    }
}

public sealed class IustiaIdolPrayerAutoInstaller : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeInstaller()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<IustiaIdolPrayerAutoInstaller>() != null)
#else
        if (FindObjectOfType<IustiaIdolPrayerAutoInstaller>() != null)
#endif
        {
            return;
        }

        GameObject host = new GameObject("IustiaIdolPrayerAutoInstaller");
        DontDestroyOnLoad(host);
        host.AddComponent<IustiaIdolPrayerAutoInstaller>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        InstallInScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallInScene();
    }

    private static void InstallInScene()
    {
#if UNITY_2023_1_OR_NEWER
        InteractableItem[] items = FindObjectsByType<InteractableItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        InteractableItem[] items = FindObjectsOfType<InteractableItem>();
#endif
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            InteractableItem item = items[i];
            if (item == null || item.GetComponent<IustiaIdolPrayer>() != null)
            {
                continue;
            }

            if (LooksLikeIustiaIdol(item))
            {
                item.gameObject.AddComponent<IustiaIdolPrayer>();
            }
        }
    }

    private static bool LooksLikeIustiaIdol(InteractableItem item)
    {
        if (item == null)
        {
            return false;
        }

        Item represented = item.representedItem;
        string raw = $"{item.name} {represented?.name} {represented?.itemId} {represented?.itemName}";
        string normalized = NormalizeForSearch(raw);
        return normalized.Contains("iustia") && (normalized.Contains("idole") || normalized.Contains("idol"));
    }

    private static string NormalizeForSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string lower = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(lower.Length);
        for (int i = 0; i < lower.Length; i++)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(lower[i]);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(lower[i]);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
