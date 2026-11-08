using System;
using UnityEngine;

/// <summary>Twenty planned identities; a reserved entry is not an authored note or passage.</summary>
[CreateAssetMenu(menuName = "Lit/Narrative/Secret Passage Catalog")]
public sealed class SecretPassageCatalog : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        public int number;
        public string noteId;
        public bool reserved;
        public string passageScene;
        public string noteScene;
        public SecretPassageDefinition definition;
    }
    public CycleDefinition notesCycle;
    public Entry[] entries = Array.Empty<Entry>();
    public Entry Find(Item note)
    {
        if (note == null) return null;
        foreach (var entry in entries)
            if (entry != null && !entry.reserved && entry.definition != null && entry.definition.note == note && entry.noteId == note.itemId) return entry;
        return null;
    }
    public int CountRead(CycleProgressionService service)
    {
        if (service == null || !service.IsReady || notesCycle == null) return 0;
        int count = 0;
        foreach (var entry in entries)
            if (entry != null && service.HasFact(notesCycle, CycleStepKind.Read, entry.noteId)) count++;
        return count;
    }
}
