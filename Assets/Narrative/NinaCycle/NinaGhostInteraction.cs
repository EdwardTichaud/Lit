using UnityEngine;

/// <summary>Optional adapter; ordinary ghosts keep their existing conversation flow.</summary>
public sealed class NinaGhostInteraction : MonoBehaviour, IGhostInteractionAvailability
{
    public NinaCycleController cycle;
    public bool isScar;
    public bool Interact(GhostController ghost) => cycle != null && cycle.Interact(ghost, isScar);

    public bool TryEvaluateGhostReveal(GhostController ghost, SquadCharacterController controller, out float distance01)
    {
        distance01 = 0f;
        // Nina reste ecoutable avant le dilemme et Scar reste controle par son
        // activation narrative, mais tous deux suivent la revelation visuelle
        // standard : invisibles et non interactifs hors de proximite.
        return ghost != null && ghost.isActiveAndEnabled && controller != null &&
               gameObject.activeInHierarchy && controller.CurrentHp > 0 &&
               ghost.TryEvaluateDefaultProximityReveal(controller, out distance01);
    }

    public bool AllowsGhostRuntimeOutline(GhostController ghost)
    {
        return ghost != null && gameObject.activeInHierarchy && ghost.AllowsDefaultRuntimeOutline;
    }
}
