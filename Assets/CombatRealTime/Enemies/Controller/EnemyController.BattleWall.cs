using Unity.Netcode;
using UnityEngine;

public sealed partial class EnemyController
{
    private const string DefaultBattleWallPrefabPath = "Assets/Prefabs/BattleWall.prefab";

    [SerializeField, Tooltip("Prefab de barrière instancié au point de départ du combat.")]
    private GameObject battleWallPrefab;
    private GameObject activeBattleWall;
    private bool ShouldCreateBattleWall => Configuration.ArenaCreateBattleWall;

    /// <summary>Called by the combat authority once this enemy becomes engaged.</summary>
    public void BeginBattleWall()
    {
        if (!ShouldCreateBattleWall || activeBattleWall != null || !isActiveAndEnabled) return;
        if (Online && !IsServer) return;

        if (battleWallPrefab == null)
        {
            Debug.LogError("[EnemyBattleWall] BattleWall absent sur '" + name + "'. Assigner " + DefaultBattleWallPrefabPath + ".", this);
            return;
        }

        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        if (Online && IsSpawned)
        {
            // The host (or a dedicated server) needs the collision volume
            // immediately; the RPC creates the identical local copy on every
            // connected client. The local guard prevents a host duplicate.
            SpawnBattleWallLocal(position, rotation);
            SpawnBattleWallClientRpc(position, rotation);
        }
        else
        {
            SpawnBattleWallLocal(position, rotation);
        }
    }

    /// <summary>Removes this enemy's arena when its combat ends or it despawns.</summary>
    public void EndBattleWall()
    {
        if (Online && !IsServer) return;
        if (Online && IsSpawned)
        {
            ClearBattleWallLocal();
            DespawnBattleWallClientRpc();
        }
        else ClearBattleWallLocal();
    }

    private void BattleWallOnDisable() => ClearBattleWallLocal();

    [ClientRpc]
    private void SpawnBattleWallClientRpc(Vector3 position, Quaternion rotation)
    {
        SpawnBattleWallLocal(position, rotation);
    }

    [ClientRpc]
    private void DespawnBattleWallClientRpc() => ClearBattleWallLocal();

    private void SpawnBattleWallLocal(Vector3 position, Quaternion rotation)
    {
        if (activeBattleWall != null || battleWallPrefab == null) return;
        activeBattleWall = Instantiate(battleWallPrefab, position, rotation);
        activeBattleWall.name = battleWallPrefab.name + "_" + name;
        BattleWallContainment containment = activeBattleWall.GetComponent<BattleWallContainment>();
        if (containment == null)
        {
            containment = activeBattleWall.AddComponent<BattleWallContainment>();
        }

        containment.Initialize(this, RealTimeCombatManager.Instance != null ? RealTimeCombatManager.Instance.PlayerRoot : null);
    }

    private void ClearBattleWallLocal()
    {
        if (activeBattleWall == null) return;
        Destroy(activeBattleWall);
        activeBattleWall = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (battleWallPrefab != null) return;
        battleWallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBattleWallPrefabPath);
        if (battleWallPrefab != null) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
