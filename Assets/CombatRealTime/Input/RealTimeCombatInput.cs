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
    private readonly Queue<BasicSkillRequest> basicSkillQueue = new Queue<BasicSkillRequest>();
    private Coroutine basicComboRoutine;
    private float lastBasicAttackQueuedAt = float.NegativeInfinity;
    private BasicSkillContext? lastBasicSkillContext;

    private readonly struct BasicSkillRequest
    {
        public readonly BasicSkillsSO Skill;
        public readonly BasicSkillContext Context;

        public BasicSkillRequest(BasicSkillsSO skill, BasicSkillContext context)
        {
            Skill = skill;
            Context = context;
        }
    }

    public int SelectedSlot => selectedSlot;
    public bool IsInputActive => actionMap != null && actionMap.enabled;
    public string InputDiagnostics =>
        "map=" + (actionMap != null ? actionMap.name : "None") +
        " enabled=" + IsInputActive +
        " callbacks=" + callbacksSubscribed +
        " mode=" + InputModeCoordinator.CurrentMode +
        " context=" + GamepadInputContextStack.Current +
        " palette=" + paletteOpen +
        " counterCinematic=" + IsCounterCinematicPlaying;
    private bool IsCounterCinematicPlaying => CounterSkillCombatController.Instance != null && CounterSkillCombatController.Instance.IsCinematicPlaying;

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

    /// <summary>
    /// Temporarily hands the map to a combat Timeline without discarding the
    /// current BasicSkill combo index. A normal combat exit still uses
    /// SetInputActive(false) and clears the queue as before.
    /// </summary>
    public void SetCinematicInputSuspended(bool suspended)
    {
        ResolveActions();
        if (suspended)
        {
            LocalPlayerInput.SetCombatInputActive(false);
            GamepadInputContextStack.Pop(this);
            ClosePalette();
            Trace("Combat input suspended for cinematic | " + InputDiagnostics + ".");
            return;
        }

        LocalPlayerInput.SetCombatInputActive(true);
        GamepadInputContextStack.Push(this, GamepadInputContext.Combat);
        Subscribe();
        Trace("Combat input restored after cinematic | " + InputDiagnostics + ".");
    }

    private void ResolveActions()
    {
        InputActionMap sharedMap = LocalPlayerInput.FindSharedActionMap(actionMapName);
        if (ReferenceEquals(actionMap, sharedMap))
        {
            return;
        }

        // LocalPlayerInput est persistant mais son PlayerInputs runtime peut
        // etre recree lors d'une transition. Ne jamais garder les callbacks
        // sur l'ancienne instance de map : elle ne sera plus activee par le
        // coordinateur et rendrait toutes les actions combat inertes.
        Unsubscribe();
        actionMap = sharedMap;
        counterAction = null;
        dodgeAction = null;
        basicAttackAction = null;
        jumpAction = null;
        paletteAction = null;
        paletteNavigateAction = null;
        paletteConfirmAction = null;
        switchEnemyLockAction = null;
        lightSkillAction = null;
        companionFusionAction = null;

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
        if (!paletteOpen && !IsCounterCinematicPlaying) CounterSkillCombatController.Instance?.BeginGuard();
    }

    private void OnCounterCanceled(InputAction.CallbackContext context)
    {
        CounterSkillCombatController.Instance?.EndGuard();
    }

    private void OnDodge(InputAction.CallbackContext context)
    {
        if (IsCounterCinematicPlaying) return;

        RealTimeCombatManager.Instance?.RegisterReaction(RealTimeCombatReaction.Dodge);
        if (!paletteOpen)
        {
            GetComponent<CombatMobilityController>()?.RequestDodge();
        }
    }

    private void OnBasicAttack(InputAction.CallbackContext context)
    {
        if (paletteOpen || IsCounterCinematicPlaying)
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

        BasicSkillContext basicSkillContext = ResolveBasicSkillContext(manager);
        if (lastBasicSkillContext.HasValue && lastBasicSkillContext.Value != basicSkillContext)
        {
            // A combo never crosses the ground/air boundary. The active clip
            // is left untouched; only a waiting follow-up is invalidated.
            BasicSkillContext previousContext = lastBasicSkillContext.Value;
            ClearBasicSkillCombo();
            skillsManager.ResetBasicSkillCombo(previousContext);
            skillsManager.ResetBasicSkillCombo(basicSkillContext);
            Trace("BasicAttack contexte change: " + previousContext + " -> " + basicSkillContext + ".");
        }

        if (basicSkillQueue.Count >= maximumBufferedBasicSkills)
        {
            Trace("BasicAttack ignoree: buffer deja plein.");
            return;
        }

        float currentTime = Time.unscaledTime;
        if (currentTime - lastBasicAttackQueuedAt > basicComboResetDelaySeconds)
        {
            skillsManager.ResetBasicSkillCombo(basicSkillContext);
        }

        if (!skillsManager.TryReserveNextBasicSkill(basicSkillContext, out BasicSkillsSO skill))
        {
            Trace("BasicAttack ignoree: aucun BasicSkillsSO " + basicSkillContext + " configure.");
            return;
        }

        lastBasicSkillContext = basicSkillContext;
        lastBasicAttackQueuedAt = currentTime;

        // Aerial attacks must take over the jump pose in the same input frame.
        // Waiting for the generic combo coroutine leaves a frame for UCC's jump
        // presentation to reclaim the Animator, which made the first air hit
        // appear to be ignored.
        if (basicSkillContext == BasicSkillContext.Airborne &&
            !manager.IsPlayerActionActive &&
            basicSkillQueue.Count == 0)
        {
            skillsManager.SetAnimationEventSkill(skill);
            if (manager.TryUseSkill(skill))
            {
                Trace("AirBasicAttack demarree immediatement: " + skill.SkillName + ".");
                return;
            }

            skillsManager.ResetBasicSkillCombo(BasicSkillContext.Airborne);
            lastBasicSkillContext = null;
            Trace("AirBasicAttack refusee par RealTimeCombatManager: " + skill.SkillName + ".");
            return;
        }

        basicSkillQueue.Enqueue(new BasicSkillRequest(skill, basicSkillContext));
        if (basicComboRoutine == null)
        {
            basicComboRoutine = StartCoroutine(PlayBasicSkillCombo(skillsManager));
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (paletteOpen || IsCounterCinematicPlaying)
        {
            return;
        }

        RealTimeCombatManager.Instance?.FacePlayerTowardsLockedEnemy();
        RealTimeCombatManager.Instance?.RegisterReaction(RealTimeCombatReaction.Jump);
        GetComponent<CombatMobilityController>()?.RequestJump();
    }

    private void OnOpenPalette(InputAction.CallbackContext context)
    {
        if (IsCounterCinematicPlaying) return;
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
        if (IsCounterCinematicPlaying) return;
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
        if (IsCounterCinematicPlaying) return;
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
        if (IsCounterCinematicPlaying) return;
        if (!paletteOpen)
        {
            RealTimeCombatManager.Instance?.TrySwitchEnemyLock();
        }
    }

    private void OnLightSkill(InputAction.CallbackContext context)
    {
        if (IsCounterCinematicPlaying) return;
        if (!paletteOpen)
        {
            GetComponent<LightSkillCombatController>()?.TryUseLightSkill();
        }
    }

    private void OnCompanionFusion(InputAction.CallbackContext context)
    {
        if (paletteOpen || IsCounterCinematicPlaying)
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

            BasicSkillRequest request = basicSkillQueue.Dequeue();
            BasicSkillContext currentContext = ResolveBasicSkillContext(manager);
            if (currentContext != request.Context)
            {
                skillsManager.ResetBasicSkillCombo(request.Context);
                skillsManager.ResetBasicSkillCombo(currentContext);
                lastBasicSkillContext = null;
                basicSkillQueue.Clear();
                Trace("BasicAttack buffer annule: contexte " + request.Context + " devenu " + currentContext + ".");
                break;
            }

            BasicSkillsSO skill = request.Skill;
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
        lastBasicSkillContext = null;
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

    private static BasicSkillContext ResolveBasicSkillContext(RealTimeCombatManager manager)
    {
        Transform player = manager != null ? manager.PlayerRoot : null;
        if (player == null)
        {
            return BasicSkillContext.Grounded;
        }

        LitOpsiveLocomotionBridge bridge = player.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        if (bridge != null)
        {
            return bridge.Grounded ? BasicSkillContext.Grounded : BasicSkillContext.Airborne;
        }

        SquadCharacterController controller = player.GetComponentInChildren<SquadCharacterController>(true);
        return controller == null || controller.IsGrounded
            ? BasicSkillContext.Grounded
            : BasicSkillContext.Airborne;
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
