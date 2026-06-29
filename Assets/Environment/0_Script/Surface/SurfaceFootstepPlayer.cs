using UnityEngine;

[DisallowMultipleComponent]
public class SurfaceFootstepPlayer : MonoBehaviour
{
    [Header("Surface")]
    [SerializeField] private SurfaceDefinition defaultSurface;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private Transform raycastOrigin;
    [SerializeField, Min(0f)] private float raycastHeight = 0.4f;
    [SerializeField, Min(0.05f)] private float raycastDistance = 1.6f;

    [Header("Footsteps")]
    [SerializeField, Min(0.05f)] private float stepDistance = 0.65f;
    [SerializeField, Min(0.01f)] private float minimumStepInterval = 0.18f;
    [SerializeField, Min(0f)] private float minimumPlanarSpeed = 0.15f;
    [SerializeField, Min(0f)] private float teleportResetDistance = 3f;

    private Vector3 lastPosition;
    private float distanceSinceLastStep;
    private float nextStepTime;
    private bool hasLastPosition;
    private AudioClip lastFootstepClip;

    private void OnEnable()
    {
        ResetStepTracking();
    }

    private void Update()
    {
        Vector3 currentPosition = transform.position;
        if (!hasLastPosition)
        {
            lastPosition = currentPosition;
            hasLastPosition = true;
            return;
        }

        Vector3 delta = currentPosition - lastPosition;
        lastPosition = currentPosition;

        Vector3 up = transform.up;
        Vector3 planarDelta = Vector3.ProjectOnPlane(delta, up);
        float planarDistance = planarDelta.magnitude;
        if (teleportResetDistance > 0f && delta.magnitude > teleportResetDistance)
        {
            distanceSinceLastStep = 0f;
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        if (planarDistance / deltaTime < minimumPlanarSpeed)
        {
            distanceSinceLastStep = 0f;
            return;
        }

        if (!TrySampleGround(out RaycastHit groundHit))
        {
            distanceSinceLastStep = 0f;
            return;
        }

        distanceSinceLastStep += planarDistance;
        if (distanceSinceLastStep < stepDistance || Time.time < nextStepTime)
        {
            return;
        }

        PlayFootstep(groundHit);
        distanceSinceLastStep = 0f;
        nextStepTime = Time.time + minimumStepInterval;
    }

    private bool TrySampleGround(out RaycastHit hit)
    {
        Vector3 up = transform.up;
        Vector3 origin = raycastOrigin != null ? raycastOrigin.position : transform.position + up * raycastHeight;
        return Physics.Raycast(
            origin,
            -up,
            out hit,
            Mathf.Max(0.05f, raycastDistance),
            groundMask,
            QueryTriggerInteraction.Ignore);
    }

    private void PlayFootstep(RaycastHit groundHit)
    {
        SurfaceDefinition surface = SurfaceResolver.Resolve(groundHit, defaultSurface);
        if (surface == null || !surface.HasFootstepClips)
        {
            return;
        }

        AudioClip clip = surface.GetRandomFootstepClip(lastFootstepClip);
        if (clip == null)
        {
            return;
        }

        lastFootstepClip = clip;
        AudioManager manager = AudioManager.EnsureInstance();
        manager.PlayClip(clip, groundHit.point, surface.FootstepVolume, surface.GetRandomFootstepPitch());
    }

    private void ResetStepTracking()
    {
        lastPosition = transform.position;
        distanceSinceLastStep = 0f;
        nextStepTime = 0f;
        hasLastPosition = true;
        lastFootstepClip = null;
    }
}
