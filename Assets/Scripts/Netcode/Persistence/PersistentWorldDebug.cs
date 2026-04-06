using UnityEngine;

public static class PersistentWorldDebug
{
    private const string Prefix = "[PersistentWorld]";

    public static bool Enabled { get; set; } = true;

    public static void Log(string message, Object context = null)
    {
        if (!Enabled)
        {
            return;
        }

        if (context != null)
        {
            Debug.Log($"{Prefix} {message}", context);
            return;
        }

        Debug.Log($"{Prefix} {message}");
    }

    public static void Warn(string message, Object context = null)
    {
        if (context != null)
        {
            Debug.LogWarning($"{Prefix} {message}", context);
            return;
        }

        Debug.LogWarning($"{Prefix} {message}");
    }

    public static void Error(string message, Object context = null)
    {
        if (context != null)
        {
            Debug.LogError($"{Prefix} {message}", context);
            return;
        }

        Debug.LogError($"{Prefix} {message}");
    }

    public static bool Assert(bool condition, string message, Object context = null)
    {
        if (condition)
        {
            return true;
        }

        Error(message, context);
        return false;
    }

    public static void LogSnapshotObjectAudit(
        string stage,
        string listKind,
        PersistentObjectSnapshot snapshot,
        string resolutionMode,
        Object context = null,
        PersistentNetworkObject resolvedObject = null,
        string note = null)
    {
        if (!Enabled || snapshot == null)
        {
            return;
        }

        string resolvedPath = resolvedObject != null
            ? DescribeTransform(resolvedObject.transform)
            : "<unresolved>";
        string resolvedType = resolvedObject != null
            ? DescribePersistentObjectType(resolvedObject)
            : string.Empty;
        string safeStage = string.IsNullOrWhiteSpace(stage) ? "snapshot-audit" : stage;
        string safeListKind = string.IsNullOrWhiteSpace(listKind) ? "unknown" : listKind;
        string safeResolutionMode = string.IsNullOrWhiteSpace(resolutionMode) ? "unknown" : resolutionMode;
        string safeNote = note ?? string.Empty;

        Log(
            $"snapshot object audit stage='{safeStage}' list='{safeListKind}' persistentId='{snapshot.PersistentId}' kind='{snapshot.ObjectKind}' prefab='{snapshot.RuntimePrefabId}' resolutionMode='{safeResolutionMode}' destroyIfMissing={snapshot.DestroyIfMissing} scene='{snapshot.SceneName}' resolvedPath='{resolvedPath}' resolvedType='{resolvedType}' note='{safeNote}'",
            resolvedObject != null ? resolvedObject : context);
    }

    public static string DescribeTransform(Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        string sceneName = target.gameObject.scene.IsValid() ? target.gameObject.scene.name : "NoScene";
        return $"{sceneName}:{path}";
    }

    public static string DescribePersistentObjectType(PersistentNetworkObject persistentObject)
    {
        if (persistentObject == null)
        {
            return string.Empty;
        }

        GameObject gameObject = persistentObject.gameObject;
        if (gameObject == null)
        {
            return nameof(PersistentNetworkObject);
        }

        if (gameObject.GetComponent<KnowledgeManager>() != null)
        {
            return nameof(KnowledgeManager);
        }

        if (gameObject.GetComponent<SquadCharacterController>() != null)
        {
            return nameof(SquadCharacterController);
        }

        if (gameObject.GetComponent<BuildingInfoInteractable>() != null)
        {
            return nameof(BuildingInfoInteractable);
        }

        if (gameObject.GetComponent<InteractableItem>() != null)
        {
            return nameof(InteractableItem);
        }

        if (gameObject.GetComponent<Brasero>() != null)
        {
            return nameof(Brasero);
        }

        if (gameObject.GetComponent<TwoLeverPuzzle>() != null)
        {
            return nameof(TwoLeverPuzzle);
        }

        if (gameObject.GetComponent<TrouEtroit>() != null)
        {
            return nameof(TrouEtroit);
        }

        return gameObject.name;
    }
}
