using UnityEngine;

/// <summary>
/// Marque appliquée par l'unité Link pour cibler plusieurs ennemis.
/// </summary>
public class LinkMark : MonoBehaviour
{
    private int remainingTurns = -1;

    public void ApplyDuration(int turns)
    {
        remainingTurns = turns;
    }

    public void Tick()
    {
        if (remainingTurns < 0)
            return;
        remainingTurns -= 1;
        if (remainingTurns <= 0)
            Destroy(this);
    }
}
