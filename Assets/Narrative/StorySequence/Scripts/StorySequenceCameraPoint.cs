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
        [SerializeField, Tooltip("Conserve exactement la rotation du repere, sans recadrage automatique.")]
        private bool useAuthoredRotation;
        [SerializeField, Range(0f, 1f)] private float breathingStrength;
        [Header("Slow group orbit")]
        [SerializeField, Range(0f, 45f), Tooltip("Gentle arc around Look Target. Zero keeps the authored shot static.")]
        private float orbitArcDegrees;
        [SerializeField, Min(10f)] private float orbitPeriod = 180f;

        public string PointId => pointId;
        public Transform CameraTransform => transform;
        public Transform LookTarget => lookTarget;
        public Vector3 LookAtOffset => lookAtOffset;
        public float FieldOfView => fieldOfView;
        public bool UseAuthoredRotation => useAuthoredRotation;
        public float BreathingStrength => breathingStrength;

        public Vector3 EvaluatePosition(float elapsed)
        {
            if (lookTarget == null || orbitArcDegrees <= 0f) return transform.position;
            Vector3 center = lookTarget.position + lookAtOffset;
            float angle = orbitArcDegrees * Mathf.Sin(Mathf.Max(0f, elapsed) * (2f * Mathf.PI / Mathf.Max(10f, orbitPeriod)));
            return center + Quaternion.AngleAxis(angle, Vector3.up) * (transform.position - center);
        }

        public void SetFieldOfView(float value) => fieldOfView = Mathf.Clamp(value, 15f, 100f);

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
