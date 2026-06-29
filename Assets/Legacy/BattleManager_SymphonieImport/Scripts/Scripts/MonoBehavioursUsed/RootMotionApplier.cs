using UnityEngine;

namespace Symphonie.Movement
{
    /// <summary>
    /// Applies root motion from a source Animator onto a target transform or CharacterController.
    /// </summary>
    public class RootMotionApplier : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator source qui fournit le delta Root Motion.")]
        [SerializeField] private Animator sourceAnimator;
        [Tooltip("Transform cible a deplacer via le Root Motion.")]
        [SerializeField] private Transform targetTransform;
        [Tooltip("CharacterController optionnel pour appliquer le delta.")]
        [SerializeField] private CharacterController controller;

        [Header("Root Motion")]
        [Tooltip("Active l'application du Root Motion (sinon aucun delta applique).")]
        [SerializeField] private bool isActive = true;
        [Tooltip("Applique la rotation du Root Motion sur la cible.")]
        [SerializeField] private bool applyRotation = true;
        [Tooltip("Ignore la composante Y du Root Motion (Y gere par controller/gravite).")]
        [SerializeField] private bool lockYToGround = true;
        [Tooltip("Ajuste la vitesse du Root Motion pour coller a une vitesse cible.")]
        [SerializeField] private bool scaleToDesiredSpeed = true;
        [Tooltip("Vitesse cible pour recalibrer le Root Motion.")]
        [SerializeField] private float desiredSpeed = 0f;
        [Tooltip("Multiplicateur applique au Root Motion avant recalibrage.")]
        [SerializeField] private float speedScale = 1f;

        public Vector3 LastAppliedDelta { get; private set; }
        public Vector3 LastAppliedVelocity { get; private set; }
        public Quaternion LastAppliedRotationDelta { get; private set; } = Quaternion.identity;
        public Quaternion LastAppliedRotation { get; private set; } = Quaternion.identity;

        public Animator SourceAnimator
        {
            get => sourceAnimator;
            set => sourceAnimator = value;
        }

        public Transform TargetTransform
        {
            get => targetTransform;
            set => targetTransform = value;
        }

        public CharacterController Controller
        {
            get => controller;
            set => controller = value;
        }

        public bool IsActive
        {
            get => isActive;
            set => isActive = value;
        }

        public bool ApplyRotation
        {
            get => applyRotation;
            set => applyRotation = value;
        }

        public bool LockYToGround
        {
            get => lockYToGround;
            set => lockYToGround = value;
        }

        public bool ScaleToDesiredSpeed
        {
            get => scaleToDesiredSpeed;
            set => scaleToDesiredSpeed = value;
        }

        public float DesiredSpeed
        {
            get => desiredSpeed;
            set => desiredSpeed = value;
        }

        public float SpeedScale
        {
            get => speedScale;
            set => speedScale = value;
        }

        private void Awake()
        {
            if (targetTransform == null)
                targetTransform = transform;

            if (controller == null)
                controller = GetComponent<CharacterController>();
        }

        public void Configure(Animator source, Transform target, CharacterController characterController)
        {
            sourceAnimator = source;
            targetTransform = target != null ? target : transform;
            controller = characterController;
        }

        private void LateUpdate()
        {
            if (!isActive || sourceAnimator == null || targetTransform == null)
            {
                ClearLastDelta();
                return;
            }

            Vector3 delta = sourceAnimator.deltaPosition;
            Quaternion deltaRotation = sourceAnimator.deltaRotation;

            if (lockYToGround)
                delta.y = 0f;

            float scale = speedScale;
            if (scaleToDesiredSpeed && desiredSpeed > 0.0001f)
            {
                float rootSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                scale = rootSpeed > 0.0001f ? scale * (desiredSpeed / rootSpeed) : 0f;
            }

            delta *= scale;

            if (controller != null && controller.enabled)
                controller.Move(delta);
            else
                targetTransform.position += delta;

            if (applyRotation && deltaRotation != Quaternion.identity)
                targetTransform.rotation *= deltaRotation;

            LastAppliedDelta = delta;
            LastAppliedVelocity = Time.deltaTime > 0f ? delta / Time.deltaTime : Vector3.zero;
            LastAppliedRotationDelta = deltaRotation;
            LastAppliedRotation = targetTransform.rotation;
        }

        private void ClearLastDelta()
        {
            LastAppliedDelta = Vector3.zero;
            LastAppliedVelocity = Vector3.zero;
            LastAppliedRotationDelta = Quaternion.identity;
            LastAppliedRotation = Quaternion.identity;
        }
    }
}
