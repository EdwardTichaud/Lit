using Unity.Netcode;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public enum CombatCommandRejection : byte
    {
        None, NotServer, UnregisteredActor, Ownership, Assignment, InvalidCommand,
        Replay, RateLimited, Dead, Busy, UnknownSkill, InvalidTarget, OutOfRange,
        Cooldown, ExecutorRejected, ExecutorFailed
    }

    public enum CombatActionEndReason : byte
    {
        Completed, Cancelled, Expired, Death, Despawn, Disconnect,
        OwnershipChanged, SceneChanged, AuthorityDisabled, ExecutorFailed
    }

    public enum CombatCommandTargetMode : byte { None, Required, Optional, Self, RequiredOrSelf }

    // A ticket identifies one action, not permission for a client to report damage.
    public readonly struct CombatActionTicket
    {
        public readonly ulong AuthorityEpoch;
        public readonly ulong ActorId;
        public readonly ulong Generation;

        public CombatActionTicket(ulong authorityEpoch, ulong actorId, ulong generation)
        {
            AuthorityEpoch = authorityEpoch;
            ActorId = actorId;
            Generation = generation;
        }
    }

    /// <summary>Server-authored immutable policy. No values are supplied by an RPC.</summary>
    public sealed class CombatCommandPolicy
    {
        public CombatCommandTargetMode TargetMode { get; }
        public float MinimumStartRange { get; }
        public float MaximumStartRange { get; }
        public float MaximumVerticalSeparation { get; }
        public float CooldownSeconds { get; }
        public float MaximumDurationSeconds { get; }
        public bool ReserveCinematicPair { get; }
        public int ImpactEventCount { get; }
        public float MaximumImpactRange { get; }

        public CombatCommandPolicy(CombatCommandTargetMode targetMode, float minimumStartRange,
            float maximumStartRange, float maximumVerticalSeparation, float cooldownSeconds,
            float maximumDurationSeconds, bool reserveCinematicPair = false,
            int impactEventCount = 1, float maximumImpactRange = 3f)
        {
            if (!Finite(minimumStartRange) || minimumStartRange < 0 ||
                !Finite(maximumStartRange) || maximumStartRange < minimumStartRange ||
                !Finite(maximumVerticalSeparation) || maximumVerticalSeparation < 0 ||
                !Finite(cooldownSeconds) || cooldownSeconds < 0 ||
                !Finite(maximumDurationSeconds) || maximumDurationSeconds <= 0 ||
                !Finite(maximumImpactRange) || maximumImpactRange < 0 ||
                impactEventCount < 0 || impactEventCount > 64 ||
                (targetMode != CombatCommandTargetMode.None && targetMode != CombatCommandTargetMode.Required &&
                 targetMode != CombatCommandTargetMode.Optional && targetMode != CombatCommandTargetMode.Self && targetMode != CombatCommandTargetMode.RequiredOrSelf) ||
                (reserveCinematicPair && targetMode != CombatCommandTargetMode.Required))
                throw new System.ArgumentOutOfRangeException(nameof(targetMode), "Invalid server combat policy.");
            TargetMode = targetMode;
            MinimumStartRange = minimumStartRange;
            MaximumStartRange = maximumStartRange;
            MaximumVerticalSeparation = maximumVerticalSeparation;
            CooldownSeconds = cooldownSeconds;
            MaximumDurationSeconds = maximumDurationSeconds;
            ReserveCinematicPair = reserveCinematicPair;
            ImpactEventCount = impactEventCount;
            MaximumImpactRange = maximumImpactRange;
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }

    public readonly struct CombatCommandContext
    {
        public readonly CombatActionTicket Ticket;
        public readonly NetworkObject Actor;
        public readonly NetworkObject Target;
        public readonly ScriptableObject Skill;
        public readonly int SkillId;
        public readonly Vector3 Direction;

        internal CombatCommandContext(CombatActionTicket ticket, NetworkObject actor, NetworkObject target,
            ScriptableObject skill, int skillId, Vector3 direction)
        {
            Ticket = ticket;
            Actor = actor;
            Target = target;
            Skill = skill;
            SkillId = skillId;
            Direction = direction;
        }
    }

    /// <summary>Trusted server adapter for a specific actor. Never rebind the local combat singleton.</summary>
    public interface ICombatCommandExecutor
    {
        // Check live loadout, actor state, resources, relationship/LOS and authoritative lock.
        // Validation must have no side effects. Missing support must return false.
        bool CanStart(CombatCommandContext context);
        // Start actor-scoped execution. Do not apply an impact synchronously here.
        bool TryStart(CombatCommandContext context);
        // Mandatory server timeline/contact/shape check, including free-attack target eligibility.
        bool CanApplyImpact(CombatCommandContext context, NetworkObject target, int impactEvent);
        // Called once after ticket invalidation and reservation release, including failed starts.
        void End(CombatCommandContext context, CombatActionEndReason reason);
    }

    public interface ICombatCommandReplacementExecutor
    {
        // Pure preflight while the outgoing action still owns its resources.
        bool CanReplace(CombatCommandContext current, CombatCommandContext next);
    }
}
