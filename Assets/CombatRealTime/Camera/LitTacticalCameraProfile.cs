using UnityEngine;

public enum TacticalObstacleMode { Sliding, VisibilityMask, IgnoreObstacles }

[CreateAssetMenu(menuName = "Lit/Camera/Tactical Profile")]
public sealed class LitTacticalCameraProfile : ScriptableObject
{
    [Header("Framing")]
    [Range(20, 100)] public float fieldOfView = 60;
    public Vector2 distanceLimits = new Vector2(1, 20);
    public float initialDistance = 10;
    public Vector2 pitchLimits = new Vector2(25, 75);
    public float initialPitch = 50;
    [Header("Action framing (opt-in)")]
    public LitTacticalActionFraming actionFraming = new LitTacticalActionFraming();
    [Header("Damping (seconds)")]
    [Min(.001f)] public float followTime = .16f;
    [Min(.001f)] public float panTime = .10f;
    [Min(.001f)] public float zoomTime = .12f;
    [Min(.001f)] public float recenterTime = .25f;
    [Min(.001f)] public float transitionTime = .3f;
    // Legacy serialized field: aim now shares UCC interpolation, with no second damping control.
    [HideInInspector]
    public float renderedAimSharpness = 28f;
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
    [Min(1), Tooltip("Maximum actual camera distance from the player in free inspection, including zoom.")]
    public float maximumFreeCameraDistance = 20;
    [Min(.1f), Tooltip("Retraction speed used to escape a blocked camera trajectory.")]
    public float collisionRecoverySpeed = 8;
    [Min(.01f)] public float boundarySlowZone = 4;
    [Min(.1f)] public float teleportDistance = 3;
    [Min(.1f)] public float groundProbeHeight = 2;
    [Min(.1f)] public float maximumGroundStep = 1.2f;
    [Min(.01f)] public float groundSmoothTime = .16f;
    [Min(.01f)] public float collisionReturnTime = .15f;
    [Min(0), Tooltip("Stable clearance required before collision distance restores; retraction remains immediate.")]
    public float collisionClearHoldTime = .08f;
    [Header("Occlusion")]
    [Tooltip("VisibilityMask is follow-only. IgnoreObstacles also applies to free L3; cinematics use neither effect.")]
    public TacticalObstacleMode obstacleMode = TacticalObstacleMode.IgnoreObstacles;
    [Range(.01f, .5f), Tooltip("Circle radius as a fraction of screen height, corrected for aspect ratio.")]
    public float maskRadius = .16f;
    [Range(.001f, .1f), Tooltip("Dithered soft edge width as a fraction of screen height.")]
    public float maskFeather = .025f;
    [Min(.01f)] public float maskFadeTime = .12f;
    [Min(.01f)] public float occlusionInterval = .05f;
    [Min(0)] public float occlusionRestoreDelay = .15f;
    [Min(0), Tooltip("Preserve walkable geometry up to this height above the character's feet.")]
    public float maskGroundClearance = .25f;
    [Header("Developer: adaptive near clip (IgnoreObstacles follow only)")]
    public bool adaptiveNearClip = false;
    [Min(0)] public float nearClipPlayerMargin = 5;
    [Min(.001f)] public float nearClipSmoothTime = .15f;
    [Min(0)] public float nearClipBodySafety = .5f;
    public bool showDiagnostics;

    public static TacticalObstacleMode ResolveObstacleMode(TacticalObstacleMode requested, bool freeCamera, bool cinematic)
        => cinematic || (freeCamera && requested == TacticalObstacleMode.VisibilityMask) ? TacticalObstacleMode.Sliding : requested;

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
