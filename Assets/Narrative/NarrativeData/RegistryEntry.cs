// Role:
// ScriptableObject data for one administrative registry entry.
// Usage:
// Create assets from Create > Scriptable Objects > Narrative > Registry Entry.
// Responsibilities:
// Represent births, deaths, relocations, room assignments, corrections, and other
// bureaucratic traces used by the investigation.
// Dependencies:
// TemporalAge, Item readable references, and string object IDs.
// Precautions:
// This is data only. Do not add gameplay logic here unless a dedicated registry
// UI/system exists.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kind of administrative entry represented by a RegistryEntry asset.
/// </summary>
public enum RegistryEntryType
{
    Unknown = 0,
    Birth = 1,
    Death = 2,
    Relocation = 3,
    Habitation = 4,
    Correction = 5,
    Vigil = 6,
    Ration = 7,
    Maintenance = 8
}

/// <summary>
/// Data asset for one line of a birth, death, movement, habitation, or maintenance register.
/// </summary>
[CreateAssetMenu(fileName = "RegistryEntry", menuName = "Scriptable Objects/Narrative/Registry Entry")]
public class RegistryEntry : ScriptableObject
{
    [Header("Identity")]
    /// <summary>Stable registry entry ID.</summary>
    public string entryId;
    /// <summary>Person name as written in the register.</summary>
    public string personName;
    /// <summary>Administrative category of this entry.</summary>
    public RegistryEntryType entryType = RegistryEntryType.Unknown;

    [Header("Context")]
    /// <summary>Temporal age associated with the entry.</summary>
    public TemporalAge age = TemporalAge.Age666;
    /// <summary>District mentioned by the entry.</summary>
    public string district;
    /// <summary>Room or chamber mentioned by the entry.</summary>
    public string room;
    /// <summary>Cause of death, relocation, correction, or maintenance if relevant.</summary>
    public string cause;
    /// <summary>Freeform note exactly for ambiguous administrative details.</summary>
    [TextArea]
    public string note;
    /// <summary>True if the name or entry is struck out in the surviving register.</summary>
    public bool isStruckOut;

    [Header("References")]
    /// <summary>Readable Item assets that mention or show this registry entry.</summary>
    public List<Item> readableReferences = new List<Item>();
    /// <summary>Object IDs linked to this entry.</summary>
    public List<string> associatedObjectIds = new List<string>();
}
