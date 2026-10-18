using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public struct CombatNetworkHealthSnapshot : INetworkSerializable, IEquatable<CombatNetworkHealthSnapshot>
    {
        public ulong Revision;
        public int Current, Maximum;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref Revision); serializer.SerializeValue(ref Current); serializer.SerializeValue(ref Maximum); }
        public bool Equals(CombatNetworkHealthSnapshot other) => Revision == other.Revision && Current == other.Current && Maximum == other.Maximum;
    }

    public struct CombatNetworkActionSnapshot : INetworkSerializable, IEquatable<CombatNetworkActionSnapshot>
    {
        public ulong Epoch, Generation;
        public int SkillId;
        public FixedString64Bytes Guid;
        public bool Active;
        public bool HasTarget;
        public NetworkObjectReference Target;
        public Vector3 Direction;
        public double StartedAt;
        public float CooldownRemaining, CooldownTimeScale;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Epoch); serializer.SerializeValue(ref Generation);
            serializer.SerializeValue(ref SkillId); serializer.SerializeValue(ref Guid);
            serializer.SerializeValue(ref Active); serializer.SerializeValue(ref Direction); serializer.SerializeValue(ref StartedAt);
            serializer.SerializeValue(ref HasTarget); serializer.SerializeValue(ref Target);
            serializer.SerializeValue(ref CooldownRemaining); serializer.SerializeValue(ref CooldownTimeScale);
        }
        public bool Equals(CombatNetworkActionSnapshot other) => Epoch == other.Epoch && Generation == other.Generation &&
            SkillId == other.SkillId && Guid.Equals(other.Guid) && Active == other.Active && Direction == other.Direction && StartedAt == other.StartedAt &&
            CooldownRemaining == other.CooldownRemaining && CooldownTimeScale == other.CooldownTimeScale && HasTarget == other.HasTarget && Target.Equals(other.Target);
    }

    public struct CombatNetworkCooldownSnapshot : INetworkSerializable, IEquatable<CombatNetworkCooldownSnapshot>
    {
        public int SkillId;
        public float Remaining, TimeScale;
        public double SampledAt;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SkillId); serializer.SerializeValue(ref Remaining);
            serializer.SerializeValue(ref TimeScale); serializer.SerializeValue(ref SampledAt);
        }
        public bool Equals(CombatNetworkCooldownSnapshot other) => SkillId == other.SkillId && Remaining == other.Remaining && TimeScale == other.TimeScale && SampledAt == other.SampledAt;
    }

    public struct CombatNetworkProtectionSnapshot : INetworkSerializable, IEquatable<CombatNetworkProtectionSnapshot>
    {
        public int AuraId, LinkId;
        public NetworkObjectReference Ally;
        public double LinkUntil;
        public float Reserve;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref AuraId); serializer.SerializeValue(ref LinkId);
            serializer.SerializeValue(ref Ally); serializer.SerializeValue(ref LinkUntil); serializer.SerializeValue(ref Reserve);
        }
        public bool Equals(CombatNetworkProtectionSnapshot other) => AuraId == other.AuraId && LinkId == other.LinkId &&
            Ally.Equals(other.Ally) && LinkUntil == other.LinkUntil && Reserve == other.Reserve;
    }

    public struct CombatNetworkPassiveSnapshot : INetworkSerializable, IEquatable<CombatNetworkPassiveSnapshot>
    {
        public int SkillId;
        public FixedString64Bytes Guid;
        public NetworkObjectReference Target;
        public bool Drain;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SkillId); serializer.SerializeValue(ref Guid);
            serializer.SerializeValue(ref Target); serializer.SerializeValue(ref Drain);
        }
        public bool Equals(CombatNetworkPassiveSnapshot other) => SkillId == other.SkillId && Guid.Equals(other.Guid) && Target.Equals(other.Target) && Drain == other.Drain;
    }

    public struct CombatNetworkControlSnapshot : INetworkSerializable, IEquatable<CombatNetworkControlSnapshot>
    {
        public int SkillId;
        public FixedString64Bytes Guid;
        public ulong Generation;
        public NetworkObjectReference Target;
        public Vector3 Position;
        public double Until;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SkillId); serializer.SerializeValue(ref Guid); serializer.SerializeValue(ref Generation);
            serializer.SerializeValue(ref Target); serializer.SerializeValue(ref Position); serializer.SerializeValue(ref Until);
        }
        public bool Equals(CombatNetworkControlSnapshot other) => SkillId == other.SkillId && Guid.Equals(other.Guid) &&
            Generation == other.Generation && Target.Equals(other.Target) && Position == other.Position && Until == other.Until;
    }

    public struct CombatNetworkThreatSnapshot : INetworkSerializable, IEquatable<CombatNetworkThreatSnapshot>
    {
        public ulong Revision;
        public NetworkObjectReference[] Threats;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Revision);
            int count = Threats != null ? Threats.Length : 0;
            serializer.SerializeValue(ref count);
            if (count < 0 || count > 64) throw new InvalidOperationException("Invalid combat threat snapshot size.");
            if (serializer.IsReader) Threats = new NetworkObjectReference[count];
            for (int index = 0; index < count; index++) serializer.SerializeValue(ref Threats[index]);
        }
        public bool Equals(CombatNetworkThreatSnapshot other)
        {
            if (Revision != other.Revision || (Threats?.Length ?? 0) != (other.Threats?.Length ?? 0)) return false;
            for (int index = 0; index < (Threats?.Length ?? 0); index++) if (!Threats[index].Equals(other.Threats[index])) return false;
            return true;
        }
    }
}
