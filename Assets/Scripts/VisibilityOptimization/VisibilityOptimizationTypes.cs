using UnityEngine;

public enum VisibilityOptimizationCategory
{
    StaticMesh,
    DynamicObject,
    Light,
    NPC,
    Decoration,
    Interactive,
    Critical
}

public enum VisibilityOptimizationState
{
    Visible,
    RendererCulled,
    LightCulled,
    Paused,
    Excluded
}

public readonly struct VisibilityPauseContext
{
    public readonly VisibilityOptimizationState State;
    public readonly VisibilityOptimizationCategory Category;
    public readonly float CameraDistance;
    public readonly float PlayerDistance;
    public readonly bool InCameraFrustum;
    public readonly string Reason;

    public VisibilityPauseContext(
        VisibilityOptimizationState state,
        VisibilityOptimizationCategory category,
        float cameraDistance,
        float playerDistance,
        bool inCameraFrustum,
        string reason)
    {
        State = state;
        Category = category;
        CameraDistance = cameraDistance;
        PlayerDistance = playerDistance;
        InCameraFrustum = inCameraFrustum;
        Reason = reason ?? string.Empty;
    }
}

public interface IPausableWhenInvisible
{
    void SetPausedWhenInvisible(bool paused, VisibilityPauseContext context);
}

public interface IVisibilityUpdateRateTarget
{
    void SetVisibilityUpdateInterval(float intervalSeconds, VisibilityPauseContext context);
}

public interface ICameraVisibilityObstacle
{
    bool PreserveForCameraFade { get; }
    bool NeverCullWhenBetweenCameraAndPlayer { get; }
}
