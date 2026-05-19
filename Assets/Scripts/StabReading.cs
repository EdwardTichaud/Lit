// Role:
// World interaction for a normal scene GameObject that opens StabPanel and writes
// a configured text into StabPanel/Root/Frame_1/Text (TMP).
// Usage:
// Add this component to a readable prop with a collider. Fill Reading Text.
// Optionally assign Stab Panel, Reading Text Target, Interaction Collider, or an
// InteractionBox prefab; otherwise the script resolves the scene defaults.
// Dependencies:
// CharacterInteractionDetection, LocalInputRouter, InputFocusStack, TMP.
// Precautions:
// This is intentionally not an Item/InteractableItem and never adds inventory
// content. It only uses the same detection contract so the local character can
// select and read it.
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Lit/Interaction/Stab Reading")]
public class StabReading : MonoBehaviour, ICharacterDetectedInteractable
{
    [Header("Reading")]
    [SerializeField, TextArea(4, 16), Tooltip("Texte affiche dans le StabPanel quand le joueur lit cet objet.")]
    private string readingText;
    [SerializeField, Tooltip("Panel de lecture. Laisse vide pour chercher un GameObject nomme StabPanel dans la scene.")]
    private GameObject stabPanel;
    [SerializeField, Tooltip("Texte TMP cible. Laisse vide pour utiliser StabPanel -> enfant -> enfant -> enfant, puis fallback recursif.")]
    private TMP_Text readingTextTarget;
    [SerializeField, Tooltip("Cache le StabPanel au demarrage pour qu'il ne soit visible qu'apres interaction.")]
    private bool hidePanelOnStart = true;
    [SerializeField, Tooltip("Nom utilise pour retrouver automatiquement le panel si aucune reference n'est assignee.")]
    private string stabPanelName = "StabPanel";

    [Header("Interaction")]
    [SerializeField, Tooltip("Collider de reference pour la detection. Laisse vide pour auto-detecter un collider sur cet objet ou ses enfants.")]
    private Collider interactionCollider;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale a laquelle le personnage peut lire cet objet.")]
    private float interactionMaxDistance = 1.75f;
    [SerializeField, Tooltip("Priorite de selection si plusieurs interactions sont proches.")]
    private int interactionPriority = 120;
    [SerializeField, Tooltip("Texte affiche dans l'InteractionBox.")]
    private string interactionText = "Lire";

    [Header("Interaction UI")]
    [SerializeField, Tooltip("Affiche une InteractionBox quand l'objet est cible.")]
    private bool showInteractionUi = true;
    [SerializeField, Tooltip("Prefab/objet UI d'interaction optionnel.")]
    private GameObject interactionBox;
    [SerializeField, Tooltip("Parent des boxes UI. Laisse vide pour instancier la box en world space.")]
    private Transform boxesPanel;
    [SerializeField, Tooltip("Offset en world pour la box d'interaction.")]
    private Vector3 interactionOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField, Tooltip("Camera UI/world pour positionner l'interaction box.")]
    private Camera targetCamera;

    [Header("Audio")]
    [SerializeField, Tooltip("Son joue a l'ouverture du StabPanel.")]
    private ActionAudioCue openCue = ActionAudioCue.InventoryReadOpen;
    [SerializeField, Tooltip("Son joue a la fermeture du StabPanel.")]
    private ActionAudioCue closeCue = ActionAudioCue.InventoryReadClose;

    private GameObject currentCharacter;
    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;
    private Collider resolvedInteractionCollider;
    private CanvasGroup stabPanelCanvasGroup;
    private bool readingOpen;

    private void Reset()
    {
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
    }

    private void Awake()
    {
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
        ResolveRuntimeReferences(logWarnings: false);
        resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        if (interactionCollider == null)
        {
            interactionCollider = resolvedInteractionCollider;
        }

        if (hidePanelOnStart)
        {
            SetReadingPanelVisible(false);
        }
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;
        CloseReadingPanel(playAudio: false);
        DestroyInteractionInstance();
        currentCharacter = null;
    }

    private void LateUpdate()
    {
        UpdateInteractionUiPosition();
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null && isActiveAndEnabled;
    }

    public Collider GetInteractionDetectionCollider()
    {
        if (resolvedInteractionCollider == null)
        {
            resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        }

        return resolvedInteractionCollider;
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
        return interactionPriority;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        if (currentCharacter == character)
        {
            return;
        }

        currentCharacter = character;
        ShowInteraction(currentCharacter != null && showInteractionUi);
    }

    public void OpenReading()
    {
        if (!ResolveRuntimeReferences(logWarnings: true))
        {
            return;
        }

        readingTextTarget.text = readingText ?? string.Empty;
        SetReadingPanelVisible(true);
        readingOpen = true;
        InputFocusStack.Push(this);
        PlayUiActionAudio(openCue);
    }

    public void CloseReading()
    {
        CloseReadingPanel(playAudio: true);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (readingOpen || LocalInputRouter.IsInteractConsumed || InputFocusStack.HasAnyFocus())
        {
            return;
        }

        GameObject character = ResolveInteractionCharacter();
        if (character == null)
        {
            return;
        }

        if (!LocalInputRouter.TryConsumeInteract())
        {
            return;
        }

        OpenReading();
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!readingOpen || !InputFocusStack.HasFocus(this))
        {
            return;
        }

        CloseReadingPanel(playAudio: true);
    }

    private GameObject ResolveInteractionCharacter()
    {
        if (CanUseCharacter(currentCharacter, requireLocalControl: true))
        {
            return currentCharacter;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        return CanUseCharacter(controlled, requireLocalControl: true) ? controlled : null;
    }

    private bool CanUseCharacter(GameObject character, bool requireLocalControl)
    {
        if (character == null)
        {
            return false;
        }

        if (requireLocalControl && !IsSameCharacter(LocalPlayerUtils.GetControlledCharacter(), character))
        {
            return false;
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            character.transform,
            GetInteractionDetectionCollider(),
            GetInteractionAnchor(),
            interactionMaxDistance);
    }

    private bool ResolveRuntimeReferences(bool logWarnings)
    {
        ResolveStabPanel();
        ResolvePanelCanvasGroup();
        ResolveReadingTextTarget();

        if (stabPanel != null && readingTextTarget != null)
        {
            return true;
        }

        if (logWarnings)
        {
            Debug.LogWarning(
                $"StabReading '{name}' ne peut pas ouvrir la lecture: StabPanel ou TextMeshPro cible introuvable.",
                this);
        }

        return false;
    }

    private void ResolveStabPanel()
    {
        if (stabPanel != null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(stabPanelName))
        {
            stabPanel = GameObject.Find(stabPanelName);
            if (stabPanel != null)
            {
                return;
            }

            Transform inactivePanel = FindSceneTransformByName(stabPanelName);
            if (inactivePanel != null)
            {
                stabPanel = inactivePanel.gameObject;
            }
        }
    }

    private void ResolvePanelCanvasGroup()
    {
        if (stabPanelCanvasGroup != null || stabPanel == null)
        {
            return;
        }

        stabPanelCanvasGroup = stabPanel.GetComponent<CanvasGroup>();
    }

    private void ResolveReadingTextTarget()
    {
        if (readingTextTarget != null || stabPanel == null)
        {
            return;
        }

        Transform fixedTarget = ResolveThirdChild(stabPanel.transform);
        if (fixedTarget != null)
        {
            readingTextTarget = fixedTarget.GetComponent<TMP_Text>();
            if (readingTextTarget != null)
            {
                return;
            }
        }

        readingTextTarget = stabPanel.GetComponentInChildren<TMP_Text>(true);
    }

    private static Transform ResolveThirdChild(Transform root)
    {
        Transform current = root;
        for (int depth = 0; depth < 3; depth++)
        {
            if (current == null || current.childCount == 0)
            {
                return null;
            }

            current = current.GetChild(0);
        }

        return current;
    }

    private void SetReadingPanelVisible(bool visible)
    {
        ResolveStabPanel();
        ResolvePanelCanvasGroup();
        if (stabPanel == null)
        {
            return;
        }

        if (!stabPanel.activeSelf && visible)
        {
            stabPanel.SetActive(true);
        }

        if (stabPanelCanvasGroup != null)
        {
            stabPanelCanvasGroup.alpha = visible ? 1f : 0f;
            stabPanelCanvasGroup.interactable = visible;
            stabPanelCanvasGroup.blocksRaycasts = visible;
            if (visible)
            {
                stabPanelCanvasGroup.transform.SetAsLastSibling();
            }

            return;
        }

        stabPanel.SetActive(visible);
    }

    private void CloseReadingPanel(bool playAudio)
    {
        bool wasOpen = readingOpen;
        readingOpen = false;
        SetReadingPanelVisible(false);
        InputFocusStack.Pop(this);
        if (wasOpen && playAudio)
        {
            PlayUiActionAudio(closeCue);
        }
    }

    private void ShowInteraction(bool show)
    {
        if (!show)
        {
            DestroyInteractionInstance();
            return;
        }

        if (interactionBoxInstance == null)
        {
            interactionBoxInstance = CreateInstance(interactionBox, boxesPanel);
            if (interactionBoxInstance == null)
            {
                interactionBoxInstance = CreateFallbackInteractionBox(boxesPanel);
            }

            if (interactionBoxInstance != null)
            {
                interactionCanvas = interactionBoxInstance.GetComponentInParent<Canvas>();
                ApplyInteractionText(interactionBoxInstance);
            }
        }

        if (interactionBoxInstance != null)
        {
            interactionBoxInstance.SetActive(true);
        }
    }

    private void ApplyInteractionText(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        TMP_Text tmp = instance.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = interactionText;
            return;
        }

        Text fallbackText = instance.GetComponentInChildren<Text>(true);
        if (fallbackText != null)
        {
            fallbackText.text = interactionText;
        }
    }

    private void UpdateInteractionUiPosition()
    {
        if (interactionBoxInstance == null || !interactionBoxInstance.activeSelf)
        {
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Transform anchor = GetInteractionAnchor();
        if (cam == null || anchor == null)
        {
            return;
        }

        Vector3 worldPosition = anchor.position + interactionOffset;
        Canvas canvas = interactionCanvas != null ? interactionCanvas : interactionBoxInstance.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            RectTransform rect = interactionBoxInstance.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            Vector3 screenPos = cam.WorldToScreenPoint(worldPosition);
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rect.position = screenPos;
                return;
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = canvas.worldCamera != null ? canvas.worldCamera : cam;
            if (canvasRect != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out Vector2 localPoint))
            {
                rect.localPosition = localPoint;
            }

            return;
        }

        interactionBoxInstance.transform.position = worldPosition;
        Vector3 toCamera = interactionBoxInstance.transform.position - cam.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            interactionBoxInstance.transform.rotation = Quaternion.LookRotation(toCamera);
        }
    }

    private void DestroyInteractionInstance()
    {
        if (interactionBoxInstance == null)
        {
            return;
        }

        Destroy(interactionBoxInstance);
        interactionBoxInstance = null;
        interactionCanvas = null;
    }

    private GameObject CreateInstance(GameObject source, Transform parent)
    {
        if (source == null)
        {
            return null;
        }

        return parent != null ? Instantiate(source, parent) : Instantiate(source);
    }

    private GameObject CreateFallbackInteractionBox(Transform parent)
    {
        GameObject instance = new GameObject("StabReadingInteractionBox", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(GraphicRaycaster));
        if (parent != null)
        {
            instance.transform.SetParent(parent, false);
        }

        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220f, 50f);
        rect.localScale = Vector3.one * 0.03f;

        Canvas canvas = instance.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        TextMeshProUGUI label = instance.GetComponent<TextMeshProUGUI>();
        label.text = interactionText;
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        return instance;
    }

    private void PlayUiActionAudio(ActionAudioCue cue)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            manager.PlayUiActionCue(cue);
        }
    }

    private static Transform FindSceneTransformByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (candidate.name == targetName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsSameCharacter(GameObject controlled, GameObject candidate)
    {
        if (controlled == null || candidate == null)
        {
            return false;
        }

        if (controlled == candidate)
        {
            return true;
        }

        Transform controlledTransform = controlled.transform;
        Transform candidateTransform = candidate.transform;
        return controlledTransform.IsChildOf(candidateTransform) || candidateTransform.IsChildOf(controlledTransform);
    }
}
