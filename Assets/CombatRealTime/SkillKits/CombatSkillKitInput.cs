using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Solo support kits or the multiplayer actor's own kit; never spawns teammates.</summary>
[DisallowMultipleComponent]
public sealed class CombatSkillKitInput : MonoBehaviour
{
    private CombatSkillKitCatalog catalog;
    private RealTimeCombatInput combatInput;
    private RealTimeCombatManager manager;
    private InputAction selectKit, wheelSelectKit, cancel, support, cyclePalette;
    private bool subscribed, cancelNeedsRelease, faceNeedsRelease;
    private int consumedFrame = -1;
    private Transform openedActor;
    private bool openedSolo;
    private SoloTeamProgression subscribedTeam;
    private InputControl heldSelectControl;
    private CombatSkillKit heldSelectedKit;
    private InputControl heldPaletteControl;
    private readonly List<SkillSO> cycleRewardSkills = new();
    private int palettePage;
    private CycleProgressionService subscribedProgression;
    public bool IsOpen { get; private set; }
    public CombatSkillKit ActiveKit { get; private set; }
    public bool ShowingCycleRewards => palettePage > 0;
    public int PalettePageCount => 1 + (cycleRewardSkills.Count + 3) / 4;
    public bool IsSolo => SoloTeamProgression.IsSolo;
    public bool IsAvailable => isActiveAndEnabled && catalog != null &&
        (IsSolo || TryResolveKit(ActiveKit, out _));

    public static SkillSO GetOwnedSkill(SquadCharacterController actor, CombatSkillKit kit, int slot)
    {
        var manager = RealTimeCombatManager.Instance;
        var input = manager != null ? manager.GetComponent<CombatSkillKitInput>() : null;
        return actor != null && manager != null && (manager.IsCombatActive || manager.AllowsFreeAttacks) && manager.PlayerRoot == actor.transform &&
            input != null && input.IsAvailable && input.IsKitSlotAvailable(kit, slot)
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
        if (!ShowingCycleRewards) return IsKitSlotAvailable(kit, slot);
        return CycleSharedSkills.IsUnlocked(GetSlotSkill(kit, slot));
    }

    private bool IsKitSlotAvailable(CombatSkillKit kit, int slot)
    {
        if (catalog == null || catalog.GetSkill(kit, slot) == null) return false;
        return IsSolo ? SoloTeamProgression.Resolve()?.IsUnlocked(kit, slot) == true :
            TryResolveKit(kit, out var owned) && owned == kit;
    }

    private SkillSO[] GetAvailableSkills(CombatSkillKit kit)
    {
        var result = new SkillSO[4];
        for (int i = 0; i < result.Length; i++)
            if (IsSlotAvailable(kit, i)) result[i] = GetSlotSkill(kit, i);
        return result;
    }

    private SkillSO GetSlotSkill(CombatSkillKit kit, int slot)
    {
        if (slot < 0 || slot >= 4) return null;
        if (!ShowingCycleRewards) return catalog != null ? catalog.GetSkill(kit, slot) : null;
        int index = (palettePage - 1) * 4 + slot;
        return index < cycleRewardSkills.Count ? cycleRewardSkills[index] : null;
    }

    private void RefreshCycleRewards()
    {
        cycleRewardSkills.Clear();
        CycleSharedSkills.AppendTo(cycleRewardSkills);
        palettePage = Mathf.Clamp(palettePage, 0, PalettePageCount - 1);
    }

    private bool DisplayPalette(CombatSkillKit kit)
    {
        string title = ShowingCycleRewards ? "Souvenirs" + (PalettePageCount > 2 ? $" {palettePage}/{PalettePageCount - 1}" : "") : kit.ToString();
        string hint = PalettePageCount > 1 ? (ShowingCycleRewards ? "LB/RB : Palettes" : "LB/RB : Souvenirs") : null;
        if (combatInput == null || !combatInput.DisplaySkillKit(title, GetAvailableSkills(kit), hint)) return false;
        ApplyLockedSlots(kit);
        return true;
    }

    public bool CyclePalette(int direction)
    {
        if (!IsOpen || !IsAvailable || InputModeCoordinator.CurrentMode != InputMode.CombatWheel || direction == 0) return false;
        RefreshCycleRewards();
        if (PalettePageCount <= 1) return false;
        int previous = palettePage;
        palettePage = (palettePage + (direction > 0 ? 1 : -1) + PalettePageCount) % PalettePageCount;
        if (DisplayPalette(ActiveKit)) return true;
        palettePage = previous;
        return false;
    }

    private void HookProgression()
    {
        if (subscribedProgression == CycleProgressionService.Instance) return;
        if (subscribedProgression != null) subscribedProgression.Changed -= OnCycleRewardsChanged;
        subscribedProgression = CycleProgressionService.Instance;
        if (subscribedProgression != null) subscribedProgression.Changed += OnCycleRewardsChanged;
    }

    private void OnCycleRewardsChanged()
    {
        RefreshCycleRewards();
        if (IsOpen) DisplayPalette(ActiveKit);
    }

    private void OnEnable()
    {
        manager = GetComponent<RealTimeCombatManager>();
        combatInput = GetComponent<RealTimeCombatInput>();
        catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        InputModeCoordinator.ModeChanged += OnModeChanged;
        subscribedTeam = SoloTeamProgression.Resolve();
        if (subscribedTeam != null) subscribedTeam.Changed += OnTeamChanged;
        HookProgression();
        TrySubscribe();
    }

    private void Update()
    {
        HookProgression();
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
        cyclePalette = wheel.FindAction("CycleSkillPalette", false);
        if (selectKit == null || wheelSelectKit == null || cancel == null || support == null || cyclePalette == null) return;
        selectKit.performed += OnSelect;
        wheelSelectKit.performed += OnSelect;
        selectKit.canceled += OnSelectionReleased;
        wheelSelectKit.canceled += OnSelectionReleased;
        cancel.performed += OnCancel;
        support.performed += OnSupport;
        cyclePalette.performed += OnCyclePalette;
        cyclePalette.canceled += OnPaletteReleased;
        subscribed = true;
    }

    private void OnDisable()
    {
        Close();
        InputModeCoordinator.ModeChanged -= OnModeChanged;
        if (subscribedTeam != null) subscribedTeam.Changed -= OnTeamChanged;
        subscribedTeam = null;
        if (subscribedProgression != null) subscribedProgression.Changed -= OnCycleRewardsChanged;
        subscribedProgression = null;
        Unsubscribe();
        faceNeedsRelease = false;
        heldSelectControl = null;
        heldPaletteControl = null;
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
            cyclePalette.performed -= OnCyclePalette;
            cyclePalette.canceled -= OnPaletteReleased;
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
        if (IsOpen && ActiveKit == kit && !ShowingCycleRewards) return true;
        palettePage = 0;
        RefreshCycleRewards();
        if (!DisplayPalette(kit)) return false;
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
        { Feedback(GetSlotSkill(ActiveKit, slot) == null ? "Emplacement vide" : "Compétence verrouillée"); return false; }
        var skill = GetSlotSkill(ActiveKit, slot);
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

    private void OnCyclePalette(InputAction.CallbackContext context)
    {
        if (heldPaletteControl != null && heldPaletteControl.EvaluateMagnitude() <= .5f) heldPaletteControl = null;
        if (context.control == heldPaletteControl) return;
        heldPaletteControl = context.control;
        CyclePalette(context.ReadValue<float>() > 0 ? 1 : -1);
    }

    private void OnPaletteReleased(InputAction.CallbackContext context)
    {
        if (heldPaletteControl != null && heldPaletteControl.EvaluateMagnitude() <= .5f) heldPaletteControl = null;
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
        palettePage = 0;
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
            wheel?.SetKitSlotLocked(i, GetSlotSkill(kit, i) != null && !IsSlotAvailable(kit, i));
    }
    private void OnTeamChanged()
    {
        if (!IsOpen || !IsSolo) return;
        var team = SoloTeamProgression.Resolve();
        if (team == null || !team.IsRecruited(ActiveKit)) { Close(); return; }
        RefreshCycleRewards();
        DisplayPalette(ActiveKit);
    }
}
