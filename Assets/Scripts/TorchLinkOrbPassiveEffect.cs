using UnityEngine;

[CreateAssetMenu(fileName = "TorchLinkOrbPassiveEffect", menuName = "Scriptable Objects/Effects/Passive Torch Link Orb")]
// Effet legacy desactive. La liaison de torches est maintenant geree par TorchColorLinkSystem.
public class TorchLinkOrbPassiveEffect : Effect, IItemPassiveEffect
{
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return false;
    }

    public void Tick(ItemPassiveContext context)
    {
    }
}
