using System;
using Unity.Collections;
using Unity.Netcode;

// Association clientId -> characterId (reseau).
public struct NetPlayerAssignment : INetworkSerializable, IEquatable<NetPlayerAssignment>
{
    public ulong ClientId;
    public FixedString64Bytes CharacterId;

    public NetPlayerAssignment(ulong clientId, FixedString64Bytes characterId)
    {
        ClientId = clientId;
        CharacterId = characterId;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref CharacterId);
    }

    public bool Equals(NetPlayerAssignment other)
    {
        return ClientId == other.ClientId && CharacterId.Equals(other.CharacterId);
    }

    public override bool Equals(object obj)
    {
        return obj is NetPlayerAssignment other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ClientId, CharacterId);
    }
}
