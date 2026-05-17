// Role:
// ScriptableObject data for one family lineage in the castle.
// Usage:
// Create assets from Create > Scriptable Objects > Narrative > Lineage Record.
// Responsibilities:
// Group family members, rooms, districts, and important objects by lineage.
// Dependencies:
// FamilyRecord and string IDs used by registries/readables.
// Precautions:
// IDs should stay stable once referenced by readables, registries, or save data.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data asset describing a family lineage and the known evidence linked to it.
/// </summary>
[CreateAssetMenu(fileName = "LineageRecord", menuName = "Scriptable Objects/Narrative/Lineage Record")]
public class LineageRecord : ScriptableObject
{
    [Header("Identity")]
    /// <summary>Stable lineage ID used by other narrative records.</summary>
    public string lineageId;
    /// <summary>Human-readable lineage name shown in tools or UI.</summary>
    public string displayName;

    [Header("Members")]
    /// <summary>Known FamilyRecord assets belonging to this lineage.</summary>
    public List<FamilyRecord> knownMembers = new List<FamilyRecord>();
    /// <summary>Member IDs mentioned by records but not yet backed by an asset.</summary>
    public List<string> unresolvedMemberIds = new List<string>();

    [Header("Places and Objects")]
    /// <summary>Districts or rooms occupied by this lineage over time.</summary>
    public List<string> occupiedDistrictsOrRooms = new List<string>();
    /// <summary>Object IDs associated with this lineage.</summary>
    public List<string> associatedObjectIds = new List<string>();

    [Header("Status")]
    /// <summary>General status of the lineage in surviving records.</summary>
    public FamilyRecordStatus status = FamilyRecordStatus.Unknown;
    /// <summary>Freeform design notes and unresolved questions.</summary>
    [TextArea]
    public string notes;
}
