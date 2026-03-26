using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public class LeverPlayableDirectorTarget : MonoBehaviour, ILeverTarget
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private bool playOnActivate = true;
    [SerializeField] private bool stopOnDeactivate;
    [SerializeField] private bool rewindBeforePlay = true;
    [SerializeField] private bool logDebug;

    private void Awake()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    public void HandleLeverStateChanged(Lever lever, bool active)
    {
        if (director == null)
        {
            Debug.LogWarning($"[LeverTarget] event='director_missing' lever='{lever?.name ?? "null"}' target='{name}'", this);
            return;
        }

        if (active)
        {
            if (!playOnActivate)
            {
                return;
            }

            if (rewindBeforePlay)
            {
                director.time = 0d;
                director.Evaluate();
            }

            director.Play();
            if (logDebug)
            {
                Debug.Log($"[LeverTarget] event='director_play' lever='{lever?.name ?? "null"}' target='{name}' director='{director.name}'", this);
            }

            return;
        }

        if (!stopOnDeactivate)
        {
            return;
        }

        director.Stop();
        if (logDebug)
        {
            Debug.Log($"[LeverTarget] event='director_stop' lever='{lever?.name ?? "null"}' target='{name}' director='{director.name}'", this);
        }
    }
}
