using System;

namespace Lit.Combat.Migration.Network
{
    /// <summary>Deterministic wire ID from an authored Unity asset GUID, never an asset name or list index.</summary>
    public static class CombatSkillNetworkId
    {
        public static bool TryGet(string assetGuid, out int id)
        {
            id = 0;
            if (!Guid.TryParseExact(assetGuid, "N", out var guid) || guid == Guid.Empty) return false;
            string canonical = guid.ToString("N");
            uint hash = 2166136261;
            unchecked { foreach (char value in canonical) hash = (hash ^ value) * 16777619; }
            id = (int)(hash & 0x7fffffff);
            return id != 0;
        }
    }
}
