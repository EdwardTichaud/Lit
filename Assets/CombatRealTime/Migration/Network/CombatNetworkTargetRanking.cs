namespace Lit.Combat.Migration.Network
{
    public static class CombatNetworkTargetRanking
    {
        // Smaller angular error first, then distance, then stable spawned identity.
        public static bool IsBetter(double angle, double distanceSquared, ulong id,
            double bestAngle, double bestDistanceSquared, ulong bestId) =>
            angle < bestAngle || angle == bestAngle &&
            (distanceSquared < bestDistanceSquared || distanceSquared == bestDistanceSquared && id < bestId);
    }
}
