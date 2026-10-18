using System;
using Lit.Combat.Migration.Network;

public sealed partial class RealTimeCombatManager
{
    private ICombatTarget cinematicTarget;
    private object cinematicTargetOwner;
    private CombatCinematicReservationLease cinematicReservation;
    private Action cinematicReservationLost;

    public bool TryBeginCinematicTarget(object owner, ICombatTarget target, Action interrupted)
    {
        if (cinematicReservation != null || playerRoot == null || !SkillTargetingContract.Alive(target) ||
            !CombatCinematicReservations.TryAcquire(owner, playerRoot, target, out var lease)) return false;
        cinematicTargetOwner = owner;
        cinematicTarget = target;
        cinematicReservation = lease;
        cinematicReservationLost = interrupted;
        lease.Released += OnCinematicReservationLost;
        return true;
    }

    public void EndCinematicTarget(object owner)
    {
        if (!ReferenceEquals(cinematicTargetOwner, owner)) return;
        var lease = cinematicReservation;
        cinematicReservation = null; cinematicTarget = null;
        cinematicTargetOwner = null; cinematicReservationLost = null;
        if (lease != null) { lease.Released -= OnCinematicReservationLost; lease.Release(); }
    }

    private void OnCinematicReservationLost()
    {
        var callback = cinematicReservationLost;
        EndCinematicTarget(cinematicTargetOwner);
        callback?.Invoke();
    }
}
