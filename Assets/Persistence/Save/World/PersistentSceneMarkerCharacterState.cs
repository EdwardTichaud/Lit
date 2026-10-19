using System;
using UnityEngine;

/// <summary>
/// Persists the runtime object owned by a SceneMarker while keeping the marker
/// itself alive in the scene, including after an enemy disables its own root.
/// </summary>
[DisallowMultipleComponent]
public sealed class PersistentSceneMarkerCharacterState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class StateData
    {
        public bool HasRuntimeInstance;
        public bool Active;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public int CurrentHp;
        public int MaxHp;
    }

    private SceneMarker marker;
    private StateData pendingState;

    public string ProviderId => "scene_marker_character";

    private void Awake()
    {
        // The provider can live on the baked actor when its parent marker must
        // not receive a second, nested NetworkObject.
        ResolveMarker();
    }

    private void Update()
    {
        ResolveMarker();
        if (pendingState != null && marker != null && marker.RuntimeInstance != null)
        {
            ApplyToMarker(pendingState);
            pendingState = null;
        }
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        ResolveMarker();
        GameObject instance = marker != null ? marker.RuntimeInstance : null;
        if (instance == null)
        {
            return PersistentStateJson.ToBytes(new StateData { HasRuntimeInstance = false });
        }

        CharacterInfo health = instance.GetComponent<CharacterInfo>();
        return PersistentStateJson.ToBytes(new StateData
        {
            HasRuntimeInstance = true,
            Active = instance.activeSelf,
            Position = instance.transform.position,
            Rotation = instance.transform.rotation,
            Scale = instance.transform.localScale,
            CurrentHp = health != null ? health.CurrentHp : 0,
            MaxHp = health != null ? health.MaxHp : 0
        });
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        ResolveMarker();
        if (phase != PersistentApplyPhase.ApplyGameplayState || marker == null ||
            !PersistentStateJson.TryFromBytes(state, ProviderId, marker, context, out StateData data))
        {
            return;
        }

        if (marker.RuntimeInstance == null)
        {
            pendingState = data;
            return;
        }

        ApplyToMarker(data);
    }

    private void ResolveMarker()
    {
        if (marker == null) marker = GetComponent<SceneMarker>() ?? GetComponentInParent<SceneMarker>(true);
    }

    private void ApplyToMarker(StateData data)
    {
        if (marker == null || data == null)
        {
            return;
        }

        if (!data.HasRuntimeInstance)
        {
            if (marker.RuntimeInstance != null)
            {
                marker.RuntimeInstance.SetActive(false);
            }
            return;
        }

        marker.ApplyPersistedState(
            data.Position,
            data.Rotation,
            data.Scale,
            data.CurrentHp,
            data.MaxHp,
            data.Active);
    }
}
