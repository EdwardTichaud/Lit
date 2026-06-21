public interface IPersistentStateProvider
{
    string ProviderId { get; }

    byte[] CaptureState(PersistentStateContext context);

    void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context);
}
