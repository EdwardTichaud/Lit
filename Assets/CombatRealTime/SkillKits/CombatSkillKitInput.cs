using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Solo kit selection; the existing actor executes skills, never a hidden teammate.</summary>
[DisallowMultipleComponent]
public sealed class CombatSkillKitInput : MonoBehaviour
{
    private CombatSkillKitCatalog catalog;
    private RealTimeCombatInput combatInput;
    private RealTimeCombatManager manager;
    private InputAction selectKit, wheelSelectKit, cancel, support;
    private readonly InputAction[] buttons = new InputAction[4];
    private bool subscribed, cancelNeedsRelease, faceNeedsRelease;
    private int consumedFrame = -1;
    private Transform openedActor;
    private InputControl heldSelectControl;
    private CombatSkillKit heldSelectedKit;
    public bool IsOpen { get; private set; }
    public CombatSkillKit ActiveKit { get; private set; }
    public bool IsSolo => SoloTeamProgression.IsSolo;
    public bool IsAvailable => isActiveAndEnabled && IsSolo && catalog != null;

    public static SkillSO GetOwnedSkill(SquadCharacterController actor, CombatSkillKit kit, int slot)
    {
        var manager = RealTimeCombatManager.Instance;
        var input = manager != null ? manager.GetComponent<CombatSkillKitInput>() : null;
        return actor != null && manager != null && manager.IsCombatActive && manager.PlayerRoot == actor.transform &&
            input != null && input.IsAvailable && SoloTeamProgression.Resolve()?.IsUnlocked(kit, slot) == true
                ? input.catalog.GetSkill(kit, slot) : null;
    }

    private void OnEnable()
    {
        manager = GetComponent<RealTimeCombatManager>();
        combatInput = GetComponent<RealTimeCombatInput>();
        catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        InputModeCoordinator.ModeChanged += OnModeChanged;
        var team = SoloTeamProgression.Resolve();
        if (team != null) team.Changed += OnTeamChanged;
        TrySubscribe();
    }

    private void Update()
    {
        if (subscribed && LocalPlayerInput.FindSharedActionMap("RealTimeCombat") != selectKit.actionMap)
        {
            Close();
            Unsubscribe();
        }
        if (!subscribed) TrySubscribe();
        var pad = Gamepad.current;
        if (heldSelectControl != null && heldSelectControl.EvaluateMagnitude() <= .5f) heldSelectControl = null;
        if (faceNeedsRelease && !AnyFaceHeld(pad)) faceNeedsRelease = false;
        if (cancelNeedsRelease && (pad == null || !pad.leftTrigger.isPressed)) cancelNeedsRelease = false;
        if (IsOpen && (!IsSolo || manager == null || !manager.isActiveAndEnabled || !manager.IsCombatActive ||
            manager.PlayerRoot != openedActor ||
            manager.PlayerRoot == null || manager.PlayerRoot.GetComponent<SquadCharacterController>()?.CurrentHp <= 0)) Close();
    }

    private void TrySubscribe()
    {
        var combat = LocalPlayerInput.FindSharedActionMap("RealTimeCombat");
        var wheel = LocalPlayerInput.FindSharedActionMap("CombatWheel");
        if (combat == null || wheel == null) return;
        selectKit = combat.FindAction("SelectSkillKit", false);
        wheelSelectKit = wheel.FindAction("SelectSkillKit", false);
        cancel = wheel.FindAction("Cancel", false);
        support = wheel.FindAction("CycleSupportTarget", false);
        var names = new[] { "SkillX", "SkillY", "SkillA", "SkillB" };
        for (int i = 0; i < 4; i++) buttons[i] = wheel.FindAction(names[i], false);
        if (selectKit == null || wheelSelectKit == null || cancel == null || support == null ||
            System.Array.Exists(buttons, action => action == null)) return;
        selectKit.performed += OnSelect;
        wheelSelectKit.performed += OnSelect;
        selectKit.canceled += OnSelectionReleased;
        wheelSelectKit.canceled += OnSelectionReleased;
        cancel.performed += OnCancel;
        support.performed += OnSupport;
        foreach (var button in buttons) button.performed += OnSkill;
        subscribed = true;
    }

    private void OnDisable()
    {
        Close();
        InputModeCoordinator.ModeChanged -= OnModeChanged;
        var team = GetComponent<SoloTeamProgression>();
        if (team == null && SquadManager.Instance != null) team = SquadManager.Instance.GetComponent<SoloTeamProgression>();
        if (team != null) team.Changed -= OnTeamChanged;
        Unsubscribe();
        faceNeedsRelease = false;
        heldSelectControl = null;
    }

    private void Unsubscribe()
    {
        if (subscribed)
        {
            selectKit.performed -= OnSelect;
            wheelSelectKit.performed -= OnSelect;
            selectKit.canceled -= OnSelectionReleased;
            wheelSelectKit.canceled -= OnSelectionReleased;
            cancel.performed -= OnCancel;
            support.performed -= OnSupport;
            foreach (var button in buttons) button.performed -= OnSkill;
        }
        subscribed = false;
    }

    public bool AllowsDefaultAction(InputAction.CallbackContext context) =>
        !(context.control?.device is Gamepad) || (!IsOpen && !faceNeedsRelease && Time.frameCount != consumedFrame);

    private static bool AnyFaceHeld(Gamepad pad) => pad != null &&
        (pad.buttonWest.isPressed || pad.buttonNorth.isPressed || pad.buttonSouth.isPressed || pad.buttonEast.isPressed);

    private void OnSelect(InputAction.CallbackContext context)
    {
        if (!CombatSkillKitCatalog.TryDirection(context.ReadValue<Vector2>(), out var kit)) return;
        // Enabling the destination map can replay the still-held D-pad. Only
        // a physical release/new press (or another direction) is a new request.
        if (heldSelectControl == context.control && heldSelectedKit == kit) return;
        heldSelectControl = context.control;
        heldSelectedKit = kit;
        if (IsOpen && ActiveKit == kit) Close();
        else Open(kit);
    }

    private void OnSelectionReleased(InputAction.CallbackContext context)
    {
        // Map disabling also emits canceled, but must not count as a release.
        if (context.control != null && context.control.EvaluateMagnitude() <= .5f) heldSelectControl = null;
    }

    public bool Open(CombatSkillKit kit)
    {
        var team = SoloTeamProgression.Resolve();
        if (team == null || !team.IsRecruited(kit)) { Feedback("Ce membre n’a pas encore rejoint l’équipe"); return false; }
        if (!IsAvailable || manager == null || !manager.isActiveAndEnabled || !manager.IsCombatActive || manager.IsCinematicSequenceActive ||
            CounterSkillCombatController.Instance != null && CounterSkillCombatController.Instance.IsCinematicPlaying ||
            (InputModeCoordinator.CurrentMode != InputMode.Combat && InputModeCoordinator.CurrentMode != InputMode.CombatWheel)) return false;
        var actor = manager.PlayerRoot != null ? manager.PlayerRoot.GetComponentInChildren<SquadCharacterController>(true) : null;
        if (actor == null || !actor.isActiveAndEnabled || actor.CurrentHp <= 0) return false;
        if (IsOpen && ActiveKit == kit) return true;
        if (!combatInput.DisplaySkillKit(kit.ToString(), team.GetAvailableSkills(kit))) return false;
        ApplyLockedSlots(kit, team);
        ActiveKit = kit;
        if (!IsOpen)
        {
            combatInput.CancelBufferedBasicSkills();
            CounterSkillCombatController.Instance?.EndGuard();
            faceNeedsRelease = AnyFaceHeld(Gamepad.current);
            cancelNeedsRelease = Gamepad.current != null && Gamepad.current.leftTrigger.isPressed;
            IsOpen = true;
            openedActor = manager.PlayerRoot;
            InputModeCoordinator.Enter(this, InputMode.CombatWheel);
        }
        return true;
    }

    private void OnSkill(InputAction.CallbackContext context)
    {
        if (!IsOpen || faceNeedsRelease || InputModeCoordinator.CurrentMode != InputMode.CombatWheel) return;
        int slot = System.Array.IndexOf(buttons, context.action);
        TryUseSlot(slot);
    }

    public bool TryUseSlot(int slot)
    {
        if (!IsOpen || !IsAvailable || InputModeCoordinator.CurrentMode != InputMode.CombatWheel || manager == null) return false;
        if (!manager.IsCombatActive || manager.IsCinematicSequenceActive ||
            CounterSkillCombatController.Instance != null && CounterSkillCombatController.Instance.IsCinematicPlaying) return false;
        if (manager.PlayerRoot != openedActor || !manager.isActiveAndEnabled) { Close(); return false; }
        var team = SoloTeamProgression.Resolve();
        if (team == null || !team.IsUnlocked(ActiveKit, slot))
        { Feedback(catalog.GetSkill(ActiveKit, slot) == null ? "Emplacement vide" : "Compétence verrouillée"); return false; }
        var skill = catalog.GetSkill(ActiveKit, slot);
        consumedFrame = Time.frameCount;
        faceNeedsRelease = AnyFaceHeld(Gamepad.current);
        var skills = FindAnyObjectByType<SkillsManager>(FindObjectsInactive.Include);
        var previous = skills != null ? skills.AnimationEventSkill : null;
        skills?.SetAnimationEventSkill(skill);
        bool accepted = manager.TryUseSkill(skill);
        if (accepted) Close();
        else
        {
            skills?.SetAnimationEventSkill(previous);
            float cooldown = manager.GetSkillCooldownRemaining(skill);
            Feedback(cooldown > 0 ? $"Recharge : {cooldown:0.#} s" : "Skill indisponible : cible, portee ou action en cours");
        }
        return accepted;
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (!cancelNeedsRelease) Close();
    }

    private void OnSupport(InputAction.CallbackContext context)
    {
        // R3 cycles allies only in the modal kit, never the enemy lock.
        var skill = combatInput.CurrentWheelSkill;
        if (IsOpen) manager?.TryCycleSupportTarget(skill);
    }

    private void Feedback(string message)
    {
        if (manager?.PlayerRoot != null)
            CombatDamageWorldFeedback.ShowMessage(manager.PlayerRoot, message, Color.yellow, 2.3f);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        openedActor = null;
        combatInput?.HideSkillKit();
        InputModeCoordinator.Exit(this);
    }

    private void OnModeChanged(InputMode mode)
    {
        // Pause, dialogue, cinematics and scene transitions must not leave a
        // hidden wheel owner underneath their contexts.
        if (IsOpen && mode != InputMode.CombatWheel) Close();
    }

    private void ApplyLockedSlots(CombatSkillKit kit, SoloTeamProgression team)
    {
        var wheel = FindAnyObjectByType<SkillWheel>(FindObjectsInactive.Include);
        for (int i = 0; i < 4; i++)
            wheel?.SetKitSlotLocked(i, catalog.GetSkill(kit, i) != null && !team.IsUnlocked(kit, i));
    }
    private void OnTeamChanged()
    {
        if (!IsOpen) return;
        var team = SoloTeamProgression.Resolve();
        if (team == null || !team.IsRecruited(ActiveKit)) { Close(); return; }
        combatInput.DisplaySkillKit(ActiveKit.ToString(), team.GetAvailableSkills(ActiveKit));
        ApplyLockedSlots(ActiveKit, team);
    }
}
