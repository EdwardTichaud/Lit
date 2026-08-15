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
    private CinemachineBrain explicitBrain;
    private LitCameraDirector explicitCameraDirector;

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
        if (explicitBrain != null)
        {
            BeginCameraControlNow(explicitBrain);
            return;
        }

        BeginCameraControlNow();
    }

    /// <summary>
    /// Binds the Timeline to the gameplay camera and gives Cinemachine authority
    /// before the first Timeline frame is evaluated.
    /// </summary>
    public bool BeginCameraControlNow()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        BindCinemachineTracks();
        if (controlsCamera)
        {
            return true;
        }

        controlsCamera = LitCameraDirector.EnsureInstance()?.BeginTimelineCinemachineControl() == true;
        if (!controlsCamera)
        {
            Debug.LogWarning("[TimelineCamera] Impossible de donner le controle de la camera a la Timeline. " +
                             "Verifiez la Main Camera et son CinemachineBrain.", this);
        }

        return controlsCamera;
    }

    /// <summary>Uses an explicitly resolved gameplay Brain instead of Camera.main.</summary>
    public bool BeginCameraControlNow(CinemachineBrain brain)
    {
        if (!Application.isPlaying || brain == null) return false;

        BindCinemachineTracks(brain);
        explicitBrain = brain;
        Camera gameplayCamera = brain.GetComponent<Camera>();
        explicitCameraDirector = LitCameraDirector.EnsureInstance(gameplayCamera);
        controlsCamera = explicitCameraDirector != null &&
                         explicitCameraDirector.BeginTimelineCinemachineControl();
        if (!controlsCamera)
        {
            explicitBrain = null;
            Debug.LogError("[TimelineCamera] La Main Camera explicite ne possede pas de LitCameraDirector valide.", this);
        }
        return controlsCamera;
    }

    public void EndCameraControlNow()
    {
        EndCameraControl();
    }

    private void EndCameraControl()
    {
        if (!controlsCamera)
        {
            return;
        }

        if (explicitBrain != null)
        {
            if (explicitCameraDirector != null)
            {
                explicitCameraDirector.EndTimelineCinemachineControl();
                explicitCameraDirector = null;
            }
            explicitBrain = null;
        }
        else if (LitCameraDirector.Instance != null)
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
        BindCinemachineTracks(cameraDirector != null ? cameraDirector.CinemachineBrain : null);
    }

    private void BindCinemachineTracks(CinemachineBrain brain)
    {
        if (brain == null || director == null || director.playableAsset == null) return;

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
