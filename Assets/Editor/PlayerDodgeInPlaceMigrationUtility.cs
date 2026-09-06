/// <summary>Compatibility menu; preserves the existing impulse and all authored dodge profiles.</summary>
public static class PlayerDodgeInPlaceMigrationUtility
{
    [UnityEditor.MenuItem("Lit/Animation/Migrate Player Dodges To InPlace")]
    public static void Migrate() => PlayerInPlaceMigration.Migrate();
}
