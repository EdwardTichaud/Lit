using UnityEditor;

/// <summary>Compatibility entry points; Lucian uses the single whole-body contract.</summary>
public static class ActorAnimationContractValidator
{
    [MenuItem("Lit/Animation Presentation/Validate Actor Contract")]
    public static void Validate() => SimpleLucianAnimationSetup.Validate();
    public static void ValidateTechnical() => SimpleLucianAnimationSetup.Validate();
}
