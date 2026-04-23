using UnityEngine;

[CreateAssetMenu(fileName = "RestoreHealthEffect", menuName = "Scriptable Objects/Effects/Restore Health")]
public class RestoreHealthEffect : Effect
{
    [SerializeField, Min(1), Tooltip("PV rendus au personnage qui utilise l'item.")]
    private int amount = 3;

    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null || controller.CurrentHp >= controller.MaxHp)
        {
            return false;
        }

        int before = controller.CurrentHp;
        controller.SetCurrentHp(controller.CurrentHp + amount);
        return controller.CurrentHp > before;
    }
}
