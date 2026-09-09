using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class EnemyUnificationMigration
{
    [MenuItem("Tools/Lit/Enemies/Migrate legacy enemy resources")]
    public static async void Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            UnityEngine.Debug.LogWarning("Terminer le mode Play et la compilation avant la migration.");
            return;
        }
        var start = new ProcessStartInfo("python", "Tools/EnemyUnification/migrate_resources.py")
        {
            WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        AssetDatabase.DisallowAutoRefresh();
        try
        {
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                if (process.ExitCode != 0) UnityEngine.Debug.LogError(await error);
                else UnityEngine.Debug.Log("Migration ennemie terminee.\n" + await output);
            }
        }
        catch (System.Exception exception) { UnityEngine.Debug.LogException(exception); }
        finally { AssetDatabase.AllowAutoRefresh(); AssetDatabase.Refresh(); }
    }
}
