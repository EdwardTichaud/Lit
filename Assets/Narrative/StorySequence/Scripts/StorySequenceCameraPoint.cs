using UnityEngine;

namespace Lit.Story
{
    [DisallowMultipleComponent]
    public sealed class StorySequenceCameraPoint : MonoBehaviour
    {
        [SerializeField] private string pointId;
        [SerializeField] private Transform lookTarget;
        [SerializeField] private Vector3 lookAtOffset;
        [SerializeField, Range(15f, 100f)] private float fieldOfView = 45f;

        public string PointId => pointId;
        public Transform CameraTransform => transform;
        public Transform LookTarget => lookTarget;
        public Vector3 LookAtOffset => lookAtOffset;
        public float FieldOfView => fieldOfView;

        public bool Matches(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   string.Equals(pointId, id.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        private void Reset()
        {
            pointId = gameObject.name;
        }
    }
}
