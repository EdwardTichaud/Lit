using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Autorite reseau des Savoirs de la session. Le catalogue est replique pour les
/// clients tardifs; les annonces ne sont emises que pour une revelation nouvelle.
/// </summary>
[DisallowMultipleComponent]
public sealed class KnowledgeSynchronizationService : NetworkBehaviour
{
    public static KnowledgeSynchronizationService Instance { get; private set; }

    private readonly NetworkList<FixedString128Bytes> revealedKnowledgeIds = new NetworkList<FixedString128Bytes>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        revealedKnowledgeIds.OnListChanged += OnKnowledgeListChanged;
        ApplyReplicatedKnowledge();
        if (IsServer)
        {
            MergePersistedKnowledge();
        }
    }

    public override void OnNetworkDespawn()
    {
        revealedKnowledgeIds.OnListChanged -= OnKnowledgeListChanged;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool RequestReveal(KnowledgeSO knowledge, string revealerName, string origin)
    {
        string id = PersistentGameplayLookup.GetKnowledgeId(knowledge);
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (!IsSpawned)
        {
            return KnowledgeReveal.RequestLocalReveal(knowledge, revealerName, origin);
        }

        if (IsServer)
        {
            return CommitReveal(id, revealerName, origin);
        }

        RequestRevealServerRpc(new FixedString128Bytes(id), new FixedString64Bytes(revealerName ?? string.Empty), new FixedString128Bytes(origin ?? string.Empty));
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRevealServerRpc(FixedString128Bytes knowledgeId, FixedString64Bytes claimedRevealer, FixedString128Bytes origin, ServerRpcParams rpcParams = default)
    {
        // L'identite affichee vient de l'attribution serveur, jamais d'un texte client forge.
        string revealer = ResolveRevealerName(rpcParams.Receive.SenderClientId, claimedRevealer.ToString());
        CommitReveal(knowledgeId.ToString(), revealer, origin.ToString());
    }

    private bool CommitReveal(string knowledgeId, string revealerName, string origin)
    {
        KnowledgeSO knowledge = PersistentGameplayLookup.ResolveKnowledge(knowledgeId);
        if (knowledge == null || ContainsId(knowledgeId))
        {
            return false;
        }

        revealedKnowledgeIds.Add(new FixedString128Bytes(knowledgeId));
        KnowledgeManager.GetOrCreate().ApplyValidatedKnowledge(knowledge);
        KnowledgeRevealedClientRpc(new FixedString128Bytes(knowledgeId), new FixedString64Bytes(revealerName ?? string.Empty), new FixedString128Bytes(origin ?? string.Empty));
        return true;
    }

    [ClientRpc]
    private void KnowledgeRevealedClientRpc(FixedString128Bytes knowledgeId, FixedString64Bytes revealerName, FixedString128Bytes origin)
    {
        KnowledgeSO knowledge = PersistentGameplayLookup.ResolveKnowledge(knowledgeId.ToString());
        if (knowledge == null)
        {
            return;
        }

        KnowledgeManager.GetOrCreate().ApplyValidatedKnowledge(knowledge);
    }

    private void OnKnowledgeListChanged(NetworkListEvent<FixedString128Bytes> change)
    {
        if (change.Type == NetworkListEvent<FixedString128Bytes>.EventType.Add ||
            change.Type == NetworkListEvent<FixedString128Bytes>.EventType.Value)
        {
            KnowledgeSO knowledge = PersistentGameplayLookup.ResolveKnowledge(change.Value.ToString());
            if (knowledge != null)
            {
                KnowledgeManager.GetOrCreate().ApplyValidatedKnowledge(knowledge);
            }
        }
    }

    private void ApplyReplicatedKnowledge()
    {
        for (int i = 0; i < revealedKnowledgeIds.Count; i++)
        {
            KnowledgeSO knowledge = PersistentGameplayLookup.ResolveKnowledge(revealedKnowledgeIds[i].ToString());
            if (knowledge != null)
            {
                KnowledgeManager.GetOrCreate().ApplyValidatedKnowledge(knowledge);
            }
        }
    }

    private void MergePersistedKnowledge()
    {
        IReadOnlyList<KnowledgeSO> unlocked = KnowledgeManager.GetOrCreate().UnlockedKnowledge;
        for (int i = 0; unlocked != null && i < unlocked.Count; i++)
        {
            string id = PersistentGameplayLookup.GetKnowledgeId(unlocked[i]);
            if (!string.IsNullOrWhiteSpace(id) && !ContainsId(id))
            {
                revealedKnowledgeIds.Add(new FixedString128Bytes(id));
            }
        }
    }

    /// <summary>Appelee apres restauration de sauvegarde pour republier le catalogue serveur.</summary>
    public void SynchronizeRestoredKnowledge()
    {
        if (IsSpawned && IsServer)
        {
            MergePersistedKnowledge();
        }
    }

    private bool ContainsId(string id)
    {
        for (int i = 0; i < revealedKnowledgeIds.Count; i++)
        {
            if (string.Equals(revealedKnowledgeIds[i].ToString(), id, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static string ResolveRevealerName(ulong clientId, string fallback)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            foreach (NetworkObject networkObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject == null || networkObject.OwnerClientId != clientId) continue;
                SquadCharacterController controller = networkObject.GetComponent<SquadCharacterController>();
                if (controller != null && controller.CharacterData != null)
                {
                    return controller.CharacterData.ResolveDisplayName();
                }
            }
        }
        return string.IsNullOrWhiteSpace(fallback) ? "L'equipe" : fallback;
    }
}
