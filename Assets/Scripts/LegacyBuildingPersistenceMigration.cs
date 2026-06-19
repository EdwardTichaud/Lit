using System;
using System.Collections.Generic;

// Couche de compatibilite a conserver avant toute suppression physique du code
// Building. Elle garde les anciennes donnees dormantes tant que le module est
// desactive, sans reconstruire de batiments dans le monde.
public static class LegacyBuildingPersistenceMigration
{
    public static List<BuiltConstructionData> ResolveCharacterSaveData(
        CharacterSaveData previouslyLoadedData,
        List<BuiltConstructionData> runtimeCapture)
    {
        if (LegacyBuildingSystem.Enabled)
        {
            return CloneBuiltConstructions(runtimeCapture);
        }

        if (!LegacyBuildingSystem.PreserveLegacySaveData)
        {
            return new List<BuiltConstructionData>();
        }

        return CloneBuiltConstructions(previouslyLoadedData?.builtConstructions);
    }

    public static List<PersistentObjectSnapshot> CaptureLegacyWorldSnapshots(WorldSnapshot snapshot)
    {
        List<PersistentObjectSnapshot> results = new List<PersistentObjectSnapshot>();
        if (snapshot?.RuntimeObjects == null)
        {
            return results;
        }

        for (int i = 0; i < snapshot.RuntimeObjects.Count; i++)
        {
            PersistentObjectSnapshot candidate = snapshot.RuntimeObjects[i];
            if (LegacyBuildingSystem.IsBuildingSnapshot(candidate))
            {
                results.Add(CloneSnapshot(candidate));
            }
        }

        return results;
    }

    public static void MergeLegacyWorldSnapshots(
        WorldSnapshot destination,
        IReadOnlyList<PersistentObjectSnapshot> preservedSnapshots)
    {
        if (LegacyBuildingSystem.Enabled ||
            !LegacyBuildingSystem.PreserveLegacyWorldSnapshots ||
            destination == null ||
            preservedSnapshots == null)
        {
            return;
        }

        destination.RuntimeObjects ??= new List<PersistentObjectSnapshot>();
        HashSet<string> knownIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < destination.RuntimeObjects.Count; i++)
        {
            PersistentObjectSnapshot existing = destination.RuntimeObjects[i];
            if (existing != null && !string.IsNullOrWhiteSpace(existing.PersistentId))
            {
                knownIds.Add(existing.PersistentId);
            }
        }

        for (int i = 0; i < preservedSnapshots.Count; i++)
        {
            PersistentObjectSnapshot snapshot = preservedSnapshots[i];
            if (snapshot == null ||
                string.IsNullOrWhiteSpace(snapshot.PersistentId) ||
                !knownIds.Add(snapshot.PersistentId))
            {
                continue;
            }

            destination.RuntimeObjects.Add(CloneSnapshot(snapshot));
        }
    }

    private static List<BuiltConstructionData> CloneBuiltConstructions(
        IReadOnlyList<BuiltConstructionData> source)
    {
        List<BuiltConstructionData> results = new List<BuiltConstructionData>();
        if (source == null)
        {
            return results;
        }

        for (int i = 0; i < source.Count; i++)
        {
            BuiltConstructionData entry = source[i];
            if (entry == null)
            {
                continue;
            }

            results.Add(new BuiltConstructionData
            {
                buildId = entry.buildId,
                itemId = entry.itemId,
                buildingDataId = entry.buildingDataId,
                level = entry.level,
                isHomeChest = entry.isHomeChest,
                position = entry.position,
                rotation = entry.rotation,
                scale = entry.scale
            });
        }

        return results;
    }

    private static PersistentObjectSnapshot CloneSnapshot(PersistentObjectSnapshot source)
    {
        if (source == null)
        {
            return null;
        }

        PersistentObjectSnapshot clone = new PersistentObjectSnapshot
        {
            PersistentId = source.PersistentId,
            ObjectKind = source.ObjectKind,
            RuntimePrefabId = source.RuntimePrefabId,
            SceneName = source.SceneName,
            DestroyIfMissing = source.DestroyIfMissing,
            Transform = source.Transform,
            StateBlobs = new List<StateBlobSnapshot>()
        };

        if (source.StateBlobs == null)
        {
            return clone;
        }

        for (int i = 0; i < source.StateBlobs.Count; i++)
        {
            StateBlobSnapshot blob = source.StateBlobs[i];
            if (blob == null)
            {
                continue;
            }

            clone.StateBlobs.Add(new StateBlobSnapshot
            {
                ProviderId = blob.ProviderId,
                Payload = blob.Payload != null ? (byte[])blob.Payload.Clone() : Array.Empty<byte>()
            });
        }

        return clone;
    }
}
