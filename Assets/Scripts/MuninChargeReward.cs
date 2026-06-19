using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum MuninChargeRewardType
{
    MemoryShard = 0,
    PacifiedMemory = 1,
    VigilAltar = 2,
    Custom = 3
}

// Point d'entree configurable pour toutes les recharges de Munin.
// Une nouvelle recompense se branche par UnityEvent, KnowledgeRequirement ou GhostController.
[DisallowMultipleComponent]
[AddComponentMenu("Lit/Munin/Munin Charge Reward")]
public class MuninChargeReward : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler,
    ILitInfluenceReceiver, IRuntimeOutlineVisibilityGate
{
    [Header("Reward")]
    [SerializeField, Min(0)] protected int rewardAmount = 1;
    [SerializeField] protected MuninChargeRewardType rewardType = MuninChargeRewardType.MemoryShard;
    [SerializeField, Tooltip("Ignore rewardAmount et remplit Munin jusqu'au maximum.")]
    protected bool refillToMaximum;
    [SerializeField, Tooltip("Rend cette source inutilisable apres une recompense effective.")]
    protected bool consumeOnUse = true;
    [SerializeField, Min(0f), Tooltip("Delai entre deux usages si la source n'est pas consommee.")]
    protected float reuseCooldown;

    [Header("Requirements")]
    [SerializeField, Tooltip("Connaissances directement requises.")]
    protected KnowledgeRequirement optionalKnowledgeRequirement = new KnowledgeRequirement();
    [SerializeField, Tooltip("Progression d'enquete exprimee avec les connaissances, categories ou tags existants.")]
    protected KnowledgeRequirement optionalInvestigationRequirement = new KnowledgeRequirement();
    [SerializeField, Tooltip("Fantome devant etre compris/apaisé avant attribution.")]
    protected GhostController optionalGhostRequirement;

    [Header("Activation")]
    [SerializeField, Tooltip("Permet de recuperer la recompense avec Interact.")]
    protected bool grantOnInteract = true;
    [SerializeField, Tooltip("Attribue la recompense quand le personnage controle entre dans le trigger.")]
    protected bool grantOnTriggerEnter;
    [SerializeField, Tooltip("Reevalue automatiquement la recompense apres un nouveau savoir.")]
    protected bool grantWhenRequirementsBecomeSatisfied;
    [SerializeField, Tooltip("Attribue automatiquement la recompense lorsque le fantome configure est apaise.")]
    protected bool grantWhenGhostUnderstood;
    [SerializeField, Tooltip("Seul le personnage controle localement peut recevoir cette source.")]
    protected bool requireControlledCharacter = true;

    [Header("Interaction")]
    [SerializeField] protected Collider interactionCollider;
    [SerializeField] protected Transform interactionAnchor;
    [SerializeField, Min(0.1f)] protected float interactionMaxDistance = 2f;
    [SerializeField] protected int interactionPriority = 95;
    [SerializeField, Tooltip("La source n'est selectionnable que sous l'influence d'une Flame allumee.")]
    protected bool requireLitInfluenceForInteraction;
    [SerializeField] protected bool reactToFlameInfluence = true;

    [Header("Feedback")]
    [SerializeField] protected bool showFeedback = true;
    [SerializeField, Tooltip("Titre optionnel. Une valeur vide utilise le texte du rewardType.")]
    protected string feedbackTitle;
    [SerializeField, Min(0f)] protected float feedbackDuration = 1.8f;
    [SerializeField] protected AudioClipSO rewardSfx;
    [SerializeField] protected GameObject rewardVfxPrefab;
    [SerializeField] protected Transform feedbackAnchor;
    [SerializeField] protected UnityEvent<int> onRewardGranted = new UnityEvent<int>();

    [Header("Consumed Presentation")]
    [SerializeField, Tooltip("Masque les renderers et desactive les colliders apres consommation.")]
    protected bool hidePresentationWhenConsumed = true;
    [SerializeField, Tooltip("Root visuel optionnel. Laisse vide pour utiliser ce GameObject.")]
    protected GameObject presentationRoot;

    [Header("Runtime")]
    [SerializeField] private bool consumed;

    private readonly HashSet<int> activeLitInfluenceSourceIds = new HashSet<int>();
    private GameObject detectedCharacter;
    private KnowledgeManager boundKnowledgeManager;
    private GhostController boundGhost;
    private float nextBindingRefreshTime;
    private float nextUseTime;

    public int RewardAmount => Mathf.Max(0, rewardAmount);
    public MuninChargeRewardType RewardType => rewardType;
    public bool RefillToMaximum => refillToMaximum;
    public bool ConsumeOnUse => consumeOnUse;
    public bool IsConsumed => consumed;
    public bool AllowsRuntimeOutline => CanPresentInteraction();

    protected virtual void Reset()
    {
        interactionAnchor = transform;
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
    }

    protected virtual void Awake()
    {
        interactionAnchor = interactionAnchor != null ? interactionAnchor : transform;
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
        ApplyConsumedPresentation();
    }

    protected virtual void OnEnable()
    {
        RefreshBindings();
        ApplyConsumedPresentation();
    }

    protected virtual void OnDisable()
    {
        UnbindKnowledgeManager();
        UnbindGhost();
        detectedCharacter = null;
        activeLitInfluenceSourceIds.Clear();
    }

    protected virtual void Update()
    {
        if (Time.unscaledTime >= nextBindingRefreshTime)
        {
            nextBindingRefreshTime = Time.unscaledTime + 0.5f;
            RefreshBindings();
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!grantOnTriggerEnter || other == null)
        {
            return;
        }

        GameObject character = ResolveCharacterRoot(other);
        if (character != null)
        {
            TryGrantToCharacter(character);
        }
    }

    public bool TryGrantToControlledCharacter()
    {
        return TryGrantToCharacter(LocalPlayerUtils.GetControlledCharacter());
    }

    public bool GrantReward()
    {
        return TryGrantToControlledCharacter();
    }

    public bool TryGrantToCharacter(GameObject character)
    {
        if (!CanGrant(character))
        {
            return false;
        }

        MuninController munin = MuninController.FindForCharacter(character);
        if (munin == null)
        {
            return false;
        }

        string reason = ResolveFeedbackTitle();
        int gained = munin.GrantChargeReward(RewardAmount, refillToMaximum, reason);
        if (gained <= 0)
        {
            return false;
        }

        nextUseTime = Time.unscaledTime + Mathf.Max(0f, reuseCooldown);
        if (consumeOnUse)
        {
            consumed = true;
            ApplyConsumedPresentation();
        }

        PlayFeedback(gained, reason);
        onRewardGranted.Invoke(gained);

        if (RuntimeOutlineSelectionManager.IsActiveInteractable(this))
        {
            RuntimeOutlineSelectionManager.Clear();
        }

        return true;
    }

    public bool AreRequirementsSatisfied()
    {
        KnowledgeManager manager = KnowledgeManager.Instance;
        bool knowledgeSatisfied = optionalKnowledgeRequirement == null ||
                                  optionalKnowledgeRequirement.IsSatisfied(manager);
        bool investigationSatisfied = optionalInvestigationRequirement == null ||
                                      optionalInvestigationRequirement.IsSatisfied(manager);
        bool ghostSatisfied = optionalGhostRequirement == null || optionalGhostRequirement.IsUnderstood;
        return knowledgeSatisfied && investigationSatisfied && ghostSatisfied;
    }

    public void RestoreConsumedState(bool value)
    {
        consumed = value;
        ApplyConsumedPresentation();
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null && grantOnInteract && CanPresentInteraction() && AreRequirementsSatisfied();
    }

    public Collider GetInteractionDetectionCollider()
    {
        if (interactionCollider == null)
        {
            interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, null);
        }

        return interactionCollider;
    }

    public Transform GetInteractionAnchor()
    {
        return interactionAnchor != null ? interactionAnchor : transform;
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
        detectedCharacter = character;
    }

    public bool TryHandleLocalInteract()
    {
        if (!grantOnInteract || detectedCharacter == null)
        {
            return false;
        }

        GameObject character = detectedCharacter;
        if (!CharacterInteractionDetection.IsCharacterWithinRange(
                character.transform,
                GetInteractionDetectionCollider(),
                GetInteractionAnchor(),
                GetInteractionMaxDistance(null)))
        {
            return true;
        }

        TryGrantToCharacter(character);
        return true;
    }

    public void OnLitInfluenceEnter(LitInfluenceInfo info)
    {
        if (ShouldReactToLitInfluence(info) && info.SourceId != 0)
        {
            activeLitInfluenceSourceIds.Add(info.SourceId);
        }
    }

    public void OnLitInfluenceStay(LitInfluenceInfo info)
    {
        OnLitInfluenceEnter(info);
    }

    public void OnLitInfluenceExit(LitInfluenceInfo info)
    {
        if (info.SourceId != 0)
        {
            activeLitInfluenceSourceIds.Remove(info.SourceId);
        }
    }

    protected void ConfigureDefaults(
        MuninChargeRewardType type,
        int amount,
        bool refill,
        bool interact,
        bool trigger,
        bool requirements,
        bool ghost,
        bool consume,
        float cooldown,
        bool requireLight,
        bool hideWhenConsumed)
    {
        rewardType = type;
        rewardAmount = Mathf.Max(0, amount);
        refillToMaximum = refill;
        grantOnInteract = interact;
        grantOnTriggerEnter = trigger;
        grantWhenRequirementsBecomeSatisfied = requirements;
        grantWhenGhostUnderstood = ghost;
        consumeOnUse = consume;
        reuseCooldown = Mathf.Max(0f, cooldown);
        requireLitInfluenceForInteraction = requireLight;
        hidePresentationWhenConsumed = hideWhenConsumed;
    }

    private bool CanGrant(GameObject character)
    {
        if (!isActiveAndEnabled || consumed || Time.unscaledTime < nextUseTime || character == null)
        {
            return false;
        }

        if (requireControlledCharacter && !IsControlledCharacter(character))
        {
            return false;
        }

        if (requireLitInfluenceForInteraction && activeLitInfluenceSourceIds.Count == 0)
        {
            return false;
        }

        return AreRequirementsSatisfied();
    }

    private bool CanPresentInteraction()
    {
        return isActiveAndEnabled &&
               !consumed &&
               Time.unscaledTime >= nextUseTime &&
               (!requireLitInfluenceForInteraction || activeLitInfluenceSourceIds.Count > 0);
    }

    private bool ShouldReactToLitInfluence(LitInfluenceInfo info)
    {
        return reactToFlameInfluence &&
               (info.SourceKind == LitInfluenceSourceKind.Flame ||
                info.SourceKind == LitInfluenceSourceKind.AncientFlame);
    }

    private void RefreshBindings()
    {
        KnowledgeManager manager = KnowledgeManager.Instance;
        if (manager != boundKnowledgeManager)
        {
            UnbindKnowledgeManager();
            boundKnowledgeManager = manager;
            if (boundKnowledgeManager != null)
            {
                boundKnowledgeManager.KnowledgeUnlocked += OnKnowledgeUnlocked;
            }
        }

        if (optionalGhostRequirement != boundGhost)
        {
            UnbindGhost();
            boundGhost = optionalGhostRequirement;
            if (boundGhost != null)
            {
                boundGhost.Understood += OnGhostUnderstood;
            }
        }
    }

    private void UnbindKnowledgeManager()
    {
        if (boundKnowledgeManager != null)
        {
            boundKnowledgeManager.KnowledgeUnlocked -= OnKnowledgeUnlocked;
            boundKnowledgeManager = null;
        }
    }

    private void UnbindGhost()
    {
        if (boundGhost != null)
        {
            boundGhost.Understood -= OnGhostUnderstood;
            boundGhost = null;
        }
    }

    private void OnKnowledgeUnlocked(KnowledgeSO knowledge)
    {
        if (grantWhenRequirementsBecomeSatisfied &&
            HasConfiguredKnowledgeRequirements() &&
            AreRequirementsSatisfied())
        {
            TryGrantToControlledCharacter();
        }
    }

    private void OnGhostUnderstood(GhostController ghost)
    {
        if (grantWhenGhostUnderstood &&
            optionalGhostRequirement != null &&
            ghost == optionalGhostRequirement &&
            AreRequirementsSatisfied())
        {
            TryGrantToControlledCharacter();
        }
    }

    private bool HasConfiguredKnowledgeRequirements()
    {
        return optionalKnowledgeRequirement != null && !optionalKnowledgeRequirement.IsEmpty() ||
               optionalInvestigationRequirement != null && !optionalInvestigationRequirement.IsEmpty();
    }

    private string ResolveFeedbackTitle()
    {
        if (!string.IsNullOrWhiteSpace(feedbackTitle))
        {
            return feedbackTitle.Trim();
        }

        switch (rewardType)
        {
            case MuninChargeRewardType.MemoryShard:
                return "Mémoire retrouvée";
            case MuninChargeRewardType.PacifiedMemory:
                return "Mémoire apaisée";
            case MuninChargeRewardType.VigilAltar:
                return "Veillée retrouvée";
            default:
                return "Souvenir retrouvé";
        }
    }

    private void PlayFeedback(int gained, string reason)
    {
        if (showFeedback)
        {
            string suffix = gained > 1 ? "charges" : "charge";
            InfoBoxUI.TryShow($"{reason}\nMunin récupère {gained} {suffix}", feedbackDuration);
        }

        Vector3 position = feedbackAnchor != null ? feedbackAnchor.position : transform.position;
        if (rewardSfx != null)
        {
            AudioManager.EnsureInstance()?.PlayClip(rewardSfx, position);
        }

        if (rewardVfxPrefab != null)
        {
            Instantiate(rewardVfxPrefab, position, Quaternion.identity);
        }
    }

    private void ApplyConsumedPresentation()
    {
        if (!hidePresentationWhenConsumed)
        {
            return;
        }

        GameObject root = presentationRoot != null ? presentationRoot : gameObject;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = !consumed;
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = !consumed;
            }
        }
    }

    private static GameObject ResolveCharacterRoot(Collider collider)
    {
        SquadCharacterController controller = collider.GetComponentInParent<SquadCharacterController>();
        if (controller != null)
        {
            return controller.gameObject;
        }

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    private static bool IsControlledCharacter(GameObject character)
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled == null || character == null)
        {
            return false;
        }

        return controlled.transform == character.transform ||
               controlled.transform.IsChildOf(character.transform) ||
               character.transform.IsChildOf(controlled.transform);
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        rewardAmount = Mathf.Max(0, rewardAmount);
        reuseCooldown = Mathf.Max(0f, reuseCooldown);
        interactionMaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
        feedbackDuration = Mathf.Max(0f, feedbackDuration);
        interactionAnchor = interactionAnchor != null ? interactionAnchor : transform;
        interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, interactionCollider);
    }
#endif
}
