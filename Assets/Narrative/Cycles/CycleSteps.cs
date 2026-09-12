using System;
using UnityEngine;

public enum CycleCategory { Main, Side }
public enum CycleStatus { Unavailable, Available, InProgress, Completed }
public enum CycleStepKind { Knowledge, DialogueCompleted, EnemyDefeated, Interaction, SequenceCompleted }
public enum CycleRequirementKind { Step, Knowledge, CycleCompleted }
public enum CycleRequirementMode { All, Any }

[Serializable]
public sealed class CycleRequirement
{
    [Tooltip("Fait requis : etape de ce cycle, connaissance partagee ou autre cycle termine.")]
    public CycleRequirementKind kind;
    [Tooltip("Identifiant stable de l'etape de ce cycle.")]
    public string stepId;
    [Tooltip("Connaissance requise ; vide ne valide pas cette condition.")]
    public KnowledgeSO knowledge;
    [Tooltip("Autre cycle requis ; vide ne valide pas cette condition.")]
    public CycleDefinition cycle;
}

[Serializable]
public sealed class CycleRequirements
{
    [Tooltip("Toutes les conditions ou au moins une. Une liste vide n'impose aucun prerequis.")]
    public CycleRequirementMode mode;
    [Tooltip("Conditions a satisfaire selon le mode choisi. Vide : aucun prerequis.")]
    public CycleRequirement[] conditions = Array.Empty<CycleRequirement>();
}

[Serializable]
public sealed class CycleStep
{
    [Tooltip("Identifiant de sauvegarde unique dans ce cycle. Ne jamais le renommer apres publication.")]
    public string id;
    [Tooltip("Nom lisible de l'objectif pour l'auteur et le futur journal.")]
    public string title;
    [TextArea, Tooltip("Description de l'objectif ; vide masque la description dans le futur journal.")]
    public string description;
    [Tooltip("Evenement attendu. Les connaissances et les defaites deja enregistrees sont des faits persistants.")]
    public CycleStepKind kind;
    [Tooltip("ID de dialogue, interaction, rencontre ou sequence dans les liaisons de scene.")]
    public string sourceId;
    [Tooltip("Connaissance attendue pour une etape de connaissance.")]
    public KnowledgeSO knowledge;
    [Tooltip("Prerequis de cette etape. Les evenements recus trop tot ne sont pas memorises.")]
    public CycleRequirements prerequisites = new CycleRequirements();
    [Tooltip("Toutes les etapes terminales doivent etre validees pour terminer le cycle.")]
    public bool terminal;
    [Tooltip("Competence partagee accordee une seule fois. Vide signifie aucune recompense.")]
    public SkillSO rewardSkill;
    [HideInInspector] public int legacyAnyFlags;
    [HideInInspector] public int legacyWriteFlags;
}

[Serializable]
public sealed class CycleEncounterBinding
{
    [Tooltip("Source de l'etape de defaite ; unique dans ce cycle.")] public string id;
    [Tooltip("Marker de l'ennemi. Son instance runtime est prioritaire sur sa copie de scene.")] public SceneMarker marker;
    [Tooltip("Ennemi directement place dans la scene si aucun marker n'est utilise.")] public EnemyController enemy;
    [NonSerialized] public CharacterInfo health;
    [NonSerialized] public Action<CharacterInfo> callback;
    public CharacterInfo ResolveHealth()
    {
        var actor = marker != null ? marker.RuntimeInstance != null ? marker.RuntimeInstance : marker.BakedCharacterInstance : null;
        return actor != null ? actor.GetComponentInChildren<CharacterInfo>(true) : enemy != null ? enemy.Health : null;
    }
}

[Serializable]
public sealed class CycleSequenceBinding
{
    [Tooltip("Source de l'etape de sequence ; unique dans ce cycle.")] public string id;
    [Tooltip("Director de cette scene ; vide garde l'etape en attente avec diagnostic.")] public UnityEngine.Playables.PlayableDirector director;
    [Tooltip("Liaisons de la Timeline ; requises pour lancer la sequence.")] public Lit.Timeline.TimelineBindingProfile profile;
}
