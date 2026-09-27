/// <summary>
/// Names shared by every runtime combat enemy animator.  Controllers may use
/// different clips and state machines, but these entry points stay stable for
/// EnemyController and SkillSO.
/// </summary>
public static class EnemyAnimatorContract
{
    public const string CombatIdle = "CombatIdle";
    public const string Hit = "Hit";
    public const string Death = "Death";

    public static readonly string[] RequiredParameters =
    {
        "HorizontalMovement",
        "ForwardMovement",
        "LocomotionTier",
        "CombatStrafeActive",
        "CombatMoveMagnitude"
    };
}
