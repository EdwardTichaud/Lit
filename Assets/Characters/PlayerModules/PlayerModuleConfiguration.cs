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
    private bool resolvedInPlayMode;
    public T Resolve(Component owner, Func<PlayerSettings, T> select)
    {
        if (resolvedOwner != owner || resolvedInPlayMode != Application.isPlaying || (squad == null && info == null))
        {
            // OnValidate can run before the other character components are available.
            // Retry missing dependencies and discard editor-time copies on entering Play.
            squad = owner.GetComponentInParent<SquadCharacterController>(true);
            info = owner.GetComponentInParent<CharacterInfo>(true);
            resolvedOwner = owner;
            resolvedInPlayMode = Application.isPlaying;
            settings = null;
        }
        // The squad component may exist before the creation system binds its data.
        // In that interval the prefab's CharacterInfo is already authoritative.
        CharacterData current = squad != null ? squad.CharacterData : null;
        if (current == null && info != null) current = info.CharacterData;
        if (settings == null || current != source)
        {
            source = current;
            T authored = current != null && current.playerSettings != null ? select(current.playerSettings) : null;
            settings = authored != null ? JsonUtility.FromJson<T>(JsonUtility.ToJson(authored)) : new T();
        }
        return settings;
    }
}
