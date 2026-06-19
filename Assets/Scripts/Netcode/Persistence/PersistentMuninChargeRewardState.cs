using System;
using UnityEngine;

// Persiste uniquement la consommation d'une source de recharge unique.
[DisallowMultipleComponent]
public sealed class PersistentMuninChargeRewardState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class RewardStateData
    {
        public bool Consumed;
    }

    [SerializeField] private MuninChargeReward reward;

    public string ProviderId => "munin_charge_reward";

    private void Awake()
    {
        ResolveReward();
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        ResolveReward();
        return reward == null
            ? Array.Empty<byte>()
            : PersistentStateJson.ToBytes(new RewardStateData { Consumed = reward.IsConsumed });
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (phase != PersistentApplyPhase.ApplyGameplayState)
        {
            return;
        }

        ResolveReward();
        if (reward == null ||
            !PersistentStateJson.TryFromBytes(state, ProviderId, reward, context, out RewardStateData data))
        {
            return;
        }

        reward.RestoreConsumedState(data.Consumed);
    }

    private void ResolveReward()
    {
        if (reward == null)
        {
            reward = GetComponent<MuninChargeReward>();
        }
    }
}
