using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TransgenerationalObject", menuName = "Scriptable Objects/Narrative/Transgenerational Object")]
public class TransgenerationalObjectRecord : ScriptableObject
{
    [Header("Identity")]
    public string objectId;
    public string displayName;
    [TextArea]
    public string description;

    [Header("Lineage")]
    public string associatedLineageId;
    public List<string> successiveOwnerIds = new List<string>();

    [Header("Temporal Presence")]
    public List<TemporalAge> appearsAtAges = new List<TemporalAge>();
    public List<string> foundLocations = new List<string>();

    [Header("Narrative")]
    [TextArea]
    public string notes;
}
