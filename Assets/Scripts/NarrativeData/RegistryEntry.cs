using System.Collections.Generic;
using UnityEngine;

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

[CreateAssetMenu(fileName = "RegistryEntry", menuName = "Scriptable Objects/Narrative/Registry Entry")]
public class RegistryEntry : ScriptableObject
{
    [Header("Identity")]
    public string entryId;
    public string personName;
    public RegistryEntryType entryType = RegistryEntryType.Unknown;

    [Header("Context")]
    public TemporalAge age = TemporalAge.Age666;
    public string district;
    public string room;
    public string cause;
    [TextArea]
    public string note;
    public bool isStruckOut;

    [Header("References")]
    public List<Item> readableReferences = new List<Item>();
    public List<string> associatedObjectIds = new List<string>();
}
