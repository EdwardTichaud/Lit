using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class SnapshotSerializer
{
    public sealed class SnapshotChunk
    {
        public int Index;
        public int TotalChunks;
        public byte[] Payload;
    }

    public byte[] Serialize(WorldSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return Array.Empty<byte>();
        }

        int estimatedCapacity = Mathf.Max(
            4096,
            ((snapshot.SceneObjects != null ? snapshot.SceneObjects.Count : 0) +
             (snapshot.RuntimeObjects != null ? snapshot.RuntimeObjects.Count : 0)) * 512);

        FastBufferWriter writer = new FastBufferWriter(estimatedCapacity, Allocator.Temp, int.MaxValue);
        try
        {
            writer.WriteValueSafe(snapshot.SchemaVersion);
            writer.WriteValueSafe(snapshot.SceneName ?? string.Empty);
            writer.WriteValueSafe(snapshot.CapturedAtTime);

            WritePlayers(ref writer, snapshot.Players);
            WriteObjects(ref writer, snapshot.SceneObjects);
            WriteObjects(ref writer, snapshot.RuntimeObjects);
            WriteWorldVariables(ref writer, snapshot.WorldVariables);

            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    public WorldSnapshot Deserialize(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return new WorldSnapshot();
        }

        FastBufferReader reader = new FastBufferReader(bytes, Allocator.Temp);
        try
        {
            WorldSnapshot snapshot = new WorldSnapshot();

            reader.ReadValueSafe(out snapshot.SchemaVersion);
            reader.ReadValueSafe(out snapshot.SceneName);
            reader.ReadValueSafe(out snapshot.CapturedAtTime);

            snapshot.Players = ReadPlayers(ref reader);
            snapshot.SceneObjects = ReadObjects(ref reader);
            snapshot.RuntimeObjects = ReadObjects(ref reader);
            snapshot.WorldVariables = ReadWorldVariables(ref reader);

            return snapshot;
        }
        finally
        {
            reader.Dispose();
        }
    }

    public List<SnapshotChunk> ChunkPayload(byte[] payload, int maxChunkPayloadBytes)
    {
        List<SnapshotChunk> chunks = new List<SnapshotChunk>();
        if (payload == null || payload.Length == 0)
        {
            chunks.Add(new SnapshotChunk
            {
                Index = 0,
                TotalChunks = 1,
                Payload = Array.Empty<byte>()
            });
            return chunks;
        }

        int clampedChunkSize = Mathf.Max(1024, maxChunkPayloadBytes);
        int totalChunks = Mathf.CeilToInt(payload.Length / (float)clampedChunkSize);

        for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            int offset = chunkIndex * clampedChunkSize;
            int length = Mathf.Min(clampedChunkSize, payload.Length - offset);
            byte[] chunkPayload = new byte[length];
            Buffer.BlockCopy(payload, offset, chunkPayload, 0, length);

            chunks.Add(new SnapshotChunk
            {
                Index = chunkIndex,
                TotalChunks = totalChunks,
                Payload = chunkPayload
            });
        }

        return chunks;
    }

    private static void WritePlayers(ref FastBufferWriter writer, List<PlayerSnapshot> players)
    {
        int count = players != null ? players.Count : 0;
        writer.WriteValueSafe(count);
        for (int i = 0; i < count; i++)
        {
            PlayerSnapshot snapshot = players[i] ?? new PlayerSnapshot();
            writer.WriteValueSafe(snapshot.OwnerClientId);
            writer.WriteValueSafe(snapshot.PlayerId ?? string.Empty);
            writer.WriteValueSafe(snapshot.CharacterId ?? string.Empty);
            writer.WriteValueSafe(snapshot.ControlledObjectId ?? string.Empty);
            WriteVector3(ref writer, snapshot.Position);
            WriteQuaternion(ref writer, snapshot.Rotation);
            WriteBytes(ref writer, snapshot.CustomState);
        }
    }

    private static List<PlayerSnapshot> ReadPlayers(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        List<PlayerSnapshot> players = new List<PlayerSnapshot>(Mathf.Max(0, count));

        for (int i = 0; i < count; i++)
        {
            PlayerSnapshot snapshot = new PlayerSnapshot();
            reader.ReadValueSafe(out snapshot.OwnerClientId);
            reader.ReadValueSafe(out snapshot.PlayerId);
            reader.ReadValueSafe(out snapshot.CharacterId);
            reader.ReadValueSafe(out snapshot.ControlledObjectId);
            snapshot.Position = ReadVector3(ref reader);
            snapshot.Rotation = ReadQuaternion(ref reader);
            snapshot.CustomState = ReadBytes(ref reader);
            players.Add(snapshot);
        }

        return players;
    }

    private static void WriteObjects(ref FastBufferWriter writer, List<PersistentObjectSnapshot> objects)
    {
        int count = objects != null ? objects.Count : 0;
        writer.WriteValueSafe(count);

        for (int i = 0; i < count; i++)
        {
            PersistentObjectSnapshot snapshot = objects[i] ?? new PersistentObjectSnapshot();
            writer.WriteValueSafe(snapshot.PersistentId ?? string.Empty);
            writer.WriteValueSafe((byte)snapshot.ObjectKind);
            writer.WriteValueSafe(snapshot.RuntimePrefabId ?? string.Empty);
            writer.WriteValueSafe(snapshot.SceneName ?? string.Empty);
            writer.WriteValueSafe(snapshot.DestroyIfMissing);
            WriteTransform(ref writer, snapshot.Transform);

            int stateBlobCount = snapshot.StateBlobs != null ? snapshot.StateBlobs.Count : 0;
            writer.WriteValueSafe(stateBlobCount);
            for (int blobIndex = 0; blobIndex < stateBlobCount; blobIndex++)
            {
                StateBlobSnapshot blob = snapshot.StateBlobs[blobIndex] ?? new StateBlobSnapshot();
                writer.WriteValueSafe(blob.ProviderId ?? string.Empty);
                WriteBytes(ref writer, blob.Payload);
            }
        }
    }

    private static List<PersistentObjectSnapshot> ReadObjects(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        List<PersistentObjectSnapshot> snapshots = new List<PersistentObjectSnapshot>(Mathf.Max(0, count));

        for (int i = 0; i < count; i++)
        {
            PersistentObjectSnapshot snapshot = new PersistentObjectSnapshot();
            reader.ReadValueSafe(out snapshot.PersistentId);

            reader.ReadValueSafe(out byte objectKind);
            snapshot.ObjectKind = (PersistentObjectKind)objectKind;

            reader.ReadValueSafe(out snapshot.RuntimePrefabId);
            reader.ReadValueSafe(out snapshot.SceneName);
            reader.ReadValueSafe(out snapshot.DestroyIfMissing);
            snapshot.Transform = ReadTransform(ref reader);

            reader.ReadValueSafe(out int stateBlobCount);
            snapshot.StateBlobs = new List<StateBlobSnapshot>(Mathf.Max(0, stateBlobCount));
            for (int blobIndex = 0; blobIndex < stateBlobCount; blobIndex++)
            {
                StateBlobSnapshot blob = new StateBlobSnapshot();
                reader.ReadValueSafe(out blob.ProviderId);
                blob.Payload = ReadBytes(ref reader);
                snapshot.StateBlobs.Add(blob);
            }

            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    private static void WriteWorldVariables(ref FastBufferWriter writer, List<WorldVariableSnapshot> variables)
    {
        int count = variables != null ? variables.Count : 0;
        writer.WriteValueSafe(count);

        for (int i = 0; i < count; i++)
        {
            WorldVariableSnapshot variable = variables[i] ?? new WorldVariableSnapshot();
            writer.WriteValueSafe(variable.Key ?? string.Empty);
            writer.WriteValueSafe((byte)variable.ValueType);

            switch (variable.ValueType)
            {
                case WorldVariableValueType.Int:
                    writer.WriteValueSafe(variable.IntValue);
                    break;
                case WorldVariableValueType.Float:
                    writer.WriteValueSafe(variable.FloatValue);
                    break;
                case WorldVariableValueType.Bool:
                    writer.WriteValueSafe(variable.BoolValue);
                    break;
                default:
                    writer.WriteValueSafe(variable.StringValue ?? string.Empty);
                    break;
            }
        }
    }

    private static List<WorldVariableSnapshot> ReadWorldVariables(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        List<WorldVariableSnapshot> variables = new List<WorldVariableSnapshot>(Mathf.Max(0, count));

        for (int i = 0; i < count; i++)
        {
            WorldVariableSnapshot variable = new WorldVariableSnapshot();
            reader.ReadValueSafe(out variable.Key);

            reader.ReadValueSafe(out byte valueType);
            variable.ValueType = (WorldVariableValueType)valueType;

            switch (variable.ValueType)
            {
                case WorldVariableValueType.Int:
                    reader.ReadValueSafe(out variable.IntValue);
                    break;
                case WorldVariableValueType.Float:
                    reader.ReadValueSafe(out variable.FloatValue);
                    break;
                case WorldVariableValueType.Bool:
                    reader.ReadValueSafe(out variable.BoolValue);
                    break;
                default:
                    reader.ReadValueSafe(out variable.StringValue);
                    break;
            }

            variables.Add(variable);
        }

        return variables;
    }

    private static void WriteTransform(ref FastBufferWriter writer, TransformStateSnapshot snapshot)
    {
        WriteVector3(ref writer, snapshot.Position);
        WriteQuaternion(ref writer, snapshot.Rotation);
        WriteVector3(ref writer, snapshot.Scale);
        writer.WriteValueSafe(snapshot.ActiveSelf);
    }

    private static TransformStateSnapshot ReadTransform(ref FastBufferReader reader)
    {
        TransformStateSnapshot snapshot = new TransformStateSnapshot
        {
            Position = ReadVector3(ref reader),
            Rotation = ReadQuaternion(ref reader),
            Scale = ReadVector3(ref reader)
        };
        reader.ReadValueSafe(out snapshot.ActiveSelf);
        return snapshot;
    }

    private static void WriteVector3(ref FastBufferWriter writer, Vector3 value)
    {
        writer.WriteValueSafe(value.x);
        writer.WriteValueSafe(value.y);
        writer.WriteValueSafe(value.z);
    }

    private static Vector3 ReadVector3(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out float x);
        reader.ReadValueSafe(out float y);
        reader.ReadValueSafe(out float z);
        return new Vector3(x, y, z);
    }

    private static void WriteQuaternion(ref FastBufferWriter writer, Quaternion value)
    {
        writer.WriteValueSafe(value.x);
        writer.WriteValueSafe(value.y);
        writer.WriteValueSafe(value.z);
        writer.WriteValueSafe(value.w);
    }

    private static Quaternion ReadQuaternion(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out float x);
        reader.ReadValueSafe(out float y);
        reader.ReadValueSafe(out float z);
        reader.ReadValueSafe(out float w);
        return new Quaternion(x, y, z, w);
    }

    private static void WriteBytes(ref FastBufferWriter writer, byte[] payload)
    {
        int length = payload != null ? payload.Length : 0;
        writer.WriteValueSafe(length);
        if (length <= 0)
        {
            return;
        }

        writer.WriteBytesSafe(payload, length);
    }

    private static byte[] ReadBytes(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out int length);
        if (length <= 0)
        {
            return Array.Empty<byte>();
        }

        byte[] payload = new byte[length];
        reader.ReadBytesSafe(ref payload, length);
        return payload;
    }
}
