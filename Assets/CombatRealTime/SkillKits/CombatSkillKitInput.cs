using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Solo support kits or the multiplayer actor's own kit; never spawns teammates.</summary>
[DisallowMultipleComponent]
public sealed class CombatSkillKitInput : MonoBehaviour
{
    private CombatSkillKitCatalog catalog;
    private RealTimeCombatInput combatInput;
    private RealTimeCombatManager manager;
    private InputAction selectKit, wheelSelectKit, cancel, support;
    private bool subscribed, cancelNeedsRelease, faceNeedsRelease;
    private int consumedFrame = -1;
    private Transform openedActor;
    private bool openedSolo;
    private SoloTeamProgression subscribedTeam;
    private InputControl heldSelectControl;
    private CombatSkillKit heldSelectedKit;
    public bool IsOpen { get; private set; }
    public CombatSkillKit ActiveKit { get; private set; }
    public bool IsSolo => SoloTeamProgression.IsSolo;
    public bool IsAvailable => isActiveAndEnabled && catalog != null &&
        (IsSolo || TryResolveKit(ActiveKit, out _));

    public static SkillSO GetOwnedSkill(SquadCharacterController actor, CombatSkillKit kit, int slot)
    {
        var manager = RealTimeCombatManager.Instance;
        var input = manager != null ? manager.GetComponent<CombatSkillKitInput>() : null;
        return actor != null && manager != null && (manager.IsCombatActive || manager.AllowsFreeAttacks) && manager.PlayerRoot == actor.transform &&
            input != null && input.IsAvailable && input.IsSlotAvailable(kit, slot)
                ? input.catalog.GetSkill(kit, slot) : null;
    }

    private bool TryResolveKit(CombatSkillKit requested, out CombatSkillKit kit)
    {
        var actor = manager != null && manager.PlayerRoot != null
            ? manager.PlayerRoot.GetComponentInChildren<SquadCharacterController>(true) : null;
        return TryResolveActorKit(catalog, actor != null ? actor.CharacterData : null, IsSolo, requested, out kit);
    }

    private static bool TryResolveActorKit(CombatSkillKitCatalog source, CharacterData character, bool solo,
        CombatSkillKit requested, out CombatSkillKit kit)
    {
        kit = requested;
        if (source == null) return false;
        if (solo) return source.GetSkills(requested) != null;
        if (character == null) return false;
        foreach (CombatSkillKit candidate in System.Enum.GetValues(typeof(CombatSkillKit)))
        {
            if (source.IsCharacter(character, candidate))
            { kit = candidate; return true; }
        }
        return false;
    }

    private bool IsSlotAvailable(CombatSkillKit kit, int slot)
    {
        if (catalog == null || catalog.GetSkill(kit, slot) == null) return false;
        return IsSolo ? SoloTeamProgression.Resolve()?.IsUnlocked(kit, slot) == true :
            TryResolveKit(kit, out var owned) && owned == kit;
    }

    private SkillSO[] GetAvailableSkills(CombatSkillKit kit)
    {
        var result = new SkillSO[4];
        for (int i = 0; i < result.Length; i++)
            if (IsSlotAvailable(kit, i)) result[i] = catalog.GetSkill(kit, i);
        return result;
    }

    private void OnEnable()
    {
        manager = GetComponent<RealTimeCombatManager>();
        combatInput = GetComponent<RealTimeCombatInput>();
        catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        InputModeCoordinator.ModeChanged += OnModeChanged;
        subscribedTeam = SoloTeamProgression.Resolve();
        if (subscribedTeam != null) subscribedTeam.Changed += OnTeamChanged;
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
        RefreshSelectionRelease();
        if (faceNeedsRelease && !AnyFaceHeld(pad)) faceNeedsRelease = false;
        if (cancelNeedsRelease && (pad == null || !pad.buttonEast.isPressed)) cancelNeedsRelease = false;
        if (IsOpen && (IsSolo != openedSolo || !TryResolveKit(ActiveKit, out var owned) || owned != ActiveKit ||
            manager == null || !manager.isActiveAndEnabled || !manager.IsCombatActive && !manager.AllowsFreeAttacks ||
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
        if (selectKit == null || wheelSelectKit == null || cancel == null || support == null) return;
        selectKit.performed += OnSelect;
        wheelSelectKit.performed += OnSelect;
        selectKit.canceled += OnSelectionReleased;
        wheelSelectKit.canceled += OnSelectionReleased;
        cancel.performed += OnCancel;
        support.performed += OnSupport;
        subscribed = true;
    }

    private void OnDisable()
    {
        Close();
        InputModeCoordinator.ModeChanged -= OnModeChanged;
        if (subscribedTeam != null) subscribedTeam.Changed -= OnTeamChanged;
        subscribedTeam = null;
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
        if (!TryConsumeSelection(context.control, kit)) return;
        if (IsOpen)
        {
            if (CombatSkillKitCatalog.TrySkillDirection(context.ReadValue<Vector2>(), out int slot)) TryUseSlot(slot);
        }
        else Open(kit);
    }

    private bool TryConsumeSelection(InputControl control, CombatSkillKit direction)
    {
        if (control == null || !control.device.added) return false;
        RefreshSelectionRelease();
        if (heldSelectControl == control && heldSelectedKit == direction) return false;
        heldSelectControl = control;
        heldSelectedKit = direction;
        return true;
    }

    private void OnSelectionReleased(InputAction.CallbackContext context)
    {
        // Map disabling also emits canceled, but must not count as a release.
        if (heldSelectControl != null && context.control == heldSelectControl) RefreshSelectionRelease();
    }

    private void RefreshSelectionRelease()
    {
        if (heldSelectControl != null && (!heldSelectControl.device.added ||
            heldSelectControl.EvaluateMagnitude() <= .5f)) heldSelectControl = null;
    }

    public bool Open(CombatSkillKit kit)
    {
        if (!TryResolveKit(kit, out kit)) return false;
        if (IsSolo && SoloTeamProgression.Resolve()?.IsRecruited(kit) != true)
        { Feedback("Ce membre n’a pas encore rejoint l’équipe"); return false; }
        if (!IsAvailable || manager == null || !manager.isActiveAndEnabled || !manager.IsCombatActive && !manager.AllowsFreeAttacks || manager.IsCinematicSequenceActive ||
            CounterSkillCombatController.Instance != null && CounterSkillCombatController.Instance.IsCinematicPlaying ||
            (InputModeCoordinator.CurrentMode != InputMode.Combat && InputModeCoordinator.CurrentMode != InputMode.CombatWheel)) return false;
        var actor = manager.PlayerRoot != null ? manager.PlayerRoot.GetComponentInChildren<SquadCharacterController>(true) : null;
        if (actor == null || !actor.isActiveAndEnabled || actor.CurrentHp <= 0) return false;
        if (actor.GetComponent<PlayerActionPresentationController>()?.IsChargingSkill == true) return false;
        if (IsOpen && ActiveKit == kit) return true;
        if (combatInput == null || !combatInput.DisplaySkillKit(kit.ToString(), GetAvailableSkills(kit))) return false;
        ApplyLockedSlots(kit);
        ActiveKit = kit;
        if (!IsOpen)
        {
            combatInput.CancelBufferedBasicSkills();
            CounterSkillCombatController.Instance?.EndGuard();
            faceNeedsRelease = AnyFaceHeld(Gamepad.current);
            cancelNeedsRelease = Gamepad.current != null && Gamepad.current.buttonEast.isPressed;
            IsOpen = true;
            openedActor = manager.PlayerRoot;
            openedSolo = IsSolo;
            InputModeCoordinator.Enter(this, InputMode.CombatWheel);
        }
        return true;
    }

    public bool TryUseSlot(int slot)
    {
        if (!IsOpen || !IsAvailable || InputModeCoordinator.CurrentMode != InputMode.CombatWheel || manager == null) return false;
        if (!manager.IsCombatActive && !manager.AllowsFreeAttacks || manager.IsCinematicSequenceActive ||
            CounterSkillCombatController.Instance != null && CounterSkillCombatController.Instance.IsCinematicPlaying) return false;
        if (manager.PlayerRoot != openedActor || !manager.isActiveAndEnabled || IsSolo != openedSolo ||
            !TryResolveKit(ActiveKit, out var owned) || owned != ActiveKit) { Close(); return false; }
        if (!IsSlotAvailable(ActiveKit, slot))
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
            Feedback(cooldown > 0 ? $"Recharge : {cooldown:0.#} s" : manager.LastSkillTargetingFailure ?? "Skill indisponible : cible, portee ou action en cours");
        }
        return accepted;
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (!IsOpen || cancelNeedsRelease || InputModeCoordinator.CurrentMode != InputMode.CombatWheel) return;
        consumedFrame = Time.frameCount;
        faceNeedsRelease = AnyFaceHeld(Gamepad.current);
        Close();
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

    private void ApplyLockedSlots(CombatSkillKit kit)
    {
        var wheel = FindAnyObjectByType<SkillWheel>(FindObjectsInactive.Include);
        for (int i = 0; i < 4; i++)
            wheel?.SetKitSlotLocked(i, catalog.GetSkill(kit, i) != null && !IsSlotAvailable(kit, i));
    }
    private void OnTeamChanged()
    {
        if (!IsOpen || !IsSolo) return;
        var team = SoloTeamProgression.Resolve();
        if (team == null || !team.IsRecruited(ActiveKit)) { Close(); return; }
        combatInput.DisplaySkillKit(ActiveKit.ToString(), GetAvailableSkills(ActiveKit));
        ApplyLockedSlots(ActiveKit);
    }
}
