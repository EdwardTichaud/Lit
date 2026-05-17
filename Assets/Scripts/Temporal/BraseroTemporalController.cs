// Role:
// Light bridge between an existing Brasero interaction and a TemporalZone.
// Usage:
// Attach near a Brasero or on a zone control object when a brasero should change
// the dominant temporal age of a zone.
// Responsibilities:
// Resolve optional references, listen to Brasero state changes, and step the zone.
// Dependencies:
// Brasero, TemporalZone.
// Precautions:
// This script must not replace the existing Brasero behaviour; it only adds a
// temporal-age response on top of it.
using UnityEngine;

/// <summary>
/// Connects a brasero activation to a temporal zone age change.
/// </summary>
[DisallowMultipleComponent]
public class BraseroTemporalController : MonoBehaviour
{
    /// <summary>
    /// Defines how this controller changes the target zone when activated.
    /// </summary>
    public enum ActivationMode
    {
        Advance = 0,
        Rewind = 1,
        ToggleDirection = 2
    }

    [Header("References")]
    /// <summary>Zone whose dominant age will be changed.</summary>
    [SerializeField] private TemporalZone targetZone;
    /// <summary>Optional existing brasero used as the trigger source.</summary>
    [SerializeField, Tooltip("Brasero existant optionnel. Laisse l'interaction actuelle piloter le feu.")]
    private Brasero linkedBrasero;
    /// <summary>If true, searches parent objects for a TemporalZone.</summary>
    [SerializeField] private bool autoFindZoneInParents = true;
    /// <summary>If true, searches this GameObject for a Brasero.</summary>
    [SerializeField] private bool autoFindBraseroOnSelf = true;

    [Header("Activation")]
    /// <summary>Age stepping mode used when activation occurs.</summary>
    [SerializeField] private ActivationMode activationMode = ActivationMode.Advance;
    /// <summary>If true, ignores brasero extinguish events.</summary>
    [SerializeField, Tooltip("Si lie a un Brasero, avance seulement quand il passe allume.")]
    private bool triggerOnlyWhenLit = true;
    /// <summary>Direction used by the next ToggleDirection activation.</summary>
    [SerializeField, Tooltip("Direction initiale quand ActivationMode = ToggleDirection.")]
    private bool nextToggleAdvances = true;

    /// <summary>Current zone controlled by this bridge.</summary>
    public TemporalZone TargetZone => targetZone;
    /// <summary>Optional brasero that triggers this bridge.</summary>
    public Brasero LinkedBrasero => linkedBrasero;

    private void OnEnable()
    {
        // Unity calls OnEnable when the component becomes active.
        // Resolve references before subscribing so scene setup can stay lightweight.
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        // Always unsubscribe when disabled to avoid calling into destroyed objects.
        Unsubscribe();
    }

    private void OnValidate()
    {
        // Keeps inspector previews convenient without requiring Play Mode.
        ResolveReferences();
    }

    /// <summary>
    /// Applies the configured activation mode to the target zone.
    /// </summary>
    [ContextMenu("Activate")]
    public void Activate()
    {
        if (targetZone == null)
        {
            return;
        }

        switch (activationMode)
        {
            case ActivationMode.Rewind:
                targetZone.StepBackward();
                break;
            case ActivationMode.ToggleDirection:
                if (nextToggleAdvances)
                {
                    targetZone.StepForward();
                }
                else
                {
                    targetZone.StepBackward();
                }

                nextToggleAdvances = !nextToggleAdvances;
                break;
            case ActivationMode.Advance:
            default:
                targetZone.StepForward();
                break;
        }
    }

    /// <summary>
    /// Moves the target zone forward by one temporal step.
    /// </summary>
    public void AdvanceZone()
    {
        if (targetZone != null)
        {
            targetZone.StepForward();
        }
    }

    /// <summary>
    /// Moves the target zone backward by one temporal step.
    /// </summary>
    public void RewindZone()
    {
        if (targetZone != null)
        {
            targetZone.StepBackward();
        }
    }

    private void ResolveReferences()
    {
        if (targetZone == null && autoFindZoneInParents)
        {
            targetZone = GetComponentInParent<TemporalZone>(true);
        }

        if (linkedBrasero == null && autoFindBraseroOnSelf)
        {
            linkedBrasero = GetComponent<Brasero>();
        }
    }

    private void Subscribe()
    {
        if (linkedBrasero != null)
        {
            linkedBrasero.StateChanged += OnBraseroStateChanged;
        }
    }

    private void Unsubscribe()
    {
        if (linkedBrasero != null)
        {
            linkedBrasero.StateChanged -= OnBraseroStateChanged;
        }
    }

    private void OnBraseroStateChanged(Brasero brasero, bool isLit)
    {
        // Existing Brasero fires both lit/unlit state changes.
        // Some zones should react only to the moment the player lights the brasero.
        if (triggerOnlyWhenLit && !isLit)
        {
            return;
        }

        Activate();
    }
}
