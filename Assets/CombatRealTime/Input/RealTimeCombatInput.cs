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

    private InputActionMap actionMap;
    private InputAction dodgeAction;
    private InputAction basicAttackAction;
    private InputAction jumpAction;
    private InputAction paletteAction;
    private InputAction paletteNavigateAction;
    private InputAction paletteConfirmAction;
    private InputAction switchEnemyLockAction;
    private InputAction lightSkillAction;
    private bool paletteOpen;
    private bool paletteInputSuppressed;
    private int selectedSlot;
    private readonly Queue<BasicSkillsSO> basicSkillQueue = new Queue<BasicSkillsSO>();
    private Coroutine basicComboRoutine;
    private float lastBasicAttackQueuedAt = float.NegativeInfinity;

    public int SelectedSlot => selectedSlot;

    private void OnEnable()
    {
        ResolveActions();
        Subscribe();
        if (enableOnStart)
        {
            actionMap?.Enable();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        actionMap?.Disable();
        ClosePalette(resolveIfMissing: false);
        ClearBasicSkillCombo();
    }

    public void SetInputActive(bool active)
    {
        ResolveActions();
        if (active)
        {
            actionMap?.Enable();
            return;
        }

        actionMap?.Disable();
        ClosePalette();
        ClearBasicSkillCombo();
    }

    private void ResolveActions()
    {
        if (actionMap != null || actions == null)
        {
            return;
        }

        actionMap = actions.FindActionMap(actionMapName, false);
        if (actionMap == null)
        {
            Debug.LogWarning("[RealTimeCombatInput] ActionMap '" + actionMapName + "' introuvable.", this);
            return;
        }

        dodgeAction = actionMap.FindAction("Dodge", false);
        basicAttackAction = actionMap.FindAction("BasicAttack", false);
        jumpAction = actionMap.FindAction("Jump", false);
        paletteAction = actionMap.FindAction("OpenPalette", false);
        paletteNavigateAction = actionMap.FindAction("NavigatePalette", false);
        paletteConfirmAction = actionMap.FindAction("ConfirmPalette", false);
        switchEnemyLockAction = actionMap.FindAction("SwitchEnemyLock", false);
        lightSkillAction = actionMap.FindAction("LightSkill", false);
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
    }

    private void Unsubscribe()
    {
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
    }

    private static void OnDodge(InputAction.CallbackContext context)
    {
        RealTimeCombatManager.Instance?.RegisterReaction(RealTimeCombatReaction.Dodge);
    }

    private void OnBasicAttack(InputAction.CallbackContext context)
    {
        if (paletteOpen)
        {
            return;
        }

        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager == null || !manager.IsCombatActive || manager.LockedEnemy == null)
        {
            return;
        }

        if (!manager.CanAcceptBasicSkillInput)
        {
            return;
        }

        SkillsManager skillsManager = FindAnyObjectByType<SkillsManager>(FindObjectsInactive.Include);
        if (skillsManager == null)
        {
            return;
        }

        if (basicSkillQueue.Count >= maximumBufferedBasicSkills)
        {
            return;
        }

        float currentTime = Time.unscaledTime;
        if (currentTime - lastBasicAttackQueuedAt > basicComboResetDelaySeconds)
        {
            skillsManager.ResetBasicSkillCombo();
        }

        if (!skillsManager.TryReserveNextBasicSkill(out BasicSkillsSO skill))
        {
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
        if (paletteOpen)
        {
            return;
        }

        RealTimeCombatManager.Instance?.RegisterReaction(RealTimeCombatReaction.Jump);
    }

    private void OnOpenPalette(InputAction.CallbackContext context)
    {
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
        if (!paletteOpen)
        {
            RealTimeCombatManager.Instance?.TrySwitchEnemyLock();
        }
    }

    private void OnLightSkill(InputAction.CallbackContext context)
    {
        if (!paletteOpen)
        {
            GetComponent<LightSkillCombatController>()?.TryUseLightSkill();
        }
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
}
