using UnityEngine;

public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    [Header("Zone courante")]
    public ZoneSO currentZone;
    [Header("Harmonique prédominante actuelle")]
    [Tooltip("Harmonique dominante déterminée par le rythme de la musique de la zone courante.")]
    public HarmonicType currentPredominantHarmonic = HarmonicType.Lumiere;

    /// <summary>
    /// Évènement déclenché lorsqu'une nouvelle harmonique prédominante est définie.
    /// Permet aux autres systèmes (effets visuels, gameplay...) de réagir au
    /// changement de musique ambiante.
    /// </summary>
    public event System.Action<HarmonicType> OnPredominantHarmonicChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Définit la nouvelle ZoneSO courante et synchronise le BattlefieldManager.
    /// </summary>
    public void SetCurrentZone(ZoneSO newZone)
    {
        if (newZone == null)
        {
            Debug.LogWarning("[ZoneManager] Nouvelle zone null !");
            return;
        }

        if (newZone == currentZone)
            return; // Pas besoin de changer

        currentZone = newZone;
        // Met à jour l'harmonique prédominante globale selon la zone sélectionnée
        currentPredominantHarmonic = newZone.predominantHarmonic;
        // Avertit les abonnés qu'une nouvelle harmonique domine
        OnPredominantHarmonicChanged?.Invoke(currentPredominantHarmonic);

        // Notifie le BattlefieldsManager
        if (BattlefieldManager.Instance != null)
            BattlefieldManager.Instance.SetCurrentZone(newZone);

        Debug.Log($"[ZoneManager] Nouvelle zone courante : {newZone.zoneName}");
        Debug.Log($"[ZoneManager] Harmonique prédominante : {currentPredominantHarmonic}");

        ZoneNameDisplay.Instance?.ShowCurrentZoneInfo();

        if (newZone.zoneMusic != null)
            AudioManager.Instance?.PlayExplorationMusic(newZone.zoneMusic);
    }
}
