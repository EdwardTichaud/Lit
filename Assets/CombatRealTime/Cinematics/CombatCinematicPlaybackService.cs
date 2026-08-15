using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class CombatCinematicPlaybackService : MonoBehaviour
{
    [SerializeField] private Transform poolRoot;
    [SerializeField, Tooltip("Journalise l'acquisition et la restitution des rigs de LightSkill.")]
    private bool logCinematicPlaybackDiagnostics = true;

    private readonly Dictionary<CombatCinematicRig, Stack<CombatCinematicRig>> available = new Dictionary<CombatCinematicRig, Stack<CombatCinematicRig>>();
    private readonly Dictionary<CombatCinematicRig, CombatCinematicRig> origins = new Dictionary<CombatCinematicRig, CombatCinematicRig>();
    private CombatCinematicRig activeRig;
    private Action<CombatCinematicRig> completed;

    public bool IsPlaying => activeRig != null;
    public CombatCinematicRig ActiveRig => activeRig;

    private void Awake()
    {
        if (poolRoot == null)
        {
            poolRoot = new GameObject("CombatCinematicPool").transform;
            poolRoot.SetParent(transform, false);
        }
    }

    private void OnDisable()
    {
        StopActive();
    }

    public bool TryPlay(
        CombatCinematicRig prefab,
        CombatCinematicContext context,
        string playerAnimatorTrack,
        string enemyAnimatorTrack,
        Action<CombatCinematicRig> onCompleted,
        out string error)
    {
        return TryPlay(prefab, context, null, playerAnimatorTrack, enemyAnimatorTrack, onCompleted, out error);
    }

    public bool TryPlay(
        CombatCinematicRig prefab,
        CombatCinematicContext context,
        PlayableAsset timeline,
        string playerAnimatorTrack,
        string enemyAnimatorTrack,
        CombatCinematicPlacement? placement,
        Action<CombatCinematicRig> onCompleted,
        out string error)
    {
        error = null;
        if (activeRig != null)
        {
            error = "Une cinematique de combat est deja active.";
            return false;
        }
        if (prefab == null)
        {
            error = "Prefab CombatCinematicRig manquant.";
            return false;
        }

        CombatCinematicRig rig = Acquire(prefab);
        Trace("Acquire | prefab='" + prefab.name + "' | runtime='" + rig.name + "' | timeline='" +
              (timeline != null ? timeline.name : "Baked") + "' | placement=" + placement.HasValue + ".");
        rig.gameObject.SetActive(true);
        activeRig = rig;
        completed = onCompleted;
        rig.Stopped += OnRigStopped;
        if (!rig.TryPlay(context, timeline, playerAnimatorTrack, enemyAnimatorTrack, placement, out error))
        {
            rig.Stopped -= OnRigStopped;
            activeRig = null;
            completed = null;
            Release(prefab, rig);
            return false;
        }
        Trace("Play accepte | runtime='" + rig.name + "'.");
        return true;
    }

    public bool TryPlay(
        CombatCinematicRig prefab,
        CombatCinematicContext context,
        PlayableAsset timeline,
        string playerAnimatorTrack,
        string enemyAnimatorTrack,
        Action<CombatCinematicRig> onCompleted,
        out string error)
    {
        return TryPlay(prefab, context, timeline, playerAnimatorTrack, enemyAnimatorTrack, null, onCompleted, out error);
    }

    public void StopActive()
    {
        activeRig?.Stop();
    }

    private CombatCinematicRig Acquire(CombatCinematicRig prefab)
    {
        if (!available.TryGetValue(prefab, out Stack<CombatCinematicRig> pool))
        {
            pool = new Stack<CombatCinematicRig>();
            available.Add(prefab, pool);
        }
        CombatCinematicRig rig = pool.Count > 0 ? pool.Pop() : Instantiate(prefab, poolRoot);
        origins[rig] = prefab;
        rig.transform.SetParent(null, false);
        rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        rig.name = prefab.name + " (Runtime)";
        return rig;
    }

    private void OnRigStopped(CombatCinematicRig rig)
    {
        if (rig == null || rig != activeRig) return;
        rig.Stopped -= OnRigStopped;
        origins.TryGetValue(rig, out CombatCinematicRig prefab);
        activeRig = null;
        Action<CombatCinematicRig> callback = completed;
        completed = null;
        callback?.Invoke(rig);
        if (prefab != null) Release(prefab, rig);
    }

    private void Release(CombatCinematicRig prefab, CombatCinematicRig rig)
    {
        Trace("Release | runtime='" + rig.name + "' | prefab='" + (prefab != null ? prefab.name : "None") + "'.");
        rig.ResetForPool();
        rig.transform.SetParent(poolRoot, false);
        rig.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        if (!available.TryGetValue(prefab, out Stack<CombatCinematicRig> pool))
        {
            pool = new Stack<CombatCinematicRig>();
            available.Add(prefab, pool);
        }
        pool.Push(rig);
    }

    private void Trace(string message)
    {
        if (logCinematicPlaybackDiagnostics)
        {
            Debug.Log("[LightSkill Debug] Playback " + message, this);
        }
    }

}
