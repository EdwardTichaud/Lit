using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        private readonly NetworkVariable<int> floatSkill = new NetworkVariable<int>();
        private int remoteFloatState;

        private void TickFloatPresentation()
        {
            if (presentation == null || animator == null) return;
            if (IsServer)
            {
                var skill = presentation.PassiveChargedFloatSkill;
                floatSkill.Value = skill != null && catalogValid && catalog.TryGet(skill, out var entry) ? entry.Id : 0;
                return;
            }
            if (presentation.IsActionActive || !animator.isActiveAndEnabled ||
                CombatCinematicReservations.ContainsParticipant(transform)) return;
            if (floatSkill.Value != 0 && catalogValid && catalog.TryGet(floatSkill.Value, out var current) && current.skill is ChargedSkillSO charged)
            {
                int state = Animator.StringToHash(charged.playerFloatState);
                if (!animator.HasState(0, state)) return;
                remoteFloatState = state;
                if (!ShowingState(state))
                {
                    stateMotion?.SetActionPolicy(state, PlayerActionMovementPolicy.VisualOnly);
                    animator.CrossFadeInFixedTime(state, .08f, 0, 0);
                }
            }
            else if (remoteFloatState != 0)
            {
                bool ownPose = ShowingState(remoteFloatState);
                remoteFloatState = 0;
                int falling = Animator.StringToHash("Base Layer.Falling");
                if (ownPose && animator.HasState(0, falling)) animator.CrossFadeInFixedTime(falling, .08f, 0, 0);
            }
        }

        private bool ShowingState(int state) => animator.GetCurrentAnimatorStateInfo(0).fullPathHash == state ||
            animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == state;
    }
}
