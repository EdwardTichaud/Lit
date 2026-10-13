using UnityEngine;

public sealed partial class AnimationEvents
{
    public void OpenSkillContactWindow(AnimationEvent source)
    {
        if (Preview(nameof(OpenSkillContactWindow))) return;
        var owner = Presentation;
        if (owner != null && owner.DebugContactMotion) Debug.Log($"[TargetMobility] Contact event | accepted={owner.AcceptAnimationEvent(source)} clip={source.animatorClipInfo.clip.name}", owner);
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleOpenSkillContactWindow();
    }
}
