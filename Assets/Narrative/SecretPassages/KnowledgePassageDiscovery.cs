using System.Collections.Generic;
using Lit.Story;
using Unity.Netcode;
using UnityEngine;

/// <summary>Knowledge/Observer discovery adapter. Does not own traversal or a second save store.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(TrouEtroit))]
public sealed class KnowledgePassageDiscovery : MonoBehaviour
{
    public SecretPassageDefinition definition;
    [Tooltip("Presentation locale playOnce, unique a ce passage. Aucun texte de remplacement genere.")]
    public StorySequenceRunner firstRecognition;
    [Tooltip("Facultatif : sinon resolu sur le service de progression de la session.")]
    public WorldRulesStateManager worldRules;
    private TrouEtroit passage;
    private bool ownsPlayback;
    private float nextPresentationCheck;
    private bool importLegacyDetection;
    private readonly HashSet<string> acknowledgedAttempts = new HashSet<string>();
    private bool Authority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
    public bool IsConfigured => definition != null && definition.HasValidIdentity;
    public bool IsKnown => IsConfigured && KnowledgeManager.Instance != null && KnowledgeManager.Instance.HasKnowledge(definition.knowledge);

    private void Awake() => passage = GetComponent<TrouEtroit>();

    private WorldRulesStateManager ResolveRules()
    {
        if (worldRules == null && CycleProgressionService.Instance != null)
            worldRules = CycleProgressionService.Instance.GetComponent<WorldRulesStateManager>();
        return worldRules;
    }

    public static string ResolveCharacterId(GameObject character)
    {
        if (character == null) return null;
        var info = character.GetComponent<CharacterInfo>();
        var data = info != null ? info.SourceData : null;
        if (data == null) data = character.GetComponent<SquadCharacterController>()?.CharacterData;
        return data != null && !string.IsNullOrWhiteSpace(data.characterId) ? data.characterId : null;
    }

    public bool ShouldRequest(GameObject character, bool hasObserver)
    {
        if (!isActiveAndEnabled || !IsConfigured || character == null) return false;
        if (IsKnown) return true;
        string id = ResolveCharacterId(character);
        if (!hasObserver || id == null || acknowledgedAttempts.Contains(id)) return false;
        if (!Authority) return true;
        var rules = ResolveRules();
        return !SecretPassageAttemptLedger.HasAttempted(rules, definition.passageId, id) ||
            SecretPassageAttemptLedger.RecordedResult(rules, definition.passageId, id) == SecretPassageAttemptLedger.Result.Succeeded;
    }

    /// <summary>Called only after TrouEtroit validates the real actor, health, movement and proximity.</summary>
    public bool ServerTryDiscover(GameObject character, bool hasObserver)
    {
        if (!Authority || !isActiveAndEnabled || !IsConfigured) return false;
        var result = SecretPassageAttemptLedger.Observe(ResolveRules(), definition.passageId,
            ResolveCharacterId(character), hasObserver, IsKnown, () => Random.Range(1, 7));
        if (result == SecretPassageAttemptLedger.Result.AlreadyKnown) return true;
        if (result != SecretPassageAttemptLedger.Result.Succeeded) return false;
        KnowledgeReveal.Reveal(definition.knowledge, character, "secret_passage_observer");
        return IsKnown;
    }

    public bool HasSpentAttempt(GameObject character, out string characterId)
    {
        characterId = ResolveCharacterId(character);
        return Authority && IsConfigured && SecretPassageAttemptLedger.HasAttempted(ResolveRules(), definition.passageId, characterId) && (IsKnown ||
            SecretPassageAttemptLedger.RecordedResult(ResolveRules(), definition.passageId, characterId) == SecretPassageAttemptLedger.Result.Failed);
    }

    public void AcknowledgeAttempt(string characterId)
    { if (!string.IsNullOrWhiteSpace(characterId)) acknowledgedAttempts.Add(characterId); }

    // Called by the existing saved Detected-state provider, not by Observer or client acknowledgements.
    public void RestoreLegacyDetection(bool wasDetected) => importLegacyDetection = wasDetected;

    public bool TryImportLegacyDetection()
    {
        if (!importLegacyDetection || !Authority || !IsConfigured || KnowledgeManager.Instance == null) return false;
        // Shared knowledge is restored during zone preparation; importing earlier could be overwritten.
        if (LoadingScreenService.IsLoading || GameFlowService.IsPreparingGameplayScene ||
            GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning) return false;
        if (!IsKnown) KnowledgeReveal.Reveal(definition.knowledge, (GameObject)null, "secret_passage_legacy");
        if (IsKnown) importLegacyDetection = false;
        return IsKnown;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPresentationCheck) return;
        nextPresentationCheck = Time.unscaledTime + .2f;
        TryImportLegacyDetection();
        if (passage == null) passage = GetComponent<TrouEtroit>();
        var player = LocalPlayerContext.LocalCharacterRoot;
        if (player == null || passage == null || !passage.isActiveAndEnabled || !passage.IsInsideInteractionVolume(player.position))
        {
            if (ownsPlayback && firstRecognition != null) firstRecognition.Abort();
            ownsPlayback = false;
            return;
        }
        if (!IsKnown || !passage.IsDetected || firstRecognition == null || firstRecognition.Sequence == null || !firstRecognition.Sequence.playOnce) return;
        if (StorySequenceCompletionStore.IsCompleted(firstRecognition.Sequence)) { ownsPlayback = false; return; }
        if (ownsPlayback) { if (!firstRecognition.IsPlaying) ownsPlayback = false; return; }
        var actor = player.GetComponent<SquadCharacterController>();
        if (actor == null || actor.CurrentHp <= 0 || LoadingScreenService.IsLoading || GameFlowService.IsPreparingGameplayScene ||
            GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning ||
            StorySequenceRunner.IsAnySequencePlaying || InputModeCoordinator.IsGameplayBlocked ||
            InputModeCoordinator.CurrentMode != InputMode.Exploration) return;
        ownsPlayback = firstRecognition.Play();
    }

    private void OnDisable()
    {
        if (ownsPlayback && firstRecognition != null) firstRecognition.Abort();
        ownsPlayback = false;
        acknowledgedAttempts.Clear();
    }
}
