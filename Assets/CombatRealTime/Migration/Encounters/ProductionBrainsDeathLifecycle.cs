using System.Collections;
using Unity.Netcode;
using UnityEngine;

public interface IEnemyDeathPose
{
    bool IsDeathPosePending { get; }
    void EnsureDeathPose();
}

public interface IEnemyFinalDeathHandler
{
    bool TryCompleteDeath();
}

/// <summary>Retains the marker-owned corpse for saves; only the server removes its network spawn.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyDeathPresentation))]
[RequireComponent(typeof(ProductionBrainsFlamePolicy))]
public sealed class ProductionBrainsDeathLifecycle : NetworkBehaviour, IEnemyFinalDeathHandler, ILitBrainsEncounterPolicy
{
    private bool warned;
    public bool AllowsDecisions => GetComponent<ProductionBrainsFlamePolicy>()?.isActiveAndEnabled == true;
    public bool AllowsDamage => AllowsDecisions;
    public bool IsReturning => false;
    public bool IsFlameDormant => false;
    public bool FilterIncomingDamage(int requested, SquadCharacterController source, out int permitted)
    {
        permitted = AllowsDamage ? Mathf.Max(0, requested) : 0;
        return AllowsDamage;
    }

    public bool TryCompleteDeath()
    {
        var health = GetComponent<CharacterInfo>();
        if (health == null || !health.IsDead || GetComponent<EnemyDeathPresentation>()?.HasCompletedPresentation != true) return false;
        var network = NetworkManager.Singleton;
        bool online = network != null && network.IsListening;
        if (online && !network.IsServer) return false;
        var persistent = GetComponent<PersistentNetworkObject>();
        var registry = NetworkObjectRegistry.Instance;
        if (online && (persistent == null || !persistent.HasPersistentId || registry == null ||
            !registry.TryGet(persistent.PersistentId, out var registered) || registered != persistent ||
            !SceneMarker.TryGetRegisteredMarker(persistent.PersistentId, out var marker) || marker.RuntimeInstance != gameObject ||
            GetComponent<PersistentSceneMarkerCharacterState>() == null))
        {
            if (!warned)
            {
                warned = true;
                Debug.LogError("[ProductionEnemyDeath] Presentation finished, but final despawn waits for the retained SceneMarker health/persistence contract.", this);
            }
            return false;
        }
        // Validate the existing save provider before NGO callbacks or disabling can unregister it.
        if (online)
        {
            var snapshot = persistent.CaptureSnapshot(null);
            if (!snapshot.StateBlobs.Exists(blob => blob.ProviderId == "scene_marker_character" && blob.Payload != null && blob.Payload.Length > 0))
                return false;
            if (!NetworkObject.IsSpawned) return false;
            NetworkObject.Despawn(false);
        }
        gameObject.SetActive(false);
        // OnDisable unregisters PersistentNetworkObject. Keep this retained, inactive corpse
        // addressable by the same identity so the next save captures zero HP and Active=false.
        if (persistent != null && persistent.HasPersistentId && registry != null) registry.Register(persistent);
        return true;
    }

    public override void OnNetworkDespawn()
    {
        if (isActiveAndEnabled && NetworkManager.Singleton?.IsServer != true && GetComponent<CharacterInfo>()?.IsDead == true)
            StartCoroutine(DeactivateAfterDespawn());
    }
    private IEnumerator DeactivateAfterDespawn()
    {
        // Let NGO finish every sibling's despawn callback before disabling the shared root.
        yield return null;
        if (GetComponent<CharacterInfo>()?.IsDead == true) gameObject.SetActive(false);
    }
}

public partial class LitBrainsEnemy : IEnemyDeathPose
{
    private bool productionDeathPoseStarted;
    public bool IsDeathPosePending => cinematicSuspended || productionAirborne || chargedAirborne;
    public void EnsureDeathPose()
    {
        if (!IsDead || IsDeathPosePending || productionDeathPoseStarted) return;
        productionMotionActive = false;
        productionMotionSkill = null;
        BeginDeathPresentation();
    }
}
