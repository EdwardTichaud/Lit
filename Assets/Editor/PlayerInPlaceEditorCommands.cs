using UnityEditor;

/// <summary>Explicit refresh for automated Editor validation after changing migration code.</summary>
public static class PlayerInPlaceEditorCommands
{
    [MenuItem("Lit/Animation/Refresh Migration Scripts")]
    public static void RefreshScripts()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
    }
}
