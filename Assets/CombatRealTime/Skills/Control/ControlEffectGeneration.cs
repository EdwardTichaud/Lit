using UnityEngine;

public static class ControlEffectGeneration
{
    private static ulong next;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => next = 0;
    public static ulong Next() => ++next;
}
