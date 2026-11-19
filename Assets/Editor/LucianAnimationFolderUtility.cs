/// <summary>Compatibility entry point for the former Lucian-only folder organizer.</summary>
public static class LucianAnimationFolderUtility
{
    public const string Destination = "Assets/Animations";
    public static void Organize() => AnimationLibraryAssets.ValidateOrganization();
}
