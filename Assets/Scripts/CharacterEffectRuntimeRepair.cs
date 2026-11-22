using INab.VFXAssets;

/// <summary>Compatibility for existing gameplay callers. The effect owns its lifecycle.</summary>
public static class CharacterEffectRuntimeRepair
{
    public static void EnsureReady(CharacterEffect effect)
    {
        if (effect != null) effect.EnsureReady();
    }
}
