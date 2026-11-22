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

        private bool triggered,reserved,awaitingExit;
        private void OnEnable(){if(runner!=null){runner.PlaybackStarted+=ConsumeReservation;runner.PreparationFailed+=CancelReservation;}}
        private void OnDisable(){if(runner!=null){runner.PlaybackStarted-=ConsumeReservation;runner.PreparationFailed-=CancelReservation;}}
        private void ConsumeReservation()
        {
            if(!reserved)return;
            reserved=false;triggered=true;
            if(disableAfterTrigger&&triggerCollider!=null)triggerCollider.enabled=false;
        }
        private void CancelReservation(){if(reserved){reserved=false;awaitingExit=true;}}
        private void OnTriggerExit(Collider other){if(IsLocalPlayer(other))awaitingExit=false;}

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
            if (triggered || reserved || awaitingExit || runner == null || !IsLocalPlayer(other))
            {
                return;
            }

            // OnTriggerStay retries after arrival. Do not reserve a scene-owned dialogue
            // while GameFlow is still moving the character or retiring its source scene.
            if (LoadingScreenService.IsLoading || GameFlowService.IsPreparingGameplayScene ||
                (GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning) ||
                InputModeCoordinator.IsGameplayBlocked) return;

            reserved=true;
            if(!runner.Play())reserved=false;
        }

        private static bool IsLocalPlayer(Collider other)
        {
            Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
            return localRoot != null && other != null &&
                   (other.transform == localRoot || other.transform.IsChildOf(localRoot));
        }
    }
}
