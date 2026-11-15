using UnityEngine;

namespace Lit.Story
{
    // Optional, local presentation for sequences staged from the player's eyes.
    [DisallowMultipleComponent]
    public sealed class StorySequenceConversationView : MonoBehaviour
    {
        [SerializeField] private StorySequenceCameraPoint cameraPoint;
        [SerializeField] private GameObject interlocutorVisual;
        [SerializeField] private Vector3 eyeOffset = new Vector3(0f, .04f, .24f);
        [SerializeField] private Vector3 interlocutorOffset = new Vector3(0f, .02f, 1.65f);

        private GameObject visualInstance;

        public void Prepare(StorySequenceActor player, StorySequenceCameraDriver driver)
        {
            Clear();
            if (!isActiveAndEnabled || player == null || driver == null ||
                cameraPoint == null || interlocutorVisual == null || !driver.BeginCinematic())
                return;

            ResolveStaging(player, out Vector3 position, out Vector3 target, out Quaternion rotation);
            cameraPoint.transform.SetPositionAndRotation(position, rotation);
            visualInstance = Instantiate(interlocutorVisual, target, Quaternion.identity, transform);
            driver.SetCameraPoint(cameraPoint, 0f);
        }

        private void ResolveStaging(StorySequenceActor player, out Vector3 position, out Vector3 target, out Quaternion rotation)
        {
            Transform root = player.Root;
            Transform anchor = player.CameraAnchor;
            Vector3 eye = anchor != root ? anchor.position : root.position + Vector3.up * 1.25f;
            Quaternion facing = Quaternion.Euler(0f, root.eulerAngles.y, 0f);
            position = eye + facing * eyeOffset;
            target = eye + facing * interlocutorOffset;
            Vector3 direction = target - position;
            rotation = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction, Vector3.up) : facing;
        }

        public void Clear()
        {
            if (visualInstance == null) return;
            visualInstance.SetActive(false);
            Destroy(visualInstance);
            visualInstance = null;
        }

        private void OnDisable() => Clear();
    }
}
