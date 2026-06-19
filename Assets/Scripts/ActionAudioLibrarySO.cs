// Role:
// Central ScriptableObject mapping gameplay action cues to reusable AudioClipSO assets.
// Usage:
// Loaded or assigned by audio/interaction systems to play consistent UI, inventory,
// character, combat, puzzle, and world feedback sounds.
// Responsibilities:
// Keep cue-to-clip lookup data in one place.
// Dependencies:
// AudioClipSO.
// Precautions:
// Keep enum numeric values stable if save data, settings, or analytics ever store them.
using UnityEngine;

/// <summary>
/// Identifiers for common gameplay/UI audio feedback events.
/// </summary>
public enum ActionAudioCue
{
    None = 0,
    UiOpen = 1,
    UiClose = 2,
    UiConfirm = 3,
    UiCancel = 4,
    UiInvalid = 5,
    InventoryOpen = 10,
    InventoryClose = 11,
    InventoryTake = 12,
    InventoryDeposit = 13,
    InventoryDrop = 14,
    InventoryPlaceStart = 15,
    InventoryPlaceConfirm = 16,
    InventoryPlaceCancel = 17,
    InventoryUse = 18,
    InventoryBreak = 19,
    InventoryUnlock = 20,
    InventoryLockpickSuccess = 21,
    InventoryLockpickFailure = 22,
    InventoryTrap = 23,
    InventoryReadOpen = 24,
    InventoryReadPage = 25,
    InventoryReadClose = 26,
    BeaconColorSelect = 27,
    BuildPanelOpen = 40,
    BuildPanelClose = 41,
    BuildPlacementStart = 42,
    BuildComplete = 43,
    BuildUpgrade = 44,
    CraftSuccess = 45,
    CraftFailure = 46,
    CharacterDamage = 60,
    CharacterHeal = 61,
    CharacterDeath = 62,
    CharacterJump = 63,
    CharacterLand = 64,
    FlameToggle = 65,
    LadderUse = 66,
    Teleport = 67,
    ReturnHome = 68,
    LabyrinthStart = 69,
    SkillCheckSuccess = 80,
    SkillCheckFailure = 81,
    CombatAttack = 90,
    CombatHit = 91,
    CombatTurn = 92,
    CombatVictory = 93,
    CombatDefeat = 94,
    PuzzleSuccess = 100,
    PuzzleFailure = 101,
    DestructibleDestroy = 110,
}

/// <summary>
/// ScriptableObject library resolving ActionAudioCue values to AudioClipSO assets.
/// </summary>
[CreateAssetMenu(fileName = "ActionAudioLibrary", menuName = "Scriptable Objects/Audio/Action Audio Library")]
public class ActionAudioLibrarySO : ScriptableObject
{
    [Header("UI")]
    /// <summary>Clip for opening UI panels.</summary>
    public AudioClipSO uiOpen;
    /// <summary>Clip for closing UI panels.</summary>
    public AudioClipSO uiClose;
    /// <summary>Clip for confirming UI actions.</summary>
    public AudioClipSO uiConfirm;
    /// <summary>Clip for cancelling UI actions.</summary>
    public AudioClipSO uiCancel;
    /// <summary>Clip for invalid UI actions.</summary>
    public AudioClipSO uiInvalid;

    [Header("Inventory")]
    /// <summary>Clip for opening inventory.</summary>
    public AudioClipSO inventoryOpen;
    /// <summary>Clip for closing inventory.</summary>
    public AudioClipSO inventoryClose;
    /// <summary>Clip for taking an item.</summary>
    public AudioClipSO inventoryTake;
    /// <summary>Clip for depositing an item.</summary>
    public AudioClipSO inventoryDeposit;
    /// <summary>Clip for dropping an item.</summary>
    public AudioClipSO inventoryDrop;
    /// <summary>Clip for starting item placement.</summary>
    public AudioClipSO inventoryPlaceStart;
    /// <summary>Clip for confirming item placement.</summary>
    public AudioClipSO inventoryPlaceConfirm;
    /// <summary>Clip for cancelling item placement.</summary>
    public AudioClipSO inventoryPlaceCancel;
    /// <summary>Clip for using an item.</summary>
    public AudioClipSO inventoryUse;
    /// <summary>Clip for breaking or consuming an item.</summary>
    public AudioClipSO inventoryBreak;
    /// <summary>Clip for unlocking through inventory actions.</summary>
    public AudioClipSO inventoryUnlock;
    /// <summary>Clip for successful lockpicking.</summary>
    public AudioClipSO inventoryLockpickSuccess;
    /// <summary>Clip for failed lockpicking.</summary>
    public AudioClipSO inventoryLockpickFailure;
    /// <summary>Clip for trap feedback.</summary>
    public AudioClipSO inventoryTrap;
    /// <summary>Clip for opening a readable.</summary>
    public AudioClipSO inventoryReadOpen;
    /// <summary>Clip for turning a readable page.</summary>
    public AudioClipSO inventoryReadPage;
    /// <summary>Clip for closing a readable.</summary>
    public AudioClipSO inventoryReadClose;
    /// <summary>Clip for selecting a beacon color.</summary>
    public AudioClipSO beaconColorSelect;

    [Header("Building / Craft")]
    /// <summary>Clip for opening the building panel.</summary>
    public AudioClipSO buildPanelOpen;
    /// <summary>Clip for closing the building panel.</summary>
    public AudioClipSO buildPanelClose;
    /// <summary>Clip for starting building placement.</summary>
    public AudioClipSO buildPlacementStart;
    /// <summary>Clip for completing building placement.</summary>
    public AudioClipSO buildComplete;
    /// <summary>Clip for upgrading a building.</summary>
    public AudioClipSO buildUpgrade;
    /// <summary>Clip for successful crafting.</summary>
    public AudioClipSO craftSuccess;
    /// <summary>Clip for failed crafting.</summary>
    public AudioClipSO craftFailure;

    [Header("Character")]
    /// <summary>Clip for character damage.</summary>
    public AudioClipSO characterDamage;
    /// <summary>Clip for character healing.</summary>
    public AudioClipSO characterHeal;
    /// <summary>Clip for character death.</summary>
    public AudioClipSO characterDeath;
    /// <summary>Clip for jumping.</summary>
    public AudioClipSO characterJump;
    /// <summary>Clip for landing.</summary>
    public AudioClipSO characterLand;
    /// <summary>Clip for toggling the flame.</summary>
    public AudioClipSO flameToggle;
    /// <summary>Clip for ladder use.</summary>
    public AudioClipSO ladderUse;
    /// <summary>Clip for teleportation.</summary>
    public AudioClipSO teleport;
    /// <summary>Clip for returning home.</summary>
    public AudioClipSO returnHome;
    /// <summary>Clip for starting a labyrinth or expedition step.</summary>
    public AudioClipSO labyrinthStart;

    [Header("Skill Checks")]
    /// <summary>Clip for successful skill checks.</summary>
    public AudioClipSO skillCheckSuccess;
    /// <summary>Clip for failed skill checks.</summary>
    public AudioClipSO skillCheckFailure;

    [Header("Combat")]
    /// <summary>Clip for player or enemy combat attacks.</summary>
    public AudioClipSO combatAttack;
    /// <summary>Clip for combat hits.</summary>
    public AudioClipSO combatHit;
    /// <summary>Clip for combat turn transitions.</summary>
    public AudioClipSO combatTurn;
    /// <summary>Clip for combat victory.</summary>
    public AudioClipSO combatVictory;
    /// <summary>Clip for combat defeat.</summary>
    public AudioClipSO combatDefeat;

    [Header("Puzzles")]
    /// <summary>Clip for puzzle success.</summary>
    public AudioClipSO puzzleSuccess;
    /// <summary>Clip for puzzle failure.</summary>
    public AudioClipSO puzzleFailure;

    [Header("World")]
    /// <summary>Clip for destroying destructible world objects.</summary>
    public AudioClipSO destructibleDestroy;

    /// <summary>
    /// Returns the clip configured for the given action cue, or null if none is configured.
    /// </summary>
    public AudioClipSO Resolve(ActionAudioCue cue)
    {
        switch (cue)
        {
            case ActionAudioCue.UiOpen: return uiOpen;
            case ActionAudioCue.UiClose: return uiClose;
            case ActionAudioCue.UiConfirm: return uiConfirm;
            case ActionAudioCue.UiCancel: return uiCancel;
            case ActionAudioCue.UiInvalid: return uiInvalid;
            case ActionAudioCue.InventoryOpen: return inventoryOpen;
            case ActionAudioCue.InventoryClose: return inventoryClose;
            case ActionAudioCue.InventoryTake: return inventoryTake;
            case ActionAudioCue.InventoryDeposit: return inventoryDeposit;
            case ActionAudioCue.InventoryDrop: return inventoryDrop;
            case ActionAudioCue.InventoryPlaceStart: return inventoryPlaceStart;
            case ActionAudioCue.InventoryPlaceConfirm: return inventoryPlaceConfirm;
            case ActionAudioCue.InventoryPlaceCancel: return inventoryPlaceCancel;
            case ActionAudioCue.InventoryUse: return inventoryUse;
            case ActionAudioCue.InventoryBreak: return inventoryBreak;
            case ActionAudioCue.InventoryUnlock: return inventoryUnlock;
            case ActionAudioCue.InventoryLockpickSuccess: return inventoryLockpickSuccess;
            case ActionAudioCue.InventoryLockpickFailure: return inventoryLockpickFailure;
            case ActionAudioCue.InventoryTrap: return inventoryTrap;
            case ActionAudioCue.InventoryReadOpen: return inventoryReadOpen;
            case ActionAudioCue.InventoryReadPage: return inventoryReadPage;
            case ActionAudioCue.InventoryReadClose: return inventoryReadClose;
            case ActionAudioCue.BeaconColorSelect: return beaconColorSelect;
            case ActionAudioCue.BuildPanelOpen: return buildPanelOpen;
            case ActionAudioCue.BuildPanelClose: return buildPanelClose;
            case ActionAudioCue.BuildPlacementStart: return buildPlacementStart;
            case ActionAudioCue.BuildComplete: return buildComplete;
            case ActionAudioCue.BuildUpgrade: return buildUpgrade;
            case ActionAudioCue.CraftSuccess: return craftSuccess;
            case ActionAudioCue.CraftFailure: return craftFailure;
            case ActionAudioCue.CharacterDamage: return characterDamage;
            case ActionAudioCue.CharacterHeal: return characterHeal;
            case ActionAudioCue.CharacterDeath: return characterDeath;
            case ActionAudioCue.CharacterJump: return characterJump;
            case ActionAudioCue.CharacterLand: return characterLand;
            case ActionAudioCue.FlameToggle: return flameToggle;
            case ActionAudioCue.LadderUse: return ladderUse;
            case ActionAudioCue.Teleport: return teleport;
            case ActionAudioCue.ReturnHome: return returnHome;
            case ActionAudioCue.LabyrinthStart: return labyrinthStart;
            case ActionAudioCue.SkillCheckSuccess: return skillCheckSuccess;
            case ActionAudioCue.SkillCheckFailure: return skillCheckFailure;
            case ActionAudioCue.CombatAttack: return combatAttack;
            case ActionAudioCue.CombatHit: return combatHit;
            case ActionAudioCue.CombatTurn: return combatTurn;
            case ActionAudioCue.CombatVictory: return combatVictory;
            case ActionAudioCue.CombatDefeat: return combatDefeat;
            case ActionAudioCue.PuzzleSuccess: return puzzleSuccess;
            case ActionAudioCue.PuzzleFailure: return puzzleFailure;
            case ActionAudioCue.DestructibleDestroy: return destructibleDestroy;
            default: return null;
        }
    }
}
