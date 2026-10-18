using UnityEngine;

/// <summary>Receives translated production motion events on the actor's Animator object.</summary>
[DisallowMultipleComponent]
public sealed class LitBrainsAnimationEvents : MonoBehaviour
{
    private LitBrainsEnemy Owner => GetComponent<LitBrainsEnemy>() ?? GetComponentInParent<LitBrainsEnemy>();

    public void ProductionBrainsBeginAdvance(AnimationEvent source) => Owner?.HandleProductionBrainsBeginAdvance(source);
    public void ProductionBrainsEndAdvance(AnimationEvent source) => Owner?.HandleProductionBrainsEndAdvance(source);
    public void ProductionBrainsBeginAirborne(AnimationEvent source) => Owner?.HandleProductionBrainsBeginAirborne(source);
    public void ProductionBrainsRequestLanding(AnimationEvent source) => Owner?.HandleProductionBrainsRequestLanding(source);
    public void ProductionBrainsBeginRush(AnimationEvent source) => Owner?.HandleProductionBrainsBeginRush(source);
    public void ProductionBrainsEndRush(AnimationEvent source) => Owner?.HandleProductionBrainsEndRush(source);
}
