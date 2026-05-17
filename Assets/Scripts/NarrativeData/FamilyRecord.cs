// Role:
// ScriptableObject data for one person or family member in the castle records.
// Usage:
// Create assets from Create > Scriptable Objects > Narrative > Family Record.
// Responsibilities:
// Store relationships, life ages, occupied places, linked objects, and record status.
// Dependencies:
// TemporalAge and string IDs shared with lineage, registry, and object records.
// Precautions:
// Prefer stable IDs over names; names can be struck out, corrected, or ambiguous in lore.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Administrative/narrative status of a person or lineage in records.
/// </summary>
public enum FamilyRecordStatus
{
    Unknown = 0,
    Normal = 1,
    StruckOut = 2,
    Moved = 3,
    Missing = 4,
    Unspecified = 5
}

/// <summary>
/// Data asset describing one person in a reconstructable lineage.
/// </summary>
[CreateAssetMenu(fileName = "FamilyRecord", menuName = "Scriptable Objects/Narrative/Family Record")]
public class FamilyRecord : ScriptableObject
{
    [Header("Identity")]
    /// <summary>Stable person record ID.</summary>
    public string recordId;
    /// <summary>Name visible to designers or UI.</summary>
    public string displayName;
    /// <summary>Lineage ID this person belongs to.</summary>
    public string lineageId;

    [Header("Relations")]
    /// <summary>IDs of parent records.</summary>
    public List<string> parentIds = new List<string>();
    /// <summary>IDs of child records.</summary>
    public List<string> childIds = new List<string>();
    /// <summary>ID of the spouse record, if known.</summary>
    public string spouseId;

    [Header("Life")]
    /// <summary>Approximate birth age in the internal temporal grid.</summary>
    public TemporalAge birthAge = TemporalAge.Age000;
    /// <summary>Approximate death age in the internal temporal grid.</summary>
    public TemporalAge deathAge = TemporalAge.Age666;
    /// <summary>Known or suspected cause of death.</summary>
    public string causeOfDeath;

    [Header("Places and Objects")]
    /// <summary>Rooms or districts occupied by this person.</summary>
    public List<string> occupiedDistrictsOrRooms = new List<string>();
    /// <summary>Object IDs associated with this person.</summary>
    public List<string> associatedObjectIds = new List<string>();

    [Header("Status")]
    /// <summary>How the person appears in surviving records.</summary>
    public FamilyRecordStatus status = FamilyRecordStatus.Unknown;
    /// <summary>Freeform notes, contradictions, or design intent.</summary>
    [TextArea]
    public string notes;
}
