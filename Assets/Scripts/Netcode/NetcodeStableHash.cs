// Hash stable simple (FNV-1a) pour identifier des objets Netcode sans GUID editor.
public static class NetcodeStableHash
{
    public static uint Hash32(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0u;
        }

        unchecked
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= fnvPrime;
            }

            return hash == 0u ? 1u : hash;
        }
    }
}
