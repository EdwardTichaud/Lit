// Role:
// Scene controller that binds a GhostData asset to a ghost GameObject.
// Usage:
// Attach to a ghost GameObject, assign GhostData, and configure knowledge reactions
// on the asset. The player interacts with the ghost; no free text input is used.
// Responsibilities:
// Expose ghost text, detect available knowledge reactions, unlock new knowledge,
// and mark the ghost understood when a reaction succeeds.
// Dependencies:
// GhostData, KnowledgeManager, CharacterInteractionDetection, LocalInputRouter, InfoBoxUI.
// Precautions:
// This controller does not replace LocalVoiceLineController. Use voice lines for
// audio/subtitle delivery, and this component for knowledge-based interaction state.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Scene-side dissolve effect triggered by a ghost knowledge reaction.
/// </summary>
[System.Serializable]
public class GhostDissolveEffectRule
{
    [Tooltip("Optional ID matched against GhostKnowledgeReaction.triggerEffectIds.")]
    public string effectId;
    [Tooltip("Optional extra knowledge requirement checked at trigger time.")]
    public KnowledgeRequirement requirement = new KnowledgeRequirement();
    [Tooltip("Scene GameObjects whose renderers should receive the ghost dissolve.")]
    public List<GameObject> targetObjects = new List<GameObject>();
    [Tooltip("Also trigger GhostDissolveController components found below each target.")]
    public bool includeChildren = true;
    [Tooltip("Add a GhostDissolveController to a target if none is found.")]
    public bool addControllerIfMissing = true;
    [Tooltip("Prevent this rule from firing more than once during the current runtime.")]
    public bool triggerOnce = true;
    [Tooltip("Duration override. A value <= 0 uses the controller default.")]
    public float durationOverride = -1f;

    [System.NonSerialized] private bool hasTriggered;

    public bool CanTrigger(GhostKnowledgeReaction reaction, KnowledgeManager manager)
    {
        if (triggerOnce && hasTriggered)
        {
            return false;
        }

        if (!MatchesEffectId(reaction))
        {
            return false;
        }

        return requirement == null || requirement.IsSatisfied(manager);
    }

    public int Trigger()
    {
        if (targetObjects == null || targetObjects.Count == 0)
        {
            return 0;
        }

        int triggeredCount = 0;
        for (int i = 0; i < targetObjects.Count; i++)
        {
            GameObject target = targetObjects[i];
            if (target == null)
            {
                continue;
            }

            triggeredCount += TriggerTarget(target);
        }

        if (triggeredCount > 0)
        {
            hasTriggered = true;
        }

        return triggeredCount;
    }

    private bool MatchesEffectId(GhostKnowledgeReaction reaction)
    {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            return true;
        }

        if (reaction == null || reaction.triggerEffectIds == null)
        {
            return false;
        }

        string trimmedId = effectId.Trim();
        for (int i = 0; i < reaction.triggerEffectIds.Count; i++)
        {
            string candidate = reaction.triggerEffectIds[i];
            if (!string.IsNullOrWhiteSpace(candidate) && string.Equals(candidate.Trim(), trimmedId, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private int TriggerTarget(GameObject target)
    {
        GhostDissolveController[] controllers = includeChildren
            ? target.GetComponentsInChildren<GhostDissolveController>(true)
            : target.GetComponents<GhostDissolveController>();

        if ((controllers == null || controllers.Length == 0) && addControllerIfMissing)
        {
            controllers = new[] { target.AddComponent<GhostDissolveController>() };
        }

        if (controllers == null || controllers.Length == 0)
        {
            return 0;
        }

        int triggeredCount = 0;
        for (int i = 0; i < controllers.Length; i++)
        {
            GhostDissolveController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            if (durationOverride > 0f)
            {
                controller.TriggerDissolve(durationOverride);
            }
            else
            {
                controller.TriggerDissolve();
            }

            triggeredCount++;
        }

        return triggeredCount;
    }
}

/// <summary>
/// Assigns a GhostData asset to a scene ghost and resolves reactions from unlocked knowledge.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Lit/Narrative/Ghost Controller")]
public class GhostController : MonoBehaviour, ICharacterDetectedInteractable
{
    [Header("Data")]
    /// <summary>Ghost investigation data assigned to this scene object.</summary>
    [SerializeField] private GhostData ghostData;
    /// <summary>If true, a successful understanding reaction hides further one-shot interaction.</summary>
    [SerializeField] private bool playOnce = true;
    /// <summary>If true, listening to the ghost unlocks GhostData.knowledgeUnlockedOnListen.</summary>
    [SerializeField] private bool unlockKnowledgeOnListen = true;

    [Header("Interaction")]
    [SerializeField, Tooltip("Collider de reference pour la detection.")]
    private Collider interactionCollider;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale d'interaction.")]
    private float interactionMaxDistance = 2.25f;
    [SerializeField, Tooltip("Priorite de selection si plusieurs interactions sont proches.")]
    private int interactionPriority = 90;
    [SerializeField, Tooltip("Texte affiche dans l'InteractionBox.")]
    private string interactionText = "Ecouter";

    [Header("Interaction UI")]
    [SerializeField, Tooltip("Affiche une InteractionBox quand le fantome est cible.")]
    private bool showInteractionUi = true;
    [SerializeField, Tooltip("Prefab/objet UI d'interaction optionnel.")]
    private GameObject interactionBox;
    [SerializeField, Tooltip("Parent des boxes UI. Laisse vide pour instancier la box en world space.")]
    private Transform boxesPanel;
    [SerializeField, Tooltip("Offset en world pour la box d'interaction.")]
    private Vector3 interactionOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField, Tooltip("Camera UI/world pour positionner l'interaction box.")]
    private Camera targetCamera;

    [Header("Feedback")]
    [SerializeField, Tooltip("Duree d'affichage InfoBox. 0 utilise la duree par defaut.")]
    private float feedbackDuration = 0f;
    [SerializeField, Tooltip("Texte affiche si playOnce est actif et que le fantome est deja compris.")]
    private string alreadyUnderstoodMessage = "Ce souvenir a deja ete compris.";
    [SerializeField, Tooltip("Prefixe optionnel devant la reponse du joueur.")]
    private string playerOptionPrefix = "Vous : ";
    [SerializeField, Tooltip("Prefixe optionnel devant la reponse du fantome.")]
    private string ghostResponsePrefix = "";

    [Header("Reaction Choices")]
    [SerializeField, Tooltip("Affiche une liste d'options si plusieurs reactions de connaissance sont disponibles.")]
    private bool showReactionChoiceUi = true;
    [SerializeField, Tooltip("Si une seule reaction est disponible, elle est jouee directement.")]
    private bool autoUseSingleAvailableReaction = true;
    [SerializeField, Tooltip("Parent UI optionnel pour la fenetre des reactions. Laisse vide pour creer un canvas simple.")]
    private Transform reactionChoiceParent;
    [SerializeField, Tooltip("Largeur cible de la fenetre generee si aucun parent/prefab dedie n'est fourni.")]
    private float reactionChoiceWidth = 680f;
    [SerializeField, Tooltip("Libelle du bouton de fermeture.")]
    private string closeChoiceText = "Reculer";

    [Header("Dissolve Effects")]
    [SerializeField, Tooltip("Scene-side dissolve effects triggered by knowledge reactions.")]
    private List<GhostDissolveEffectRule> dissolveEffectRules = new List<GhostDissolveEffectRule>();

    [Header("Proximity Dissolve")]
    [SerializeField, Tooltip("If enabled, the ghost stays fully dissolved while no controlled character is nearby.")]
    private bool enableProximityDissolve = true;
    [SerializeField, Tooltip("Targets affected by proximity reveal. Empty means this GameObject.")]
    private List<GameObject> proximityDissolveTargets = new List<GameObject>();
    [SerializeField, Tooltip("Collider trigger used as the reveal zone. Empty uses the interaction collider, then proximityDistance as fallback.")]
    private Collider proximityTriggerCollider;
    [SerializeField, Min(0.1f), Tooltip("Fallback reveal radius when no proximity trigger collider is available.")]
    private float proximityDistance = 2.25f;
    [SerializeField, Min(0f), Tooltip("Inner distance from the ghost anchor where the ghost is fully visible.")]
    private float proximityFullyVisibleDistance = 0f;
    [SerializeField, Min(0f), Tooltip("Optional dissolve follow speed. 0 applies distance changes immediately.")]
    private float proximityDissolveFollowSpeed = 0f;
    [SerializeField, Tooltip("Dissolve amount applied when a character is close.")]
    private float proximityVisibleDissolveAmount = 0f;
    [SerializeField, Tooltip("Dissolve amount applied when no character is close.")]
    private float proximityHiddenDissolveAmount = 1.12f;
    [SerializeField, Tooltip("Add GhostDissolveController to proximity targets if none exists.")]
    private bool addProximityDissolveControllerIfMissing = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onGhostDataChanged = new UnityEvent();
    [SerializeField] private UnityEvent onListened = new UnityEvent();
    [SerializeField] private UnityEvent onKnowledgeReactionUsed = new UnityEvent();
    [SerializeField] private UnityEvent onNoKnowledgeReactionAvailable = new UnityEvent();
    [SerializeField] private UnityEvent onGhostUnderstood = new UnityEvent();

    private readonly List<GhostKnowledgeReaction> availableReactionBuffer = new List<GhostKnowledgeReaction>();
    private readonly List<GhostKnowledgeReaction> choiceReactionBuffer = new List<GhostKnowledgeReaction>();
    private readonly List<Button> reactionChoiceButtons = new List<Button>();
    private readonly List<GhostDissolveController> proximityDissolveControllers = new List<GhostDissolveController>();

    private GameObject currentCharacter;
    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;
    private GameObject reactionChoicePanelInstance;
    private Transform reactionChoiceContentRoot;
    private Collider resolvedInteractionCollider;
    private bool isUnderstood;
    private float currentProximityDissolveAmount = float.NaN;

    public GhostData Data => ghostData;
    public bool HasData => ghostData != null;
    public bool IsUnderstood => isUnderstood;

    private void Reset()
    {
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
    }

    private void Awake()
    {
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
        resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        if (interactionCollider == null)
        {
            interactionCollider = resolvedInteractionCollider;
        }
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
        ResolveProximityDissolveControllers();
        ApplyProximityDissolveAmount(ResolveProximityDissolveAmount(), instant: true);
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;
        CloseReactionChoiceUi();
        DestroyInteractionInstance();
        currentCharacter = null;
    }

    private void LateUpdate()
    {
        UpdateInteractionUiPosition();
        UpdateProximityDissolve();
    }

    public void SetGhostData(GhostData data)
    {
        if (ghostData == data)
        {
            return;
        }

        ghostData = data;
        isUnderstood = false;
        currentProximityDissolveAmount = float.NaN;
        onGhostDataChanged.Invoke();
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null && isActiveAndEnabled && ghostData != null && (!playOnce || !isUnderstood);
    }

    public Collider GetInteractionDetectionCollider()
    {
        if (resolvedInteractionCollider == null)
        {
            resolvedInteractionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        }

        return resolvedInteractionCollider;
    }

    public Transform GetInteractionAnchor()
    {
        return transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return Mathf.Max(0.1f, interactionMaxDistance);
    }

    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return interactionPriority;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        if (currentCharacter == character)
        {
            return;
        }

        currentCharacter = character;
        ShowInteraction(currentCharacter != null && showInteractionUi && (!playOnce || !isUnderstood));
    }

    public string GetDisplayName()
    {
        if (ghostData == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(ghostData.displayName) ? ghostData.name : ghostData.displayName;
    }

    public string GetApparitionLine()
    {
        return ghostData != null ? ghostData.apparitionLine : string.Empty;
    }

    public string GetQuestion()
    {
        return ghostData != null ? ghostData.question : string.Empty;
    }

    public int GetAvailableReactions(List<GhostKnowledgeReaction> results)
    {
        if (results == null)
        {
            return 0;
        }

        results.Clear();
        if (ghostData == null || ghostData.reactions == null || ghostData.reactions.Count == 0)
        {
            return 0;
        }

        KnowledgeManager manager = KnowledgeManager.Instance;
        for (int i = 0; i < ghostData.reactions.Count; i++)
        {
            GhostKnowledgeReaction reaction = ghostData.reactions[i];
            if (reaction != null && reaction.IsAvailable(manager))
            {
                results.Add(reaction);
            }
        }

        return results.Count;
    }

    public bool TryResolveBestReaction(out GhostKnowledgeReaction reaction)
    {
        reaction = null;
        GetAvailableReactions(availableReactionBuffer);
        reaction = ResolveBestReaction(availableReactionBuffer);
        return reaction != null;
    }

    private GhostKnowledgeReaction ResolveBestReaction(List<GhostKnowledgeReaction> reactions)
    {
        if (reactions == null || reactions.Count == 0)
        {
            return null;
        }

        int bestScore = int.MinValue;
        GhostKnowledgeReaction bestReaction = null;
        for (int i = 0; i < reactions.Count; i++)
        {
            GhostKnowledgeReaction candidate = reactions[i];
            int score = candidate != null ? candidate.GetSpecificityScore() : int.MinValue;
            if (candidate != null && score > bestScore)
            {
                bestReaction = candidate;
                bestScore = score;
            }
        }

        return bestReaction;
    }

    public bool InteractWithGhost()
    {
        if (ghostData == null)
        {
            return false;
        }

        if (playOnce && isUnderstood)
        {
            InfoBoxUI.TryShow(alreadyUnderstoodMessage, feedbackDuration);
            return false;
        }

        KnowledgeManager manager = KnowledgeManager.GetOrCreate();
        if (unlockKnowledgeOnListen && manager != null)
        {
            manager.UnlockKnowledgeList(ghostData.knowledgeUnlockedOnListen);
        }

        onListened.Invoke();

        int availableCount = GetAvailableReactions(availableReactionBuffer);
        if (availableCount == 0)
        {
            InfoBoxUI.TryShow(BuildMissingKnowledgeFeedback(), feedbackDuration);
            onNoKnowledgeReactionAvailable.Invoke();
            return false;
        }

        if (showReactionChoiceUi && (!autoUseSingleAvailableReaction || availableCount > 1))
        {
            ShowReactionChoiceUi(availableReactionBuffer);
            return true;
        }

        GhostKnowledgeReaction reaction = ResolveBestReaction(availableReactionBuffer);
        return UseKnowledgeReaction(reaction);
    }

    public bool UseKnowledgeReaction(GhostKnowledgeReaction reaction)
    {
        if (ghostData == null || reaction == null)
        {
            return false;
        }

        KnowledgeManager manager = KnowledgeManager.GetOrCreate();
        if (manager != null)
        {
            manager.UnlockKnowledgeList(reaction.unlockKnowledge);
        }

        bool marksGhostUnderstood = reaction.marksGhostUnderstood;
        if (marksGhostUnderstood)
        {
            TriggerScriptableObjectDissolveTargets(instant: false);
        }

        TriggerDissolveEffects(reaction, manager);
        bool feedbackShown = InfoBoxUI.TryShow(
            BuildReactionFeedback(reaction),
            feedbackDuration,
            marksGhostUnderstood ? CompleteUnderstoodAfterSolvedDialogue : null);
        onKnowledgeReactionUsed.Invoke();

        if (marksGhostUnderstood && !feedbackShown)
        {
            ApplyUnderstoodState(true, invokeEvent: true);
        }

        return true;
    }

    private void TriggerDissolveEffects(GhostKnowledgeReaction reaction, KnowledgeManager manager)
    {
        if (dissolveEffectRules == null || dissolveEffectRules.Count == 0)
        {
            return;
        }

        for (int i = 0; i < dissolveEffectRules.Count; i++)
        {
            GhostDissolveEffectRule rule = dissolveEffectRules[i];
            if (rule != null && rule.CanTrigger(reaction, manager))
            {
                rule.Trigger();
            }
        }
    }

    private void UpdateProximityDissolve()
    {
        if (!enableProximityDissolve || ghostData == null || playOnce && isUnderstood)
        {
            return;
        }

        ApplyProximityDissolveAmount(ResolveProximityDissolveAmount(), instant: false);
    }

    private float ResolveProximityDissolveAmount()
    {
        if (!enableProximityDissolve || ghostData == null)
        {
            return proximityHiddenDissolveAmount;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled == null)
        {
            return proximityHiddenDissolveAmount;
        }

        Vector3 characterPosition = ResolveCharacterProximityPosition(controlled);
        if (!TryResolveProximityDistance01(characterPosition, out float distance01))
        {
            return proximityHiddenDissolveAmount;
        }

        return Mathf.Lerp(
            proximityVisibleDissolveAmount,
            proximityHiddenDissolveAmount,
            Mathf.Clamp01(distance01));
    }

    private static Vector3 ResolveCharacterProximityPosition(GameObject character)
    {
        if (character == null)
        {
            return Vector3.zero;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = character.GetComponentInChildren<SquadCharacterController>(true);
        }

        return controller != null ? controller.GetInteractionOriginWorldPosition() : character.transform.position;
    }

    private bool TryResolveProximityDistance01(Vector3 characterPosition, out float distance01)
    {
        distance01 = 1f;

        Transform anchor = GetInteractionAnchor();
        Vector3 anchorPosition = anchor != null ? anchor.position : transform.position;
        Collider trigger = ResolveProximityTriggerCollider();
        if (trigger != null)
        {
            if (!IsPositionInsideCollider(trigger, characterPosition))
            {
                return false;
            }

            float maxDistance = ResolveColliderDistanceFromAnchor(trigger, anchorPosition, characterPosition);
            distance01 = Mathf.InverseLerp(
                Mathf.Max(0f, proximityFullyVisibleDistance),
                Mathf.Max(0.001f, maxDistance),
                Vector3.Distance(anchorPosition, characterPosition));
            return true;
        }

        float fallbackDistance = Mathf.Max(0.1f, proximityDistance);
        float distance = Vector3.Distance(anchorPosition, characterPosition);
        if (distance > fallbackDistance)
        {
            return false;
        }

        distance01 = Mathf.InverseLerp(
            Mathf.Max(0f, proximityFullyVisibleDistance),
            fallbackDistance,
            distance);
        return true;
    }

    private Collider ResolveProximityTriggerCollider()
    {
        if (IsUsableProximityCollider(proximityTriggerCollider))
        {
            return proximityTriggerCollider;
        }

        if (IsUsableProximityCollider(interactionCollider) && interactionCollider.isTrigger)
        {
            return interactionCollider;
        }

        Collider interaction = GetInteractionDetectionCollider();
        return IsUsableProximityCollider(interaction) && interaction.isTrigger ? interaction : null;
    }

    private static bool IsUsableProximityCollider(Collider collider)
    {
        return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
    }

    private static bool IsPositionInsideCollider(Collider collider, Vector3 position)
    {
        if (collider == null)
        {
            return false;
        }

        Vector3 closest = collider.ClosestPoint(position);
        return (closest - position).sqrMagnitude <= 0.0001f;
    }

    private float ResolveColliderDistanceFromAnchor(Collider collider, Vector3 anchorPosition, Vector3 characterPosition)
    {
        if (collider == null)
        {
            return Mathf.Max(0.1f, proximityDistance);
        }

        Vector3 direction = characterPosition - anchorPosition;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Mathf.Max(0.1f, proximityDistance);
        }

        Bounds bounds = collider.bounds;
        float probeDistance = Mathf.Max(0.1f, bounds.extents.magnitude + Vector3.Distance(anchorPosition, bounds.center) + 0.1f);
        Vector3 boundaryProbe = anchorPosition + direction.normalized * probeDistance;
        Vector3 boundaryPoint = collider.ClosestPoint(boundaryProbe);
        float resolvedDistance = Vector3.Distance(anchorPosition, boundaryPoint);
        return Mathf.Max(0.1f, resolvedDistance);
    }

    private void ApplyProximityDissolveAmount(float targetAmount, bool instant)
    {
        if (!enableProximityDissolve)
        {
            return;
        }

        float nextAmount = targetAmount;
        if (!instant && proximityDissolveFollowSpeed > 0f && !float.IsNaN(currentProximityDissolveAmount))
        {
            nextAmount = Mathf.MoveTowards(
                currentProximityDissolveAmount,
                targetAmount,
                proximityDissolveFollowSpeed * Time.deltaTime);
        }

        if (!float.IsNaN(currentProximityDissolveAmount) &&
            Mathf.Abs(currentProximityDissolveAmount - nextAmount) <= 0.0005f)
        {
            return;
        }

        currentProximityDissolveAmount = nextAmount;

        ResolveProximityDissolveControllers();
        for (int i = 0; i < proximityDissolveControllers.Count; i++)
        {
            GhostDissolveController dissolve = proximityDissolveControllers[i];
            if (dissolve != null)
            {
                if (dissolve.IsTransitioning)
                {
                    dissolve.LerpDissolveAmount(nextAmount, 0f);
                }
                else
                {
                    dissolve.SetDissolveAmount(nextAmount);
                }
            }
        }
    }

    private void ResolveProximityDissolveControllers()
    {
        proximityDissolveControllers.Clear();
        if (proximityDissolveTargets != null && proximityDissolveTargets.Count > 0)
        {
            for (int i = 0; i < proximityDissolveTargets.Count; i++)
            {
                AddProximityDissolveTarget(proximityDissolveTargets[i]);
            }
        }
        else
        {
            AddProximityDissolveTarget(gameObject);
        }
    }

    private void AddProximityDissolveTarget(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        GhostDissolveController[] controllers = target.GetComponentsInChildren<GhostDissolveController>(true);
        if ((controllers == null || controllers.Length == 0) && addProximityDissolveControllerIfMissing)
        {
            GhostDissolveController created = target.AddComponent<GhostDissolveController>();
            proximityDissolveControllers.Add(created);
            return;
        }

        if (controllers == null)
        {
            return;
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            GhostDissolveController controller = controllers[i];
            if (controller != null && !proximityDissolveControllers.Contains(controller))
            {
                proximityDissolveControllers.Add(controller);
            }
        }
    }

    public void RestoreUnderstoodState(bool understood)
    {
        ApplyUnderstoodState(understood, invokeEvent: false);
        if (understood)
        {
            TriggerScriptableObjectDissolveTargets(instant: true);
        }
    }

    public string GetPersistentGhostId()
    {
        if (ghostData == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(ghostData.ghostId))
        {
            return ghostData.ghostId.Trim();
        }

        return !string.IsNullOrWhiteSpace(ghostData.name) ? ghostData.name : string.Empty;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (LocalInputRouter.IsInteractConsumed || InputFocusStack.HasAnyFocus())
        {
            return;
        }

        GameObject character = ResolveInteractionCharacter();
        if (character == null)
        {
            return;
        }

        if (!LocalInputRouter.TryConsumeInteract())
        {
            return;
        }

        InteractWithGhost();
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (reactionChoicePanelInstance == null || !reactionChoicePanelInstance.activeSelf)
        {
            return;
        }

        CloseReactionChoiceUi();
    }

    private GameObject ResolveInteractionCharacter()
    {
        if (CanUseCharacter(currentCharacter, requireLocalControl: true))
        {
            return currentCharacter;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        return CanUseCharacter(controlled, requireLocalControl: true) ? controlled : null;
    }

    private bool CanUseCharacter(GameObject character, bool requireLocalControl)
    {
        if (character == null || ghostData == null)
        {
            return false;
        }

        if (requireLocalControl && !IsSameCharacter(LocalPlayerUtils.GetControlledCharacter(), character))
        {
            return false;
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            character.transform,
            GetInteractionDetectionCollider(),
            GetInteractionAnchor(),
            interactionMaxDistance);
    }

    private string BuildMissingKnowledgeFeedback()
    {
        string line = !string.IsNullOrWhiteSpace(ghostData.missingKnowledgeLine)
            ? ghostData.missingKnowledgeLine
            : ghostData.question;

        return JoinFeedbackLines(ghostData.apparitionLine, ghostData.question, line);
    }

    private string BuildReactionFeedback(GhostKnowledgeReaction reaction)
    {
        string option = reaction != null ? reaction.optionText : string.Empty;
        string response = reaction != null ? reaction.responseLine : string.Empty;
        if (!string.IsNullOrWhiteSpace(option) && !string.IsNullOrWhiteSpace(playerOptionPrefix))
        {
            option = playerOptionPrefix + option.Trim();
        }

        if (!string.IsNullOrWhiteSpace(response) && !string.IsNullOrWhiteSpace(ghostResponsePrefix))
        {
            response = ghostResponsePrefix + response.Trim();
        }

        return JoinFeedbackLines(ghostData.apparitionLine, option, response);
    }

    private void ShowReactionChoiceUi(List<GhostKnowledgeReaction> reactions)
    {
        CloseReactionChoiceUi();
        if (reactions == null || reactions.Count == 0)
        {
            return;
        }

        choiceReactionBuffer.Clear();
        for (int i = 0; i < reactions.Count; i++)
        {
            if (reactions[i] != null)
            {
                choiceReactionBuffer.Add(reactions[i]);
            }
        }

        choiceReactionBuffer.Sort(CompareReactionsForChoice);
        CreateReactionChoicePanel();
        if (reactionChoicePanelInstance == null || reactionChoiceContentRoot == null)
        {
            GhostKnowledgeReaction fallback = ResolveBestReaction(choiceReactionBuffer);
            UseKnowledgeReaction(fallback);
            return;
        }

        for (int i = 0; i < choiceReactionBuffer.Count; i++)
        {
            CreateReactionChoiceButton(choiceReactionBuffer[i]);
        }

        CreateReactionCloseButton();
        reactionChoicePanelInstance.SetActive(true);
        InputFocusStack.Push(this);
    }

    private static int CompareReactionsForChoice(GhostKnowledgeReaction left, GhostKnowledgeReaction right)
    {
        int leftScore = left != null ? left.GetSpecificityScore() : int.MinValue;
        int rightScore = right != null ? right.GetSpecificityScore() : int.MinValue;
        return rightScore.CompareTo(leftScore);
    }

    private void CreateReactionChoicePanel()
    {
        Canvas canvas = null;
        Transform parent = reactionChoiceParent;
        if (parent == null)
        {
            GameObject canvasObject = new GameObject("GhostReactionChoiceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 140;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            parent = canvasObject.transform;
        }

        reactionChoicePanelInstance = new GameObject("GhostReactionChoicePanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        reactionChoicePanelInstance.transform.SetParent(parent, false);

        RectTransform panelRect = reactionChoicePanelInstance.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(Mathf.Max(320f, reactionChoiceWidth), 0f);

        Image image = reactionChoicePanelInstance.GetComponent<Image>();
        image.color = new Color(0.04f, 0.045f, 0.05f, 0.94f);

        VerticalLayoutGroup layout = reactionChoicePanelInstance.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 22, 22);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = reactionChoicePanelInstance.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateReactionChoiceLabel(GetDisplayName(), 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateReactionChoiceLabel(GetQuestion(), 18f, FontStyles.Normal, TextAlignmentOptions.Center);

        GameObject content = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(reactionChoicePanelInstance.transform, false);
        reactionChoiceContentRoot = content.transform;

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.childAlignment = TextAnchor.MiddleCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (canvas != null)
        {
            reactionChoicePanelInstance.transform.SetAsLastSibling();
        }
    }

    private void CreateReactionChoiceLabel(string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        if (reactionChoicePanelInstance == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObject.transform.SetParent(reactionChoicePanelInstance.transform, false);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text.Trim();
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;

        LayoutElement layout = labelObject.GetComponent<LayoutElement>();
        layout.minHeight = Mathf.Max(28f, fontSize + 10f);
    }

    private void CreateReactionChoiceButton(GhostKnowledgeReaction reaction)
    {
        if (reaction == null || reactionChoiceContentRoot == null)
        {
            return;
        }

        Button button = CreateChoiceButton(reaction.optionText, reactionChoiceContentRoot);
        if (button == null)
        {
            return;
        }

        button.onClick.AddListener(() =>
        {
            CloseReactionChoiceUi();
            UseKnowledgeReaction(reaction);
        });
        reactionChoiceButtons.Add(button);
    }

    private void CreateReactionCloseButton()
    {
        if (reactionChoiceContentRoot == null || string.IsNullOrWhiteSpace(closeChoiceText))
        {
            return;
        }

        Button button = CreateChoiceButton(closeChoiceText, reactionChoiceContentRoot);
        if (button == null)
        {
            return;
        }

        button.onClick.AddListener(CloseReactionChoiceUi);
        reactionChoiceButtons.Add(button);
    }

    private Button CreateChoiceButton(string label, Transform parent)
    {
        GameObject buttonObject = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.16f, 0.18f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.24f, 0.24f, 0.27f, 1f);
        colors.pressedColor = new Color(0.11f, 0.11f, 0.13f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.minHeight = 48f;
        layout.preferredHeight = 56f;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 8f);
        textRect.offsetMax = new Vector2(-18f, -8f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = string.IsNullOrWhiteSpace(label) ? "..." : label.Trim();
        text.fontSize = 18f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        return button;
    }

    private void CloseReactionChoiceUi()
    {
        if (reactionChoicePanelInstance == null)
        {
            InputFocusStack.Pop(this);
            return;
        }

        for (int i = 0; i < reactionChoiceButtons.Count; i++)
        {
            if (reactionChoiceButtons[i] != null)
            {
                reactionChoiceButtons[i].onClick.RemoveAllListeners();
            }
        }

        reactionChoiceButtons.Clear();
        choiceReactionBuffer.Clear();

        GameObject root = reactionChoicePanelInstance;
        Canvas panelCanvas = root.GetComponentInParent<Canvas>();
        if (reactionChoiceParent == null && panelCanvas != null)
        {
            root = panelCanvas.gameObject;
        }

        Destroy(root);
        reactionChoicePanelInstance = null;
        reactionChoiceContentRoot = null;
        InputFocusStack.Pop(this);
    }

    private static string JoinFeedbackLines(string first, string second, string third)
    {
        string result = string.Empty;
        AppendLine(ref result, first);
        AppendLine(ref result, second);
        AppendLine(ref result, third);
        return result;
    }

    private static void AppendLine(ref string result, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (!string.IsNullOrEmpty(result))
        {
            result += "\n";
        }

        result += line.Trim();
    }

    private void ApplyUnderstoodState(bool understood, bool invokeEvent)
    {
        isUnderstood = understood;
        ShowInteraction(currentCharacter != null && showInteractionUi && (!playOnce || !isUnderstood));
        if (understood && invokeEvent)
        {
            onGhostUnderstood.Invoke();
        }
    }

    private void CompleteUnderstoodAfterSolvedDialogue()
    {
        if (this == null)
        {
            return;
        }

        ApplyUnderstoodState(true, invokeEvent: true);
    }

    private int TriggerScriptableObjectDissolveTargets(bool instant)
    {
        if (ghostData == null || ghostData.dissolveTargetGameObjectIds == null || ghostData.dissolveTargetGameObjectIds.Count == 0)
        {
            return 0;
        }

        int triggeredCount = 0;
        HashSet<string> triggeredIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < ghostData.dissolveTargetGameObjectIds.Count; i++)
        {
            string targetId = ghostData.dissolveTargetGameObjectIds[i];
            if (string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            targetId = targetId.Trim();
            if (!triggeredIds.Add(targetId))
            {
                continue;
            }

            if (!GameObjectID.TryFindGameObject(targetId, out GameObject target) || target == null)
            {
                Debug.LogWarning($"[GhostController] GameObjectID '{targetId}' introuvable pour le dissolve du fantome '{GetPersistentGhostId()}'.", this);
                continue;
            }

            triggeredCount += TriggerDissolveTarget(
                target,
                ghostData.includeChildrenInDissolveTargets,
                ghostData.addDissolveControllerIfMissing,
                ghostData.dissolveTargetDurationOverride,
                instant);
        }

        return triggeredCount;
    }

    private static int TriggerDissolveTarget(GameObject target, bool includeChildren, bool addControllerIfMissing, float durationOverride, bool instant)
    {
        if (target == null)
        {
            return 0;
        }

        GhostDissolveController[] controllers = includeChildren
            ? target.GetComponentsInChildren<GhostDissolveController>(true)
            : target.GetComponents<GhostDissolveController>();

        if ((controllers == null || controllers.Length == 0) && addControllerIfMissing)
        {
            controllers = new[] { target.AddComponent<GhostDissolveController>() };
        }

        if (controllers == null || controllers.Length == 0)
        {
            return 0;
        }

        int triggeredCount = 0;
        for (int i = 0; i < controllers.Length; i++)
        {
            GhostDissolveController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            if (instant)
            {
                controller.HideInstant();
            }
            else if (durationOverride > 0f)
            {
                controller.TriggerDissolve(durationOverride);
            }
            else
            {
                controller.TriggerDissolve();
            }

            triggeredCount++;
        }

        return triggeredCount;
    }

    private void ShowInteraction(bool show)
    {
        if (!show)
        {
            DestroyInteractionInstance();
            return;
        }

        if (interactionBoxInstance == null)
        {
            interactionBoxInstance = CreateInstance(interactionBox, boxesPanel);
            if (interactionBoxInstance == null)
            {
                interactionBoxInstance = CreateFallbackInteractionBox(boxesPanel);
            }

            if (interactionBoxInstance != null)
            {
                interactionCanvas = interactionBoxInstance.GetComponentInParent<Canvas>();
                ApplyInteractionText(interactionBoxInstance);
            }
        }

        if (interactionBoxInstance != null)
        {
            interactionBoxInstance.SetActive(true);
        }
    }

    private void ApplyInteractionText(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        TMP_Text tmp = instance.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = interactionText;
            return;
        }

        Text fallbackText = instance.GetComponentInChildren<Text>(true);
        if (fallbackText != null)
        {
            fallbackText.text = interactionText;
        }
    }

    private void UpdateInteractionUiPosition()
    {
        if (interactionBoxInstance == null || !interactionBoxInstance.activeSelf)
        {
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Transform anchor = GetInteractionAnchor();
        if (cam == null || anchor == null)
        {
            return;
        }

        Vector3 worldPosition = anchor.position + interactionOffset;
        Canvas canvas = interactionCanvas != null ? interactionCanvas : interactionBoxInstance.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            RectTransform rect = interactionBoxInstance.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            Vector3 screenPos = cam.WorldToScreenPoint(worldPosition);
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rect.position = screenPos;
                return;
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = canvas.worldCamera != null ? canvas.worldCamera : cam;
            if (canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out Vector2 localPoint))
            {
                rect.localPosition = localPoint;
            }

            return;
        }

        interactionBoxInstance.transform.position = worldPosition;
        Vector3 toCamera = interactionBoxInstance.transform.position - cam.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            interactionBoxInstance.transform.rotation = Quaternion.LookRotation(toCamera);
        }
    }

    private void DestroyInteractionInstance()
    {
        if (interactionBoxInstance == null)
        {
            return;
        }

        Destroy(interactionBoxInstance);
        interactionBoxInstance = null;
        interactionCanvas = null;
    }

    private GameObject CreateInstance(GameObject source, Transform parent)
    {
        if (source == null)
        {
            return null;
        }

        return parent != null ? Instantiate(source, parent) : Instantiate(source);
    }

    private GameObject CreateFallbackInteractionBox(Transform parent)
    {
        GameObject instance = new GameObject("GhostInteractionBox", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(GraphicRaycaster));
        if (parent != null)
        {
            instance.transform.SetParent(parent, false);
        }

        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220f, 50f);
        rect.localScale = Vector3.one * 0.03f;

        Canvas canvas = instance.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        TextMeshProUGUI label = instance.GetComponent<TextMeshProUGUI>();
        label.text = interactionText;
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        return instance;
    }

    private static bool IsSameCharacter(GameObject a, GameObject b)
    {
        return a != null && b != null && a.transform.root == b.transform.root;
    }
}
