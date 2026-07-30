using UnityEngine;

namespace Lit.Story
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class StorySequenceProximityTrigger : MonoBehaviour
    {
        [SerializeField] private StorySequenceRunner runner;
        [SerializeField] private Collider triggerCollider;
        [SerializeField] private bool disableAfterTrigger = true;

        private bool triggered;

        private void Reset()
        {
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void Awake()
        {
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider>();
            }

            if (runner != null && runner.Sequence != null && runner.Sequence.playOnce &&
                StorySequenceCompletionStore.IsCompleted(runner.Sequence))
            {
                triggered = true;
                if (triggerCollider != null)
                {
                    triggerCollider.enabled = false;
                }
            }
        }

        private void OnTriggerEnter(Collider other) => TryTrigger(other);

        private void OnTriggerStay(Collider other) => TryTrigger(other);

        private void TryTrigger(Collider other)
        {
            if (triggered || runner == null || !IsLocalPlayer(other))
            {
                return;
            }

            if (!runner.Play())
            {
                return;
            }

            triggered = true;
            if (disableAfterTrigger && triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }

        private static bool IsLocalPlayer(Collider other)
        {
            Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
            return localRoot != null && other != null &&
                   (other.transform == localRoot || other.transform.IsChildOf(localRoot));
        }
    }
}
