// Role:
// ScriptableObject data for one time-trapped ghost investigation.
// Usage:
// Create assets from Create > Scriptable Objects > Narrative > Ghost Data, then
// assign them to a GhostController in scene.
// Responsibilities:
// Store ghost identity, one question, knowledge reactions, and clues.
// Dependencies:
// RegistryEntry, FamilyRecord, TransgenerationalObjectRecord, Item, TemporalAge.
// Precautions:
// Keep this as authoring data. Runtime state, UI, multiplayer sync, and persistence
// belong in scene controllers or dedicated systems.
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Type of answer expected by a ghost question.
/// </summary>
public enum GhostAnswerType
{
    Unknown = 0,
    Person = 1,
    Location = 2,
    Event = 3,
    CauseOfDeath = 4,
    Object = 5,
    Custom = 6
}

/// <summary>
/// Type of clue that can help solve a ghost question.
/// </summary>
public enum GhostEvidenceType
{
    Unknown = 0,
    RegistryEntry = 1,
    FamilyRecord = 2,
    TransgenerationalObject = 3,
    ReadableItem = 4,
    Location = 5,
    CustomNote = 6
}

/// <summary>
/// One clue reference used by a ghost investigation.
/// </summary>
[Serializable]
public class GhostEvidenceReference
{
    [Header("Type")]
    /// <summary>Kind of clue represented by this entry.</summary>
    public GhostEvidenceType evidenceType = GhostEvidenceType.Unknown;

    [Header("References")]
    /// <summary>Registry entry that contains or confirms this clue.</summary>
    public RegistryEntry registryEntry;
    /// <summary>Family record linked to this clue.</summary>
    public FamilyRecord familyRecord;
    /// <summary>Transgenerational object linked to this clue.</summary>
    public TransgenerationalObjectRecord transgenerationalObject;
    /// <summary>Readable item that exposes this clue in game.</summary>
    public Item readableItem;
    /// <summary>Stable room, district, or scene location ID.</summary>
    public string locationId;

    [Header("Design Note")]
    /// <summary>Freeform note explaining what this clue proves.</summary>
    [TextArea]
    public string note;
}

/// <summary>
/// One possible ghost reaction unlocked by what the player knows.
/// </summary>
[Serializable]
public class GhostKnowledgeReaction
{
    [Header("Player Option")]
    /// <summary>Text representing what the player can say or prove once requirements are met.</summary>
    [TextArea(1, 3)]
    public string optionText;
    /// <summary>Knowledge condition required for this option to exist.</summary>
    public KnowledgeRequirement requirement = new KnowledgeRequirement();
    /// <summary>Higher priority wins when several reactions are available.</summary>
    public int priority;

    [Header("Ghost Response")]
    /// <summary>Line spoken by the ghost after this knowledge is used.</summary>
    [TextArea(2, 5)]
    public string responseLine;
    /// <summary>If true, this reaction counts as understanding or appeasing the ghost.</summary>
    public bool marksGhostUnderstood = true;

    [Header("Unlocks")]
    /// <summary>Knowledge unlocked when this reaction is used.</summary>
    public List<KnowledgeSO> unlockKnowledge = new List<KnowledgeSO>();

    [Header("Scene Effects")]
    /// <summary>Scene-side effect IDs triggered when this reaction is used.</summary>
    public List<string> triggerEffectIds = new List<string>();

    [Header("Design Notes")]
    /// <summary>Freeform notes about why this reaction exists.</summary>
    [TextArea]
    public string notes;

    public bool IsAvailable(KnowledgeManager manager)
    {
        return requirement == null || requirement.IsSatisfied(manager);
    }

    public int GetSpecificityScore()
    {
        return Mathf.Max(0, priority) + (requirement != null ? requirement.GetSpecificityScore() : 0);
    }
}

/// <summary>
/// Data asset describing a ghost trapped in time and the investigation tied to it.
/// </summary>
[CreateAssetMenu(fileName = "GhostData", menuName = "Scriptable Objects/Narrative/Ghost Data")]
public class GhostData : ScriptableObject
{
    [Header("Identity")]
    /// <summary>Stable ghost ID used by saves, scene references, and multiplayer sync.</summary>
    public string ghostId;
    /// <summary>Name shown in tools or UI when this ghost is identified.</summary>
    public string displayName;
    /// <summary>Age where this ghost apparition primarily belongs.</summary>
    public TemporalAge apparitionAge = TemporalAge.Age666;
    /// <summary>Stable room, district, or scene location ID where this ghost can appear.</summary>
    public string apparitionLocationId;
    /// <summary>Prefab used by scene markers to preview and bake this apparition.</summary>
    public GameObject worldPrefab;

    [Header("Investigation")]
    /// <summary>Stable ID of the person, object, place, or event searched by this ghost.</summary>
    public string targetId;
    /// <summary>Designer-facing target name, for example Jon.</summary>
    public string targetDisplayName;
    /// <summary>Kind of answer the player must infer.</summary>
    public GhostAnswerType expectedAnswerType = GhostAnswerType.Person;

    [Header("Question")]
    /// <summary>Line spoken when the ghost appears.</summary>
    [TextArea(2, 4)]
    public string apparitionLine;
    /// <summary>Question shown or spoken to the player.</summary>
    [TextArea(2, 6)]
    public string question;

    [Header("Knowledge Reactions")]
    /// <summary>Knowledge unlocked simply by listening to this ghost.</summary>
    public List<KnowledgeSO> knowledgeUnlockedOnListen = new List<KnowledgeSO>();
    /// <summary>Possible reactions enabled by the player's unlocked knowledge.</summary>
    public List<GhostKnowledgeReaction> reactions = new List<GhostKnowledgeReaction>();
    /// <summary>Line shown when no knowledge-based reaction is available yet.</summary>
    [TextArea(2, 5)]
    public string missingKnowledgeLine = "Il manque encore un élément pour comprendre ce souvenir.";

    [Header("Evidence")]
    /// <summary>Clues that allow players to solve this ghost investigation.</summary>
    public List<GhostEvidenceReference> evidence = new List<GhostEvidenceReference>();

    [Header("Scene Effects")]
    /// <summary>GameObjectID values dissolved when this ghost is solved.</summary>
    public List<string> dissolveTargetGameObjectIds = new List<string>();
    /// <summary>Also search GhostDissolveController components under each resolved target.</summary>
    public bool includeChildrenInDissolveTargets = true;
    /// <summary>Add a GhostDissolveController at runtime if a resolved target has none.</summary>
    public bool addDissolveControllerIfMissing = true;
    /// <summary>Duration override for dissolved GameObjectID targets. A value <= 0 uses the controller default.</summary>
    public float dissolveTargetDurationOverride = -1f;

    [Header("Design Notes")]
    /// <summary>Freeform notes about the ghost's purpose in the zone or story.</summary>
    [TextArea]
    public string notes;

    public GameObject ResolveWorldPrefab()
    {
        return worldPrefab;
    }
}
