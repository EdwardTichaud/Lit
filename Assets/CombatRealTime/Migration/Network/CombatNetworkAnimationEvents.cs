using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    /// <summary>Actor-local native Animator events; there are deliberately no impact RPCs.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatNetworkAnimationEvents : MonoBehaviour
    {
        private CombatNetworkActorRuntime owner;
        internal bool Bind(CombatNetworkActorRuntime actor)
        {
            if (owner != null && owner != actor) return false;
            owner = actor; return true;
        }
        internal void Unbind(CombatNetworkActorRuntime actor) { if (owner == actor) owner = null; }
        internal bool IsBoundTo(CombatNetworkActorRuntime actor) => owner == actor;
        public void HitEnemy(AnimationEvent source) => owner?.OnAnimationImpact(source);
        public void ResolveSkillImpact(AnimationEvent source) => owner?.OnAnimationImpact(source);
        public void ResolveSkillImpactAndRetreat(AnimationEvent source) => owner?.OnAnimationImpact(source);
        public void ChargedSkillContact(AnimationEvent source) => owner?.OnAnimationImpact(source);
        public void ResolveCounterSkillImpact(AnimationEvent source) => owner?.TryHandleNetworkCinematicImpact(source);
        public void ResolveLightSkillImpact(AnimationEvent source) => owner?.TryHandleNetworkCinematicImpact(source);
        public void ResolveCinematicSkillImpact(AnimationEvent source) => owner?.TryHandleNetworkCinematicImpact(source);
        public void OpenSkillContactWindow(AnimationEvent source) => owner?.OnAnimationContactWindow(source);
        public void ShowSword(AnimationEvent source) => owner?.OnWeaponEvent(source, true);
        public void HideSword(AnimationEvent source) => owner?.OnWeaponEvent(source, false);
        public void HideSwordWhenComboEnds(AnimationEvent source) => owner?.OnWeaponEvent(source, false);
        public void ShowBow(AnimationEvent source) => owner?.OnBowEvent(source, true);
        public void HideBow(AnimationEvent source) => owner?.OnBowEvent(source, false);
        public void InstantiateSkillVFX(AnimationEvent source) => owner?.OnVfxEvent(source, -1);
        public void InstantiateSkillVFXAtIndex(AnimationEvent source) => owner?.OnVfxEvent(source, source.intParameter);
    }
}
