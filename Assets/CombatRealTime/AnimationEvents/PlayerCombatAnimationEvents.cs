using UnityEngine;

// Animation clip receiver only; gameplay and presentation belong to their services.
[DisallowMultipleComponent]
public sealed class PlayerCombatAnimationEvents : MonoBehaviour
{
    private PlayerActionPresentationController presentation;
    private PlayerActionPresentationController Presentation
    {
        get
        {
            if (presentation == null) presentation = GetComponentInParent<PlayerActionPresentationController>();
            return presentation != null && presentation.isActiveAndEnabled ? presentation : null;
        }
    }
    public void QTE(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleQTE(source.stringParameter);
    }
    public void ResolveLightSkillImpact(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveLightSkillImpact();
    }
    public void ResolveCounterSkillImpact(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveCounterSkillImpact();
    }
    public void ResolveCinematicSkillImpact(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveCinematicSkillImpact();
    }
    public void InstantiateSkillVFX(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleInstantiateSkillVFX();
    }
    public void InstantiateSkillVFXAtIndex(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleInstantiateSkillVFXAtIndex(source.intParameter);
    }
    public void ShowBow(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleShowBow();
    }
    public void HideBow(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleHideBow();
    }
    public void ShowSword(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleShowSword();
    }
    public void HideSword(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleHideSword();
    }
    public void HideSwordWhenComboEnds(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleHideSwordWhenComboEnds();
    }
    public void Dash(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleDash();
    }
    public void StopDash(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleStopDash();
    }
    public void HitEnemy(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleHitEnemy();
    }
    public void ResolveSkillImpact(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveSkillImpact();
    }
    public void ResolveSkillImpactAndRetreat(AnimationEvent source)
    {
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveSkillImpactAndRetreat();
    }
}
