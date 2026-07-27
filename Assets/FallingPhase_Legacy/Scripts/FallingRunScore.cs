using UnityEngine;

public enum FallingRank
{
    F,
    E,
    D,
    C,
    B,
    A,
    S,
    SS,
    SSS
}

[DisallowMultipleComponent]
public sealed class FallingRunScore : MonoBehaviour
{
    [SerializeField] private FallingPlayerController player;
    [SerializeField, Min(1f)] private float distanceScoreMultiplier = 2f;
    [SerializeField, Min(1f)] private float boostScorePerSecond = 6f;

    public float Score { get; private set; }
    public FallingRank CurrentRank { get; private set; }

    private float lastDistance;
    private float appliedImpactPenalty;

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        float distance = player.DistanceTravelled;
        float gainedDistance = Mathf.Max(0f, distance - lastDistance);
        lastDistance = distance;

        Score += gainedDistance * distanceScoreMultiplier;
        if (player.CurrentForwardSpeed > 30f)
        {
            Score += boostScorePerSecond * Time.deltaTime;
        }

        float newImpactPenalty = Mathf.Max(0f, player.ImpactPenalty - appliedImpactPenalty);
        appliedImpactPenalty = player.ImpactPenalty;
        Score = Mathf.Max(0f, Score - newImpactPenalty);
        CurrentRank = EvaluateRank(Score);
    }

    public static FallingRank EvaluateRank(float score)
    {
        if (score >= 2400f) return FallingRank.SSS;
        if (score >= 1900f) return FallingRank.SS;
        if (score >= 1500f) return FallingRank.S;
        if (score >= 1150f) return FallingRank.A;
        if (score >= 850f) return FallingRank.B;
        if (score >= 600f) return FallingRank.C;
        if (score >= 380f) return FallingRank.D;
        if (score >= 180f) return FallingRank.E;
        return FallingRank.F;
    }
}
