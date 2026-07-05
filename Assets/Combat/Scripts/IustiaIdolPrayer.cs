using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Role: interaction d'idole qui active/desactive une priere de soutien en combat.
// Usage: ajoutee aux idoles Iustia de scene, manuellement ou par l'auto-installer.
// Responsibilities: detecter le personnage, transmettre la priere au CombatSessionManager, auto-installer les idoles existantes.
// Dependencies: ICharacterDetectedInteractable, LocalInputRouter, CombatSessionManager, InteractableItem.
// Precautions: l'auto-installer se base sur les noms d'items; verifier les faux positifs si les assets changent.
/// <summary>
/// Interactable permettant au joueur de prier une idole Iustia pendant un combat.
/// </summary>
public class IustiaIdolPrayer : MonoBehaviour, ICharacterDetectedInteractable
{
    private static readonly List<IustiaIdolPrayer> activeIdols = new List<IustiaIdolPrayer>();

    /// <summary>
    /// Collider utilise pour mesurer la distance d'interaction.
    /// </summary>
    [SerializeField, Tooltip("Collider utilise pour detecter l'interaction avec l'idole.")]
    private Collider interactionCollider;
    /// <summary>
    /// Distance maximale autorisee entre le personnage et l'idole.
    /// </summary>
    [SerializeField, Min(0.1f), Tooltip("Distance maximale d'interaction avec l'idole.")]
    private float interactionMaxDistance = 2f;
    /// <summary>
    /// Si vrai, la priere s'arrete quand le personnage sort de portee.
    /// </summary>
    [SerializeField, Tooltip("Arrete la priere locale quand le joueur sort de la portee.")]
    private bool stopPrayerWhenOutOfRange = true;

    private GameObject currentCharacter;

    /// <summary>
    /// Etat local indique par le manager de combat pour l'UI et les restrictions.
    /// </summary>
    public static bool LocalPrayerActive { get; private set; }

    private void Awake()
    {
        // Unity appelle Awake au chargement; on resout le collider avant la detection.
        ResolveInteractionCollider();
    }

    private void OnEnable()
    {
        // OnEnable inscrit l'idole active et raccorde l'input interaction.
        if (!activeIdols.Contains(this))
        {
            activeIdols.Add(this);
        }

        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteract;
    }

    private void OnDisable()
    {
        // OnDisable libere l'idole et stoppe une priere locale qui dependait d'elle.
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
        // Update surveille la portee car la priere peut rester active apres l'interaction initiale.
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

    /// <summary>
    /// Indique si l'idole peut etre proposee par le systeme de detection.
    /// </summary>
    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null && isActiveAndEnabled && !CombatSessionManager.IsCharacterInCombat(controller);
    }

    /// <summary>
    /// Retourne le collider de detection d'interaction.
    /// </summary>
    public Collider GetInteractionDetectionCollider()
    {
        return ResolveInteractionCollider();
    }

    /// <summary>
    /// Retourne l'ancre d'interaction de l'idole.
    /// </summary>
    public Transform GetInteractionAnchor()
    {
        return transform;
    }

    /// <summary>
    /// Retourne la distance maximale d'interaction.
    /// </summary>
    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return Mathf.Max(0.1f, interactionMaxDistance);
    }

    /// <summary>
    /// Retourne une priorite elevee pour l'interaction d'idole.
    /// </summary>
    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return 150;
    }

    /// <summary>
    /// Recoit le personnage detecte et arrete la priere si celui-ci sort de portee.
    /// </summary>
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

    /// <summary>
    /// Met a jour l'etat local de priere recu depuis le combat.
    /// </summary>
    public static void SetLocalPrayerState(bool active)
    {
        LocalPrayerActive = active;
    }

    /// <summary>
    /// Indique si au moins une idole active est a portee du personnage.
    /// </summary>
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
        // La logique finale de cout/effet reste dans CombatSessionManager.
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

/// <summary>
/// Installe automatiquement IustiaIdolPrayer sur les InteractableItem qui ressemblent a des idoles Iustia.
/// </summary>
public sealed class IustiaIdolPrayerAutoInstaller : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeInstaller()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindAnyObjectByType<IustiaIdolPrayerAutoInstaller>() != null)
#else
        if (FindAnyObjectByType<IustiaIdolPrayerAutoInstaller>() != null)
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
        // L'installer doit aussi traiter la scene deja chargee.
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
        InteractableItem[] items = FindObjectsByType<InteractableItem>(FindObjectsInactive.Exclude);
#else
        InteractableItem[] items = FindObjectsByType<InteractableItem>();
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
        // Detection volontairement souple pour couvrir les assets FR/EN existants.
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
