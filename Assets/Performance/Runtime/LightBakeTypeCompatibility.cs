using UnityEngine;

namespace Lit.Performance
{
    /// <summary>
    /// Preserves the Editor bake-mode classification while using the baked output
    /// that Unity exposes to standalone Players.
    /// </summary>
    public static class LightBakeTypeCompatibility
    {
        public static LightmapBakeType GetBakeType(Light target)
        {
#if UNITY_EDITOR
            return target.lightmapBakeType;
#else
            return target.bakingOutput.lightmapBakeType;
#endif
        }

        public static bool IsBaked(LightmapBakeType bakeType)
        {
            return bakeType == LightmapBakeType.Baked;
        }

        public static bool IsBaked(Light target)
        {
            return IsBaked(GetBakeType(target));
        }
    }
}
