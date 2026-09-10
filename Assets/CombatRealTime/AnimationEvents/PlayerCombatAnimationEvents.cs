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
    public void QTE(string input) => Presentation?.HandleQTE(input);
    public void ResolveLightSkillImpact() => Presentation?.HandleResolveLightSkillImpact();
    public void ResolveCounterSkillImpact() => Presentation?.HandleResolveCounterSkillImpact();
    public void ResolveCinematicSkillImpact() => Presentation?.HandleResolveCinematicSkillImpact();
    public void InstantiateSkillVFX() => Presentation?.HandleInstantiateSkillVFX();
    public void InstantiateSkillVFXAtIndex(int cueIndex) => Presentation?.HandleInstantiateSkillVFXAtIndex(cueIndex);
    public void ShowBow() => Presentation?.HandleShowBow();
    public void HideBow() => Presentation?.HandleHideBow();
    public void ShowSword() => Presentation?.HandleShowSword();
    public void HideSword() => Presentation?.HandleHideSword();
    public void HideSwordWhenComboEnds() => Presentation?.HandleHideSwordWhenComboEnds();
    public void Dash() => Presentation?.HandleDash();
    public void StopDash() => Presentation?.HandleStopDash();
    public void HitEnemy() => Presentation?.HandleHitEnemy();
    public void ResolveSkillImpact() => Presentation?.HandleResolveSkillImpact();
    public void ResolveSkillImpactAndRetreat() => Presentation?.HandleResolveSkillImpactAndRetreat();
}
