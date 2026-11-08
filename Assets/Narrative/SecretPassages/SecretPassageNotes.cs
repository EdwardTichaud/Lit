using Unity.Netcode;
using UnityEngine;

/// <summary>Actual readable openings become authoritative Read facts, independently of passage knowledge.</summary>
public static class SecretPassageNotes
{
    public static SecretPassageCatalog Catalog => Resources.Load<SecretPassageCatalog>("Narrative/SecretPassageCatalog");

    public static void ReportOpened(Item note, GameObject reader)
    {
        var catalog = Catalog;
        if (catalog == null || catalog.Find(note) == null) return;
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            var service = WorldInteractionService.Instance;
            if (service != null && service.IsSpawned) service.RequestSecretNoteReadServerRpc(note.itemId);
        }
        else ServerRecordRead(note, reader, catalog);
    }

    public static bool ServerRecordRead(Item note, GameObject reader, SecretPassageCatalog catalog = null)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer) return false;
        if (catalog == null) catalog = Catalog;
        var entry = catalog != null ? catalog.Find(note) : null;
        var actor = reader != null ? reader.GetComponent<SquadCharacterController>() : null;
        var progression = CycleProgressionService.Instance;
        if (entry == null || catalog.notesCycle == null || !note.IsReadable() || actor == null || actor.CurrentHp <= 0 ||
            progression == null || !progression.IsReady || !progression.Authority) return false;
        bool allowed = false;
        foreach (var item in actor.Items) if (item == note) { allowed = true; break; }
        if (!allowed)
            foreach (var worldNote in Object.FindObjectsByType<InteractableItem>())
                if (worldNote.representedItem == note && worldNote.CanBeDetectedBy(actor) &&
                    CharacterInteractionDetection.IsCharacterWithinRange(actor.transform, worldNote.GetInteractionDetectionCollider(),
                        worldNote.GetInteractionAnchor(), worldNote.GetInteractionMaxDistance(actor) + .35f)) { allowed = true; break; }
        if (!allowed) return false;
        if (progression.HasFact(catalog.notesCycle, CycleStepKind.Read, entry.noteId)) return true;
        return progression.ApplyEvent(catalog.notesCycle, CycleStepKind.Read, entry.noteId);
    }
}
