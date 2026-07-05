using System;
using Unity.Collections;
using Unity.Netcode;

// Stack d'items serialisable pour Netcode.
public struct NetItemStack : INetworkSerializable, IEquatable<NetItemStack>
{
    public FixedString64Bytes ItemId;
    public int Quantity;

    public NetItemStack(FixedString64Bytes itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemId);
        serializer.SerializeValue(ref Quantity);
    }

    public bool Equals(NetItemStack other)
    {
        return ItemId.Equals(other.ItemId) && Quantity == other.Quantity;
    }

    public override bool Equals(object obj)
    {
        return obj is NetItemStack other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ItemId.GetHashCode(), Quantity);
    }
}

// Stack de PV defensifs serialisable pour Netcode.
public struct NetCombatDefenseItemHitPointStack : INetworkSerializable, IEquatable<NetCombatDefenseItemHitPointStack>
{
    public FixedString64Bytes ItemId;
    public int HitPoints;
    public int Quantity;

    public NetCombatDefenseItemHitPointStack(FixedString64Bytes itemId, int hitPoints, int quantity)
    {
        ItemId = itemId;
        HitPoints = hitPoints;
        Quantity = quantity;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemId);
        serializer.SerializeValue(ref HitPoints);
        serializer.SerializeValue(ref Quantity);
    }

    public bool Equals(NetCombatDefenseItemHitPointStack other)
    {
        return ItemId.Equals(other.ItemId) && HitPoints == other.HitPoints && Quantity == other.Quantity;
    }

    public override bool Equals(object obj)
    {
        return obj is NetCombatDefenseItemHitPointStack other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ItemId.GetHashCode(), HitPoints, Quantity);
    }
}
