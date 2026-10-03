using UnityEngine;

[CreateAssetMenu(menuName = "Lit/Camera/Tactical Profile")]
public sealed class LitTacticalCameraProfile : ScriptableObject
{
    [Header("Framing")]
    [Range(20, 100)] public float fieldOfView = 60;
    public Vector2 distanceLimits = new Vector2(4, 18);
    public float initialDistance = 10;
    public Vector2 pitchLimits = new Vector2(25, 75);
    public float initialPitch = 50;
    [Header("Damping (seconds)")]
    [Min(.001f)] public float followTime = .16f;
    [Min(.001f)] public float panTime = .10f;
    [Min(.001f)] public float zoomTime = .12f;
    [Min(.001f)] public float recenterTime = .25f;
    [Min(.001f)] public float transitionTime = .3f;
    [Header("Input")]
    public float panSpeed = 10;
    public float mouseOrbitSensitivity = .12f;
    public float dragSensitivity = .015f;
    public float wheelSensitivity = .015f;
    public float gamepadOrbitSpeed = 120;
    public float gamepadZoomSpeed = 8;
    public bool edgeScrolling;
    [Min(1)] public float edgePixels = 20;
    [Header("Safety")]
    [Min(1)] public float maximumPanRadius = 20;
    [Min(.01f)] public float boundarySlowZone = 4;
    [Min(.1f)] public float teleportDistance = 3;
    [Min(.1f)] public float groundProbeHeight = 2;
    [Min(.1f)] public float maximumGroundStep = 1.2f;
    [Min(.01f)] public float groundSmoothTime = .16f;
    [Min(.01f)] public float collisionReturnTime = .15f;
    [Header("Occlusion")]
    [Min(.01f)] public float occlusionInterval = .05f;
    [Min(0)] public float occlusionRestoreDelay = .15f;
    public bool showDiagnostics;

    private void OnValidate()
    {
        distanceLimits.x = Mathf.Max(.5f, distanceLimits.x);
        distanceLimits.y = Mathf.Max(distanceLimits.x, distanceLimits.y);
        initialDistance = Mathf.Clamp(initialDistance, distanceLimits.x, distanceLimits.y);
        pitchLimits.x = Mathf.Clamp(pitchLimits.x, 1, 85);
        pitchLimits.y = Mathf.Clamp(pitchLimits.y, pitchLimits.x, 85);
        initialPitch = Mathf.Clamp(initialPitch, pitchLimits.x, pitchLimits.y);
        boundarySlowZone = Mathf.Clamp(boundarySlowZone, .01f, maximumPanRadius);
    }
}
