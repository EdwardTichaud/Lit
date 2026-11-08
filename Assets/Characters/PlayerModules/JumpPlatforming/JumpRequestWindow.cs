using UnityEngine;

/// <summary>Local-time input forgiveness only. Never owns character motion.</summary>
public sealed class JumpRequestWindow
{
    private float timeSinceGround = float.PositiveInfinity;
    private float remaining;
    private float landingReservation = -1f;
    public bool HasRequest => remaining > 0f;

    public void Tick(float deltaTime, bool grounded, bool canArmGround, bool recoveringLanding, float maximumLandingWait)
    {
        float dt = Mathf.Max(0f, deltaTime);
        timeSinceGround = grounded && canArmGround ? 0f : timeSinceGround + dt;
        if (landingReservation < 0f) remaining = Mathf.Max(0f, remaining - dt);
        // Contact must occur within the original buffer. Only then may a request
        // wait for the existing landing animation/owned lock to finish.
        if (HasRequest && grounded && recoveringLanding && landingReservation < 0f)
            landingReservation = Mathf.Max(0f, maximumLandingWait);
        if (landingReservation >= 0f)
        {
            landingReservation -= dt;
            if (landingReservation <= 0f) ClearRequest();
        }
    }

    public void Request(float bufferSeconds)
    {
        remaining = Mathf.Max(.0001f, bufferSeconds);
        landingReservation = -1f;
    }

    public bool CanConsume(bool grounded, float coyoteSeconds) =>
        HasRequest && (grounded || coyoteSeconds > 0f && timeSinceGround <= coyoteSeconds);

    public void Consume()
    {
        ClearRequest();
        timeSinceGround = float.PositiveInfinity; // No second jump from the same ledge.
    }

    public void ClearRequest() { remaining = 0f; landingReservation = -1f; }
    public void Reset() { Consume(); }
}
