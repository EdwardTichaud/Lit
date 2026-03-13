using UnityEngine;
using UnityEngine.Playables;

// Puzzle simple: deux leviers doivent etre actifs pour declencher la timeline.
public class TwoLeverPuzzle : MonoBehaviour
{
    [Header("Levers")]
    [Tooltip("Levier A du puzzle.")]
    public Lever leverA;
    [Tooltip("Levier B du puzzle.")]
    public Lever leverB;

    [Header("Timeline")]
    [Tooltip("Timeline a jouer quand les deux leviers sont actifs.")]
    public PlayableDirector playableDirector;

    [Header("Audio")]
    [Tooltip("SFX de reussite.")]
    public AudioClipSO successSfx;

    [Header("Behavior")]
    [Tooltip("Si true, le puzzle ne peut se declencher qu'une fois.")]
    public bool playOnce = true;

    [Header("State")]
    [SerializeField, Tooltip("Etat du levier A (debug).")]
    private bool leverAActive;
    [SerializeField, Tooltip("Etat du levier B (debug).")]
    private bool leverBActive;

    private bool triggered;

    public bool IsTriggered => triggered;

    private void OnEnable()
    {
        SubscribeLever(leverA);
        SubscribeLever(leverB);
        SyncFromLevers();
        Evaluate();
    }

    private void OnDisable()
    {
        UnsubscribeLever(leverA);
        UnsubscribeLever(leverB);
    }

    private void SubscribeLever(Lever lever)
    {
        if (lever == null)
        {
            return;
        }

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
        if (lever == leverA)
        {
            leverAActive = active;
        }
        else if (lever == leverB)
        {
            leverBActive = active;
        }

        Evaluate();
    }

    public void SetLeverA(bool active)
    {
        leverAActive = active;
        Evaluate();
    }

    public void SetLeverB(bool active)
    {
        leverBActive = active;
        Evaluate();
    }

    public void ResetPuzzle()
    {
        triggered = false;
        Evaluate();
    }

    public void RestoreState(bool leverAState, bool leverBState, bool triggeredState)
    {
        leverAActive = leverAState;
        leverBActive = leverBState;
        triggered = triggeredState;

        if (leverA != null)
        {
            leverA.SetActive(leverAState);
        }

        if (leverB != null)
        {
            leverB.SetActive(leverBState);
        }

        if (!triggered)
        {
            Evaluate();
        }
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

    private void Evaluate()
    {
        // Declenchement si les deux leviers sont actifs.
        if (leverAActive && leverBActive)
        {
            TriggerTarget();
            return;
        }

        if (!playOnce)
        {
            triggered = false;
        }
    }

    private void TriggerTarget()
    {
        if (playOnce && triggered)
        {
            return;
        }

        triggered = true;
        PlaySfx(successSfx);

        if (playableDirector == null)
        {
            return;
        }

        playableDirector.Play();
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
}
