// Role:
// Scene controller that binds a GhostData asset to a ghost GameObject.
// Usage:
// Attach to a ghost GameObject, assign GhostData, and configure knowledge reactions
// on the asset. The player interacts with the ghost; no free text input is used.
// Responsibilities:
// Expose ghost text, detect available knowledge reactions, unlock new knowledge,
// and mark the ghost understood when a reaction succeeds.
// Dependencies:
// GhostData, KnowledgeManager, CharacterInteractionDetection, LocalInputRouter, DialoguePanelUI.
// Precautions:
// This controller does not replace LocalVoiceLineController. Use voice lines for
// audio/subtitle delivery, and this component for knowledge-based interaction state.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using INab.VFXAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Playables;
using UnityEngine.UI;
using Lit.Story;
using Lit.Timeline;

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

public enum GhostResolutionActionType { PlayAnimationState, SetDoorOpen, SpawnPrefab, PlayTimeline, PlayStorySequence }

[System.Serializable]
public class GhostResolutionAction
{
    public GhostResolutionActionType actionType;
    [Tooltip("Une action ne peut etre executee qu'une fois pour ce fantome.")] public bool runOnce = true;
    [Header("Animation")]
    public Animator animator;
    public bool useLocalPlayerAnimator;
    public string animationState;
    public int animationLayer;
    [Min(0f)] public float animationCrossFade = 0.1f;
    [Header("Door")]
    public Door door;
    public bool openDoor = true;
    [Header("Prefab")]
    public GameObject prefab;
    public Transform spawnTarget;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    [Header("Timeline")]
    public PlayableDirector timelineDirector;
    public TimelineBindingProfile timelineBindingProfile;
    [Header("Sequence")]
    public StorySequenceRunner storySequenceRunner;
}

[System.Serializable]
public class GhostResolutionActionBinding
{
    [Tooltip("ID d'etape. 'legacy' cible la question historique.")] public string stepId;
    [Tooltip("ID de reaction. Vide applique les actions a toute resolution de l'etape.")] public string reactionId;
    public List<GhostResolutionAction> actions = new List<GhostResolutionAction>();
}

/// <summary>Stable marker placed on ghost-spawned content to prevent duplicate instantiation.</summary>
public sealed class GhostResolutionSpawnMarker : MonoBehaviour
{
    [SerializeField] private string actionId;
    public string ActionId => actionId;
    public void SetActionId(string value) => actionId = value;
}

/// <summary>
/// Assigns a GhostData asset to a scene ghost and resolves reactions from unlocked knowledge.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Lit/Narrative/Ghost Controller")]
public class GhostController : MonoBehaviour, ICharacterDetectedInteractable, ILitInfluenceReceiver, IRuntimeOutlineVisibilityGate
{
    [Header("Data")]
    /// <summary>Ghost investigation data assigned to this scene object.</summary>
    [SerializeField] private GhostData ghostData;
    /// <summary>If true, a successful understanding reaction hides further one-shot interaction.</summary>
    [SerializeField] private bool playOnce = true;
    /// <summary>If true, listening to the ghost unlocks GhostData.knowledgeUnlockedOnListen.</summary>
    [SerializeField] private bool unlockKnowledgeOnListen = true;

    [Header("Appearance Animation")]
    [SerializeField] private Animator appearanceAnimator;
    [SerializeField] private string appearanceAnimationState;
    [SerializeField, Min(0)] private int appearanceAnimationLayer;
    [SerializeField, Min(0f)] private float appearanceAnimationCrossFade = 0.1f;

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
    [SerializeField, Tooltip("Duree d'affichage du feedback. 0 utilise la duree par defaut.")]
    private float feedbackDuration = 0f;
    [SerializeField, Tooltip("Affiche les dialogues de fantome dans DialoguePanel au lieu d'InfoBox.")]
    private bool useDialoguePanelForFeedback = true;
    [SerializeField, Tooltip("Texte affiche si playOnce est actif et que le fantome est deja compris.")]
    private string alreadyUnderstoodMessage = "Ce souvenir a deja ete compris.";
#pragma warning disable CS0414
    [SerializeField, Tooltip("Conserve pour compatibilite des assets; les reactions affichent seulement la phrase de resolution.")]
    private string playerOptionPrefix = "Vous : ";
#pragma warning restore CS0414
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
    [SerializeField, Min(0f), Tooltip("Court verrou apres l'ouverture des choix pour ne jamais reutiliser l'action qui a ouvert le dialogue.")]
    private float reactionChoiceInputRearmDelay = 0.2f;

    [Header("Dissolve Effects")]
    [SerializeField, Tooltip("Scene-side dissolve effects triggered by knowledge reactions.")]
    private List<GhostDissolveEffectRule> dissolveEffectRules = new List<GhostDissolveEffectRule>();

    [Header("Proximity Dissolve")]
    [SerializeField, Tooltip("If enabled, the ghost stays fully dissolved while no controlled character is nearby.")]
    private bool enableProximityDissolve = true;
    [SerializeField, Tooltip("Targets affected by proximity reveal. Empty means this GameObject.")]
    private List<GameObject> proximityDissolveTargets = new List<GameObject>();
    [Min(0.1f), Tooltip("Radius used by the ghost proximity sphere detection.")]
    public float proximitySpherecastRadius = 6f;
    [SerializeField, Tooltip("Layers queried by the proximity sphere detection.")]
    private LayerMask proximitySpherecastLayerMask = ~0;
    [SerializeField, Tooltip("Trigger handling used by the proximity sphere detection.")]
    private QueryTriggerInteraction proximitySpherecastTriggerInteraction = QueryTriggerInteraction.Collide;
    [SerializeField, Min(0f), Tooltip("Inner distance from the ghost anchor where the ghost is fully visible.")]
    private float proximityFullyVisibleDistance = 0f;
    [SerializeField, Min(0f), Tooltip("Optional dissolve follow speed. 0 applies distance changes immediately.")]
    private float proximityDissolveFollowSpeed = 0f;
    [SerializeField, Range(0f, 1f), Tooltip("Dissolve visibility applied when a character is close. 0 is invisible, 1 is fully visible.")]
    private float proximityVisibleDissolveAmount = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("Dissolve visibility applied when no character is close. 0 is invisible, 1 is fully visible.")]
    private float proximityHiddenDissolveAmount = 0f;
    [SerializeField, Tooltip("Add GhostDissolveController to proximity targets if none exists.")]
    private bool addProximityDissolveControllerIfMissing = true;
    [SerializeField, Tooltip("Draw the proximity sphere detection radius when the ghost is selected.")]
    private bool drawProximitySpherecastGizmo = true;
    [SerializeField, Min(0f), Tooltip("Marge conservee apres revelation pour eviter les disparitions lorsque le joueur reste a la limite du rayon.")]
    private float proximityRevealExitHysteresis = 0.5f;
    [SerializeField, Min(0f), Tooltip("Temps laisse aux VFX d'apparition avant de rendre le mesh du fantome visible.")]
    private float ghostAppearanceRendererDelay = 0.45f;
    [SerializeField, Min(0f), Tooltip("Temps laisse aux VFX de disparition pour se vider avant de masquer le mesh du fantome.")]
    private float ghostDisappearanceRendererDelay = 1f;

    [Header("Proximity Presentation")]
    [SerializeField, Tooltip("Active le feedback de Fresnel et d'effets lorsque le joueur est tres proche du fantome.")]
    private bool enableProximityPresentation = true;
    [SerializeField, Min(0f), Tooltip("Distance independante du rayon d'apparition qui fige les effets du fantome.")]
    private float proximityPresentationDistance = 1.5f;
    [SerializeField, Min(0.01f), Tooltip("Duree de transition de la puissance Fresnel.")]
    private float proximityFresnelTransitionDuration = 0.35f;
    [SerializeField, Range(0f, 1f), Tooltip("Valeur Fresnel du fantome revele hors de la distance proche.")]
    private float revealedFresnelTexturePower = 0.96f;
    [SerializeField, Range(0f, 1f), Tooltip("Valeur Fresnel appliquee lorsque le joueur est tres proche.")]
    private float closeFresnelTexturePower = 1f;
    [SerializeField, Tooltip("Cibles visuelles explicites. Si vide, utilise les cibles de dissolve puis le fantome.")]
    private List<GameObject> proximityPresentationTargets = new List<GameObject>();
    [SerializeField, Tooltip("Effets explicites. Si vide, les CharacterEffect des cibles visuelles sont utilises.")]
    private List<CharacterEffect> proximityCharacterEffects = new List<CharacterEffect>();

    [Header("Resolved Ghost Cleanup")]
    [SerializeField, Tooltip("Desactive le GameObject du fantome une fois sa resolution et la fin de ses particules terminees.")]
    private bool deactivateGameObjectAfterFinalResolution = true;
    [SerializeField, Min(0f), Tooltip("Delai minimal laisse aux VFX arretes pour finir proprement.")]
    private float resolvedEffectMinimumCleanupDelay = 0.15f;
    [SerializeField, Min(0.1f), Tooltip("Delai de securite avant desactivation si un VFX ne signale jamais la fin de ses particules.")]
    private float resolvedEffectCleanupTimeout = 3f;

    [Header("Runtime Outline")]
    [SerializeField, Range(0f, 1f), Tooltip("Outline is allowed only when the current ghost dissolve amount is below this threshold.")]
    private float outlineVisibleDissolveThreshold = 0.5f;
    [SerializeField, Tooltip("When enabled, lower dissolve values are considered visible for runtime outlines.")]
    private bool outlineVisibleBelowDissolveThreshold = true;
    [SerializeField, Tooltip("Autorise l'outline du fantome lorsqu'il est la cible interactive actuellement la plus proche du joueur.")]
    private bool outlineWhileGhostIsRevealed = true;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale tres proche a laquelle l'outline du fantome est visible.")]
    private float ghostOutlineActivationDistance = 1.35f;

    [Header("Light Influence")]
    [SerializeField, Tooltip("Si actif, ce fantome n'apparait et ne reagit que dans une zone d'influence allumee.")]
    private bool requireLitInfluenceForAppearance;
    [SerializeField, Tooltip("Autorise les flammes allumees a reveler ce fantome.")]
    private bool reactToFlameInfluence = true;
    [SerializeField, Tooltip("Verifie directement les sources allumees si le scan d'influence n'a pas encore notifie ce fantome.")]
    private bool useDirectLitInfluenceFallback = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onGhostDataChanged = new UnityEvent();
    [SerializeField] private UnityEvent onListened = new UnityEvent();
    [SerializeField] private UnityEvent onKnowledgeReactionUsed = new UnityEvent();
    [SerializeField] private UnityEvent onNoKnowledgeReactionAvailable = new UnityEvent();
    [SerializeField] private UnityEvent onGhostUnderstood = new UnityEvent();
    [SerializeField] private UnityEvent onPuzzleStepStarted = new UnityEvent();
    [SerializeField] private UnityEvent onPuzzleStepResolved = new UnityEvent();
    [SerializeField] private UnityEvent onResolutionActionFailed = new UnityEvent();

    [Header("Resolution Actions")]
    [SerializeField, Tooltip("Actions de scene executees apres une reponse de resolution.")]
    private List<GhostResolutionActionBinding> resolutionActionBindings = new List<GhostResolutionActionBinding>();

    private readonly List<GhostKnowledgeReaction> availableReactionBuffer = new List<GhostKnowledgeReaction>();
    private readonly List<GhostKnowledgeReaction> choiceReactionBuffer = new List<GhostKnowledgeReaction>();
    private readonly List<Button> reactionChoiceButtons = new List<Button>();
    private readonly Dictionary<Button, GhostKnowledgeReaction> reactionByChoiceButton = new Dictionary<Button, GhostKnowledgeReaction>();
    private readonly List<GhostDissolveController> proximityDissolveControllers = new List<GhostDissolveController>();
    private readonly List<Renderer> ghostVisibilityRenderers = new List<Renderer>();
    private readonly HashSet<Renderer> ghostVisibilityRendererSet = new HashSet<Renderer>();
    private readonly List<Renderer> proximityPresentationRenderers = new List<Renderer>();
    private readonly List<CharacterEffect> resolvedProximityCharacterEffects = new List<CharacterEffect>();
    private readonly HashSet<Renderer> proximityPresentationRendererSet = new HashSet<Renderer>();
    private readonly HashSet<CharacterEffect> proximityCharacterEffectSet = new HashSet<CharacterEffect>();
    // Unity can restore a component without running field initializers after a domain reload.
    // Keep this lazily-created instead of assuming the initializer is always available.
    private MaterialPropertyBlock proximityPresentationPropertyBlock;
    private readonly HashSet<int> activeLitInfluenceSourceIds = new HashSet<int>();
    private static readonly Collider[] ProximitySpherecastHits = new Collider[32];
    private static readonly int FresnelTexturePowerId = Shader.PropertyToID("_Frensel_Texture_Power");
    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

    private GameObject currentCharacter;
    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;
    private GameObject reactionChoicePanelInstance;
    private Transform reactionChoiceContentRoot;
    private GhostKnowledgeReaction defaultChoiceReaction;
    private int reactionChoiceShownFrame = -1;
    private bool reactionChoiceAwaitingFreshInteract;
    private Coroutine reactionChoiceInputRearmRoutine;
    private Collider resolvedInteractionCollider;
    private bool isUnderstood;
    private int currentPuzzleStepIndex;
    private bool currentStepQuestionPresented;
    private bool conversationInProgress;
    private readonly HashSet<string> completedStepIds = new HashSet<string>(System.StringComparer.Ordinal);
    private readonly HashSet<string> executedActionIds = new HashSet<string>(System.StringComparer.Ordinal);
    private bool hasAppearedToPlayer;
    private bool isRevealedToPlayer;
    private float currentProximityDissolveAmount = float.NaN;
    private bool proximityDissolveControllersResolved;
    private bool ghostVisibilityRenderersResolved;
    private bool ghostRenderersVisible;
    private bool hasAppliedGhostRendererVisibility;
    private float currentProximityFresnelTexturePower = float.NaN;
    private float targetProximityFresnelTexturePower = float.NaN;
    private bool proximityPresentationResolved;
    private bool isPlayerInProximityPresentationRange;
    private bool proximityCharacterEffectsPlaying;
    private Coroutine resolvedGhostCleanupRoutine;
    private Coroutine ghostRendererVisibilityTransitionRoutine;
    private bool ghostRendererVisibilityTransitionTarget;
    private bool finalResolvedGhostCleanupInProgress;
    private int litInfluenceCacheFrame = -1;
    private bool cachedDirectLitInfluence;
    private int controlledRevealCacheFrame = -1;
    private bool cachedControlledReveal;
    private float cachedControlledRevealDistance01 = 1f;

    public GhostData Data => ghostData;
    public bool HasData => ghostData != null;
    public bool IsUnderstood => isUnderstood;
    public IReadOnlyList<GhostResolutionActionBinding> ResolutionActionBindings => resolutionActionBindings;
    public bool HasAppearedToPlayer => hasAppearedToPlayer;
    public bool IsRevealedToPlayer => isRevealedToPlayer;
    public bool AllowsRuntimeOutline => outlineWhileGhostIsRevealed &&
                                        isRevealedToPlayer &&
                                        ghostRenderersVisible &&
                                        CanAppearAtAll() &&
                                        IsControlledPlayerWithinGhostOutlineRange();
    public event System.Action<GhostController> Understood;

    private void Reset()
    {
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
    }

    private void Awake()
    {
        NormalizeProximityDissolveConfiguration();
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
        proximityDissolveControllersResolved = false;
        proximityPresentationResolved = false;
        ghostVisibilityRenderersResolved = false;
        hasAppliedGhostRendererVisibility = false;
        InvalidateRevealCaches();
        ResolveProximityDissolveControllers();
        ResolveGhostVisibilityRenderers();
        SetGhostRenderersVisible(false);
        RefreshRevealState(instantDissolve: true);
        StartCoroutine(RefreshInitialGhostVisibilityNextFrame());
    }

    private void OnDisable()
    {
        StopGhostRendererVisibilityTransition();
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;
        CloseReactionChoiceUi();
        DestroyInteractionInstance();
        currentCharacter = null;
        activeLitInfluenceSourceIds.Clear();
        InvalidateRevealCaches();
        ApplyRevealState(false, markAppeared: false);
        SetGhostRenderersVisible(false);
        ResetProximityPresentation();
        if (RuntimeOutlineSelectionManager.IsActiveInteractable(this))
        {
            RuntimeOutlineSelectionManager.Clear();
        }
    }

    private void LateUpdate()
    {
        if (finalResolvedGhostCleanupInProgress)
        {
            return;
        }

        RefreshRevealState(instantDissolve: false);
        RefreshRuntimeOutlineVisibility();
        UpdateInteractionUiPosition();
    }

    private void OnValidate()
    {
        NormalizeProximityDissolveConfiguration();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawProximitySpherecastGizmo || !enableProximityDissolve)
        {
            return;
        }

        Vector3 anchorPosition = transform.position;
        Transform anchor = GetInteractionAnchor();
        if (anchor != null)
        {
            anchorPosition = anchor.position;
        }

        float radius = Mathf.Max(0.1f, proximitySpherecastRadius);
        Color previousColor = Gizmos.color;

        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.08f);
        Gizmos.DrawSphere(anchorPosition, radius);
        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.85f);
        Gizmos.DrawWireSphere(anchorPosition, radius);

        float fullyVisibleRadius = Mathf.Min(Mathf.Max(0f, proximityFullyVisibleDistance), radius);
        if (fullyVisibleRadius > 0.01f)
        {
            Gizmos.color = new Color(0.1f, 1f, 0.45f, 0.7f);
            Gizmos.DrawWireSphere(anchorPosition, fullyVisibleRadius);
        }

        Gizmos.color = previousColor;
    }

    public void SetGhostData(GhostData data)
    {
        if (ghostData == data)
        {
            return;
        }

        ghostData = data;
        isUnderstood = false;
        currentPuzzleStepIndex = 0;
        currentStepQuestionPresented = false;
        completedStepIds.Clear();
        executedActionIds.Clear();
        currentProximityDissolveAmount = float.NaN;
        hasAppearedToPlayer = false;
        ApplyRevealState(false, markAppeared: false);
        InvalidateRevealCaches();
        onGhostDataChanged.Invoke();
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return TryEvaluateRevealForController(controller, out _);
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
        RefreshRevealStateForCharacter(character, updateDissolve: false, instantDissolve: false);
        bool interactionAvailable = ShouldShowInteractionFor(character);
        ShowInteraction(interactionAvailable);
        if (!interactionAvailable && RuntimeOutlineSelectionManager.IsActiveInteractable(this))
        {
            RuntimeOutlineSelectionManager.Clear();
        }
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
        GhostPuzzleStep step = GetCurrentPuzzleStep();
        return step != null ? step.introductionLine : string.Empty;
    }

    public string GetQuestion()
    {
        GhostPuzzleStep step = GetCurrentPuzzleStep();
        return step != null ? step.question : string.Empty;
    }

    public int GetAvailableReactions(List<GhostKnowledgeReaction> results)
    {
        if (results == null)
        {
            return 0;
        }

        results.Clear();
        GhostPuzzleStep step = GetCurrentPuzzleStep();
        if (step == null || step.reactions == null || step.reactions.Count == 0)
        {
            return 0;
        }

        KnowledgeManager manager = KnowledgeManager.Instance;
        for (int i = 0; i < step.reactions.Count; i++)
        {
            GhostKnowledgeReaction reaction = step.reactions[i];
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
            ShowGhostFeedback(alreadyUnderstoodMessage);
            return false;
        }

        if (conversationInProgress)
        {
            return false;
        }

        KnowledgeManager manager = KnowledgeManager.GetOrCreate();
        if (unlockKnowledgeOnListen && manager != null)
        {
            manager.UnlockKnowledgeList(ghostData.knowledgeUnlockedOnListen);
        }

        onListened.Invoke();

        if (!currentStepQuestionPresented)
        {
            currentStepQuestionPresented = true;
            onPuzzleStepStarted.Invoke();
            return ShowGhostFeedback(BuildCurrentQuestionFeedback());
        }

        int availableCount = GetAvailableReactions(availableReactionBuffer);
        if (availableCount == 0)
        {
            ShowGhostFeedback(BuildMissingKnowledgeFeedback());
            onNoKnowledgeReactionAvailable.Invoke();
            return false;
        }

        // A valid answer is always an explicit player choice, even when unique.
        ShowReactionChoiceUi(availableReactionBuffer);
        return true;
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

        TriggerDissolveEffects(reaction, manager);
        conversationInProgress = true;
        bool feedbackShown = ShowGhostFeedback(BuildReactionFeedback(reaction), () => CompletePuzzleStepAfterSolvedDialogue(reaction));
        onKnowledgeReactionUsed.Invoke();

        if (!feedbackShown) CompletePuzzleStepAfterSolvedDialogue(reaction);

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

    private void RefreshRevealState(bool instantDissolve)
    {
        if (finalResolvedGhostCleanupInProgress)
        {
            return;
        }

        bool revealed = TryEvaluateControlledCharacterReveal(out float distance01);
        ApplyRevealState(revealed, markAppeared: true);
        RequestGhostRenderersVisible(revealed);
        RefreshProximityPresentation(revealed, ResolveCharacterController(LocalPlayerUtils.GetControlledCharacter()));

        if (!enableProximityDissolve)
        {
            return;
        }

        float targetAmount = revealed
            ? Mathf.Lerp(proximityVisibleDissolveAmount, proximityHiddenDissolveAmount, Mathf.Clamp01(distance01))
            : proximityHiddenDissolveAmount;
        ApplyProximityDissolveAmount(targetAmount, instantDissolve);
    }

    private void RefreshRevealStateForCharacter(GameObject character, bool updateDissolve, bool instantDissolve)
    {
        if (finalResolvedGhostCleanupInProgress)
        {
            return;
        }

        SquadCharacterController controller = ResolveCharacterController(character);
        bool revealed = TryEvaluateRevealForController(controller, out float distance01);
        ApplyRevealState(revealed, markAppeared: true);
        RequestGhostRenderersVisible(revealed);
        RefreshProximityPresentation(revealed, controller);

        if (!updateDissolve || !enableProximityDissolve)
        {
            return;
        }

        float targetAmount = revealed
            ? Mathf.Lerp(proximityVisibleDissolveAmount, proximityHiddenDissolveAmount, Mathf.Clamp01(distance01))
            : proximityHiddenDissolveAmount;
        ApplyProximityDissolveAmount(targetAmount, instantDissolve);
    }

    private IEnumerator RefreshInitialGhostVisibilityNextFrame()
    {
        yield return null;
        RefreshRevealState(instantDissolve: true);
    }

    private void ResolveGhostVisibilityRenderers()
    {
        ghostVisibilityRenderersResolved = true;
        ghostVisibilityRenderers.Clear();
        ghostVisibilityRendererSet.Clear();

        if (proximityDissolveTargets != null && proximityDissolveTargets.Count > 0)
        {
            for (int i = 0; i < proximityDissolveTargets.Count; i++) AddGhostVisibilityRenderers(proximityDissolveTargets[i]);
        }
        else
        {
            AddGhostVisibilityRenderers(gameObject);
        }

        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
    }

    private void AddGhostVisibilityRenderers(GameObject target)
    {
        if (target == null) return;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            if (ghostVisibilityRendererSet.Add(renderer)) ghostVisibilityRenderers.Add(renderer);
        }
    }

    private void SetGhostRenderersVisible(bool visible)
    {
        if (!ghostVisibilityRenderersResolved) ResolveGhostVisibilityRenderers();
        if (hasAppliedGhostRendererVisibility && ghostRenderersVisible == visible && ghostVisibilityRenderers.Count > 0) return;

        ghostRenderersVisible = visible;
        hasAppliedGhostRendererVisibility = true;
        for (int i = 0; i < ghostVisibilityRenderers.Count; i++)
        {
            Renderer renderer = ghostVisibilityRenderers[i];
            if (renderer != null) renderer.enabled = visible;
        }
    }

    private void RequestGhostRenderersVisible(bool visible)
    {
        if (finalResolvedGhostCleanupInProgress)
        {
            return;
        }

        if (ghostRendererVisibilityTransitionRoutine != null && ghostRendererVisibilityTransitionTarget == visible)
        {
            return;
        }

        StopGhostRendererVisibilityTransition();
        float delay = visible ? ghostAppearanceRendererDelay : ghostDisappearanceRendererDelay;
        if (delay <= 0f)
        {
            SetGhostRenderersVisible(visible);
            return;
        }

        if ((visible && ghostRenderersVisible) || (!visible && !ghostRenderersVisible))
        {
            return;
        }

        ghostRendererVisibilityTransitionTarget = visible;
        ghostRendererVisibilityTransitionRoutine = StartCoroutine(SetGhostRenderersVisibleAfterDelay(visible, delay));
    }

    private IEnumerator SetGhostRenderersVisibleAfterDelay(bool visible, float delay)
    {
        yield return new WaitForSeconds(delay);
        ghostRendererVisibilityTransitionRoutine = null;
        if (!finalResolvedGhostCleanupInProgress)
        {
            SetGhostRenderersVisible(visible);
        }
    }

    private void StopGhostRendererVisibilityTransition()
    {
        if (ghostRendererVisibilityTransitionRoutine == null)
        {
            return;
        }

        StopCoroutine(ghostRendererVisibilityTransitionRoutine);
        ghostRendererVisibilityTransitionRoutine = null;
    }

    private bool TryEvaluateControlledCharacterReveal(out float distance01)
    {
        if (controlledRevealCacheFrame == Time.frameCount)
        {
            distance01 = cachedControlledRevealDistance01;
            return cachedControlledReveal;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        SquadCharacterController controller = ResolveCharacterController(controlled);
        cachedControlledReveal = TryEvaluateRevealForController(controller, out cachedControlledRevealDistance01);
        controlledRevealCacheFrame = Time.frameCount;
        distance01 = cachedControlledRevealDistance01;
        return cachedControlledReveal;
    }

    private bool TryEvaluateRevealForController(SquadCharacterController controller, out float distance01)
    {
        distance01 = 1f;
        if (!CanAttemptRevealForController(controller))
        {
            return false;
        }

        if (!enableProximityDissolve)
        {
            distance01 = 0f;
            return true;
        }

        return TryResolveProximityDistance01(controller, out distance01);
    }

    private bool CanAttemptRevealForController(SquadCharacterController controller)
    {
        return controller != null && CanAppearAtAll() && HasRequiredLitInfluence();
    }

    private bool CanAppearAtAll()
    {
        return isActiveAndEnabled && ghostData != null && (!playOnce || !isUnderstood);
    }

    private static SquadCharacterController ResolveCharacterController(GameObject character)
    {
        if (character == null)
        {
            return null;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = character.GetComponentInChildren<SquadCharacterController>(true);
        }

        return controller;
    }

    private bool TryResolveProximityDistance01(SquadCharacterController controller, out float distance01)
    {
        distance01 = 1f;
        if (controller == null)
        {
            return false;
        }

        Transform anchor = GetInteractionAnchor();
        Vector3 anchorPosition = anchor != null ? anchor.position : transform.position;
        float revealDistance = Mathf.Max(0.1f, proximitySpherecastRadius);
        float detectionDistance = revealDistance + (isRevealedToPlayer ? proximityRevealExitHysteresis : 0f);
        if (!IsControllerInsideProximitySphere(controller, anchorPosition, detectionDistance))
        {
            return false;
        }

        Vector3 characterPosition = controller.GetInteractionOriginWorldPosition();
        float distance = Vector3.Distance(anchorPosition, characterPosition);
        distance01 = Mathf.InverseLerp(
            Mathf.Max(0f, proximityFullyVisibleDistance),
            revealDistance,
            distance);
        return true;
    }

    private bool IsControllerInsideProximitySphere(SquadCharacterController controller, Vector3 anchorPosition, float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            anchorPosition,
            radius,
            ProximitySpherecastHits,
            proximitySpherecastLayerMask,
            proximitySpherecastTriggerInteraction);

        bool hasCharacterCollider = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = ProximitySpherecastHits[i];
            if (hit == null)
            {
                continue;
            }

            if (!IsColliderPartOfController(hit, controller))
            {
                continue;
            }

            hasCharacterCollider = true;
            break;
        }

        ClearProximitySpherecastHitBuffer(hitCount);
        if (hasCharacterCollider)
        {
            return true;
        }

        // Keep reveal robust for character prefabs whose interaction origin is valid
        // but whose colliders are disabled, filtered, or absent at this frame.
        Vector3 characterPosition = controller.GetInteractionOriginWorldPosition();
        return (characterPosition - anchorPosition).sqrMagnitude <= radius * radius;
    }

    private static bool IsColliderPartOfController(Collider collider, SquadCharacterController controller)
    {
        if (collider == null || controller == null)
        {
            return false;
        }

        Transform colliderTransform = collider.transform;
        Transform controllerTransform = controller.transform;
        return colliderTransform.root == controllerTransform.root ||
               colliderTransform == controllerTransform ||
               colliderTransform.IsChildOf(controllerTransform) ||
               controllerTransform.IsChildOf(colliderTransform);
    }

    private static void ClearProximitySpherecastHitBuffer(int hitCount)
    {
        int clampedHitCount = Mathf.Min(hitCount, ProximitySpherecastHits.Length);
        for (int i = 0; i < clampedHitCount; i++)
        {
            ProximitySpherecastHits[i] = null;
        }
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

        EnsureProximityDissolveControllersResolved();
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

    private void RefreshProximityPresentation(bool revealed, SquadCharacterController controller)
    {
        if (!enableProximityPresentation)
        {
            return;
        }

        bool isClose = revealed && IsControllerInProximityPresentationRange(controller);
        SetProximityPresentationClose(isClose);
        UpdateProximityFresnelTexturePower();
    }

    private bool IsControllerInProximityPresentationRange(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return false;
        }

        Transform anchor = GetInteractionAnchor();
        Vector3 anchorPosition = anchor != null ? anchor.position : transform.position;
        Vector3 characterPosition = controller.GetInteractionOriginWorldPosition();
        return (characterPosition - anchorPosition).sqrMagnitude <= proximityPresentationDistance * proximityPresentationDistance;
    }

    private bool IsControlledPlayerWithinGhostOutlineRange()
    {
        SquadCharacterController controller = ResolveCharacterController(LocalPlayerUtils.GetControlledCharacter());
        if (controller == null)
        {
            return false;
        }

        Transform anchor = GetInteractionAnchor();
        Vector3 anchorPosition = anchor != null ? anchor.position : transform.position;
        Vector3 characterPosition = controller.GetInteractionOriginWorldPosition();
        float distance = Mathf.Max(0.1f, ghostOutlineActivationDistance);
        return (characterPosition - anchorPosition).sqrMagnitude <= distance * distance;
    }

    private void BeginAppearanceProximityPresentation()
    {
        if (!enableProximityPresentation)
        {
            return;
        }

        EnsureProximityPresentationResolved();
        isPlayerInProximityPresentationRange = false;
        currentProximityFresnelTexturePower = closeFresnelTexturePower;
        targetProximityFresnelTexturePower = revealedFresnelTexturePower;
        ApplyProximityFresnelTexturePower(currentProximityFresnelTexturePower);
        SetProximityCharacterEffectsPlaying(true);
    }

    private void ResetProximityPresentation()
    {
        isPlayerInProximityPresentationRange = false;
        currentProximityFresnelTexturePower = float.NaN;
        targetProximityFresnelTexturePower = float.NaN;
        SetProximityCharacterEffectsPlaying(false);
    }

    private void SetProximityPresentationClose(bool isClose)
    {
        if (isPlayerInProximityPresentationRange == isClose && !float.IsNaN(targetProximityFresnelTexturePower))
        {
            return;
        }

        EnsureProximityPresentationResolved();
        isPlayerInProximityPresentationRange = isClose;
        targetProximityFresnelTexturePower = isClose ? closeFresnelTexturePower : revealedFresnelTexturePower;
        // The close-range Fresnel feedback must not stop the ghost's own VFX:
        // those effects represent the apparition and only follow reveal/hide state.
    }

    private void UpdateProximityFresnelTexturePower()
    {
        if (float.IsNaN(targetProximityFresnelTexturePower))
        {
            return;
        }

        if (float.IsNaN(currentProximityFresnelTexturePower))
        {
            currentProximityFresnelTexturePower = targetProximityFresnelTexturePower;
        }
        else
        {
            float delta = Mathf.Abs(closeFresnelTexturePower - revealedFresnelTexturePower);
            float speed = delta <= 0.0001f ? 1f : delta / Mathf.Max(0.01f, proximityFresnelTransitionDuration);
            currentProximityFresnelTexturePower = Mathf.MoveTowards(
                currentProximityFresnelTexturePower,
                targetProximityFresnelTexturePower,
                speed * Time.deltaTime);
        }

        ApplyProximityFresnelTexturePower(currentProximityFresnelTexturePower);
    }

    private void EnsureProximityPresentationResolved()
    {
        if (!proximityPresentationResolved)
        {
            ResolveProximityPresentationTargets();
        }
    }

    private void ResolveProximityPresentationTargets()
    {
        proximityPresentationResolved = true;
        proximityPresentationRenderers.Clear();
        resolvedProximityCharacterEffects.Clear();
        proximityPresentationRendererSet.Clear();
        proximityCharacterEffectSet.Clear();

        if (proximityPresentationTargets != null && proximityPresentationTargets.Count > 0)
        {
            for (int i = 0; i < proximityPresentationTargets.Count; i++) AddProximityPresentationTarget(proximityPresentationTargets[i]);
        }
        else if (proximityDissolveTargets != null && proximityDissolveTargets.Count > 0)
        {
            for (int i = 0; i < proximityDissolveTargets.Count; i++) AddProximityPresentationTarget(proximityDissolveTargets[i]);
        }
        else
        {
            AddProximityPresentationTarget(gameObject);
        }

        if (proximityCharacterEffects != null && proximityCharacterEffects.Count > 0)
        {
            for (int i = 0; i < proximityCharacterEffects.Count; i++) AddProximityCharacterEffect(proximityCharacterEffects[i]);
        }
    }

    private void AddProximityPresentationTarget(GameObject target)
    {
        if (target == null) return;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            if (proximityPresentationRendererSet.Add(renderer)) proximityPresentationRenderers.Add(renderer);
        }

        CharacterEffect[] effects = target.GetComponentsInChildren<CharacterEffect>(true);
        for (int i = 0; i < effects.Length; i++) AddProximityCharacterEffect(effects[i]);
    }

    private void AddProximityCharacterEffect(CharacterEffect effect)
    {
        if (effect != null && proximityCharacterEffectSet.Add(effect)) resolvedProximityCharacterEffects.Add(effect);
    }

    private void ApplyProximityFresnelTexturePower(float value)
    {
        EnsureProximityPresentationResolved();
        if (proximityPresentationPropertyBlock == null) proximityPresentationPropertyBlock = new MaterialPropertyBlock();
        for (int rendererIndex = 0; rendererIndex < proximityPresentationRenderers.Count; rendererIndex++)
        {
            Renderer renderer = proximityPresentationRenderers[rendererIndex];
            if (renderer == null) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null || !material.HasProperty(FresnelTexturePowerId)) continue;
                proximityPresentationPropertyBlock.Clear();
                renderer.GetPropertyBlock(proximityPresentationPropertyBlock, materialIndex);
                proximityPresentationPropertyBlock.SetFloat(FresnelTexturePowerId, value);
                renderer.SetPropertyBlock(proximityPresentationPropertyBlock, materialIndex);
            }
        }
    }

    private void SetProximityCharacterEffectsPlaying(bool shouldPlay)
    {
        if (proximityCharacterEffectsPlaying == shouldPlay) return;
        EnsureProximityPresentationResolved();
        proximityCharacterEffectsPlaying = shouldPlay;
        for (int i = 0; i < resolvedProximityCharacterEffects.Count; i++)
        {
            CharacterEffect effect = resolvedProximityCharacterEffects[i];
            if (effect == null) continue;
            if (shouldPlay) effect.StartEffect();
            else effect.StopEffect();
        }
    }

    private void RefreshRuntimeOutlineVisibility()
    {
        // Runtime outlines are owned by RuntimeOutlineSelectionManager. A
        // revealed ghost must never force its own outline, otherwise it can
        // visually override a closer item, flame, door, or another ghost.
        if (RuntimeOutlineSelectionManager.IsActiveInteractable(this))
        {
            RuntimeOutlineSelectionManager.RefreshActiveInteractable();
        }
    }

    private bool HasVisibleRuntimeOutlineDissolve()
    {
        if (TryAnyProximityDissolveControllerVisible(out bool visible))
        {
            return visible;
        }

        if (!float.IsNaN(currentProximityDissolveAmount))
        {
            return IsRuntimeOutlineDissolveVisible(currentProximityDissolveAmount);
        }

        return !enableProximityDissolve;
    }

    private bool TryAnyProximityDissolveControllerVisible(out bool visible)
    {
        visible = false;
        EnsureProximityDissolveControllersResolved();

        bool hasController = false;
        for (int i = 0; i < proximityDissolveControllers.Count; i++)
        {
            GhostDissolveController dissolve = proximityDissolveControllers[i];
            if (dissolve == null)
            {
                continue;
            }

            hasController = true;
            if (IsRuntimeOutlineDissolveVisible(dissolve.CurrentDissolveAmount))
            {
                visible = true;
                return true;
            }
        }

        return hasController;
    }

    private bool IsRuntimeOutlineDissolveVisible(float dissolveAmount)
    {
        float clampedAmount = Mathf.Clamp01(dissolveAmount);
        float clampedThreshold = Mathf.Clamp01(outlineVisibleDissolveThreshold);
        return outlineVisibleBelowDissolveThreshold
            ? clampedAmount < clampedThreshold
            : clampedAmount > clampedThreshold;
    }

    private void EnsureProximityDissolveControllersResolved()
    {
        if (!proximityDissolveControllersResolved)
        {
            ResolveProximityDissolveControllers();
        }
    }

    private void ResolveProximityDissolveControllers()
    {
        proximityDissolveControllersResolved = true;
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
        if ((controllers == null || controllers.Length == 0) &&
            addProximityDissolveControllerIfMissing &&
            TargetSupportsProximityDissolve(target))
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

    private static bool TargetSupportsProximityDissolve(GameObject target)
    {
        if (target == null) return false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material != null && material.HasProperty(DissolveAmountId)) return true;
            }
        }

        return false;
    }

    private void ApplyRevealState(bool revealed, bool markAppeared)
    {
        if (revealed && markAppeared)
        {
            MarkAppearedToPlayer();
        }

        if (isRevealedToPlayer == revealed)
        {
            return;
        }

        isRevealedToPlayer = revealed;
        if (revealed)
        {
            PlayAppearanceAnimation();
            BeginAppearanceProximityPresentation();
        }
        else
        {
            HandleRevealLost();
            ResetProximityPresentation();
        }
    }

    private void PlayAppearanceAnimation()
    {
        if (appearanceAnimator == null || string.IsNullOrWhiteSpace(appearanceAnimationState)) return;
        int layer = Mathf.Max(0, appearanceAnimationLayer);
        int state = Animator.StringToHash(appearanceAnimationState);
        if (!appearanceAnimator.HasState(layer, state))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[GhostController] Animation state '{appearanceAnimationState}' introuvable sur '{appearanceAnimator.name}'.", this);
#endif
            return;
        }
        appearanceAnimator.CrossFade(state, Mathf.Max(0f, appearanceAnimationCrossFade), layer);
    }

    private void MarkAppearedToPlayer()
    {
        hasAppearedToPlayer = true;
    }

    private void HandleRevealLost()
    {
        if (currentCharacter != null)
        {
            currentCharacter = null;
        }

        ShowInteraction(false);
        if (reactionChoicePanelInstance != null)
        {
            CloseReactionChoiceUi();
        }

        if (RuntimeOutlineSelectionManager.IsActiveInteractable(this))
        {
            RuntimeOutlineSelectionManager.Clear();
        }
    }

    private bool ShouldShowInteractionFor(GameObject character)
    {
        if (character == null || !showInteractionUi)
        {
            return false;
        }

        SquadCharacterController controller = ResolveCharacterController(character);
        return CanBeDetectedBy(controller);
    }

    private bool HasRequiredLitInfluence()
    {
        if (!requireLitInfluenceForAppearance)
        {
            return true;
        }

        if (activeLitInfluenceSourceIds.Count > 0)
        {
            return true;
        }

        if (!useDirectLitInfluenceFallback)
        {
            return false;
        }

        if (litInfluenceCacheFrame == Time.frameCount)
        {
            return cachedDirectLitInfluence;
        }

        cachedDirectLitInfluence = HasDirectLitInfluence();
        litInfluenceCacheFrame = Time.frameCount;
        return cachedDirectLitInfluence;
    }

    private bool HasDirectLitInfluence()
    {
        Collider targetCollider = GetInteractionDetectionCollider();
        Vector3 fallbackPoint = ResolveLitInfluenceProbePoint(targetCollider);

        if (reactToFlameInfluence)
        {
            IReadOnlyList<Flame> flames = LitInfluenceSourceFrameCache.ActiveFlames;
            for (int i = 0; i < flames.Count; i++)
            {
                Flame flame = flames[i];
                if (flame != null && flame.ProvidesLitInfluenceTo(targetCollider, fallbackPoint))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private Vector3 ResolveLitInfluenceProbePoint(Collider targetCollider)
    {
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        Transform anchor = GetInteractionAnchor();
        return anchor != null ? anchor.position : transform.position;
    }

    public void OnLitInfluenceEnter(LitInfluenceInfo info)
    {
        if (!ShouldReactToLitInfluence(info) || info.SourceId == 0)
        {
            return;
        }

        activeLitInfluenceSourceIds.Add(info.SourceId);
        InvalidateRevealCaches();
    }

    public void OnLitInfluenceStay(LitInfluenceInfo info)
    {
        if (!ShouldReactToLitInfluence(info) || info.SourceId == 0)
        {
            return;
        }

        activeLitInfluenceSourceIds.Add(info.SourceId);
        InvalidateRevealCaches();
    }

    public void OnLitInfluenceExit(LitInfluenceInfo info)
    {
        if (info.SourceId == 0)
        {
            return;
        }

        activeLitInfluenceSourceIds.Remove(info.SourceId);
        InvalidateRevealCaches();
        if (!HasRequiredLitInfluence())
        {
            ApplyRevealState(false, markAppeared: false);
        }
    }

    private bool ShouldReactToLitInfluence(LitInfluenceInfo info)
    {
        switch (info.SourceKind)
        {
            case LitInfluenceSourceKind.Flame:
            case LitInfluenceSourceKind.AncientFlame:
                return reactToFlameInfluence;

            default:
                return false;
        }
    }

    private void InvalidateRevealCaches()
    {
        litInfluenceCacheFrame = -1;
        controlledRevealCacheFrame = -1;
    }

    public void RestoreUnderstoodState(bool understood)
    {
        ApplyUnderstoodState(understood, invokeEvent: false);
        if (understood)
        {
            TriggerScriptableObjectDissolveTargets(instant: true);
        }
    }

    public int GetCurrentPuzzleStepIndex() => currentPuzzleStepIndex;
    public bool HasPresentedCurrentPuzzleStep() => currentStepQuestionPresented;
    public List<string> GetCompletedPuzzleStepIds() => new List<string>(completedStepIds);
    public List<string> GetExecutedResolutionActionIds() => new List<string>(executedActionIds);

    public void RestorePuzzleProgress(int stepIndex, bool questionPresented, List<string> completedSteps, List<string> executedActions)
    {
        currentPuzzleStepIndex = Mathf.Clamp(stepIndex, 0, ghostData != null ? ghostData.StepCount : 0);
        currentStepQuestionPresented = questionPresented;
        completedStepIds.Clear();
        if (completedSteps != null) foreach (string id in completedSteps) if (!string.IsNullOrWhiteSpace(id)) completedStepIds.Add(id);
        executedActionIds.Clear();
        if (executedActions != null) foreach (string id in executedActions) if (!string.IsNullOrWhiteSpace(id)) executedActionIds.Add(id);
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
        if (IsReactionChoiceOpen())
        {
            if (!reactionChoiceAwaitingFreshInteract &&
                Time.frameCount != reactionChoiceShownFrame &&
                LocalInputRouter.TryConsumeInteract())
            {
                SubmitSelectedReactionChoice();
            }

            return;
        }

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

    private bool IsReactionChoiceOpen()
    {
        return reactionChoicePanelInstance != null && reactionChoicePanelInstance.activeSelf && InputFocusStack.HasFocus(this);
    }

    private void SubmitSelectedReactionChoice()
    {
        GhostKnowledgeReaction reaction = defaultChoiceReaction;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
        {
            Button selectedButton = eventSystem.currentSelectedGameObject.GetComponent<Button>();
            if (selectedButton != null && reactionByChoiceButton.TryGetValue(selectedButton, out GhostKnowledgeReaction selectedReaction))
            {
                reaction = selectedReaction;
            }
        }

        if (reaction == null) return;
        CloseReactionChoiceUi();
        UseKnowledgeReaction(reaction);
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

        SquadCharacterController controller = ResolveCharacterController(character);
        if (!CanBeDetectedBy(controller))
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
        GhostPuzzleStep step = GetCurrentPuzzleStep();
        string line = step != null && !string.IsNullOrWhiteSpace(step.missingKnowledgeLine)
            ? step.missingKnowledgeLine
            : step != null ? step.question : string.Empty;

        return JoinFeedbackLines(step != null ? step.introductionLine : string.Empty, step != null ? step.question : string.Empty, line);
    }

    private string BuildCurrentQuestionFeedback()
    {
        GhostPuzzleStep step = GetCurrentPuzzleStep();
        return step == null ? string.Empty : JoinFeedbackLines(step.introductionLine, step.question, string.Empty);
    }

    private GhostPuzzleStep GetCurrentPuzzleStep()
    {
        return ghostData != null ? ghostData.GetStep(currentPuzzleStepIndex) : null;
    }

    private string GetCurrentStepId()
    {
        GhostPuzzleStep step = GetCurrentPuzzleStep();
        return step == null || string.IsNullOrWhiteSpace(step.stepId)
            ? "step_" + currentPuzzleStepIndex
            : step.stepId.Trim();
    }

    private string GetReactionId(GhostKnowledgeReaction reaction)
    {
        if (reaction != null && !string.IsNullOrWhiteSpace(reaction.reactionId)) return reaction.reactionId.Trim();
        GhostPuzzleStep step = GetCurrentPuzzleStep();
        int index = step != null && step.reactions != null ? step.reactions.IndexOf(reaction) : -1;
        return "reaction_" + index;
    }

    private void CompletePuzzleStepAfterSolvedDialogue(GhostKnowledgeReaction reaction)
    {
        if (this == null) return;
        StartCoroutine(CompletePuzzleStepRoutine(reaction));
    }

    private IEnumerator CompletePuzzleStepRoutine(GhostKnowledgeReaction reaction)
    {
        string stepId = GetCurrentStepId();
        completedStepIds.Add(stepId);
        onPuzzleStepResolved.Invoke();
        yield return ExecuteResolutionActions(stepId, GetReactionId(reaction));

        currentPuzzleStepIndex++;
        currentStepQuestionPresented = false;
        conversationInProgress = false;
        if (ghostData != null && currentPuzzleStepIndex < ghostData.StepCount)
        {
            currentStepQuestionPresented = true;
            onPuzzleStepStarted.Invoke();
            ShowGhostFeedback(BuildCurrentQuestionFeedback());
            yield break;
        }

        TriggerScriptableObjectDissolveTargets(instant: false);
        BeginFinalResolvedGhostCleanup();
        ApplyUnderstoodState(true, invokeEvent: true);
    }

    private IEnumerator ExecuteResolutionActions(string stepId, string reactionId)
    {
        if (resolutionActionBindings == null) yield break;
        for (int i = 0; i < resolutionActionBindings.Count; i++)
        {
            GhostResolutionActionBinding binding = resolutionActionBindings[i];
            if (binding == null || !MatchesActionBinding(binding, stepId, reactionId) || binding.actions == null) continue;
            for (int j = 0; j < binding.actions.Count; j++)
            {
                GhostResolutionAction action = binding.actions[j];
                string actionId = GetPersistentGhostId() + ":" + stepId + ":" + reactionId + ":" + i + ":" + j;
                if (action == null || (action.runOnce && executedActionIds.Contains(actionId))) continue;
                bool success = false;
                yield return ExecuteResolutionAction(action, actionId, value => success = value);
                if (success && action.runOnce) executedActionIds.Add(actionId);
                if (!success) onResolutionActionFailed.Invoke();
            }
        }
    }

    private static bool MatchesActionBinding(GhostResolutionActionBinding binding, string stepId, string reactionId)
    {
        return string.Equals((binding.stepId ?? string.Empty).Trim(), stepId, System.StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(binding.reactionId) || string.Equals(binding.reactionId.Trim(), reactionId, System.StringComparison.Ordinal));
    }

    private IEnumerator ExecuteResolutionAction(GhostResolutionAction action, string actionId, System.Action<bool> complete)
    {
        bool success = false;
        switch (action.actionType)
        {
            case GhostResolutionActionType.PlayAnimationState:
                Animator animator = action.useLocalPlayerAnimator ? ResolveLocalPlayerAnimator() : action.animator;
                int hash = Animator.StringToHash(action.animationState ?? string.Empty);
                if (animator != null && !string.IsNullOrWhiteSpace(action.animationState) && animator.HasState(Mathf.Max(0, action.animationLayer), hash))
                { animator.CrossFade(hash, Mathf.Max(0f, action.animationCrossFade), Mathf.Max(0, action.animationLayer)); success = true; }
                break;
            case GhostResolutionActionType.SetDoorOpen:
                if (action.door != null) { action.door.SetOpen(action.openDoor); success = true; }
                break;
            case GhostResolutionActionType.SpawnPrefab:
                if (action.prefab != null)
                {
                    if (action.runOnce && HasSpawnMarker(actionId)) { success = true; break; }
                    Transform target = action.spawnTarget != null ? action.spawnTarget : transform;
                    GameObject instance = Instantiate(action.prefab, target);
                    instance.transform.localPosition = action.localPosition;
                    instance.transform.localRotation = Quaternion.Euler(action.localEulerAngles);
                    GhostResolutionSpawnMarker marker = instance.GetComponent<GhostResolutionSpawnMarker>();
                    if (marker == null)
                    {
                        marker = instance.AddComponent<GhostResolutionSpawnMarker>();
                    }
                    marker.SetActionId(actionId);
                    success = true;
                }
                break;
            case GhostResolutionActionType.PlayTimeline:
                if (action.timelineDirector != null && action.timelineBindingProfile != null && TimelineManager.Instance != null)
                {
                    TimelinePlaybackHandle handle = TimelineManager.Instance.Play(action.timelineDirector, action.timelineBindingProfile);
                    while (!handle.IsDone) yield return null;
                    success = handle.State == TimelinePlaybackState.Completed;
                }
                break;
            case GhostResolutionActionType.PlayStorySequence:
                if (action.storySequenceRunner != null && action.storySequenceRunner.Play())
                {
                    while (action.storySequenceRunner != null && action.storySequenceRunner.IsPlaying) yield return null;
                    success = true;
                }
                break;
        }
        complete?.Invoke(success);
    }

    private Animator ResolveLocalPlayerAnimator()
    {
        GameObject player = LocalPlayerUtils.GetControlledCharacter();
        return player != null ? player.GetComponentInChildren<Animator>(true) : null;
    }

    private static bool HasSpawnMarker(string actionId)
    {
        GhostResolutionSpawnMarker[] markers = FindObjectsByType<GhostResolutionSpawnMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
            if (markers[i] != null && string.Equals(markers[i].ActionId, actionId, System.StringComparison.Ordinal)) return true;
        return false;
    }

    private string BuildReactionFeedback(GhostKnowledgeReaction reaction)
    {
        string response = reaction != null ? reaction.responseLine : string.Empty;
        if (!string.IsNullOrWhiteSpace(response) && !string.IsNullOrWhiteSpace(ghostResponsePrefix))
        {
            response = ghostResponsePrefix + response.Trim();
        }

        return JoinFeedbackLines(string.Empty, string.Empty, response);
    }

    private bool ShowGhostFeedback(string message)
    {
        return ShowGhostFeedback(message, null);
    }

    private bool ShowGhostFeedback(string message, System.Action onHidden)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (useDialoguePanelForFeedback && DialoguePanelUI.TryShow(message, feedbackDuration, onHidden))
        {
            return true;
        }

        return InfoBoxUI.TryShow(message, feedbackDuration, onHidden);
    }

    private void ShowReactionChoiceUi(List<GhostKnowledgeReaction> reactions)
    {
        CloseReactionChoiceUi();
        if (reactions == null || reactions.Count == 0)
        {
            return;
        }

        choiceReactionBuffer.Clear();
        reactionByChoiceButton.Clear();
        defaultChoiceReaction = null;
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
            Debug.LogError($"Ghost '{name}' could not create its answer-choice UI. The puzzle step was not resolved.", this);
            ShowGhostFeedback("Impossible d'afficher les reponses pour le moment.");
            return;
        }

        for (int i = 0; i < choiceReactionBuffer.Count; i++)
        {
            CreateReactionChoiceButton(choiceReactionBuffer[i]);
        }

        CreateReactionCloseButton();
        reactionChoicePanelInstance.SetActive(true);
        EnsureReactionChoiceEventSystem();
        SelectDefaultReactionChoice();
        reactionChoiceShownFrame = Time.frameCount;
        InputFocusStack.Push(this);
        BeginReactionChoiceInputRearm();
    }

    private static int CompareReactionsForChoice(GhostKnowledgeReaction left, GhostKnowledgeReaction right)
    {
        int leftScore = left != null ? left.GetSpecificityScore() : int.MinValue;
        int rightScore = right != null ? right.GetSpecificityScore() : int.MinValue;
        return rightScore.CompareTo(leftScore);
    }

    private static void EnsureReactionChoiceEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("GhostReactionChoiceEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private void SelectDefaultReactionChoice()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        foreach (KeyValuePair<Button, GhostKnowledgeReaction> pair in reactionByChoiceButton)
        {
            if (pair.Key != null && pair.Value == defaultChoiceReaction)
            {
                EventSystem.current.SetSelectedGameObject(pair.Key.gameObject);
                return;
            }
        }
    }

    private void BeginReactionChoiceInputRearm()
    {
        reactionChoiceAwaitingFreshInteract = true;
        SetReactionChoiceAnswerButtonsInteractable(false);
        if (reactionChoiceInputRearmRoutine != null)
        {
            StopCoroutine(reactionChoiceInputRearmRoutine);
        }

        reactionChoiceInputRearmRoutine = StartCoroutine(RearmReactionChoiceInputRoutine());
    }

    private IEnumerator RearmReactionChoiceInputRoutine()
    {
        float delay = Mathf.Max(0f, reactionChoiceInputRearmDelay);
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        reactionChoiceInputRearmRoutine = null;
        if (!IsReactionChoiceOpen())
        {
            yield break;
        }

        reactionChoiceAwaitingFreshInteract = false;
        SetReactionChoiceAnswerButtonsInteractable(true);
    }

    private void SetReactionChoiceAnswerButtonsInteractable(bool interactable)
    {
        foreach (KeyValuePair<Button, GhostKnowledgeReaction> pair in reactionByChoiceButton)
        {
            if (pair.Key != null)
            {
                pair.Key.interactable = interactable;
            }
        }
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
        reactionByChoiceButton[button] = reaction;
        if (defaultChoiceReaction == null)
        {
            defaultChoiceReaction = reaction;
        }
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
        if (reactionChoiceInputRearmRoutine != null)
        {
            StopCoroutine(reactionChoiceInputRearmRoutine);
            reactionChoiceInputRearmRoutine = null;
        }

        reactionChoiceAwaitingFreshInteract = false;
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
        reactionByChoiceButton.Clear();
        choiceReactionBuffer.Clear();
        defaultChoiceReaction = null;
        reactionChoiceShownFrame = -1;

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
        InvalidateRevealCaches();
        if (playOnce && isUnderstood)
        {
            // During the final cleanup, keep the reveal state untouched so the
            // cleanup controls the visual shutdown in one deterministic place.
            if (!finalResolvedGhostCleanupInProgress)
            {
                ApplyRevealState(false, markAppeared: false);
            }

            if (RuntimeOutlineSelectionManager.IsActiveInteractable(this))
            {
                RuntimeOutlineSelectionManager.Clear();
            }

            ShowInteraction(false);
        }
        else
        {
            ShowInteraction(ShouldShowInteractionFor(currentCharacter));
        }

        if (understood && invokeEvent)
        {
            onGhostUnderstood.Invoke();
            Understood?.Invoke(this);
        }
    }

    private void BeginFinalResolvedGhostCleanup()
    {
        if (!playOnce || !deactivateGameObjectAfterFinalResolution || finalResolvedGhostCleanupInProgress)
        {
            return;
        }

        finalResolvedGhostCleanupInProgress = true;
        StopGhostRendererVisibilityTransition();
        ShowInteraction(false);
        SetGhostRenderersVisible(false);
        SetProximityCharacterEffectsPlaying(false);
        if (resolvedGhostCleanupRoutine != null)
        {
            StopCoroutine(resolvedGhostCleanupRoutine);
        }

        resolvedGhostCleanupRoutine = StartCoroutine(FinalResolvedGhostCleanupRoutine());
    }

    private IEnumerator FinalResolvedGhostCleanupRoutine()
    {
        float elapsed = 0f;
        float minimumDelay = Mathf.Max(0f, resolvedEffectMinimumCleanupDelay);
        float timeout = Mathf.Max(minimumDelay, resolvedEffectCleanupTimeout);

        while (elapsed < timeout)
        {
            if (elapsed >= minimumDelay && !HasLiveProximityCharacterEffectParticles())
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        resolvedGhostCleanupRoutine = null;
        if (this != null && gameObject != null)
        {
            gameObject.SetActive(false);
        }
    }

    private bool HasLiveProximityCharacterEffectParticles()
    {
        EnsureProximityPresentationResolved();
        for (int i = 0; i < resolvedProximityCharacterEffects.Count; i++)
        {
            CharacterEffect effect = resolvedProximityCharacterEffects[i];
            if (effect != null && effect.vfxComponent != null && effect.vfxComponent.aliveParticleCount > 0)
            {
                return true;
            }
        }

        return false;
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

    private void NormalizeProximityDissolveConfiguration()
    {
        proximitySpherecastRadius = Mathf.Max(0.1f, proximitySpherecastRadius);
        proximityFullyVisibleDistance = Mathf.Clamp(proximityFullyVisibleDistance, 0f, proximitySpherecastRadius);
        proximityRevealExitHysteresis = Mathf.Max(0f, proximityRevealExitHysteresis);
        proximityPresentationDistance = Mathf.Max(0f, proximityPresentationDistance);
        proximityFresnelTransitionDuration = Mathf.Max(0.01f, proximityFresnelTransitionDuration);
        revealedFresnelTexturePower = Mathf.Clamp01(revealedFresnelTexturePower);
        closeFresnelTexturePower = Mathf.Clamp01(closeFresnelTexturePower);
        resolvedEffectMinimumCleanupDelay = Mathf.Max(0f, resolvedEffectMinimumCleanupDelay);
        resolvedEffectCleanupTimeout = Mathf.Max(resolvedEffectMinimumCleanupDelay, resolvedEffectCleanupTimeout);

        if (proximityVisibleDissolveAmount < proximityHiddenDissolveAmount)
        {
            proximityVisibleDissolveAmount = 1f;
            proximityHiddenDissolveAmount = 0f;
        }

        proximityVisibleDissolveAmount = Mathf.Clamp01(proximityVisibleDissolveAmount);
        proximityHiddenDissolveAmount = Mathf.Clamp01(proximityHiddenDissolveAmount);
        outlineVisibleDissolveThreshold = Mathf.Clamp01(outlineVisibleDissolveThreshold);
    }

    private static class LitInfluenceSourceFrameCache
    {
        private static readonly List<Flame> activeFlames = new List<Flame>();
        private static int cacheFrame = -1;

        public static IReadOnlyList<Flame> ActiveFlames
        {
            get
            {
                Refresh();
                return activeFlames;
            }
        }

        private static void Refresh()
        {
            if (cacheFrame == Time.frameCount)
            {
                return;
            }

            cacheFrame = Time.frameCount;
            activeFlames.Clear();

            Flame[] flames = UnityEngine.Object.FindObjectsByType<Flame>(FindObjectsInactive.Exclude);
            for (int i = 0; i < flames.Length; i++)
            {
                Flame flame = flames[i];
                if (flame != null && flame.isActiveAndEnabled && flame.IsEffectivelyLit)
                {
                    activeFlames.Add(flame);
                }
            }

        }
    }
}
