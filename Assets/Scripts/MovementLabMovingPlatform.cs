using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class MovementLabMovingPlatform : MonoBehaviour
{
    [SerializeField] private Vector3 localTravel = new Vector3(4f, 0f, 0f);
    [SerializeField, Min(0.1f)] private float period = 4f;
    [SerializeField] private float startDelay;
    [SerializeField] private Vector3 localRotationPerCycle;

    private Rigidbody targetRigidbody;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool initialized;

    private void Reset()
    {
        CacheRigidbody();
    }

    private void Awake()
    {
        CacheRigidbody();
        CaptureStartPose();
    }

    private void OnValidate()
    {
        period = Mathf.Max(0.1f, period);
        CacheRigidbody();
    }

    private void FixedUpdate()
    {
        if (!initialized)
        {
            CaptureStartPose();
        }

        float time = Mathf.Max(0f, Time.fixedTime - startDelay);
        float phase = Mathf.PingPong(time / Mathf.Max(0.1f, period), 1f);
        float eased = Mathf.SmoothStep(0f, 1f, phase);
        Vector3 worldTravel = transform.parent != null
            ? transform.parent.TransformVector(localTravel * eased)
            : localTravel * eased;
        Vector3 targetPosition = startPosition + worldTravel;
        Quaternion targetRotation = startRotation * Quaternion.Euler(localRotationPerCycle * eased);

        if (targetRigidbody != null)
        {
            targetRigidbody.MovePosition(targetPosition);
            targetRigidbody.MoveRotation(targetRotation);
            return;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    public void Configure(Vector3 travel, float cyclePeriod, float delay, Vector3 rotationPerCycle)
    {
        localTravel = travel;
        period = Mathf.Max(0.1f, cyclePeriod);
        startDelay = delay;
        localRotationPerCycle = rotationPerCycle;
        CaptureStartPose();
    }

    private void CacheRigidbody()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }

        if (targetRigidbody != null)
        {
            targetRigidbody.isKinematic = true;
            targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            targetRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    private void CaptureStartPose()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        initialized = true;
    }
}
