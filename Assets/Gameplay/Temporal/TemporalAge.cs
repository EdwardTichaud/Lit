// Role:
// Defines the internal temporal age grid used by the current archaeology gameplay.
// Usage:
// Shared by TemporalZone, TemporalObject, AgeManager, narrative metadata, and
// bridges to older year-based systems.
// Responsibilities:
// Keep all conversions between enum values, years, and 111-year steps in one place.
// Dependencies:
// Uses Unity Mathf for clamping and rounding.
// Precautions:
// Do not change enum numeric values without migrating every serialized asset and scene.
using UnityEngine;

/// <summary>
/// Internal production ages for the castle's temporal strata.
/// The player does not have to see these numbers directly in UI or narrative text.
/// </summary>
public enum TemporalAge
{
    Age000 = 0,
    Age111 = 111,
    Age222 = 222,
    Age333 = 333,
    Age444 = 444,
    Age555 = 555,
    Age666 = 666
}

/// <summary>
/// Helper methods for converting and clamping <see cref="TemporalAge"/> values.
/// Keep this class small: it is the shared bridge between data, gameplay, and shaders.
/// </summary>
public static class TemporalAgeUtility
{
    /// <summary>Number of years between two internal temporal ages.</summary>
    public const int StepYears = 111;
    /// <summary>Lowest supported internal year.</summary>
    public const int MinYear = 0;
    /// <summary>Highest supported internal year.</summary>
    public const int MaxYear = 666;
    /// <summary>Lowest supported step index.</summary>
    public const int MinStep = 0;
    /// <summary>Highest supported step index.</summary>
    public const int MaxStep = 6;

    /// <summary>
    /// Returns the previous temporal age, clamped to the supported range.
    /// </summary>
    public static TemporalAge GetPreviousAge(TemporalAge age)
    {
        return StepToAge(AgeToStep(age) - 1);
    }

    /// <summary>
    /// Returns the next temporal age, clamped to the supported range.
    /// </summary>
    public static TemporalAge GetNextAge(TemporalAge age)
    {
        return StepToAge(AgeToStep(age) + 1);
    }

    /// <summary>
    /// Normalizes a temporal age to the nearest valid internal value.
    /// </summary>
    public static TemporalAge ClampAge(TemporalAge age)
    {
        return IntToAge(AgeToInt(age));
    }

    /// <summary>
    /// Converts an enum value to an internal year and clamps impossible values.
    /// </summary>
    public static int AgeToInt(TemporalAge age)
    {
        return Mathf.Clamp((int)age, MinYear, MaxYear);
    }

    /// <summary>
    /// Converts an age to its zero-based 111-year step.
    /// </summary>
    public static int AgeToStep(TemporalAge age)
    {
        return Mathf.Clamp(Mathf.RoundToInt(AgeToInt(age) / (float)StepYears), MinStep, MaxStep);
    }

    /// <summary>
    /// Converts any year to the nearest supported temporal age.
    /// </summary>
    public static TemporalAge IntToAge(int year)
    {
        // Years may come from older systems, so clamp first and round to the closest step.
        int clampedYear = Mathf.Clamp(year, MinYear, MaxYear);
        int step = Mathf.RoundToInt(clampedYear / (float)StepYears);
        return StepToAge(step);
    }

    /// <summary>
    /// Converts a zero-based age step to a temporal age.
    /// </summary>
    public static TemporalAge StepToAge(int step)
    {
        switch (Mathf.Clamp(step, MinStep, MaxStep))
        {
            case 0:
                return TemporalAge.Age000;
            case 1:
                return TemporalAge.Age111;
            case 2:
                return TemporalAge.Age222;
            case 3:
                return TemporalAge.Age333;
            case 4:
                return TemporalAge.Age444;
            case 5:
                return TemporalAge.Age555;
            case 6:
            default:
                return TemporalAge.Age666;
        }
    }

    /// <summary>
    /// Returns a stable label useful for debug logs, tooling, and internal docs.
    /// </summary>
    public static string ToInternalLabel(TemporalAge age)
    {
        return $"Age{AgeToInt(age):000}";
    }
}
