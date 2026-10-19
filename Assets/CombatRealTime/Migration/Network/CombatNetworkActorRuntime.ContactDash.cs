using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        private bool dashWindowArmed;
        private NetworkObject contactVictim;

        internal static bool TryResolveContactDashDistance(FreeAttackProfile profile, out float distance)
        {
            distance = 0;
            if (profile == null || !profile.enabled || !CombatCommandPolicy.Finite(profile.freeDashDistance) ||
                profile.freeDashDistance <= 0 || profile.freeDashDistance > 128f) return false;
            distance = profile.freeDashDistance;
            return true;
        }

        private bool TryGetWorldContactDashDistance(out float distance)
        {
            distance = 0;
            return combatWorld != null && combatWorld.isActiveAndEnabled && TryResolveContactDashDistance(combatWorld.freeAttacks, out distance);
        }

        private PlayerAttackContext CaptureDashContext(CombatCommandContext context, SkillSO skill, int session)
        {
            var target = ResolveEnemy(context.Target);
            Vector3 direction = target?.Root != null ? Vector3.ProjectOnPlane(target.Root.position - transform.position, Vector3.up).normalized : context.Direction;
            if (direction.sqrMagnitude < .0001f) direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            return new PlayerAttackContext(session, skill, transform.position, direction, target, false);
        }

        private bool CanStartContactDash(CombatCommandContext context, CombatNetworkSkillCatalog.Entry entry)
        {
            if (!TryGetWorldContactDashDistance(out float freeDashDistance))
                return Diagnose("Dash requires an enabled world FreeAttackProfile with a finite positive distance within the supported 128 m route bound.");
            if (!bridge.Grounded || !CombatCommandPolicy.Finite(transform.position) || bridge.ScriptedMotionColliders.Length == 0)
                return Diagnose("Contact dash requires a grounded actor with authored UCC contact colliders.");
            if (context.Target != null && (!EnemyEligible(context.Target, entry, false) ||
                !entry.skill.IsWithinLaunchRange(Vector3.ProjectOnPlane(context.Target.transform.position - transform.position, Vector3.up).magnitude)))
                return Diagnose("Captured dash target is unavailable or outside the authored launch range.");
            var attack = CaptureDashContext(context, entry.skill, 0);
            if (!CombatCommandPolicy.Finite(attack.Direction) || attack.Direction.sqrMagnitude < .0001f) return false;
            float travel = presentation.GetSafeAttackTravel(entry.skill, attack, freeDashDistance);
            return CombatCommandPolicy.Finite(travel) && travel > .02f || Diagnose("Dash has no finite, safe ground route.");
        }

        internal void OnAnimationContactWindow(AnimationEvent source)
        {
            if (!IsServer || !hasAction || activeFamily != CombatNetworkSkillFamily.ContactDash || dashWindowArmed ||
                !AcceptActorEvent(source) || !authority.IsCurrent(active.Ticket) || source.functionName != "OpenSkillContactWindow" ||
                !CombatNetworkSkillCatalog.TryGetContactWindow(activeEntry.skill, out float expected, out _) || Mathf.Abs(source.time - expected) > .001f) return;
            dashWindowArmed = true;
            presentation.HandleOpenSkillContactWindow();
        }

        private bool ResolveDashContact(CombatActionTicket ticket, int session, Transform targetRoot, Vector3 point, Vector3 direction)
        {
            if (!IsServer || !hasAction || activeFamily != CombatNetworkSkillFamily.ContactDash || !SameTicket(ticket, active.Ticket) ||
                !dashWindowArmed || basicImpactResolved || session != presentationSession || !presentation.IsCurrentSession(session) ||
                !authority.IsCurrent(ticket) || !CombatCommandPolicy.Finite(point) || !CombatCommandPolicy.Finite(direction) || targetRoot == null) return false;
            var identity = targetRoot.GetComponentInParent<NetworkObject>();
            var target = ResolveEnemy(identity);
            if (target?.Root != targetRoot || active.Target != null && active.Target != identity || !EnemyEligible(identity, activeEntry, false) ||
                !activeEntry.skill.IsWithinHitRange(Vector3.ProjectOnPlane(targetRoot.position - point, Vector3.up).magnitude)) return false;
            contactVictim = identity;
            serverEventActive = true;
            try
            {
                if (!authority.TryConsumeImpact(ticket, identity)) return false;
                basicImpactResolved = true;
                DeliverBasicImpact(identity, contactPosition: point);
                return true;
            }
            finally
            {
                if (SameTicket(ticket, active.Ticket)) { serverEventActive = false; contactVictim = null; }
            }
        }
    }
}
