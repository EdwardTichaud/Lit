using UnityEngine;

namespace Lit.Story
{
    // Optional, local presentation for a conversation with a foreground interlocutor.
    [DisallowMultipleComponent]
    public sealed class StorySequenceConversationView : MonoBehaviour
    {
        [SerializeField] private StorySequenceCameraPoint cameraPoint;
        [SerializeField] private GameObject interlocutorVisual;
        [SerializeField] private Vector3 cameraOffset = new Vector3(.55f, .46f, 2.3f);
        [SerializeField] private Vector3 interlocutorOffset = new Vector3(-.26f, -.05f, 1.2f);

        [SerializeField, Min(0f)] private float interlocutorLightLumens;
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
            if (interlocutorLightLumens > 0f)
            {
                var fill = new GameObject("Interlocutor fill");
                fill.transform.SetParent(visualInstance.transform, false);
                fill.transform.position = target + Vector3.up * .18f;
                fill.AddComponent<LitContrastLight>().Configure(LightType.Point,
                    new Color(1f, .84f, .58f), UnityEngine.Rendering.LightUnit.Lumen,
                    interlocutorLightLumens, 2.6f, 55f, false, 0f, 0f, 0f, false,
                    "Brief fill from the orb so Lucian's face reads in the reverse shot.");
            }
            driver.SetCameraPoint(cameraPoint, 0f);
        }

        private void ResolveStaging(StorySequenceActor player, out Vector3 position, out Vector3 target, out Quaternion rotation)
        {
            Transform root = player.Root;
            Transform anchor = player.CameraAnchor;
            Vector3 eye = anchor != root ? anchor.position : root.position + Vector3.up * 1.25f;
            Quaternion facing = Quaternion.Euler(0f, root.eulerAngles.y, 0f);
            position = eye + facing * cameraOffset;
            target = eye + facing * interlocutorOffset;
            // Look back at the player, with room beside them for the interlocutor.
            Vector3 framingCenter = Vector3.Lerp(eye - Vector3.up * .18f, target, .24f);
            Vector3 direction = framingCenter - position;
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
