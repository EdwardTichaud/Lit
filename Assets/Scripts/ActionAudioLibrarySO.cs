using UnityEngine;

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
    TorchToggle = 65,
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

[CreateAssetMenu(fileName = "ActionAudioLibrary", menuName = "Scriptable Objects/Audio/Action Audio Library")]
public class ActionAudioLibrarySO : ScriptableObject
{
    [Header("UI")]
    public AudioClipSO uiOpen;
    public AudioClipSO uiClose;
    public AudioClipSO uiConfirm;
    public AudioClipSO uiCancel;
    public AudioClipSO uiInvalid;

    [Header("Inventory")]
    public AudioClipSO inventoryOpen;
    public AudioClipSO inventoryClose;
    public AudioClipSO inventoryTake;
    public AudioClipSO inventoryDeposit;
    public AudioClipSO inventoryDrop;
    public AudioClipSO inventoryPlaceStart;
    public AudioClipSO inventoryPlaceConfirm;
    public AudioClipSO inventoryPlaceCancel;
    public AudioClipSO inventoryUse;
    public AudioClipSO inventoryBreak;
    public AudioClipSO inventoryUnlock;
    public AudioClipSO inventoryLockpickSuccess;
    public AudioClipSO inventoryLockpickFailure;
    public AudioClipSO inventoryTrap;
    public AudioClipSO inventoryReadOpen;
    public AudioClipSO inventoryReadPage;
    public AudioClipSO inventoryReadClose;
    public AudioClipSO beaconColorSelect;

    [Header("Building / Craft")]
    public AudioClipSO buildPanelOpen;
    public AudioClipSO buildPanelClose;
    public AudioClipSO buildPlacementStart;
    public AudioClipSO buildComplete;
    public AudioClipSO buildUpgrade;
    public AudioClipSO craftSuccess;
    public AudioClipSO craftFailure;

    [Header("Character")]
    public AudioClipSO characterDamage;
    public AudioClipSO characterHeal;
    public AudioClipSO characterDeath;
    public AudioClipSO characterJump;
    public AudioClipSO characterLand;
    public AudioClipSO torchToggle;
    public AudioClipSO ladderUse;
    public AudioClipSO teleport;
    public AudioClipSO returnHome;
    public AudioClipSO labyrinthStart;

    [Header("Skill Checks")]
    public AudioClipSO skillCheckSuccess;
    public AudioClipSO skillCheckFailure;

    [Header("Combat")]
    public AudioClipSO combatAttack;
    public AudioClipSO combatHit;
    public AudioClipSO combatTurn;
    public AudioClipSO combatVictory;
    public AudioClipSO combatDefeat;

    [Header("Puzzles")]
    public AudioClipSO puzzleSuccess;
    public AudioClipSO puzzleFailure;

    [Header("World")]
    public AudioClipSO destructibleDestroy;

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
            case ActionAudioCue.TorchToggle: return torchToggle;
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
