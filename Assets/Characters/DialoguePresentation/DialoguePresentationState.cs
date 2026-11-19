using Unity.Collections;
using Unity.Netcode;

/// <summary>Owner-selected presentation. Clients never choose a new gesture for a remote actor.</summary>
public struct DialoguePresentationState : INetworkSerializable, System.IEquatable<DialoguePresentationState>
{
    public bool active, stationary, seated, suspended;
    public int generation, gesture, revision;
    public double startedAt;
    public float intensity;
    public DialogueIntent intent;
    public ulong listenerId;
    public FixedString64Bytes listenerActorId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref active);
        serializer.SerializeValue(ref suspended);
        serializer.SerializeValue(ref stationary);
        serializer.SerializeValue(ref seated);
        serializer.SerializeValue(ref generation);
        serializer.SerializeValue(ref revision);
        serializer.SerializeValue(ref startedAt);
        serializer.SerializeValue(ref gesture);
        serializer.SerializeValue(ref intensity);
        serializer.SerializeValue(ref intent);
        serializer.SerializeValue(ref listenerId);
        serializer.SerializeValue(ref listenerActorId);
    }
    public bool Equals(DialoguePresentationState other) =>
        active == other.active && suspended == other.suspended && stationary == other.stationary && seated == other.seated &&
        generation == other.generation && revision == other.revision && startedAt.Equals(other.startedAt) && gesture == other.gesture && intensity.Equals(other.intensity) && intent == other.intent &&
        listenerId == other.listenerId && listenerActorId.Equals(other.listenerActorId);
}
