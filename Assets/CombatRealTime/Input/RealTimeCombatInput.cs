using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RealTimeCombatInput : MonoBehaviour
{
    [SerializeField] private InputActionAsset actions;
    [SerializeField] private string actionMapName = "RealTimeCombat";
    [SerializeField] private bool enableOnStart;
    [SerializeField] private SkillWheel skillWheel;
    [SerializeField, Range(0f, 1f)] private float skillWheelVisibleAlpha = 0.4f;
    [SerializeField] private CanvasGroup skillWheelCanvasGroup;
    [SerializeField, Min(0f), Tooltip("Delai maximal entre deux pressions avant de reprendre le combo d'attaques basiques au premier skill.")]
    private float basicComboResetDelaySeconds = 0.85f;
    [SerializeField, Min(1), Tooltip("Nombre maximal d'attaques basiques en attente derriere l'animation en cours.")]
    private int maximumBufferedBasicSkills = 1;
    [SerializeField, Tooltip("Journalise uniquement les refus d'attaque et les activations de la map de combat.")]
    private bool logInputDiagnostics = true;

    private InputActionMap actionMap;
    private InputAction counterAction;
    private InputAction dodgeAction;
    private InputAction basicAttackAction;
    private InputAction jumpAction;
    private InputAction paletteAction;
    private InputAction paletteNavigateAction;
    private InputAction paletteConfirmAction;
    private InputAction switchEnemyLockAction;
    private InputAction lightSkillAction;
    private InputAction companionFusionAction;
    private bool paletteOpen;
    private bool paletteInputSuppressed;
    private bool callbacksSubscribed;
    private int selectedSlot;
    private readonly Queue<BasicSkillsSO> basicSkillQueue = new Queue<BasicSkillsSO>();
    private Coroutine basicComboRoutine;
    private float lastBasicAttackQueuedAt = float.NegativeInfinity;

    public int SelectedSlot => selectedSlot;
    public bool IsInputActive => actionMap != null && actionMap.enabled;
    public string InputDiagnostics =>
        "map=" + (actionMap != null ? actionMap.name : "None") +
        " enabled=" + IsInputActive +
        " callbacks=" + callbacksSubscribed +
        " mode=" + InputModeCoordinator.CurrentMode +
        " context=" + GamepadInputContextStack.Current +
        " palette=" + paletteOpen +
        " counterWheel=" + IsCounterSelectionOpen;
    private bool IsCounterSelectionOpen => CounterSkillCombatController.Instance != null && CounterSkillCombatController.Instance.IsSelectionOpen;

    private void OnEnable()
    {
        LocalPlayerInput.EnsureInstance();
        ResolveActions();
        Subscribe();
        if (enableOnStart)
        {
            LocalPlayerInput.SetCombatInputActive(true);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClosePalette(resolveIfMissing: false);
        LocalInputRouter.PopInteractionAndJumpSuppression(this);
        ClearBasicSkillCombo();
        GamepadInputContextStack.Pop(this);
        LocalPlayerInput.SetCombatInputActive(false);
    }

    public void SetInputActive(bool active)
    {
        ResolveActions();
        if (active)
        {
            // Le composant peut etre active avant LocalPlayerInput dans une
            // scene. Revalide les callbacks au lock, lorsque la map partagee
            // est necessairement disponible.
            Subscribe();
            LocalPlayerInput.SetCombatInputActive(true);
            GamepadInputContextStack.Push(this, GamepadInputContext.Combat);
            Trace("Combat input active | " + InputDiagnostics + ".");
            return;
        }

        LocalPlayerInput.SetCombatInputActive(false);
        GamepadInputContextStack.Pop(this);
        ClosePalette();
        ClearBasicSkillCombo();
        Trace("Combat input inactive | " + InputDiagnostics + ".");
    }

    private void ResolveActions()
    {
        if (actionMap != null)
        {
            return;
        }

        actionMap = LocalPlayerInput.FindSharedActionMap(actionMapName);
        if (actionMap == null)
        {
            Debug.LogWarning("[RealTimeCombatInput] ActionMap '" + actionMapName + "' introuvable.", this);
            return;
        }

        counterAction = actionMap.FindAction("Counter", false);
        dodgeAction = actionMap.FindAction("Dodge", false);
        basicAttackAction = actionMap.FindAction("BasicAttack", false);
        jumpAction = actionMap.FindAction("Jump", false);
        paletteAction = actionMap.FindAction("OpenPalette", false);
        paletteNavigateAction = actionMap.FindAction("NavigatePalette", false);
        paletteConfirmAction = actionMap.FindAction("ConfirmPalette", false);
        switchEnemyLockAction = actionMap.FindAction("SwitchEnemyLock", false);
        lightSkillAction = actionMap.FindAction("LightSkill", false);
        companionFusionAction = actionMap.FindAction("Melt", false);
    }

    private void ResolveSkillWheel()
    {
        if (skillWheel == null)
        {
            skillWheel = FindAnyObjectByType<SkillWheel>(FindObjectsInactive.Include);
        }

        if (skillWheelCanvasGroup == null && skillWheel != null)
        {
            Transform[] transforms = skillWheel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == "SkillsWheelSlots")
                {
                    skillWheelCanvasGroup = transforms[i].GetComponent<CanvasGroup>();
                    break;
                }
            }
        }
    }

    private void Subscribe()
    {
        if (callbacksSubscribed)
        {
            return;
        }

        ResolveActions();
        if (actionMap == null)
        {
            return;
        }

        if (counterAction != null)
        {
            counterAction.started += OnCounterStarted;
            counterAction.canceled += OnCounterCanceled;
        }
        if (dodgeAction != null) dodgeAction.performed += OnDodge;
        if (basicAttackAction != null) basicAttackAction.performed += OnBasicAttack;
        if (jumpAction != null) jumpAction.performed += OnJump;
        if (paletteAction != null)
        {
            paletteAction.started += OnOpenPalette;
            paletteAction.canceled += OnClosePalette;
        }
        if (paletteNavigateAction != null) paletteNavigateAction.performed += OnNavigatePalette;
        if (paletteConfirmAction != null) paletteConfirmAction.performed += OnConfirmPalette;
        if (switchEnemyLockAction != null) switchEnemyLockAction.performed += OnSwitchEnemyLock;
        if (lightSkillAction != null) lightSkillAction.performed += OnLightSkill;
        if (companionFusionAction != null) companionFusionAction.performed += OnCompanionFusion;
        callbacksSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!callbacksSubscribed)
        {
            return;
        }

        if (counterAction != null)
        {
            counterAction.started -= OnCounterStarted;
            counterAction.canceled -= OnCounterCanceled;
        }
        if (dodgeAction != null) dodgeAction.performed -= OnDodge;
        if (basicAttackAction != null) basicAttackAction.performed -= OnBasicAttack;
        if (jumpAction != null) jumpAction.performed -= OnJump;
        if (paletteAction != null)
        {
            paletteAction.started -= OnOpenPalette;
            paletteAction.canceled -= OnClosePalette;
        }
        if (paletteNavigateAction != null) paletteNavigateAction.performed -= OnNavigatePalette;
        if (paletteConfirmAction != null) paletteConfirmAction.performed -= OnConfirmPalette;
        if (switchEnemyLockAction != null) switchEnemyLockAction.performed -= OnSwitchEnemyLock;
        if (lightSkillAction != null) lightSkillAction.performed -= OnLightSkill;
        if (companionFusionAction != null) companionFusionAction.performed -= OnCompanionFusion;
        callbacksSubscribed = false;
    }

    private void OnCounterStarted(InputAction.CallbackContext context)
    {
        CounterSkillCombatController counter = CounterSkillCombatController.Instance;
        if (counter != null && counter.IsSelectionOpen)
        {
            counter.ConfirmSelection();
            return;
        }

        if (!paletteOpen) counter?.BeginGuard();
    }

    private void OnCounterCanceled(InputAction.CallbackContext context)
    {
        CounterSkillCombatController.Instance?.EndGuard();
    }

    private void OnDodge(InputAction.CallbackContext context)
    {
        if (IsCounterSelectionOpen)
        {
            CounterSkillCombatController.Instance.CancelSelection();
            return;
        }

        RealTimeCombatManager.Instance?.RegisterReaction(RealTimeCombatReaction.Dodge);
        if (!paletteOpen)
        {
            GetComponent<CombatMobilityController>()?.RequestDodge();
        }
    }

    private void OnBasicAttack(InputAction.CallbackContext context)
    {
        if (paletteOpen || IsCounterSelectionOpen)
        {
            Trace("BasicAttack ignoree: roue ouverte | " + InputDiagnostics + ".");
            return;
        }

        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager == null || !manager.IsCombatActive || manager.LockedEnemy == null)
        {
            Trace("BasicAttack ignoree: combat ou lock absent | " + InputDiagnostics + ".");
            return;
        }

        if (!manager.CanAcceptBasicSkillInput)
        {
            Trace("BasicAttack ignoree: presentation joueur indisponible.");
            return;
        }

        SkillsManager skillsManager = FindAnyObjectByType<SkillsManager>(FindObjectsInactive.Include);
        if (skillsManager == null)
        {
            Trace("BasicAttack ignoree: SkillsManager introuvable.");
            return;
        }

        if (basicSkillQueue.Count >= maximumBufferedBasicSkills)
        {
            Trace("BasicAttack ignoree: buffer deja plein.");
            return;
        }

        float currentTime = Time.unscaledTime;
        if (currentTime - lastBasicAttackQueuedAt > basicComboResetDelaySeconds)
        {
            skillsManager.ResetBasicSkillCombo();
        }

        if (!skillsManager.TryReserveNextBasicSkill(out BasicSkillsSO skill))
        {
            Trace("BasicAttack ignoree: aucun BasicSkillsSO configure.");
            return;
        }

        lastBasicAttackQueuedAt = currentTime;
        basicSkillQueue.Enqueue(skill);
        if (basicComboRoutine == null)
        {
            basicComboRoutine = StartCoroutine(PlayBasicSkillCombo(skillsManager));
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (paletteOpen || IsCounterSelectionOpen)
        {
            return;
        }

        RealTimeCombatManager.Instance?.FacePlayerTowardsLockedEnemy();
        RealTimeCombatManager.Instance?.RegisterReaction(RealTimeCombatReaction.Jump);
        GetComponent<CombatMobilityController>()?.RequestJump();
    }

    private void OnOpenPalette(InputAction.CallbackContext context)
    {
        if (IsCounterSelectionOpen) return;
        paletteOpen = true;
        ResolveSkillWheel();

        if (skillWheel == null)
        {
            return;
        }

        PushPaletteInputSuppression();
        SetSkillWheelVisible(true);
        skillWheel.SetSelectedSlot(selectedSlot);
        selectedSlot = skillWheel.SelectedSlotIndex;
    }

    private void OnClosePalette(InputAction.CallbackContext context)
    {
        ClosePalette();
    }

    private void OnNavigatePalette(InputAction.CallbackContext context)
    {
        if (IsCounterSelectionOpen)
        {
            CounterSkillCombatController.Instance.Navigate(context.ReadValue<Vector2>());
            return;
        }
        if (!paletteOpen)
        {
            return;
        }

        Vector2 direction = context.ReadValue<Vector2>();
        if (direction.sqrMagnitude < 0.25f)
        {
            return;
        }

        ResolveSkillWheel();
        if (skillWheel != null)
        {
            selectedSlot = skillWheel.SelectFromDirection(direction);
            return;
        }

        selectedSlot = (selectedSlot + (direction.x > 0f || direction.y < 0f ? 1 : 7)) % RealTimeCombatLoadout.SlotCount;
    }

    private void OnConfirmPalette(InputAction.CallbackContext context)
    {
        if (IsCounterSelectionOpen) return;
        if (!paletteOpen)
        {
            return;
        }

        ResolveSkillWheel();
        SkillSO skill = skillWheel != null ? skillWheel.GetSkill(selectedSlot) : null;
        if (skill != null)
        {
            SkillsManager skillsManager = FindAnyObjectByType<SkillsManager>(FindObjectsInactive.Include);
            skillsManager?.SetAnimationEventSkill(skill);
            RealTimeCombatManager.Instance?.TryUseSkill(skill);
        }
    }

    private void OnSwitchEnemyLock(InputAction.CallbackContext context)
    {
        if (IsCounterSelectionOpen) return;
        if (!paletteOpen)
        {
            RealTimeCombatManager.Instance?.TrySwitchEnemyLock();
        }
    }

    private void OnLightSkill(InputAction.CallbackContext context)
    {
        if (IsCounterSelectionOpen) return;
        if (!paletteOpen)
        {
            GetComponent<LightSkillCombatController>()?.TryUseLightSkill();
        }
    }

    private void OnCompanionFusion(InputAction.CallbackContext context)
    {
        if (paletteOpen || IsCounterSelectionOpen)
        {
            return;
        }

        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager == null || !manager.IsCombatActive)
        {
            return;
        }

        SpiritBondController.FindForCharacter(manager.PlayerRoot != null ? manager.PlayerRoot.gameObject : null)
            ?.RequestMeltAnimation();
    }

    private IEnumerator PlayBasicSkillCombo(SkillsManager skillsManager)
    {
        while (basicSkillQueue.Count > 0)
        {
            RealTimeCombatManager manager = RealTimeCombatManager.Instance;
            if (manager == null || !manager.IsCombatActive || manager.LockedEnemy == null)
            {
                basicSkillQueue.Clear();
                break;
            }

            yield return manager.WaitForPlayerActionChainWindow();
            if (!manager.IsCombatActive || manager.LockedEnemy == null)
            {
                basicSkillQueue.Clear();
                break;
            }

            if (!manager.CanChainBasicSkill)
            {
                continue;
            }

            BasicSkillsSO skill = basicSkillQueue.Dequeue();
            skillsManager.SetAnimationEventSkill(skill);
            if (!manager.TryUseSkill(skill))
            {
                continue;
            }

        }

        basicComboRoutine = null;
    }

    private void ClearBasicSkillCombo()
    {
        basicSkillQueue.Clear();
        lastBasicAttackQueuedAt = float.NegativeInfinity;
        if (basicComboRoutine != null)
        {
            StopCoroutine(basicComboRoutine);
            basicComboRoutine = null;
        }
    }

    public void CancelBufferedBasicSkills()
    {
        ClearBasicSkillCombo();
    }

    private void ClosePalette(bool resolveIfMissing = true)
    {
        paletteOpen = false;
        if (paletteInputSuppressed)
        {
            LocalInputRouter.PopInteractionAndJumpSuppression(this);
            paletteInputSuppressed = false;
        }

        HideSkillWheel(resolveIfMissing);
    }

    private void PushPaletteInputSuppression()
    {
        LocalInputRouter.PushInteractionAndJumpSuppression(this);
        paletteInputSuppressed = true;
    }

    private void HideSkillWheel(bool resolveIfMissing = true)
    {
        SetSkillWheelVisible(false, resolveIfMissing);
    }

    private void SetSkillWheelVisible(bool visible, bool resolveIfMissing = true)
    {
        if (resolveIfMissing)
        {
            ResolveSkillWheel();
        }

        if (skillWheelCanvasGroup == null)
        {
            return;
        }

        skillWheelCanvasGroup.alpha = visible ? skillWheelVisibleAlpha : 0f;
        skillWheelCanvasGroup.interactable = visible;
        skillWheelCanvasGroup.blocksRaycasts = visible;
    }

    private void Trace(string message)
    {
        if (logInputDiagnostics)
        {
            Debug.Log("[RealTimeCombatInput] " + message, this);
        }
    }
}
