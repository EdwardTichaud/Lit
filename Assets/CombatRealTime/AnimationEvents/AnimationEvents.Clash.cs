using UnityEngine;

public sealed partial class AnimationEvents
{
    public void OpenAttackClashWindow(AnimationEvent source) => ResolveLocal<CombatLabClashActor>()?.SetWindow(source, true);
    public void CloseAttackClashWindow(AnimationEvent source) => ResolveLocal<CombatLabClashActor>()?.SetWindow(source, false);
}
