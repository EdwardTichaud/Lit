using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public sealed class PlayerInPlaceBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;
    public void OnPreprocessBuild(BuildReport report)
    {
        try { PlayerInPlaceMigration.Validate(); }
        catch (System.Exception exception) { throw new BuildFailedException("Player InPlace contract: " + exception.Message); }
    }
}
