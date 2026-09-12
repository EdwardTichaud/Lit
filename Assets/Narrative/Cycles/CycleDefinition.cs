using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CycleCondition
{
    [Tooltip("Conditions nommees supplementaires, combinees aux anciens jalons si presents.")]
    public CycleRequirements requirements = new CycleRequirements();
    [Min(0), Tooltip("Compatibilite : tous ces anciens bits sont requis. Zero ne demande aucun bit.")] public int allFlags;
    [Min(0), Tooltip("Compatibilite : au moins un ancien bit requis. Zero ne demande aucun bit.")] public int anyFlags;
    [Tooltip("Connaissances toutes requises ; une liste vide n'impose aucune connaissance.")]
    public KnowledgeSO[] knowledge = Array.Empty<KnowledgeSO>();
    public bool Matches(int state, Func<KnowledgeSO, bool> knows)
    {
        if ((state & allFlags) != allFlags || (anyFlags != 0 && (state & anyFlags) == 0)) return false;
        if (knowledge != null) foreach (var item in knowledge)
            if (item == null || knows == null || !knows(item)) return false;
        return true;
    }
}

[Serializable]
public sealed class CycleDialogue
{
    [Tooltip("Identifiant stable du dialogue, identique a la source des etapes associees.")] public string id;
    [Min(0), Tooltip("Duree d'affichage en secondes reelles, hors fondus. Zero utilise la duree du cycle. La recompense attend la fermeture naturelle du dialogue.")]
    public float durationSeconds;
    [Tooltip("Conditions d'acces au texte principal ; sinon le texte indisponible est utilise.")]
    public CycleCondition condition = new CycleCondition();
    [TextArea, Tooltip("Texte principal du dialogue.")] public string line;
    [TextArea, Tooltip("Texte avant les prerequis ; vide empeche ce dialogue, aucune recompense.")] public string unavailableLine;
    [TextArea, Tooltip("Texte apres validation ; vide conserve le texte principal.")] public string repeatLine;
    [Min(0), Tooltip("Compatibilite : anciens jalons ecrits a l'ouverture. Les nouveaux cycles utilisent leurs etapes.")] public int openedFlags;
    [Min(0), Tooltip("Compatibilite : anciens jalons ecrits a la fermeture naturelle.")] public int completedFlags;
    [Tooltip("Recompense historique du dialogue. Pour un cycle a etapes, configurer la competence dans son etape.")] public SkillSO rewardSkill;
    [Min(0), Tooltip("Bit de recompense historique, ecrit apres fermeture naturelle ; ne pas renumeroter.")] public int rewardFlag;
    public bool HasReward(int state) => rewardFlag != 0 && (state & rewardFlag) == rewardFlag;
    [Header("Disparition apres le dialogue")]
    [Tooltip("Faire disparaitre le Ghost apres une fermeture reussie. Utilise le jalon de recompense ou les completedFlags pour la sauvegarde.")]
    public bool disappearAfterCompletion;
    [Min(0), Tooltip("Attente en secondes reelles apres la fermeture du dialogue. Zero commence la dissolution tout de suite; le Ghost se desactive apres la fin de ses effets.")]
    public float disappearanceDelay = 3f;
    public bool HasCompleted(int state) => rewardFlag != 0 ? HasReward(state) :
        completedFlags != 0 && (state & completedFlags) == completedFlags;
}

/// <summary>Flags are local to a cycle. Never renumber released flags or rename its ID.</summary>
[CreateAssetMenu(menuName = "Lit/Narrative/Cycle", fileName = "Cycle")]
public sealed class CycleDefinition : ScriptableObject
{
    [Tooltip("Identifiant unique de sauvegarde du cycle ; ne jamais renommer apres publication.")] public string cycleId;
    [Tooltip("Titre du cycle pour l'auteur et le futur journal.")] public string title;
    [TextArea, Tooltip("Resume narratif du cycle, sans effet sur sa progression.")] public string description;
    [Tooltip("Classement principal ou annexe ; ne change pas les regles cooperatives.")] public CycleCategory category;
    [Tooltip("Conditions pour rendre le cycle disponible ; une liste vide le rend disponible immediatement.")]
    public CycleRequirements prerequisites = new CycleRequirements();
    [Tooltip("Objectifs nommes ; toutes les etapes terminales sont requises pour terminer.")]
    public CycleStep[] steps = Array.Empty<CycleStep>();
    [Min(1), Tooltip("Secondes reelles maximales d'attente des presentations finales avant annulation et dechargement.")]
    public float completionPresentationTimeout = 15f;
    public bool HasSteps => steps != null && steps.Length > 0;
    public string StepKey(string id) => StateKey + ".step." + id;
    public string FactKey(string id) => StateKey + ".defeat." + id;
    public CycleStep FindStep(string id) => Array.Find(steps ?? Array.Empty<CycleStep>(), step => step != null && step.id == id);
    [Header("Fin partagee et scene du cycle")]
    [Min(0), Tooltip("Jalons tous requis pour terminer le cycle pour la partie entiere. Zero laisse la fin non configuree. Reutiliser les bits persistants existants.")]
    public int completionFlags;
    [Tooltip("Nom exact de la scene additive reservee a ce cycle. Elle est dechargee apres les effets de fin et ignoree aux prochains chargements. Vide desactive le dechargement.")]
    public string cycleSceneName;
    public bool IsCompleted(int state) => completionFlags != 0 && (state & completionFlags) == completionFlags;

    [Min(1), Tooltip("Duree par defaut des dialogues en secondes reelles, hors fondus.")] public float dialogueSeconds = 4f;
    [Tooltip("Textes des interlocuteurs ; leurs IDs sont les sources des etapes de dialogue.")]
    public CycleDialogue[] dialogues = Array.Empty<CycleDialogue>();
    [Header("Optional encounter")]
    [Min(0)] public int enemyDefeatedFlags = 1;
    public KnowledgeSO[] knowledgeOnEnemyDefeat = Array.Empty<KnowledgeSO>();
    public bool playCinematicAfterDefeat;
    [Min(0)] public float deathDelay = 3f;
    [Min(1)] public int cinematicCompletedFlags = 2;
    public string StateKey => "narrative." + cycleId;

    public float ResolveDialogueSeconds(CycleDialogue dialogue) =>
        dialogue != null && dialogue.durationSeconds > 0f ? dialogue.durationSeconds : Mathf.Max(0f, dialogueSeconds);

    public CycleDialogue FindDialogue(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || dialogues == null) return null;
        foreach (var dialogue in dialogues)
            if (dialogue != null && dialogue.id == id) return dialogue;
        return null;
    }

    public List<string> ValidateConfiguration()
    {
        var issues = new List<string>();
        CycleValidation.Validate(this, issues);
        if (!HasSteps && !string.IsNullOrWhiteSpace(cycleSceneName) && completionFlags <= 0)
            issues.Add("La scene du cycle exige des jalons de fin (completionFlags). ");
        if (string.IsNullOrWhiteSpace(cycleId)) issues.Add("Assign a stable, unique cycle ID.");
        var ids = new HashSet<string>();
        int rewardBits = 0;
        if (dialogues != null) foreach (var dialogue in dialogues)
        {
            if (dialogue == null) { issues.Add("Empty dialogue entry."); continue; }
            if (string.IsNullOrWhiteSpace(dialogue.id) || !ids.Add(dialogue.id)) issues.Add("Empty or duplicate dialogue ID: " + dialogue.id);
            if (string.IsNullOrWhiteSpace(dialogue.line)) issues.Add("Missing dialogue text: " + dialogue.id);
            if (dialogue.disappearAfterCompletion && dialogue.rewardFlag == 0 && dialogue.completedFlags == 0 &&
                !Array.Exists(steps ?? Array.Empty<CycleStep>(), step => step != null && step.kind == CycleStepKind.DialogueCompleted && step.sourceId == dialogue.id))
                issues.Add("La disparition exige un jalon de fermeture sauvegarde : " + dialogue.id);
            if (dialogue.rewardFlag != 0 || dialogue.rewardSkill != null)
            {
                int flag = dialogue.rewardFlag;
                if (flag <= 0 || (flag & (flag - 1)) != 0 || (rewardBits & flag) != 0 || dialogue.rewardSkill == null)
                    issues.Add("Reward needs a skill and a unique positive single bit: " + dialogue.id);
                rewardBits |= flag;
            }
        }
        int transitionBits = enemyDefeatedFlags | (playCinematicAfterDefeat ? cinematicCompletedFlags : 0);
        if (dialogues != null) foreach (var dialogue in dialogues)
            if (dialogue != null) transitionBits |= dialogue.openedFlags | dialogue.completedFlags;
        if ((rewardBits & transitionBits) != 0) issues.Add("Reward bits must not be granted by other transitions.");
        if (playCinematicAfterDefeat && (enemyDefeatedFlags <= 0 || cinematicCompletedFlags <= 0 ||
            (enemyDefeatedFlags & cinematicCompletedFlags) != 0)) issues.Add("Encounter and cinematic flags must be distinct and positive.");
        return issues;
    }
}
