using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Controle le panel de craft du catalyseur (selection d'orbes).
public class CatalyseurPanelController : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root du panel catalyseur.")]
    public GameObject catalyseurPanel;
    [Tooltip("Desactive le panel a la fermeture.")]
    public bool deactivatePanelOnClose = true;
    [Tooltip("Duree du fade d'ouverture/fermeture.")]
    public float panelFadeDuration = 0.15f;
    [Tooltip("Met l'alpha a 0 au demarrage.")]
    public bool setAlphaToZeroOnStart = true;
    [Tooltip("Desactive les raycasts quand cache.")]
    public bool disableRaycastsWhenHidden = true;

    [Header("Options")]
    [Tooltip("Parent des options de craft.")]
    public Transform optionsParent;
    [Tooltip("Prefab d'une option (Button + TMP_Text).")]
    public GameObject optionPrefab;
    [Tooltip("Cree une option fallback si aucun prefab n'est defini.")]
    public bool createOptionIfMissing = true;

    [Header("Input")]
    [Tooltip("Ferme le panel avec Return.")]
    public bool closeOnReturn = true;
    [Tooltip("Utilise Interact pour activer l'option selectionnee.")]
    public bool craftOnInteract = true;

    [Header("Navigation")]
    [Tooltip("Deadzone du stick pour naviguer.")]
    public float moveDeadzone = 0.5f;
    [Tooltip("Delai avant repetition de navigation.")]
    public float initialRepeatDelay = 0.35f;
    [Tooltip("Intervalle entre repetitions de navigation.")]
    public float repeatInterval = 0.12f;
    [Tooltip("Autorise le wrap de la selection.")]
    public bool wrapNavigation = true;

    [Header("Messages")]
    [Tooltip("Message si aucune recette n'est disponible.")]
    public string noRecipeMessage = "Aucune recette.";
    [Tooltip("Message si le craft echoue.")]
    public string craftFailedMessage = "Ressources insuffisantes.";
    [Tooltip("Message si le craft reussit.")]
    public string craftSuccessMessage = "Orbe fabriquee.";

    private PlayerInputs playerInputs;
    private CanvasGroup panelCanvasGroup;
    private Coroutine panelFadeRoutine;
    private bool panelOpen;
    private bool squadInputLocked;

    private BuildingInfoInteractable currentBuilding;
    private SquadCharacterController currentController;
    private readonly List<OptionEntry> optionEntries = new List<OptionEntry>();
    private int selectedIndex = -1;
    private int lastMoveDirection;
    private float nextMoveTime;

    public bool IsOpen => panelOpen;

    private void Awake()
    {
        if (catalyseurPanel == null)
        {
            catalyseurPanel = gameObject;
        }

        panelCanvasGroup = GetPanelCanvasGroup();
        if (panelCanvasGroup != null && setAlphaToZeroOnStart)
        {
            panelCanvasGroup.alpha = 0f;
            if (disableRaycastsWhenHidden)
            {
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }
        }

        if (deactivatePanelOnClose && catalyseurPanel != null && catalyseurPanel != gameObject)
        {
            catalyseurPanel.SetActive(false);
        }

        playerInputs = new PlayerInputs();
    }

    private void OnEnable()
    {
        if (playerInputs == null)
        {
            playerInputs = new PlayerInputs();
        }

        playerInputs.Enable();
        playerInputs.Player.Interact.performed += OnInteractPerformed;
        playerInputs.Player.Return.performed += OnReturnPerformed;
    }

    private void OnDisable()
    {
        if (playerInputs != null)
        {
            playerInputs.Player.Interact.performed -= OnInteractPerformed;
            playerInputs.Player.Return.performed -= OnReturnPerformed;
            playerInputs.Disable();
        }

        InputFocusStack.Pop(this);
        SetSquadInputLock(false);
        panelOpen = false;
        currentBuilding = null;
        currentController = null;
        ClearOptions();
        selectedIndex = -1;
        lastMoveDirection = 0;
        nextMoveTime = 0f;
    }

    private void Update()
    {
        if (!panelOpen)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        HandleNavigation();
    }

    public void OpenPanel(BuildingInfoInteractable building, SquadCharacterController controller)
    {
        if (building == null || controller == null)
        {
            return;
        }

        currentBuilding = building;
        currentController = controller;

        if (catalyseurPanel == null)
        {
            catalyseurPanel = gameObject;
        }

        if (catalyseurPanel != null)
        {
            catalyseurPanel.SetActive(true);
            panelCanvasGroup = GetPanelCanvasGroup();
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                if (disableRaycastsWhenHidden)
                {
                    panelCanvasGroup.interactable = false;
                    panelCanvasGroup.blocksRaycasts = false;
                }
            }
        }

        panelOpen = true;
        InputFocusStack.Push(this);
        SetSquadInputLock(true);
        RebuildOptions();
        FadePanelTo(1f, panelFadeDuration);
    }

    public void ClosePanel()
    {
        if (!panelOpen)
        {
            return;
        }

        panelOpen = false;
        InputFocusStack.Pop(this);
        SetSquadInputLock(false);
        currentBuilding = null;
        currentController = null;
        ClearOptions();
        selectedIndex = -1;
        lastMoveDirection = 0;
        nextMoveTime = 0f;
        FadePanelTo(0f, panelFadeDuration);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!panelOpen || !craftOnInteract)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        OptionEntry entry = GetSelectedOption();
        if (entry != null && entry.Button != null)
        {
            entry.Button.onClick.Invoke();
        }
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!panelOpen || !closeOnReturn)
        {
            return;
        }

        if (!HasInputFocus())
        {
            return;
        }

        ClosePanel();
    }

    private bool HasInputFocus()
    {
        return InputFocusStack.HasFocus(this);
    }

    private void RebuildOptions()
    {
        ClearOptions();

        Item buildingItem = currentBuilding != null ? currentBuilding.BuildingItem : null;
        if (buildingItem == null || buildingItem.buildingEffects == null || buildingItem.buildingEffects.Count == 0)
        {
            CreateDisabledOption(noRecipeMessage);
            return;
        }

        int created = 0;
        for (int i = 0; i < buildingItem.buildingEffects.Count; i++)
        {
            CatalyseurOrbCraftEffect effect = buildingItem.buildingEffects[i] as CatalyseurOrbCraftEffect;
            if (effect == null)
            {
                continue;
            }

            CreateOption(effect);
            created++;
        }

        if (created == 0)
        {
            CreateDisabledOption(noRecipeMessage);
            return;
        }

        SelectFirstOption();
    }

    private void CreateOption(CatalyseurOrbCraftEffect effect)
    {
        if (effect == null)
        {
            return;
        }

        GameObject optionObj = CreateOptionInstance();
        if (optionObj == null)
        {
            return;
        }

        Button button = optionObj.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            button = optionObj.AddComponent<Button>();
        }

        TMP_Text label = optionObj.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            string description = effect.GetDescription();
            if (string.IsNullOrWhiteSpace(description))
            {
                description = effect.name;
            }
            label.text = description;
        }

        button.interactable = true;
        CatalyseurOrbCraftEffect captured = effect;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => TryCraft(captured));

        optionEntries.Add(new OptionEntry(optionObj, button));
    }

    private void CreateDisabledOption(string message)
    {
        GameObject optionObj = CreateOptionInstance();
        if (optionObj == null)
        {
            InfoBoxUI.TryShow(message);
            return;
        }

        TMP_Text label = optionObj.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = message;
        }

        Button button = optionObj.GetComponentInChildren<Button>(true);
        if (button != null)
        {
            button.interactable = false;
        }

        optionEntries.Add(new OptionEntry(optionObj, button));
    }

    private GameObject CreateOptionInstance()
    {
        Transform parent = optionsParent != null ? optionsParent : transform;
        if (optionPrefab != null)
        {
            return Instantiate(optionPrefab, parent);
        }

        if (!createOptionIfMissing)
        {
            return null;
        }

        GameObject root = new GameObject("CatalyseurOption", typeof(RectTransform));
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
        }

        Image image = root.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.08f);

        Button button = root.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(root.transform, false);
        TMP_Text label = labelObj.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 20f;
        label.text = string.Empty;

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return root;
    }

    private void ClearOptions()
    {
        for (int i = optionEntries.Count - 1; i >= 0; i--)
        {
            OptionEntry entry = optionEntries[i];
            if (entry != null && entry.Root != null)
            {
                Destroy(entry.Root);
            }
        }

        optionEntries.Clear();
        selectedIndex = -1;
    }

    private OptionEntry GetSelectedOption()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null)
        {
            for (int i = 0; i < optionEntries.Count; i++)
            {
                OptionEntry entry = optionEntries[i];
                if (entry != null && entry.Button != null && entry.Button.gameObject == selected)
                {
                    return entry;
                }
            }
        }

        if (selectedIndex >= 0 && selectedIndex < optionEntries.Count)
        {
            return optionEntries[selectedIndex];
        }

        return null;
    }

    private void SelectFirstOption()
    {
        if (optionEntries.Count == 0)
        {
            return;
        }

        for (int i = 0; i < optionEntries.Count; i++)
        {
            OptionEntry entry = optionEntries[i];
            if (entry != null && entry.Button != null && entry.Button.interactable)
            {
                SelectOptionIndex(i, true);
                return;
            }
        }
    }

    private void HandleNavigation()
    {
        if (playerInputs == null || optionEntries.Count == 0)
        {
            return;
        }

        Vector2 moveInput = playerInputs.Player.Move.ReadValue<Vector2>();
        int direction = GetMoveDirection(moveInput, moveDeadzone);
        if (direction == 0)
        {
            lastMoveDirection = 0;
            nextMoveTime = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != lastMoveDirection)
        {
            MoveSelection(direction, wrapNavigation);
            lastMoveDirection = direction;
            nextMoveTime = now + initialRepeatDelay;
            return;
        }

        if (now >= nextMoveTime)
        {
            MoveSelection(direction, wrapNavigation);
            nextMoveTime = now + repeatInterval;
        }
    }

    private int GetMoveDirection(Vector2 input, float deadzone)
    {
        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        if (absX < deadzone && absY < deadzone)
        {
            return 0;
        }

        if (absY >= absX)
        {
            return input.y > 0f ? -1 : 1;
        }

        return input.x > 0f ? 1 : -1;
    }

    private void MoveSelection(int direction, bool wrap)
    {
        if (optionEntries.Count == 0)
        {
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= optionEntries.Count)
        {
            SelectFirstOption();
            return;
        }

        int nextIndex = FindNextAvailableIndex(selectedIndex, direction, wrap);
        if (nextIndex < 0)
        {
            return;
        }

        SelectOptionIndex(nextIndex, false);
    }

    private int FindNextAvailableIndex(int startIndex, int direction, bool wrap)
    {
        int count = optionEntries.Count;
        if (count == 0)
        {
            return -1;
        }

        int index = startIndex;
        for (int i = 0; i < count; i++)
        {
            index += direction > 0 ? 1 : -1;
            if (index < 0 || index >= count)
            {
                if (!wrap)
                {
                    return -1;
                }
                index = index < 0 ? count - 1 : 0;
            }

            OptionEntry entry = optionEntries[index];
            if (entry != null && entry.Button != null && entry.Button.interactable)
            {
                return index;
            }
        }

        return -1;
    }

    private void SelectOptionIndex(int index, bool force)
    {
        if (optionEntries.Count == 0)
        {
            selectedIndex = -1;
            return;
        }

        int clamped = Mathf.Clamp(index, 0, optionEntries.Count - 1);
        if (!force && clamped == selectedIndex)
        {
            return;
        }

        selectedIndex = clamped;
        OptionEntry entry = optionEntries[selectedIndex];
        if (entry != null && entry.Button != null)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(entry.Button.gameObject);
            }
        }
    }

    private void TryCraft(CatalyseurOrbCraftEffect effect)
    {
        if (effect == null || currentController == null || currentBuilding == null)
        {
            return;
        }

        bool success = effect.ApplyOnInteract(currentController, currentBuilding.BuildingItem, currentBuilding.Level);
        if (success)
        {
            if (!string.IsNullOrWhiteSpace(craftSuccessMessage))
            {
                InfoBoxUI.TryShow(craftSuccessMessage);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(craftFailedMessage))
        {
            InfoBoxUI.TryShow(craftFailedMessage);
        }
    }

    private CanvasGroup GetPanelCanvasGroup()
    {
        if (catalyseurPanel == null)
        {
            return null;
        }

        CanvasGroup group = catalyseurPanel.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = catalyseurPanel.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private void FadePanelTo(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetPanelCanvasGroup();
        if (canvasGroup == null)
        {
            return;
        }

        if (panelFadeRoutine != null)
        {
            StopCoroutine(panelFadeRoutine);
            panelFadeRoutine = null;
        }

        if (duration <= 0f || !gameObject.activeInHierarchy)
        {
            canvasGroup.alpha = targetAlpha;
            if (disableRaycastsWhenHidden)
            {
                bool visible = targetAlpha > 0.001f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (deactivatePanelOnClose && targetAlpha <= 0.001f && catalyseurPanel != null && catalyseurPanel != gameObject)
            {
                catalyseurPanel.SetActive(false);
            }

            return;
        }

        panelFadeRoutine = StartCoroutine(FadePanelRoutine(canvasGroup, targetAlpha, duration));
    }

    private System.Collections.IEnumerator FadePanelRoutine(CanvasGroup canvasGroup, float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (disableRaycastsWhenHidden)
        {
            bool visible = targetAlpha > 0.001f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (deactivatePanelOnClose && targetAlpha <= 0.001f && catalyseurPanel != null && catalyseurPanel != gameObject)
        {
            catalyseurPanel.SetActive(false);
        }

        panelFadeRoutine = null;
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

    private sealed class OptionEntry
    {
        public OptionEntry(GameObject root, Button button)
        {
            Root = root;
            Button = button;
        }

        public GameObject Root { get; }
        public Button Button { get; }
    }
}
