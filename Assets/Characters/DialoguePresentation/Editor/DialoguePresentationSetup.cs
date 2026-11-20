using System;
using UnityEditor;

public static class DialoguePresentationSetup
{
    public const string Folder = "Assets/Animations/Autres/Dialogue/Lucian";
    public const string ProfilePath = Folder + "/Lucian_DialoguePresentation.asset";

    [MenuItem("Lit/Animation/Install Lucian Dialogue Presentation")]
    public static void Install() => SimpleLucianAnimationSetup.Install();

    public static bool IsFace(string property) =>
        property.IndexOf("Jaw", StringComparison.OrdinalIgnoreCase) >= 0 ||
        property.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0 ||
        property.StartsWith("blendShape.", StringComparison.Ordinal);
}
