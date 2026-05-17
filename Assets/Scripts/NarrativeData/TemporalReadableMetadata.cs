// Role:
// Optional metadata block for readable items and narrative fragments.
// Usage:
// Referenced by Item readable data so texts can be linked to an age, district,
// lineage, religious current, and revelation level without changing old readables.
// Responsibilities:
// Store lightweight narrative tags only. It does not display UI by itself.
// Dependencies:
// TemporalAge and simple serializable collections.
// Precautions:
// Keep fields optional and backward-compatible; existing Item assets may leave this disabled.
using System;
using System.Collections.Generic;

/// <summary>
/// Religious or ideological current associated with a readable fragment.
/// </summary>
public enum ReligiousCurrent
{
    None = 0,
    Esperants = 1,
    Fatalists = 2,
    Mediators = 3,
    TruthTrace = 4,
    DeepSanctuary = 5,
    Unknown = 6
}

/// <summary>
/// How deep or explicit the narrative information is.
/// </summary>
public enum NarrativeRevelationLevel
{
    Surface = 0,
    Routine = 1,
    Contradiction = 2,
    Hidden = 3,
    DeepArchive = 4
}

/// <summary>
/// Optional narrative classification attached to a readable item.
/// </summary>
[Serializable]
public class TemporalReadableMetadata
{
    /// <summary>If false, the metadata should be ignored by UI and tooling.</summary>
    public bool enabled;
    /// <summary>Temporal age most closely associated with this readable.</summary>
    public TemporalAge associatedAge = TemporalAge.Age666;
    /// <summary>District or broad area referenced by the text.</summary>
    public string district;
    /// <summary>Room, chamber, or smaller location referenced by the text.</summary>
    public string room;
    /// <summary>Optional lineage identifier connected to this readable.</summary>
    public string lineageId;
    /// <summary>Religious or ideological current suggested by the readable.</summary>
    public ReligiousCurrent religiousCurrent = ReligiousCurrent.None;
    /// <summary>How explicit this readable is in the investigation.</summary>
    public NarrativeRevelationLevel revelationLevel = NarrativeRevelationLevel.Surface;
    /// <summary>Freeform tags for searches, filters, and future tooling.</summary>
    public List<string> narrativeTags = new List<string>();
}
