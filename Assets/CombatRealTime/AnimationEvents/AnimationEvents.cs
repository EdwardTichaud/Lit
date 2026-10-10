using UnityEngine;

// Animation clip receiver only; gameplay and presentation belong to their services.
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, null, "PlayerCombatAnimationEvents")]
[DisallowMultipleComponent]
public sealed partial class AnimationEvents : MonoBehaviour
{
    public static AnimationEvents EnsureOn(GameObject owner)
    {
        return owner.GetComponent<AnimationEvents>() ?? owner.AddComponent<AnimationEvents>();
    }
    [SerializeField] private PlayerActionPresentationController presentation;
    private PlayerActionPresentationController Presentation
    {
        get
        {
            if (!Application.isPlaying || previewOnly) return null;
            var owner = actorOwner == null && signalTarget == null && GetComponent<UnityEngine.Playables.PlayableDirector>() == null && presentation != null
                ? presentation : ResolveLocal<PlayerActionPresentationController>();
            return owner != null && owner.isActiveAndEnabled ? owner : null;
        }
    }
    public void QTE(AnimationEvent source)
    {
        if (Preview(nameof(QTE))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleQTE(source != null ? source.stringParameter : string.Empty);
        else if (Application.isPlaying && !previewOnly) ResolveLocal<EnemyController>()?.HandleQTE(source != null ? source.stringParameter : string.Empty);
    }
    public void ResolveLightSkillImpact(AnimationEvent source)
    {
        if (Preview(nameof(ResolveLightSkillImpact))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveLightSkillImpact();
    }
    public void ResolveCounterSkillImpact(AnimationEvent source)
    {
        if (Preview(nameof(ResolveCounterSkillImpact))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveCounterSkillImpact();
    }
    public void ResolveCinematicSkillImpact(AnimationEvent source)
    {
        if (Preview(nameof(ResolveCinematicSkillImpact))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveCinematicSkillImpact();
    }
    public void InstantiateSkillVFX(AnimationEvent source)
    {
        if (Preview(nameof(InstantiateSkillVFX))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleInstantiateSkillVFX();
    }
    public void InstantiateSkillVFXAtIndex(AnimationEvent source)
    {
        if (Preview(nameof(InstantiateSkillVFXAtIndex))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleInstantiateSkillVFXAtIndex(source.intParameter);
    }
    public void ShowBow(AnimationEvent source)
    {
        if (Preview(nameof(ShowBow))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleShowBow();
    }
    public void HideBow(AnimationEvent source)
    {
        if (Preview(nameof(HideBow))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleHideBow();
    }
    public void ShowSword(AnimationEvent source)
    {
        if (Preview(nameof(ShowSword))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleShowSword();
    }
    public void HideSword(AnimationEvent source)
    {
        if (Preview(nameof(HideSword))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleHideSword();
    }
    public void HideSwordWhenComboEnds(AnimationEvent source)
    {
        if (Preview(nameof(HideSwordWhenComboEnds))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleHideSwordWhenComboEnds();
    }
    public void Dash(AnimationEvent source)
    {
        if (Preview(nameof(Dash))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleDash();
    }
    public void StopDash(AnimationEvent source)
    {
        if (Preview(nameof(StopDash))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleStopDash();
    }
    public void HitEnemy(AnimationEvent source)
    {
        if (Preview(nameof(HitEnemy))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleHitEnemy();
    }
    public void ResolveSkillImpact(AnimationEvent source)
    {
        if (Preview(nameof(ResolveSkillImpact))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveSkillImpact();
    }
    public void ResolveSkillImpactAndRetreat(AnimationEvent source)
    {
        if (Preview(nameof(ResolveSkillImpactAndRetreat))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleResolveSkillImpactAndRetreat();
    }
}
