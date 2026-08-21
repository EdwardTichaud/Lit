using System;
using UnityEngine;

/// <summary>
/// Persists the runtime object owned by a SceneMarker while keeping the marker
/// itself alive in the scene, including after an enemy disables its own root.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SceneMarker))]
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
        marker = GetComponent<SceneMarker>();
    }

    private void Update()
    {
        if (pendingState != null && marker != null && marker.RuntimeInstance != null)
        {
            ApplyToMarker(pendingState);
            pendingState = null;
        }
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        GameObject instance = marker != null ? marker.RuntimeInstance : null;
        if (instance == null)
        {
            return PersistentStateJson.ToBytes(new StateData { HasRuntimeInstance = false });
        }

        CombatHealth health = instance.GetComponent<CombatHealth>();
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
