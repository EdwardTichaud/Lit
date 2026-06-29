using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Gestionnaire centralisant les <see cref="TimelineAsset"/> jouées durant les combats.
/// Chaque CharacterUnit pilote désormais sa propre timeline via son PlayableDirector ;
/// ce gestionnaire coordonne simplement les alignements caméra et les appels haut niveau.
/// </summary>
public class BattleTimelineManager : MonoBehaviour
{
    /// <summary>Instance statique accessible depuis les autres scripts.</summary>
    public static BattleTimelineManager Instance { get; private set; }

    /// <summary>
    /// Mémorise l'unité ayant lancé la dernière timeline. Cette information
    /// permet de fournir un arrêt ou un suivi de lecture par défaut lorsque
    /// l'appelant n'en précise pas explicitement l'unité.
    /// </summary>
    private CharacterUnit lastUnitPlaying;

    private void Awake()
    {
        // Mise en place classique du singleton.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Aligne l'origine de la caméra de combat sur la cible donnée.
    /// </summary>
    /// <param name="cameraTarget">Nouvelle ancre pour la caméra.</param>
    /// <param name="cameraTag">Tag de la caméra à repositionner.</param>
    /// <param name="fixedRotation">Rotation imposée, ou null pour reprendre celle de la cible.</param>
    public void AlignCameraToTarget(GameObject cameraTarget, string cameraTag, Quaternion? fixedRotation = null)
    {
        if (cameraTarget == null || string.IsNullOrEmpty(cameraTag))
            return;

        // On récupère le parent direct (BattleCamera_Origin) pour déplacer l'ensemble du rig.
        GameObject cameraGO = GameObject.FindGameObjectWithTag(cameraTag);
        Transform cameraParent = cameraGO != null ? cameraGO.transform.parent : null;

        if (cameraParent != null)
        {
            cameraParent.position = cameraTarget.transform.position;
            cameraParent.rotation = fixedRotation ?? cameraTarget.transform.rotation;
        }
    }

    /// <summary>
    /// Joue uniquement la timeline du lanceur via son PlayableDirector local.
    /// </summary>
    /// <param name="timeline">Timeline à exécuter.</param>
    /// <param name="unit">Unité dont le PlayableDirector doit être utilisé.</param>
    /// <param name="casterBinding">Objet servant de référence pour les bindings (Animator, etc.).</param>
    public void PlayCasterTimeline(TimelineAsset timeline, CharacterUnit unit, GameObject casterBinding)
    {
        if (timeline == null || unit == null)
            return;

        // Identifie automatiquement le GameObject portant l'Animator enfant du lanceur.
        GameObject binding = casterBinding ?? unit.GetCasterBindingTarget();

        unit.PlayBattleTimeline(timeline, binding);
        lastUnitPlaying = unit;
    }

    /// <summary>
    /// Met en pause la timeline actuellement jouée pour l'unité demandée.
    /// </summary>
    public void PauseCasterTimeline(CharacterUnit unit = null)
    {
        CharacterUnit target = unit ?? lastUnitPlaying;
        if (target == null)
            return;

        target.PauseBattleTimeline();
    }

    /// <summary>
    /// Reprend la timeline précédemment mise en pause pour l'unité demandée.
    /// </summary>
    public void ResumeCasterTimeline(CharacterUnit unit = null)
    {
        CharacterUnit target = unit ?? lastUnitPlaying;
        if (target == null)
            return;

        target.ResumeBattleTimeline();
    }

    /// <summary>Arrête la timeline en cours pour l'unité spécifiée.</summary>
    /// <param name="unit">Unité ciblée, ou null pour cibler la dernière unité connue.</param>
    public void StopCasterTimeline(CharacterUnit unit = null)
    {
        CharacterUnit target = unit ?? lastUnitPlaying;
        if (target == null)
            return;

        target.StopBattleTimeline();

        if (target == lastUnitPlaying)
            lastUnitPlaying = null;
    }

    /// <summary>
    /// Indique si une timeline est actuellement jouée sur l'unité demandée.
    /// </summary>
    /// <param name="unit">Unité à interroger, ou null pour consulter la dernière unité utilisée.</param>
    public bool IsCasterTimelinePlaying(CharacterUnit unit = null)
    {
        CharacterUnit target = unit ?? lastUnitPlaying;
        return target != null && target.IsBattleTimelinePlaying;
    }

    /// <summary>
    /// Indique si la timeline ciblée est simplement en pause (utile pour distinguer un arrêt complet
    /// d'une suspension volontaire via un Signal de timeline).
    /// </summary>
    public bool IsCasterTimelinePaused(CharacterUnit unit = null)
    {
        CharacterUnit target = unit ?? lastUnitPlaying;
        return target != null && target.IsBattleTimelinePaused;
    }
}

