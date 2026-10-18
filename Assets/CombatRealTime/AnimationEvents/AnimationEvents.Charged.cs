using UnityEngine;

public sealed partial class AnimationEvents
{
    public void ChargedSkillContact(AnimationEvent source)
    {
        if (Preview(nameof(ChargedSkillContact))) return;
        var owner = Presentation;
        if (owner != null && owner.AcceptAnimationEvent(source)) owner.HandleChargedSkillContact(source.intParameter);
    }
}
