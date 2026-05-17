using UnityEngine;

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

public static class TemporalAgeUtility
{
    public const int StepYears = 111;
    public const int MinYear = 0;
    public const int MaxYear = 666;
    public const int MinStep = 0;
    public const int MaxStep = 6;

    public static TemporalAge GetPreviousAge(TemporalAge age)
    {
        return StepToAge(AgeToStep(age) - 1);
    }

    public static TemporalAge GetNextAge(TemporalAge age)
    {
        return StepToAge(AgeToStep(age) + 1);
    }

    public static TemporalAge ClampAge(TemporalAge age)
    {
        return IntToAge(AgeToInt(age));
    }

    public static int AgeToInt(TemporalAge age)
    {
        return Mathf.Clamp((int)age, MinYear, MaxYear);
    }

    public static int AgeToStep(TemporalAge age)
    {
        return Mathf.Clamp(Mathf.RoundToInt(AgeToInt(age) / (float)StepYears), MinStep, MaxStep);
    }

    public static TemporalAge IntToAge(int year)
    {
        int clampedYear = Mathf.Clamp(year, MinYear, MaxYear);
        int step = Mathf.RoundToInt(clampedYear / (float)StepYears);
        return StepToAge(step);
    }

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

    public static string ToInternalLabel(TemporalAge age)
    {
        return $"Age{AgeToInt(age):000}";
    }
}
