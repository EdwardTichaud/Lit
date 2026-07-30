using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Switches camera authority for the lifetime of this PlayableDirector. It
/// deliberately does not select a virtual camera: the Cinemachine Track does.
/// </summary>
[DefaultExecutionOrder(-450)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
public sealed class LitTimelineCinemachineBridge : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;

    private bool controlsCamera;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void Awake()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    private void OnEnable()
    {
        if (director == null)
        {
            return;
        }

        director.played += OnPlayed;
        director.stopped += OnStopped;
        BindCinemachineTracks();
        if (Application.isPlaying && director.state == PlayState.Playing)
        {
            BeginCameraControl();
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnPlayed;
            director.stopped -= OnStopped;
        }

        EndCameraControl();
    }

    private void OnPlayed(PlayableDirector playableDirector)
    {
        BeginCameraControl();
    }

    private void OnStopped(PlayableDirector playableDirector)
    {
        EndCameraControl();
    }

    private void BeginCameraControl()
    {
        if (!Application.isPlaying || controlsCamera)
        {
            return;
        }

        BindCinemachineTracks();
        controlsCamera = LitCameraDirector.EnsureInstance()?.BeginTimelineCinemachineControl() == true;
    }

    private void EndCameraControl()
    {
        if (!controlsCamera)
        {
            return;
        }

        if (LitCameraDirector.Instance != null)
        {
            LitCameraDirector.Instance.EndTimelineCinemachineControl();
        }

        controlsCamera = false;
    }

    private void BindCinemachineTracks()
    {
        if (!Application.isPlaying || director == null || director.playableAsset == null)
        {
            return;
        }

        LitCameraDirector cameraDirector = LitCameraDirector.EnsureInstance();
        CinemachineBrain brain = cameraDirector != null ? cameraDirector.CinemachineBrain : null;
        if (brain == null)
        {
            return;
        }

        bool changed = false;
        foreach (PlayableBinding output in director.playableAsset.outputs)
        {
            if (output.sourceObject is not CinemachineTrack)
            {
                continue;
            }

            if (director.GetGenericBinding(output.sourceObject) == brain)
            {
                continue;
            }

            director.SetGenericBinding(output.sourceObject, brain);
            changed = true;
        }

        if (changed && director.state == PlayState.Playing)
        {
            director.RebuildGraph();
        }
    }
}
