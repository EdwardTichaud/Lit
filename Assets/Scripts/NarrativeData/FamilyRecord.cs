using System.Collections.Generic;
using UnityEngine;

public enum FamilyRecordStatus
{
    Unknown = 0,
    Normal = 1,
    StruckOut = 2,
    Moved = 3,
    Missing = 4,
    Unspecified = 5
}

[CreateAssetMenu(fileName = "FamilyRecord", menuName = "Scriptable Objects/Narrative/Family Record")]
public class FamilyRecord : ScriptableObject
{
    [Header("Identity")]
    public string recordId;
    public string displayName;
    public string lineageId;

    [Header("Relations")]
    public List<string> parentIds = new List<string>();
    public List<string> childIds = new List<string>();
    public string spouseId;

    [Header("Life")]
    public TemporalAge birthAge = TemporalAge.Age000;
    public TemporalAge deathAge = TemporalAge.Age666;
    public string causeOfDeath;

    [Header("Places and Objects")]
    public List<string> occupiedDistrictsOrRooms = new List<string>();
    public List<string> associatedObjectIds = new List<string>();

    [Header("Status")]
    public FamilyRecordStatus status = FamilyRecordStatus.Unknown;
    [TextArea]
    public string notes;
}
