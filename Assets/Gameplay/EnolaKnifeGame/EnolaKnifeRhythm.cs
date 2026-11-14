using UnityEngine;

/// <summary>One press per beat. Clock and outcomes are independent of animation and frame rate.</summary>
public sealed class EnolaKnifeRhythm
{
    public const int BeatCount = 8;
    public const int PassingScore = 6;
    public const double BeatDuration = 1.1;
    private static readonly float[] Centers = { .32f, .43f, .54f, .65f, .76f };
    private static readonly float[] Widths = { .20f, .23f, .26f, .29f, .32f };
    private readonly Vector2[] windows = new Vector2[BeatCount];
    private readonly double start;
    private int attempted = -1;
    public int Score { get; private set; }
    public EnolaKnifeRhythm(double start, System.Random random = null, Vector2? previousWindow = null)
    {
        this.start = start;
        random ??= new System.Random();
        int center = previousWindow.HasValue ? System.Array.IndexOf(Centers, previousWindow.Value.x) : -1;
        int width = previousWindow.HasValue ? System.Array.IndexOf(Widths, previousWindow.Value.y) : -1;
        for (int beat = 0; beat < BeatCount; beat++)
        {
            center = NextIndex(random, center, Centers.Length);
            width = NextIndex(random, width, Widths.Length);
            windows[beat] = new Vector2(Centers[center], Widths[width]);
        }
    }

    private static int NextIndex(System.Random random, int previous, int count) =>
        previous < 0 ? random.Next(count) : (previous + random.Next(1, count)) % count;

    public bool Complete(double now) => now >= start + BeatCount * BeatDuration;
    public int Beat(double now) => Mathf.Clamp((int)System.Math.Floor((now - start) / BeatDuration), 0, BeatCount - 1);
    public float Phase(double now) => Mathf.Clamp01((float)((now - start) / BeatDuration - Beat(now)));
    public Vector2 Window(int beat) => windows[beat];

    public bool InWindow(double now)
    {
        Vector2 window = Window(Beat(now));
        return now >= start && !Complete(now) && Mathf.Abs(Phase(now) - window.x) <= window.y * .5f;
    }
    public bool TryPress(double now, out bool hit)
    {
        hit = false;
        int beat = Beat(now);
        if (now < start || Complete(now) || attempted == beat) return false;
        attempted = beat;
        hit = InWindow(now);
        if (hit) Score++;
        return true;
    }
}
