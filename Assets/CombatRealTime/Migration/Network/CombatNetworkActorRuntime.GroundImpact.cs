using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        private Vector3 groundContactPoint;

        private bool GroundAreaEligible(NetworkObject identity)
        {
            if (!EnemyEligible(identity, activeEntry, false)) return false;
            var delta = identity.transform.position - groundContactPoint;
            return Mathf.Abs(delta.y) <= 3 && Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude <=
                activeEntry.skill.groundImpact.radius * activeEntry.skill.groundImpact.radius;
        }

        private void ResolveServerGroundImpact(SkillSO skill, bool airborne, Vector3 point)
        {
            if (!IsServer || !hasAction || activeFamily != CombatNetworkSkillFamily.GroundImpact ||
                activeEntry.skill != skill || !authority.IsCurrent(active.Ticket) || !CombatCommandPolicy.Finite(point) ||
                !presentation.ConsumeGroundImpact(skill)) return;
            groundContactPoint = point;
            basicImpactResolved = true;
            serverEventActive = true;
            var ticket = active.Ticket;
            int damage = Mathf.RoundToInt(skill.Damages * (airborne ? skill.groundImpact.airborneDamageMultiplier : 1));
            float push = airborne ? skill.groundImpact.airbornePushDistance : skill.groundImpact.groundPushDistance;
            authority.CopyRegisteredActors(candidates);
            var victims = candidates.ToArray();
            try
            {
                foreach (var candidate in victims)
                {
                    if (!hasAction || !SameTicket(ticket, active.Ticket) || !GroundAreaEligible(candidate) ||
                        !authority.TryConsumeImpact(ticket, candidate) || DeliverBasicImpact(candidate, damage) <= 0) continue;
                    Vector3 direction = Vector3.ProjectOnPlane(candidate.transform.position - point, Vector3.up).normalized;
                    if (direction.sqrMagnitude < .0001f) direction = active.Direction;
                    candidate.GetComponent<LitBrainsEnemy>()?.StartGroundImpactPush(direction, push);
                    candidate.GetComponent<EnemyController>()?.StartGroundImpactPush(direction, push);
                }
            }
            finally { if (hasAction && SameTicket(ticket, active.Ticket)) serverEventActive = false; }
        }
    }
}
