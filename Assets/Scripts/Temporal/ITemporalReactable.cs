// Role:
// Minimal contract for non-visual systems that need to react to the local age
// revealed by LocalRuntimeAgeTrigger / TemporalTorch.
// Usage:
// Implement this on scene components that should temporarily use the torch age
// while the torch reveal overlaps their renderer.
// Responsibilities:
// Keep local torch influence decoupled from shader-only code.
// Dependencies:
// LocalRuntimeAgeTrigger and the canonical temporal year grid.
// Precautions:
// The dominant zone/brasero age remains owned by AgeManager or TemporalZone.
// Local torch influence is temporary and should be cleared when the source exits.

/// <summary>
/// Receives local temporal age influence from a torch reveal.
/// </summary>
public interface ITemporalReactable
{
    /// <summary>
    /// A local reveal source started influencing this object.
    /// </summary>
    void ApplyLocalTemporalAge(LocalRuntimeAgeTrigger source, int year);

    /// <summary>
    /// The source is still influencing this object and its revealed year changed.
    /// </summary>
    void UpdateLocalTemporalAge(LocalRuntimeAgeTrigger source, int year);

    /// <summary>
    /// A local reveal source stopped influencing this object.
    /// </summary>
    void ClearLocalTemporalAge(LocalRuntimeAgeTrigger source);
}
