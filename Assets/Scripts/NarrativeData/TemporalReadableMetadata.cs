using System;
using System.Collections.Generic;

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

public enum NarrativeRevelationLevel
{
    Surface = 0,
    Routine = 1,
    Contradiction = 2,
    Hidden = 3,
    DeepArchive = 4
}

[Serializable]
public class TemporalReadableMetadata
{
    public bool enabled;
    public TemporalAge associatedAge = TemporalAge.Age666;
    public string district;
    public string room;
    public string lineageId;
    public ReligiousCurrent religiousCurrent = ReligiousCurrent.None;
    public NarrativeRevelationLevel revelationLevel = NarrativeRevelationLevel.Surface;
    public List<string> narrativeTags = new List<string>();
}
