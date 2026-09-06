using UnityEngine;

/// <summary>Optional adapter; ordinary ghosts keep their existing conversation flow.</summary>
public sealed class NinaGhostInteraction : MonoBehaviour
{
    public NinaCycleController cycle;
    public bool isScar;
    public bool Interact(GhostController ghost) => cycle != null && cycle.Interact(ghost, isScar);
}
