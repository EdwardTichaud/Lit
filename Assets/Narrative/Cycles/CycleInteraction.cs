using UnityEngine;

/// <summary>Scene binding only. GhostController owns reveal, outline and interaction detection.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GhostController))]
public sealed class CycleInteraction : MonoBehaviour, IGhostInteractionHandler
{
    public CycleController cycle;
    public string dialogueId;
    public GhostController Ghost => GetComponent<GhostController>();
    public bool Interact(GhostController ghost) => isActiveAndEnabled && ghost == Ghost &&
        cycle != null && cycle.Interact(this);
}
