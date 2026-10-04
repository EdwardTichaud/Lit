using UnityEngine;

/// <summary>Render-frame deltas accumulated until UCC consumes them. No axis is integrated twice.</summary>
public struct LitTacticalInputBuffer
{
    private Vector2 pan, orbit;
    private float zoom;
    public void Add(Vector2 panDelta, Vector2 orbitDelta, float zoomDelta)
    { pan += panDelta; orbit += orbitDelta; zoom += zoomDelta; }
    public void Consume(out Vector2 panDelta, out Vector2 orbitDelta, out float zoomDelta)
    { panDelta = pan; orbitDelta = orbit; zoomDelta = zoom; Clear(); }
    public void Clear() { pan = orbit = Vector2.zero; zoom = 0; }
}

/// <summary>Fixed simulation steps are distinct even when they share a rendered frame.</summary>
public struct LitTacticalSimulationClock
{
    private double fixedStamp;
    private int renderStamp;
    private bool hasFixedStamp, hasRenderStamp;
    public bool TryAdvance(bool fixedStep, double fixedTime, int frame, bool immediate = false)
    {
        if (immediate) return false;
        if (fixedStep)
        {
            if (hasFixedStamp && fixedStamp == fixedTime) return false;
            hasFixedStamp = true; fixedStamp = fixedTime;
        }
        else
        {
            if (hasRenderStamp && renderStamp == frame) return false;
            hasRenderStamp = true; renderStamp = frame;
        }
        return true;
    }
    public void Reset() { hasFixedStamp = hasRenderStamp = false; }
}

/// <summary>Safety retracts immediately; restoration waits for a stable available distance.</summary>
public struct LitTacticalDistanceRecovery
{
    private float distance, velocity, available, clearElapsed;
    private bool initialized, recovering;
    public void Reset(float value)
    { distance = available = value; velocity = clearElapsed = 0; initialized = true; recovering = false; }
    public float Resolve(float clear, float requested, float dt, bool immediate, float holdTime, float smoothTime)
    {
        clear = Mathf.Max(0, clear);
        if (immediate || !initialized) { Reset(clear); recovering = clear + .005f < requested; return distance; }
        bool blocked = clear + .005f < requested;
        if (clear < distance)
        {
            distance = clear; velocity = clearElapsed = 0; available = clear;
            recovering |= blocked;
            return distance;
        }
        if (blocked) recovering = true;
        if (!recovering) { distance = available = clear; return distance; }
        if (Mathf.Abs(clear - available) > .005f) clearElapsed = 0;
        available = clear;
        clearElapsed += dt;
        if (clearElapsed >= holdTime && dt > 0)
            distance = Mathf.Min(clear, Mathf.SmoothDamp(distance, clear, ref velocity, Mathf.Max(.001f, smoothTime), Mathf.Infinity, dt));
        if (!blocked && Mathf.Abs(distance - clear) <= .005f) { Reset(clear); }
        return distance;
    }
}
