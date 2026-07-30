using Opsive.UltimateCharacterController.Camera;
using UnityEngine;
using UnityEngine.Playables;

namespace Lit.Timeline
{
    /// <summary>
    /// Suspend le controle UCC uniquement lorsqu'une Timeline lie l'Animator du CameraSystem.
    /// Il ne modifie pas le comportement normal de la camera hors Timeline.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimelineCameraOverride : MonoBehaviour, ITimelinePlaybackParticipant
    {
        [SerializeField] private Animator cameraAnimator;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private CameraControllerHandler cameraControllerHandler;
        [SerializeField] private LitUccCameraCharacterBinder characterBinder;

        private int playbackDepth;
        private bool restoreControllerOnFinish;
        private bool restoreControllerHandlerOnFinish;

        public void OnTimelinePlaybackStarted(PlayableDirector director)
        {
            if (playbackDepth++ > 0)
            {
                return;
            }

            ResolveReferences();
            restoreControllerOnFinish = cameraController != null && cameraController.enabled;
            restoreControllerHandlerOnFinish = cameraControllerHandler != null && cameraControllerHandler.enabled;
            characterBinder?.BeginTimelineControl();
            if (characterBinder == null && cameraController != null)
            {
                cameraController.enabled = false;
            }

            // Le handler UCC peut continuer a alimenter le controleur avec les entrees
            // de camera. Il doit etre suspendu avec le controleur afin que les courbes
            // de la Timeline gardent la main sur Main Camera pendant toute la lecture.
            if (cameraControllerHandler != null)
            {
                cameraControllerHandler.enabled = false;
            }
        }

        public void OnTimelinePlaybackFinished(PlayableDirector director)
        {
            if (playbackDepth == 0 || --playbackDepth > 0)
            {
                return;
            }

            if (characterBinder != null)
            {
                characterBinder.EndTimelineControl(restoreControllerOnFinish);
            }
            else if (cameraController != null)
            {
                cameraController.enabled = restoreControllerOnFinish;
            }

            if (cameraControllerHandler != null)
            {
                cameraControllerHandler.enabled = restoreControllerHandlerOnFinish;
            }
        }

        private void Reset() => ResolveReferences();

        private void OnValidate() => ResolveReferences();

        private void ResolveReferences()
        {
            if (cameraAnimator == null)
            {
                cameraAnimator = GetComponent<Animator>();
            }

            if (cameraController == null)
            {
                cameraController = GetComponentInChildren<CameraController>(true);
            }

            if (cameraControllerHandler == null)
            {
                cameraControllerHandler = GetComponentInChildren<CameraControllerHandler>(true);
            }

            if (characterBinder == null)
            {
                characterBinder = GetComponentInChildren<LitUccCameraCharacterBinder>(true);
            }
        }
    }
}
