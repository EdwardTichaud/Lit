using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline; // Nécessaire pour référencer les TimelineAsset

/// <summary>
/// Décrit une étape de timeline jouée après un combat.
/// Peut référencer une TimelineAsset (lecture via TimelineManager)
/// ou un PlayableDirector prefab pour gérer des bindings spécifiques.
/// </summary>
[Serializable]
public class TimelineSequenceEntry
{
    [Tooltip("TimelineAsset jouée via TimelineManager si aucun prefab n'est défini.")]
    public TimelineAsset timelineAsset;

    [Tooltip("Prefab contenant un PlayableDirector configuré (bindings, SignalReceiver, etc.).")]
    public PlayableDirector directorPrefab;

    [Tooltip("Détruit l'instance du prefab après la lecture.")]
    public bool destroyAfterPlay = true;

    public bool HasPlayable => timelineAsset != null || directorPrefab != null;
}

/// <summary>
/// ScriptableObject regroupant la configuration complète d'un combat lancé
/// depuis une Timeline. Il permet de définir les ennemis à invoquer ainsi que
/// les séquences à jouer après la victoire ou la défaite.
/// </summary>
[CreateAssetMenu(fileName = "TimelineBattleConfig", menuName = "Symphonie/Timeline Battle Config")]
public class TimelineBattleConfigSO : ScriptableObject
{
    [Header("Ennemi principal"), Tooltip("Premier ennemi obligatoire pour le combat")]
    public CharacterData enemy1; // Ennemi qui doit toujours être défini

    [Header("Ennemis optionnels"), Tooltip("Second ennemi facultatif")]
    public CharacterData enemy2; // Peut rester null si non utilisé

    [Tooltip("Troisième ennemi facultatif")]
    public CharacterData enemy3; // Peut rester null pour un combat à deux ennemis

    [Header("Séquences post-combat")]
    [Tooltip("Séquence jouée après la victoire.")]
    public List<TimelineSequenceEntry> victoryTimelineSequence = new();

    [Tooltip("Séquence jouée après la défaite.")]
    public List<TimelineSequenceEntry> defeatTimelineSequence = new();

    [Header("Bindings post-combat")]
    [Tooltip("Tag utilisé comme caster pour les timelines post-combat jouées via TimelineManager.")]
    public string postBattleCasterTag;

    [Tooltip("Tag de la caméra à utiliser pour les timelines post-combat jouées via TimelineManager.")]
    public string postBattleCameraTag = "WorldCamera";

    public bool HasVictoryTimeline => HasSequence(victoryTimelineSequence);
    public bool HasDefeatTimeline => HasSequence(defeatTimelineSequence);

    private static bool HasSequence(List<TimelineSequenceEntry> sequence)
    {
        if (sequence == null || sequence.Count == 0)
            return false;

        for (int i = 0; i < sequence.Count; i++)
        {
            var entry = sequence[i];
            if (entry != null && entry.HasPlayable)
                return true;
        }

        return false;
    }
}
