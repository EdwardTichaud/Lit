using UnityEngine;

namespace Lit.Story
{
    [DisallowMultipleComponent]
    public sealed class StorySequenceActor : MonoBehaviour
    {
        [SerializeField] private string actorId;
        [SerializeField] private string displayName;
        [SerializeField] private Transform faceAnchor;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Animator animator;

        public string ActorId => actorId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public Transform Root => transform;
        public Transform FaceAnchor => faceAnchor != null ? faceAnchor : ResolveDefaultAnchor();
        public Transform CameraAnchor => cameraAnchor != null ? cameraAnchor : FaceAnchor;
        public Animator Animator => animator != null ? animator : ResolveAnimator();

        public bool Matches(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   string.Equals(actorId, id.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        public void ConfigureRuntime(string id, string runtimeDisplayName = null)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                actorId = id;
            }

            if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(runtimeDisplayName))
            {
                displayName = runtimeDisplayName;
            }

            ResolveReferences();
        }

        private void Reset()
        {
            actorId = gameObject.name;
            displayName = gameObject.name;
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (faceAnchor == null && animator != null && animator.isHuman)
            {
                faceAnchor = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            if (cameraAnchor == null)
            {
                cameraAnchor = faceAnchor;
            }
        }

        private Transform ResolveDefaultAnchor()
        {
            ResolveReferences();
            return faceAnchor != null ? faceAnchor : transform;
        }

        private Animator ResolveAnimator()
        {
            ResolveReferences();
            return animator;
        }
    }
}
