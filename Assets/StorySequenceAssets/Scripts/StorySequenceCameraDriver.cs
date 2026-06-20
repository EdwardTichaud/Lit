using UnityEngine;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;
using UccCameraControllerHandler = Opsive.UltimateCharacterController.Camera.CameraControllerHandler;

namespace Lit.Story
{
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class StorySequenceCameraDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private UccCameraController uccCameraController;
        [SerializeField] private UccCameraControllerHandler uccCameraHandler;
        [SerializeField] private LitUccCameraCharacterBinder uccCameraBinder;
        [SerializeField, Tooltip("Autres pilotes camera a desactiver pendant une sequence.")]
        private Behaviour[] additionalGameplayCameraBehaviours;

        [Header("Fallback Automatic Shot")]
        [SerializeField] private Vector3 fallbackLocalOffset = new Vector3(0.85f, 1.65f, 2.4f);
        [SerializeField, Range(15f, 100f)] private float fallbackFieldOfView = 45f;
        [SerializeField, Min(0f)] private float fallbackFollowSharpness = 12f;

        private StorySequenceActor activeActor;
        private StorySequenceActor activeListener;
        private StorySequenceCameraProfile activeProfile;
        private StorySequenceCameraPoint activePoint;

        private Vector3 transitionStartPosition;
        private Quaternion transitionStartRotation;
        private float transitionStartFov;
        private float transitionDuration;
        private float transitionElapsed;
        private bool transitioning;
        private bool cinematicActive;

        private bool previousUccControllerEnabled;
        private bool previousUccHandlerEnabled;
        private bool previousUccBinderEnabled;
        private bool[] previousAdditionalEnabled;

        public bool IsCinematicActive => cinematicActive;
        public float RemainingTransitionTime => transitioning
            ? Mathf.Max(0f, transitionDuration - transitionElapsed)
            : 0f;

        public bool BeginCinematic()
        {
            ResolveReferences();
            if (controlledCamera == null)
            {
                return false;
            }

            if (cinematicActive)
            {
                return true;
            }

            previousUccControllerEnabled = uccCameraController != null && uccCameraController.enabled;
            previousUccHandlerEnabled = uccCameraHandler != null && uccCameraHandler.enabled;
            previousUccBinderEnabled = uccCameraBinder != null && uccCameraBinder.enabled;

            if (additionalGameplayCameraBehaviours != null)
            {
                previousAdditionalEnabled = new bool[additionalGameplayCameraBehaviours.Length];
                for (int i = 0; i < additionalGameplayCameraBehaviours.Length; i++)
                {
                    Behaviour behaviour = additionalGameplayCameraBehaviours[i];
                    previousAdditionalEnabled[i] = behaviour != null && behaviour.enabled;
                    if (behaviour != null)
                    {
                        behaviour.enabled = false;
                    }
                }
            }

            if (uccCameraBinder != null)
            {
                uccCameraBinder.enabled = false;
            }

            if (uccCameraHandler != null)
            {
                uccCameraHandler.enabled = false;
            }

            if (uccCameraController != null)
            {
                uccCameraController.enabled = false;
            }

            cinematicActive = true;
            return true;
        }

        public void SetActorShot(
            StorySequenceActor actor,
            StorySequenceActor listener,
            StorySequenceCameraProfile profile,
            float durationOverride = -1f)
        {
            if (actor == null)
            {
                return;
            }

            BeginCinematic();
            activeActor = actor;
            activeListener = listener;
            activeProfile = profile;
            activePoint = null;
            StartTransition(ResolveTransitionDuration(profile, durationOverride));
        }

        public void SetCameraPoint(StorySequenceCameraPoint point, float duration)
        {
            if (point == null)
            {
                return;
            }

            BeginCinematic();
            activeActor = null;
            activeListener = null;
            activeProfile = null;
            activePoint = point;
            StartTransition(Mathf.Max(0f, duration));
        }

        public void EndCinematic(bool snapBackToGameplay = true)
        {
            if (!cinematicActive)
            {
                return;
            }

            cinematicActive = false;
            transitioning = false;
            activeActor = null;
            activeListener = null;
            activeProfile = null;
            activePoint = null;

            if (additionalGameplayCameraBehaviours != null && previousAdditionalEnabled != null)
            {
                int count = Mathf.Min(additionalGameplayCameraBehaviours.Length, previousAdditionalEnabled.Length);
                for (int i = 0; i < count; i++)
                {
                    if (additionalGameplayCameraBehaviours[i] != null)
                    {
                        additionalGameplayCameraBehaviours[i].enabled = previousAdditionalEnabled[i];
                    }
                }
            }

            if (uccCameraController != null)
            {
                uccCameraController.enabled = previousUccControllerEnabled;
            }

            if (uccCameraHandler != null)
            {
                uccCameraHandler.enabled = previousUccHandlerEnabled;
            }

            if (uccCameraBinder != null)
            {
                uccCameraBinder.enabled = previousUccBinderEnabled;
            }

            if (snapBackToGameplay &&
                uccCameraController != null &&
                uccCameraController.enabled &&
                uccCameraController.Character != null &&
                uccCameraController.ActiveViewType != null)
            {
                uccCameraController.PositionImmediately(true);
            }
        }

        private void LateUpdate()
        {
            if (!cinematicActive || controlledCamera == null)
            {
                return;
            }

            ResolveDesiredPose(out Vector3 targetPosition, out Quaternion targetRotation, out float targetFov);
            if (transitioning)
            {
                transitionElapsed += Time.unscaledDeltaTime;
                float t = transitionDuration <= 0f
                    ? 1f
                    : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transitionElapsed / transitionDuration));
                controlledCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(transitionStartPosition, targetPosition, t),
                    Quaternion.Slerp(transitionStartRotation, targetRotation, t));
                controlledCamera.fieldOfView = Mathf.Lerp(transitionStartFov, targetFov, t);
                if (t >= 1f)
                {
                    transitioning = false;
                }

                return;
            }

            float sharpness = activeProfile != null
                ? activeProfile.followSharpness
                : fallbackFollowSharpness;
            float followT = sharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime);
            controlledCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(controlledCamera.transform.position, targetPosition, followT),
                Quaternion.Slerp(controlledCamera.transform.rotation, targetRotation, followT));
            controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, targetFov, followT);
        }

        private void StartTransition(float duration)
        {
            if (controlledCamera == null)
            {
                return;
            }

            transitionStartPosition = controlledCamera.transform.position;
            transitionStartRotation = controlledCamera.transform.rotation;
            transitionStartFov = controlledCamera.fieldOfView;
            transitionDuration = Mathf.Max(0f, duration);
            transitionElapsed = 0f;
            transitioning = transitionDuration > 0f;

            if (!transitioning)
            {
                ResolveDesiredPose(out Vector3 position, out Quaternion rotation, out float fov);
                controlledCamera.transform.SetPositionAndRotation(position, rotation);
                controlledCamera.fieldOfView = fov;
            }
        }

        private void ResolveDesiredPose(
            out Vector3 position,
            out Quaternion rotation,
            out float fieldOfView)
        {
            if (activePoint != null)
            {
                Transform pointTransform = activePoint.CameraTransform;
                position = pointTransform.position;
                Vector3 lookTarget = activePoint.LookTarget != null
                    ? activePoint.LookTarget.position + activePoint.LookAtOffset
                    : position + pointTransform.forward;
                rotation = ResolveLookRotation(position, lookTarget, pointTransform.rotation);
                fieldOfView = activePoint.FieldOfView;
                return;
            }

            if (activeActor != null)
            {
                Transform actorRoot = activeActor.Root;
                Transform actorAnchor = activeActor.CameraAnchor;
                Vector3 localOffset = activeProfile != null
                    ? activeProfile.localCameraOffset
                    : fallbackLocalOffset;
                position = actorRoot.TransformPoint(localOffset);

                Vector3 lookTarget = actorAnchor != null ? actorAnchor.position : actorRoot.position;
                if (activeProfile != null)
                {
                    lookTarget += actorRoot.TransformVector(activeProfile.lookAtOffset);
                    if (activeProfile.frameSpeakerAndListener && activeListener != null)
                    {
                        Transform listenerAnchor = activeListener.CameraAnchor;
                        Vector3 listenerPoint = listenerAnchor != null
                            ? listenerAnchor.position
                            : activeListener.Root.position;
                        lookTarget = Vector3.Lerp(
                            lookTarget,
                            listenerPoint,
                            activeProfile.speakerToListenerLookWeight);
                    }
                }

                rotation = ResolveLookRotation(position, lookTarget, actorRoot.rotation);
                fieldOfView = activeProfile != null
                    ? activeProfile.fieldOfView
                    : fallbackFieldOfView;
                return;
            }

            position = controlledCamera.transform.position;
            rotation = controlledCamera.transform.rotation;
            fieldOfView = controlledCamera.fieldOfView;
        }

        private static Quaternion ResolveLookRotation(
            Vector3 position,
            Vector3 target,
            Quaternion fallback)
        {
            Vector3 direction = target - position;
            return direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : fallback;
        }

        private static float ResolveTransitionDuration(
            StorySequenceCameraProfile profile,
            float durationOverride)
        {
            return durationOverride >= 0f
                ? durationOverride
                : profile != null ? profile.transitionDuration : 0.6f;
        }

        private void ResolveReferences()
        {
            if (controlledCamera == null)
            {
                controlledCamera = Camera.main;
            }

            if (controlledCamera == null)
            {
                controlledCamera = FindAnyObjectByType<Camera>();
            }

            if (controlledCamera == null)
            {
                return;
            }

            if (uccCameraController == null)
            {
                uccCameraController = controlledCamera.GetComponentInParent<UccCameraController>();
            }

            if (uccCameraHandler == null && uccCameraController != null)
            {
                uccCameraHandler = uccCameraController.GetComponent<UccCameraControllerHandler>();
            }

            if (uccCameraBinder == null && uccCameraController != null)
            {
                uccCameraBinder = uccCameraController.GetComponent<LitUccCameraCharacterBinder>();
            }
        }

        private void OnDisable()
        {
            EndCinematic();
        }
    }
}
