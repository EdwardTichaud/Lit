using UnityEngine;

// Role: effet d'item qui rend des PV a un personnage de squad.
// Usage: assigne a un Item via ScriptableObject d'effet.
// Responsibilities: verifier que le soin est utile, appliquer les PV, jouer le feedback audio.
// Dependencies: Effect, SquadCharacterController, AudioManager.
// Precautions: ne pas changer la signature Apply; elle est appelee par le systeme d'items.
/// <summary>
/// Effet ScriptableObject qui restaure une quantite fixe de points de vie.
/// </summary>
[CreateAssetMenu(fileName = "RestoreHealthEffect", menuName = "Scriptable Objects/Effects/Restore Health")]
public class RestoreHealthEffect : Effect
{
    /// <summary>
    /// Quantite de PV rendue quand l'effet est applique.
    /// </summary>
    [SerializeField, Min(1), Tooltip("PV rendus au personnage qui utilise l'item.")]
    private int amount = 3;

    /// <summary>
    /// Applique le soin au personnage cible si ses PV ne sont pas deja au maximum.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null || controller.CurrentHp >= controller.MaxHp)
        {
            return false;
        }

        int before = controller.CurrentHp;
        controller.SetCurrentHp(controller.CurrentHp + amount);
        bool restored = controller.CurrentHp > before;
        if (restored)
        {
            AudioManager.EnsureInstance()?.PlayActionCue(ActionAudioCue.CharacterHeal, controller.transform.position);
        }

        return restored;
    }
}
