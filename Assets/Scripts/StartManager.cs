using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class StartManager : MonoBehaviour
{
    [Header("Startup Actions")]
    [SerializeField] private bool playScreenFadeOnStart = true;

    private ScreenFade screenFade;

    private void Start()
    {
        RunStartupActions();
    }

    public void RunStartupActions()
    {
        if (playScreenFadeOnStart)
        {
            PlayScreenFade();
        }
    }

    private void PlayScreenFade()
    {
        screenFade = GetComponent<ScreenFade>();
        if (screenFade == null)
        {
            Debug.LogWarning(
                $"[StartManager] Aucun composant ScreenFade trouve sur '{name}'. Ajoute ScreenFade sur le meme GameObject pour jouer le fondu au demarrage.",
                this);
            return;
        }

        screenFade.PlayConfiguredFade();
    }
}
