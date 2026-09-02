using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Switches camera authority only when the owning runtime rig explicitly
/// requests it. A PlayableDirector may also drive actor-only Timelines, so a
/// plain Play must never alter the gameplay camera by itself.
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
    private bool savedBrainUpdateMode;
    private CinemachineBrain.UpdateMethods previousUpdateMethod;
    private CinemachineBrain.BrainUpdateMethods previousBlendUpdateMethod;

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

        director.stopped += OnStopped;
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.stopped -= OnStopped;
        }

        EndCameraControl();
    }

    private void OnStopped(PlayableDirector playableDirector)
    {
        EndCameraControl();
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

        if (controlsCamera && explicitBrain != null && explicitBrain != brain)
        {
            EndCameraControl();
        }

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
        else
        {
            BeginManualBrainUpdates();
        }
        return controlsCamera;
    }

    /// <summary>Updates the already-bound gameplay Brain deterministically after Timeline evaluation.</summary>
    public bool UpdateTimelineCameraNow()
    {
        if (!controlsCamera || explicitBrain == null || explicitBrain.UpdateMethod != CinemachineBrain.UpdateMethods.ManualUpdate)
        {
            return false;
        }

        explicitBrain.ManualUpdate();
        return true;
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
            RestoreBrainUpdateMode();
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

    private void BeginManualBrainUpdates()
    {
        if (explicitBrain == null || savedBrainUpdateMode)
        {
            return;
        }

        previousUpdateMethod = explicitBrain.UpdateMethod;
        previousBlendUpdateMethod = explicitBrain.BlendUpdateMethod;
        savedBrainUpdateMode = true;
        explicitBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
        explicitBrain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
    }

    private void RestoreBrainUpdateMode()
    {
        if (!savedBrainUpdateMode || explicitBrain == null)
        {
            savedBrainUpdateMode = false;
            return;
        }

        explicitBrain.UpdateMethod = previousUpdateMethod;
        explicitBrain.BlendUpdateMethod = previousBlendUpdateMethod;
        savedBrainUpdateMode = false;
    }
}
