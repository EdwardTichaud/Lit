using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LineageRecord", menuName = "Scriptable Objects/Narrative/Lineage Record")]
public class LineageRecord : ScriptableObject
{
    [Header("Identity")]
    public string lineageId;
    public string displayName;

    [Header("Members")]
    public List<FamilyRecord> knownMembers = new List<FamilyRecord>();
    public List<string> unresolvedMemberIds = new List<string>();

    [Header("Places and Objects")]
    public List<string> occupiedDistrictsOrRooms = new List<string>();
    public List<string> associatedObjectIds = new List<string>();

    [Header("Status")]
    public FamilyRecordStatus status = FamilyRecordStatus.Unknown;
    [TextArea]
    public string notes;
}
