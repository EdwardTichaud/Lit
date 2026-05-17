using UnityEngine;

[DisallowMultipleComponent]
public class BraseroTemporalController : MonoBehaviour
{
    public enum ActivationMode
    {
        Advance = 0,
        Rewind = 1,
        ToggleDirection = 2
    }

    [Header("References")]
    [SerializeField] private TemporalZone targetZone;
    [SerializeField, Tooltip("Brasero existant optionnel. Laisse l'interaction actuelle piloter le feu.")]
    private Brasero linkedBrasero;
    [SerializeField] private bool autoFindZoneInParents = true;
    [SerializeField] private bool autoFindBraseroOnSelf = true;

    [Header("Activation")]
    [SerializeField] private ActivationMode activationMode = ActivationMode.Advance;
    [SerializeField, Tooltip("Si lie a un Brasero, avance seulement quand il passe allume.")]
    private bool triggerOnlyWhenLit = true;
    [SerializeField, Tooltip("Direction initiale quand ActivationMode = ToggleDirection.")]
    private bool nextToggleAdvances = true;

    public TemporalZone TargetZone => targetZone;
    public Brasero LinkedBrasero => linkedBrasero;

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

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

    public void AdvanceZone()
    {
        if (targetZone != null)
        {
            targetZone.StepForward();
        }
    }

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
        if (triggerOnlyWhenLit && !isLit)
        {
            return;
        }

        Activate();
    }
}
