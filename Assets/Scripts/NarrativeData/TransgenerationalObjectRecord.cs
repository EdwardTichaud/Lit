// Role:
// ScriptableObject data for an object transmitted across generations.
// Usage:
// Create assets from Create > Scriptable Objects > Narrative > Transgenerational Object.
// Responsibilities:
// Track the object's ID, lineage, owners, ages, found locations, and narrative notes.
// Dependencies:
// TemporalAge and string IDs shared with family/lineage/registry records.
// Precautions:
// Keep this as lightweight data until a dedicated UI or investigation system exists.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data asset describing one object that appears across several generations.
/// </summary>
[CreateAssetMenu(fileName = "TransgenerationalObject", menuName = "Scriptable Objects/Narrative/Transgenerational Object")]
public class TransgenerationalObjectRecord : ScriptableObject
{
    [Header("Identity")]
    /// <summary>Stable object ID used by records and readables.</summary>
    public string objectId;
    /// <summary>Human-readable object name.</summary>
    public string displayName;
    /// <summary>Short description of the object and its physical identity.</summary>
    [TextArea]
    public string description;

    [Header("Lineage")]
    /// <summary>Lineage ID most strongly associated with this object.</summary>
    public string associatedLineageId;
    /// <summary>Ordered or partial list of owner record IDs.</summary>
    public List<string> successiveOwnerIds = new List<string>();

    [Header("Temporal Presence")]
    /// <summary>Ages where this object is known to appear.</summary>
    public List<TemporalAge> appearsAtAges = new List<TemporalAge>();
    /// <summary>Rooms or districts where the object can be found.</summary>
    public List<string> foundLocations = new List<string>();

    [Header("Narrative")]
    /// <summary>Freeform narrative notes, contradictions, or clues.</summary>
    [TextArea]
    public string notes;
}
