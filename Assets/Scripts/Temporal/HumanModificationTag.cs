// Role:
// Lists small documentary tags for human-made architectural or administrative changes.
// Usage:
// Used by TemporalObject to label why an object changes across temporal ages.
// Responsibilities:
// Keep a simple vocabulary for design, filtering, and future registry/readable links.
// Dependencies:
// None.
// Precautions:
// Do not reorder existing values once assets use them; add new values at the end.

/// <summary>
/// Tags describing human modifications visible across temporal strata.
/// </summary>
public enum HumanModificationTag
{
    None = 0,
    WallSealed = 1,
    RoomReassigned = 2,
    BedAdded = 3,
    WindowWalled = 4,
    FlameMoved = 5,
    PassageClosed = 6,
    RegistryCorrected = 7,
    ObjectTransferred = 8,
    DoorRebuilt = 9,
    DormitoryExpanded = 10
}
