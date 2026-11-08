using UnityEngine;

/// <summary>Authored identity and knowledge shared by a note and its physical passage.</summary>
[CreateAssetMenu(menuName = "Lit/Narrative/Secret Passage", fileName = "SecretPassage")]
public sealed class SecretPassageDefinition : ScriptableObject
{
    [Tooltip("Identifiant de sauvegarde stable, commun aux deux extremites. Ne jamais regenerer apres publication.")]
    public string passageId;
    public KnowledgeSO knowledge;
    [Tooltip("Note correspondant au passage. Son acquisition et sa lecture restent distinctes de la decouverte par Observateur.")]
    public Item note;

    public bool HasValidIdentity => !string.IsNullOrWhiteSpace(passageId) && knowledge != null && !string.IsNullOrWhiteSpace(knowledge.knowledgeId);
    public bool HasReadLinkedNote => note != null && note.knowledgeUnlockedOnRead != null &&
        note.knowledgeUnlockedOnRead.Contains(knowledge) &&
        (note.knowledgeUnlockedOnPickup == null || !note.knowledgeUnlockedOnPickup.Contains(knowledge));
}
