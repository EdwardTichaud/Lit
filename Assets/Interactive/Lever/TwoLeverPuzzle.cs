using UnityEngine;

// Puzzle simple: deux leviers doivent etre actifs pour declencher un pivot.
[DisallowMultipleComponent]
public class TwoLeverPuzzle : MonoBehaviour, ILeverTarget
{
    [Header("Levers")]
    [Tooltip("Levier A du puzzle.")]
    public Lever leverA;
    [Tooltip("Levier B du puzzle.")]
    public Lever leverB;
    [SerializeField, Tooltip("Ecoute directement Lever.StateChanged. Desactive si tu relies ce puzzle via Lever.targetBindings.")]
    private bool subscribeToLeverEvents = true;

    [Header("Target")]
    [Tooltip("Script Pivot declenche quand les deux leviers sont actifs.")]
    public Pivot pivotTarget;

    [Header("Audio")]
    [Tooltip("SFX de reussite.")]
    public AudioClipSO successSfx;

    [Header("Behavior")]
    [Tooltip("Si true, le puzzle ne peut se declencher qu'une fois.")]
    public bool playOnce = true;
    [SerializeField, Tooltip("Active des logs de diagnostic pour le puzzle.")]
    private bool logDebug;

    [Header("State")]
    [SerializeField, Tooltip("Etat du levier A (debug).")]
    private bool leverAActive;
    [SerializeField, Tooltip("Etat du levier B (debug).")]
    private bool leverBActive;
    [SerializeField, Tooltip("Indique si l'action du puzzle a deja ete declenchee.")]
    private bool triggered;

    private bool restoringState;

    public bool IsTriggered => triggered;

    private void OnValidate()
    {
        if (leverA != null && leverA == leverB)
        {
            Debug.LogWarning($"[LeverPuzzle] event='invalid_setup' puzzle='{name}' reason='duplicate_lever_reference'", this);
        }

        if (pivotTarget == null)
        {
            pivotTarget = GetComponent<Pivot>();
        }
    }

    private void OnEnable()
    {
        if (subscribeToLeverEvents)
        {
            SubscribeLever(leverA);
            SubscribeLever(leverB);
        }

        SyncFromLevers();
        Evaluate("on_enable");
    }

    private void OnDisable()
    {
        if (!subscribeToLeverEvents)
        {
            return;
        }

        UnsubscribeLever(leverA);
        UnsubscribeLever(leverB);
    }

    public void HandleLeverStateChanged(Lever lever, bool active)
    {
        if (lever == null)
        {
            return;
        }

        if (lever == leverA)
        {
            leverAActive = active;
        }
        else if (lever == leverB)
        {
            leverBActive = active;
        }
        else
        {
            LogDebug("state_ignored", $"reason='unknown_lever' lever='{lever.name}'");
            return;
        }

        LogDebug("state_received", $"lever='{lever.name}' active={active} restoring={restoringState}");
        if (restoringState)
        {
            return;
        }

        Evaluate($"lever_change:{lever.name}");
    }

    public void SetLeverA(bool active)
    {
        leverAActive = active;
        Evaluate("manual_set_lever_a");
    }

    public void SetLeverB(bool active)
    {
        leverBActive = active;
        Evaluate("manual_set_lever_b");
    }

    public void ResetPuzzle()
    {
        triggered = false;
        Evaluate("reset");
    }

    public void RestoreState(bool leverAState, bool leverBState, bool triggeredState)
    {
        restoringState = true;
        leverAActive = leverAState;
        leverBActive = leverBState;
        triggered = triggeredState;

        leverA?.RestoreActiveState(leverAState, leverAState);
        leverB?.RestoreActiveState(leverBState, leverBState);

        restoringState = false;
        LogDebug("state_restored", $"leverA={leverAState} leverB={leverBState} triggered={triggeredState}");

        if (!triggered)
        {
            Evaluate("restore_state");
        }
    }

    private void SubscribeLever(Lever lever)
    {
        if (lever == null)
        {
            return;
        }

        lever.StateChanged -= OnLeverStateChanged;
        lever.StateChanged += OnLeverStateChanged;
    }

    private void UnsubscribeLever(Lever lever)
    {
        if (lever == null)
        {
            return;
        }

        lever.StateChanged -= OnLeverStateChanged;
    }

    private void OnLeverStateChanged(Lever lever, bool active)
    {
        HandleLeverStateChanged(lever, active);
    }

    private void SyncFromLevers()
    {
        if (leverA != null)
        {
            leverAActive = leverA.IsActive;
        }

        if (leverB != null)
        {
            leverBActive = leverB.IsActive;
        }
    }

    private void Evaluate(string reason)
    {
        LogDebug("evaluate", $"reason='{reason}' leverA={leverAActive} leverB={leverBActive} triggered={triggered}");
        if (leverAActive && leverBActive)
        {
            TriggerTarget(reason);
            return;
        }

        if (!playOnce)
        {
            triggered = false;
        }
    }

    private void TriggerTarget(string reason)
    {
        if (playOnce && triggered)
        {
            LogDebug("trigger_skipped", $"reason='{reason}' cause='already_triggered'");
            return;
        }

        if (pivotTarget == null)
        {
            pivotTarget = GetComponent<Pivot>();
        }

        if (pivotTarget == null)
        {
            Debug.LogWarning($"[LeverPuzzle] event='pivot_missing' puzzle='{name}' reason='{reason}'", this);
            return;
        }

        triggered = true;
        PlaySfx(successSfx);

        pivotTarget.TriggerPivot();
        LogDebug("triggered", $"reason='{reason}' pivot='{pivotTarget.name}'");
    }

    private void PlaySfx(AudioClipSO clip)
    {
        if (clip == null || clip.audioClip == null)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClip(clip, transform.position);
            return;
        }

        AudioSource.PlayClipAtPoint(clip.audioClip, transform.position, Mathf.Clamp01(clip.volume));
    }

    private void LogDebug(string eventName, string extra = "")
    {
        if (!logDebug)
        {
            return;
        }

        string suffix = string.IsNullOrWhiteSpace(extra) ? string.Empty : $" {extra}";
        Debug.Log(
            $"[LeverPuzzle] event='{eventName}' puzzle='{name}' leverA={leverAActive} leverB={leverBActive} triggered={triggered}{suffix}",
            this);
    }
}
