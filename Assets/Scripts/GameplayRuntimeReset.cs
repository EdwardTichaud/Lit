using UnityEngine;

// Reset centralise du runtime gameplay pour eviter toute continuite implicite entre parties.
public static class GameplayRuntimeReset
{
    private const string ResetLogPrefix = "[GameplayRuntimeReset]";

    public static void ResetForMenuScene(string reason)
    {
        ResetInternal(reason, destroySceneBackedSingletons: true);
    }

    public static void PrepareForGameplayStart(string reason)
    {
        ResetInternal(reason, destroySceneBackedSingletons: true);
    }

    private static void ResetInternal(string reason, bool destroySceneBackedSingletons)
    {
        string resolvedReason = string.IsNullOrWhiteSpace(reason) ? "runtime_reset" : reason.Trim();
        Debug.Log($"{ResetLogPrefix} start reason='{resolvedReason}'");

        InputFocusStack.Clear();
        LocalPlayerContext.Clear($"{ResetLogPrefix}:{resolvedReason}", LocalPlayerContext.Authority.MultiplayerAssignment);
        NetcodePlayerSessionRegistry.Clear();
        ConfirmationManager.Dismiss(null, false);
        BuildingRuntimeState.Clear();
        Zone.ResetRuntimeState();
        TorchVisionSystem.ResetRuntimeState(resolvedReason);

        if (NetcodePlayerSpawner.Instance != null)
        {
            NetcodePlayerSpawner.Instance.ResetRuntimeState(resolvedReason);
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.ResetRuntimeState(resolvedReason);
        }

        if (NetworkObjectRegistry.Instance != null)
        {
            NetworkObjectRegistry.Instance.ResetRuntimeState(resolvedReason);
        }

        if (JoinSyncSystem.Instance != null)
        {
            JoinSyncSystem.Instance.ResetRuntimeState(resolvedReason);
        }

        WorldSaveAdapter worldSaveAdapter = FindComponent<WorldSaveAdapter>();
        if (worldSaveAdapter != null)
        {
            worldSaveAdapter.ResetSessionState(resolvedReason);
        }

        WorldRulesStateManager worldRulesStateManager = FindComponent<WorldRulesStateManager>();
        if (worldRulesStateManager != null)
        {
            worldRulesStateManager.ResetRuntimeState(resolvedReason);
        }

        if (destroySceneBackedSingletons)
        {
            DestroySceneBackedSingleton(SquadManager.Instance, resolvedReason);
            DestroySceneBackedSingleton(KnowledgeManager.Instance, resolvedReason);
        }

        Debug.Log($"{ResetLogPrefix} completed reason='{resolvedReason}'");
    }

    private static void DestroySceneBackedSingleton(SquadManager manager, string reason)
    {
        if (manager == null)
        {
            return;
        }

        manager.PrepareForRuntimeReset(reason);
        Object.Destroy(manager.gameObject);
    }

    private static void DestroySceneBackedSingleton(KnowledgeManager manager, string reason)
    {
        if (manager == null)
        {
            return;
        }

        manager.PrepareForRuntimeReset(reason);
        Object.Destroy(manager.gameObject);
    }

    private static T FindComponent<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<T>(true);
#endif
    }
}
