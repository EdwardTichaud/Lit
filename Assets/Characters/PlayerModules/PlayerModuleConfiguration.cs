using System;
using UnityEngine;

// One private copy per module and actor. No component dependency is required for defaults.
public sealed class PlayerModuleConfiguration<T> where T : class, new()
{
    private CharacterData source;
    private T settings;
    private SquadCharacterController squad;
    private CharacterInfo info;
    private Component resolvedOwner;
    public T Resolve(Component owner, Func<PlayerSettings, T> select)
    {
        if (resolvedOwner != owner)
        {
            squad = owner.GetComponentInParent<SquadCharacterController>();
            info = owner.GetComponentInParent<CharacterInfo>();
            resolvedOwner = owner;
            settings = null;
        }
        CharacterData current = squad != null ? squad.CharacterData : info != null ? info.CharacterData : null;
        if (settings == null || current != source)
        {
            source = current;
            T authored = current != null && current.playerSettings != null ? select(current.playerSettings) : null;
            settings = authored != null ? JsonUtility.FromJson<T>(JsonUtility.ToJson(authored)) : new T();
        }
        return settings;
    }
}
