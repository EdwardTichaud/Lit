using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Playables; // 📽️ Gestion des timelines propres à l'unité
using UnityEngine.Animations;
using UnityEngine.Timeline;  // 🎼 Lecture des TimelineAsset assignés au PlayableDirector local
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Symphonie.Movement;

/// <summary>
/// Représente une unité de combat. L'ajout d'un <see cref="CharacterController"/>
/// permet de déléguer la gestion de la gravité à Unity pour plus de cohérence.
/// Chaque unité embarque désormais son propre <see cref="PlayableDirector"/>
/// afin de conserver l'ensemble des effets narratifs décrits dans l'Histoire de
/// Symphonie tout en gagnant en modularité lors de la lecture des timelines.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayableDirector))]
public class CharacterUnit : MonoBehaviour, IDamageable, IHealable, IBuffable, IDebuffable
{
    public CharacterData Data;
    private const string DeathStateName = "Death";
    private const string ResurectionStateName = "Resurection";
    private const string HitFrontStateName = "Hit_Front";
    private const string HitBackStateName = "Hit_Back";
    private const string HitLeftStateName = "Hit_Left";
    private const string HitRightStateName = "Hit_Right";
    private static readonly int DeathStateShortHash = Animator.StringToHash(DeathStateName);
    private static readonly int ResurectionStateShortHash = Animator.StringToHash(ResurectionStateName);
    private static readonly int HitFrontStateShortHash = Animator.StringToHash(HitFrontStateName);
    private static readonly int HitBackStateShortHash = Animator.StringToHash(HitBackStateName);
    private static readonly int HitLeftStateShortHash = Animator.StringToHash(HitLeftStateName);
    private static readonly int HitRightStateShortHash = Animator.StringToHash(HitRightStateName);

    [Header("UI Components")]
    /// <summary>
    /// Barre de vie principale de l'unité (souvent affichée en monde ou via des widgets dédiés).
    /// Les TimelineUnits s'y abonnent désormais via <see cref="OnHealthChanged"/> pour éviter
    /// les manipulations directes de cette référence.
    /// </summary>
    public HPBar hpBar;
    public CustomBar customBar;

    [Header("Animations")]
    public AnimationClip hurtAnimation;
    public AnimationClip interceptedAnimation;
    public AnimationClip interceptionAnimation;

    private SpriteRenderer spriteRenderer;
    [HideInInspector] public Animator animator;
    private RootMotionApplier rootMotionApplier;
    private bool rootMotionSuppressed;
    private bool timelineEventsRegistered;
    private ConcentrationSystem concentrationSystem;
    // Indicateur évitant les multiples avertissements lorsque l'Animator enfant est introuvable.
    private bool hasLoggedMissingChildAnimator;
    // Indicateur évitant de répéter les avertissements lorsqu'on détecte plusieurs Animator valides chez les enfants.
    private bool hasLoggedMultipleChildAnimatorsWithController;
    private PlayableGraph prepareToTargetGraph;
    private AnimationClipPlayable prepareToTargetPlayable;
    private AwakeState awakeState; // Nouveau gestionnaire unifié des états Awake et dissonant.
    private AnimatorOverrideController movementOverrideController;
    private AnimationClip movementWalkClipKey;
    private AnimationClip movementRunClipKey;
    private static readonly string[] MovementWalkClipHints = { "Walk_Loop", "WalkLoop", "Walk" };
    private static readonly string[] MovementRunClipHints = { "Run_Loop", "RunLoop", "Run" };
    private static readonly string[] MovementClipExclusions = { "Stop", "Start", "Crouch", "Crouched" };
    private static readonly int AnimatorMovementBlendHash = Animator.StringToHash("Speed");
    private static readonly int AnimatorMovementDashHash = Animator.StringToHash("Dash_Battle");
    private static readonly int AnimatorMovementBlendStateHash = Animator.StringToHash("LocomotionBT");
    private const float MovementBlendDistance = 2f;

    // Permet de suivre si l'unité joue actuellement son animation d'Idle pour déclencher les sons adéquats.
    private bool isIdleActive;

    /// <summary>Hash du state Animator "Idle_Battle" pour accélérer les vérifications de disponibilité.</summary>
    private static readonly int AnimatorStateIdleBattle = Animator.StringToHash("Idle_Battle");
    /// <summary>Durée standard (en secondes) appliquée aux fondus d'animations idle.</summary>
    private const float IdleCrossFadeDurationSeconds = 0.1f;

    // Mise en cache des ancres caméra pour éviter de reparcourir toute la hiérarchie à chaque requête.
    /// <summary>
    /// Volume visuel approximatif de l'unité (mesh, colliders…), mis en cache afin
    /// d'éviter un recalcul systématique lorsque les caméras ont besoin d'estimer
    /// la taille du personnage. Cette information est indispensable pour adapter
    /// dynamiquement le cadrage face aux très grands ennemis.
    /// </summary>
    private Bounds cachedVisualBounds;

    /// <summary>
    /// Indique si <see cref="cachedVisualBounds"/> contient déjà une valeur fiable.
    /// </summary>
    private bool hasCachedVisualBounds;

    /// <summary>
    /// PlayableDirector individuel utilisé pour jouer les timelines de combat
    /// propres à cette unité. Ce composant remplace l'ancien système centralisé
    /// et garantit que les pistes "Caster" restent toujours correctement liées.
    /// </summary>
    private PlayableDirector battleDirector;

    #region Gestion des sets personnalisés
    // Les index de sets actifs sont stockés ici pour éviter toute mutation des ScriptableObjects.
    private int currentMusicalMoveSetIndex = -1;
    private int currentItemSetIndex = -1;

    /// <summary>
    /// Renvoie le set d'attaques musicales actuellement actif pour cette unité.
    /// </summary>
    public CharacterMusicalMoveSet ActiveMusicalMoveSet => GetActiveMusicalMoveSet();

    /// <summary>
    /// Renvoie le set d'objets actuellement actif pour cette unité.
    /// </summary>
    public CharacterItemSet ActiveItemSet => GetActiveItemSet();

    /// <summary>
    /// Active un set d'attaques musicales spécifique en fonction de son nom.
    /// </summary>
    public void ActivateMusicalMoveSet(string setName)
    {
        if (Data == null)
            return;

        if (Data.musicalMoveSets == null || Data.musicalMoveSets.Count == 0)
        {
            currentMusicalMoveSetIndex = -1;
            return;
        }

        for (int i = 0; i < Data.musicalMoveSets.Count; i++)
        {
            var set = Data.musicalMoveSets[i];
            if (set == null)
                continue;

            if (string.Equals(set.setName, setName, StringComparison.OrdinalIgnoreCase))
            {
                currentMusicalMoveSetIndex = i;
                return;
            }
        }

        currentMusicalMoveSetIndex = NormalizeSetIndex(Data.musicalMoveSets, 0);
    }

    /// <summary>
    /// Active un set d'objets spécifique en fonction de son nom.
    /// </summary>
    public void ActivateItemSet(string setName)
    {
        if (Data == null)
            return;

        if (Data.itemSets == null || Data.itemSets.Count == 0)
        {
            currentItemSetIndex = -1;
            return;
        }

        for (int i = 0; i < Data.itemSets.Count; i++)
        {
            var set = Data.itemSets[i];
            if (set == null)
                continue;

            if (string.Equals(set.setName, setName, StringComparison.OrdinalIgnoreCase))
            {
                currentItemSetIndex = i;
                return;
            }
        }

        currentItemSetIndex = NormalizeSetIndex(Data.itemSets, 0);
    }

    /// <summary>
    /// Retourne une nouvelle liste d'attaques musicales en respectant l'ordre du set actif.
    /// </summary>
    public List<MusicalMoveSO> OrderMovesForCurrentSet(IList<MusicalMoveSO> baseMoves)
    {
        if (baseMoves == null)
            return new List<MusicalMoveSO>();

        return ApplyMusicalSetOrdering(baseMoves);
    }

    /// <summary>
    /// Retourne une nouvelle liste d'items en respectant l'ordre du set actif.
    /// </summary>
    public List<ItemData> OrderItemsForCurrentSet(IList<ItemData> availableItems)
    {
        if (availableItems == null)
            return new List<ItemData>();

        return ApplyItemSetOrdering(availableItems);
    }

    private CharacterMusicalMoveSet GetActiveMusicalMoveSet()
    {
        if (Data == null || Data.musicalMoveSets == null || Data.musicalMoveSets.Count == 0)
            return null;

        if (currentMusicalMoveSetIndex < 0 || currentMusicalMoveSetIndex >= Data.musicalMoveSets.Count)
            currentMusicalMoveSetIndex = NormalizeSetIndex(Data.musicalMoveSets, 0);

        return currentMusicalMoveSetIndex >= 0 ? Data.musicalMoveSets[currentMusicalMoveSetIndex] : null;
    }

    private CharacterItemSet GetActiveItemSet()
    {
        if (Data == null || Data.itemSets == null || Data.itemSets.Count == 0)
            return null;

        if (currentItemSetIndex < 0 || currentItemSetIndex >= Data.itemSets.Count)
            currentItemSetIndex = NormalizeSetIndex(Data.itemSets, 0);

        return currentItemSetIndex >= 0 ? Data.itemSets[currentItemSetIndex] : null;
    }

    private List<MusicalMoveSO> ApplyMusicalSetOrdering(IList<MusicalMoveSO> availableMoves)
    {
        var result = new List<MusicalMoveSO>();
        if (availableMoves == null)
            return result;

        var activeSet = GetActiveMusicalMoveSet();
        if (activeSet != null && activeSet.prioritizedMoves != null)
        {
            foreach (var prioritized in activeSet.prioritizedMoves)
            {
                if (prioritized == null)
                    continue;

                for (int i = 0; i < availableMoves.Count; i++)
                {
                    var candidate = availableMoves[i];
                    if (candidate == null || candidate != prioritized)
                        continue;

                    if (!result.Contains(candidate))
                        result.Add(candidate);
                    break;
                }
            }
        }

        foreach (var move in availableMoves)
        {
            if (move == null)
                continue;

            if (!result.Contains(move))
                result.Add(move);
        }

        return result;
    }

    private List<ItemData> ApplyItemSetOrdering(IList<ItemData> availableItems)
    {
        var result = new List<ItemData>();
        if (availableItems == null)
            return result;

        var activeSet = GetActiveItemSet();
        if (activeSet != null && activeSet.prioritizedItems != null)
        {
            foreach (var prioritized in activeSet.prioritizedItems)
            {
                if (prioritized == null)
                    continue;

                for (int i = 0; i < availableItems.Count; i++)
                {
                    var candidate = availableItems[i];
                    if (candidate == null || candidate != prioritized)
                        continue;

                    if (!result.Contains(candidate))
                        result.Add(candidate);
                    break;
                }
            }
        }

        foreach (var item in availableItems)
        {
            if (item == null)
                continue;

            if (!result.Contains(item))
                result.Add(item);
        }

        return result;
    }

    private void InitializeLoadoutSets()
    {
        if (Data == null)
        {
            currentMusicalMoveSetIndex = -1;
            currentItemSetIndex = -1;
            return;
        }

        currentMusicalMoveSetIndex = NormalizeSetIndex(Data.musicalMoveSets, Data.defaultMusicalMoveSetIndex);
        currentItemSetIndex = NormalizeSetIndex(Data.itemSets, Data.defaultItemSetIndex);
    }

    private static int NormalizeSetIndex<T>(IList<T> sets, int desiredIndex)
    {
        if (sets == null || sets.Count == 0)
            return -1;

        if (desiredIndex < 0 || desiredIndex >= sets.Count)
            return sets.Count > 0 ? 0 : -1;

        return desiredIndex;
    }
    #endregion

    /// <summary>
    /// Indique si l'unité est en état Awake (fusion avec l'ange gardien).
    /// </summary>
    public bool IsAwake => awakeState != null && awakeState.IsAwake;

    /// <summary>Indique si l'unité est tombée dans l'état Dissonant.</summary>
    public bool IsDissonant => awakeState != null && awakeState.IsDissonant;

    // Gestionnaire de physique pour déléguer les collisions et la gravité à Unity.
    private CharacterController controller;
    // Vitesse verticale utilisée pour la chute des unités terrestres.
    private Vector3 fallVelocity = Vector3.zero;
    // Intensité de la gravité appliquée.
    private const float gravity = -9.81f;

    [Header("Détection du sol")]
    [Tooltip("Distance maximale pour vérifier la présence d'un sol sous l'unité.")]
    public float groundCheckDistance = 2f;
    [Tooltip("Layers considérés comme du sol pendant les combats.")]
    public LayerMask battleGroundLayer = 0;

    /// <summary>
    /// Indique si l'unité touche actuellement un support solide.
    /// </summary>
    public bool IsGrounded
    {
        get
        {
            // 🎯 Avant toute chose, on vérifie si un override d'altitude est en cours.
            //     Cela permet par exemple à une attaque d'ancrage de considérer
            //     une unité aérienne comme solidement arrimée au sol durant
            //     quelques tours.
            var altitudeOverride = GetAltitudeOverrideStatus();
            if (altitudeOverride != null)
            {
                if (altitudeOverride.IsSuspendedInAir)
                    return false; // Statut "en l'air" prioritaire : on force une réponse négative.

                if (altitudeOverride.IsForcedGrounded)
                    return true; // Inutile de consulter la physique : la contrainte prime.
            }

            return controller != null && controller.isGrounded;
        }
    }

    /// <summary>
    /// Indique si l'unité est de type aérien.
    /// </summary>
    public bool IsAirUnit => Data != null && Data.isAirUnit;

    /// <summary>
    ///     Référence mise en cache du composant gérant les overrides d'altitude.
    ///     L'objectif est d'éviter les <see cref="GetComponent{T}()"/> répétés
    ///     à chaque interrogation des propriétés publiques.
    /// </summary>
    private AltitudeOverrideStatus altitudeOverrideStatus;

    /// <summary>
    ///     Accès paresseux au composant <see cref="AltitudeOverrideStatus"/>.
    ///     Le paramètre <paramref name="forceRefresh"/> permet de synchroniser
    ///     la référence si un script externe supprime le composant à la volée.
    /// </summary>
    private AltitudeOverrideStatus GetAltitudeOverrideStatus(bool forceRefresh = false)
    {
        if (forceRefresh || altitudeOverrideStatus == null)
            altitudeOverrideStatus = GetComponent<AltitudeOverrideStatus>();

        return altitudeOverrideStatus;
    }

    /// <summary>
    ///     S'assure qu'un composant <see cref="AltitudeOverrideStatus"/> est présent
    ///     sur cette unité. Les MusicalMoves peuvent ainsi appliquer leurs effets
    ///     sans se soucier de l'état initial du GameObject.
    /// </summary>
    public AltitudeOverrideStatus EnsureAltitudeOverrideStatus()
    {
        var status = GetAltitudeOverrideStatus();
        if (status == null)
        {
            status = gameObject.AddComponent<AltitudeOverrideStatus>();
            altitudeOverrideStatus = status;
        }

        return status;
    }

    /// <summary>
    ///     Indique si l'unité est actuellement contrainte à rester au sol.
    ///     Cette information complète la propriété <see cref="IsGrounded"/>
    ///     en reflétant explicitement l'existence d'un override forcé.
    /// </summary>
    public bool IsForcedGrounded => GetAltitudeOverrideStatus()?.IsForcedGrounded ?? false;

    /// <summary>
    ///     Indique si l'unité est suspendue dans les airs via un override temporaire.
    ///     Pratique pour les contrôles d'altitude ou pour désactiver certains mouvements.
    /// </summary>
    public bool IsSuspendedInAir => GetAltitudeOverrideStatus()?.IsSuspendedInAir ?? false;

    /// <summary>
    /// Détecte si un sol existe sous l'unité à portée de <see cref="groundCheckDistance"/>.
    /// Cette information sert non seulement à permettre aux attaques terrestres
    /// d'atteindre une cible en vol lorsqu'un support se trouve sous elle,
    /// mais aussi à empêcher toute attaque aérienne lorsque la moindre surface
    /// solide est détectée, fidèle au récit de l'Histoire de Symphonie où les
    /// résonances du sol répondent aux cieux.
    /// </summary>
    public bool HasGroundBelow()
    {
        // 🪄 Un override d'altitude actif prime sur la physique classique :
        //     * une unité ancrée au sol doit être traitée comme si un support
        //       se trouvait toujours sous elle ;
        //     * une unité suspendue ne doit jamais détecter de sol pendant
        //       la durée de l'effet.
        var altitudeOverride = GetAltitudeOverrideStatus();
        if (altitudeOverride != null)
        {
            if (altitudeOverride.IsForcedGrounded)
                return true;

            if (altitudeOverride.IsSuspendedInAir)
                return false;
        }

        // Définit un masque par défaut si aucun n'est précisé dans l'éditeur.
        if (battleGroundLayer == 0)
            battleGroundLayer = LayerMask.GetMask("Battle_Ground");

        // Lancement d'un rayon vers le bas pour vérifier la présence d'un sol.
        Vector3 origin = transform.position;
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance, battleGroundLayer);
    }

    /// <summary>
    /// Recherche un Animator enfant à utiliser pour les timelines des MusicalMoves
    /// ou des Items. Les GameObjects inactifs sont examinés pour couvrir les
    /// modèles masqués dans l'éditeur, et l'objet racine portant le CharacterUnit
    /// est explicitement ignoré afin de respecter les consignes de binding.
    /// </summary>
    private Animator FindChildAnimatorForBindings()
    {
        var childAnimators = GetComponentsInChildren<Animator>(includeInactive: true);
        Animator animatorWithController = null;
        bool multipleControllersDetected = false; // Permet de réinitialiser l'avertissement si la hiérarchie redevient saine.

        foreach (var candidate in childAnimators)
        {
            if (candidate == null)
                continue; // Sécurité défensive : on ignore les références manquantes dans la hiérarchie.

            if (candidate.gameObject == gameObject)
                continue; // On passe l'objet racine pour se concentrer sur les véritables modèles.

            if (candidate.runtimeAnimatorController == null)
                continue; // L'Animator ne possède pas de controller : il ne peut pas piloter les animations requises.

            if (animatorWithController == null)
            {
                animatorWithController = candidate; // Premier candidat valide trouvé : on le met en mémoire.
                continue;
            }

            multipleControllersDetected = true; // Au moins deux Animator possèdent un controller dans la hiérarchie.

            if (!hasLoggedMultipleChildAnimatorsWithController)
            {
                Debug.LogWarning(
                    $"[CharacterUnit] Plusieurs Animator enfants avec un controller trouvés sur '{name}'. " +
                    $"Utilisation du premier détecté : '{animatorWithController.name}'.");
                hasLoggedMultipleChildAnimatorsWithController = true; // On évite de spammer la console si la situation persiste.
            }
        }

        if (!multipleControllersDetected)
            hasLoggedMultipleChildAnimatorsWithController = false; // Aucun doublon détecté : on autorise un futur avertissement si nécessaire.

        return animatorWithController;
    }

    /// <summary>
    /// Expose (et met en cache) l'Animator enfant servant de référence lors du
    /// lancement d'un MusicalMove ou de l'utilisation d'un objet. Un repli sur
    /// l'Animator du GameObject principal est effectué si aucun enfant n'est
    /// trouvé afin d'éviter de bloquer la progression du combat.
    /// </summary>
    public Animator GetCasterAnimator(bool forceRefresh = false)
    {
        bool cacheInvalid = forceRefresh || animator == null
            || (animator.gameObject == gameObject && !hasLoggedMissingChildAnimator);

        if (!cacheInvalid)
            return animator;

        Animator childAnimator = FindChildAnimatorForBindings();
        if (childAnimator != null)
        {
            animator = childAnimator;
            hasLoggedMissingChildAnimator = false;
            return animator;
        }

        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator != null)
        {
            animator = rootAnimator;

            if (!hasLoggedMissingChildAnimator)
            {
                Debug.LogWarning($"[CharacterUnit] Aucun Animator enfant trouvé sur '{name}'. Utilisation de l'Animator du GameObject racine par défaut.");
                hasLoggedMissingChildAnimator = true;
            }

            return animator;
        }

        if (!hasLoggedMissingChildAnimator)
        {
            Debug.LogWarning($"[CharacterUnit] Impossible de localiser un Animator pour '{name}'. Les timelines risquent de ne pas se jouer correctement.");
            hasLoggedMissingChildAnimator = true;
        }

        animator = null;
        return animator;
    }

    private RootMotionApplier ConfigureRootMotionApplier(Animator sourceAnimator)
    {
        if (sourceAnimator == null)
            return null;

        sourceAnimator.applyRootMotion = false;

        if (rootMotionApplier == null)
            rootMotionApplier = GetComponent<RootMotionApplier>();
        if (rootMotionApplier == null)
            rootMotionApplier = gameObject.AddComponent<RootMotionApplier>();

        rootMotionApplier.Configure(sourceAnimator, transform, controller);
        rootMotionApplier.IsActive = false;
        rootMotionApplier.LockYToGround = true;
        rootMotionApplier.ApplyRotation = true;
        rootMotionApplier.ScaleToDesiredSpeed = true;
        rootMotionApplier.SpeedScale = 1f;

        return rootMotionApplier;
    }

    public RootMotionApplier EnsureRootMotionApplier()
    {
        if (rootMotionApplier != null)
            return rootMotionApplier;

        Animator sourceAnimator = GetCasterAnimator();
        return ConfigureRootMotionApplier(sourceAnimator);
    }

    public void SetRootMotionActive(bool active, float desiredSpeed = 0f, bool scaleToSpeed = true)
    {
        if (rootMotionApplier == null)
            EnsureRootMotionApplier();

        if (rootMotionApplier == null)
            return;

        rootMotionApplier.ScaleToDesiredSpeed = scaleToSpeed;
        rootMotionApplier.DesiredSpeed = desiredSpeed;
        rootMotionApplier.IsActive = active && !rootMotionSuppressed;
    }

    public void SuppressRootMotion(bool suppress)
    {
        rootMotionSuppressed = suppress;

        if (rootMotionApplier != null && suppress)
            rootMotionApplier.IsActive = false;
    }

    private void RegisterBattleTimelineEvents()
    {
        if (battleDirector == null || timelineEventsRegistered)
            return;

        battleDirector.played += HandleBattleTimelinePlayed;
        battleDirector.stopped += HandleBattleTimelineStopped;
        timelineEventsRegistered = true;
    }

    private void HandleBattleTimelinePlayed(PlayableDirector director)
    {
        SuppressRootMotion(true);
    }

    private void HandleBattleTimelineStopped(PlayableDirector director)
    {
        SuppressRootMotion(false);
    }

    /// <summary>
    /// Fournit directement l'objet à utiliser comme binding "Caster" pour les
    /// timelines. En absence d'Animator enfant, l'objet racine est renvoyé pour
    /// éviter toute erreur bloquante.
    /// </summary>
    public GameObject GetCasterBindingTarget()
    {
        Animator casterAnimator = GetCasterAnimator();
        return casterAnimator != null ? casterAnimator.gameObject : gameObject;
    }

    /// <summary>
    /// Fournit un volume englobant approximatif de l'unité afin que les caméras
    /// puissent adapter automatiquement leur cadrage selon la taille du modèle.
    /// </summary>
    /// <param name="forceRefresh">Vrai pour ignorer le cache et recalculer immédiatement.</param>
    /// <returns>Bounds monde représentant la silhouette globale de l'unité.</returns>
    public Bounds GetVisualBounds(bool forceRefresh = false)
    {
        if (!forceRefresh && hasCachedVisualBounds)
            return cachedVisualBounds;

        if (TryComputeVisualBounds(out Bounds newBounds))
        {
            // ✅ Bounds valides : on les met en cache pour les prochaines requêtes.
            cachedVisualBounds = newBounds;
            hasCachedVisualBounds = true;
            return cachedVisualBounds;
        }

        // ⚠️ Fallback : aucune géométrie exploitable n'a été trouvée (ex : FX uniquement).
        //     On construit alors un volume standard centré sur la position de l'unité afin
        //     d'éviter les divisions par zéro lors des calculs de cadrage.
        Vector3 fallbackCenter = transform.position + Vector3.up;
        cachedVisualBounds = new Bounds(fallbackCenter, new Vector3(1.5f, 2f, 1.5f));
        hasCachedVisualBounds = true;
        return cachedVisualBounds;
    }

    /// <summary>
    /// Renvoie une estimation directe de la hauteur de l'unité en se basant sur
    /// les <see cref="Bounds"/> visuels. Cette méthode est utilisée par le gestionnaire
    /// de caméras pour déterminer l'offset vertical idéal du point de focus.
    /// </summary>
    /// <param name="forceRefresh">Vrai pour déclencher un recalcul du volume.</param>
    public float GetVisualHeightEstimate(bool forceRefresh = false)
    {
        Bounds bounds = GetVisualBounds(forceRefresh);
        return Mathf.Max(bounds.size.y, 0.1f);
    }

    /// <summary>
    /// Tente de construire un volume englobant à partir des renderers et colliders
    /// présents sous l'unité. Les FX temporaires (trail, particules…) sont ignorés
    /// pour éviter de gonfler artificiellement la taille estimée.
    /// </summary>
    private bool TryComputeVisualBounds(out Bounds aggregatedBounds)
    {
        aggregatedBounds = default;
        bool hasBounds = false;

        // 🧱 Étape 1 : on parcourt l'ensemble des MeshRenderer/SkinnedMeshRenderer/SpriteRenderer.
        var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (Renderer renderer in renderers)
        {
            if (!ShouldUseRendererForBounds(renderer))
                continue;

            if (!hasBounds)
            {
                aggregatedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                aggregatedBounds.Encapsulate(renderer.bounds);
            }
        }

        // 🛡️ Étape 2 : si aucun renderer pertinent n'a été trouvé, on tente un fallback via les colliders.
        if (!hasBounds)
        {
            var colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                    continue;

                if (!hasBounds)
                {
                    aggregatedBounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    aggregatedBounds.Encapsulate(collider.bounds);
                }
            }
        }

        return hasBounds;
    }

    /// <summary>
    /// Filtre les renderers à considérer lorsqu'on calcule un volume englobant.
    /// </summary>
    private static bool ShouldUseRendererForBounds(Renderer renderer)
    {
        if (renderer == null)
            return false;

        // Les particules, traînées et lignes étirent les bounds sur de grandes distances
        // et ne reflètent pas la taille réelle de l'unité.
        if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            return false;

        return renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is SpriteRenderer;
    }

    /// <summary>
    /// Détermine l'Animator le plus adapté à partir d'un GameObject donné pour
    /// configurer les bindings d'une timeline. Cette méthode garantit que la
    /// recherche respecte la règle imposant l'utilisation d'un Animator enfant.
    /// </summary>
    private Animator ResolveAnimatorForBinding(GameObject bindingSource)
    {
        if (bindingSource == null)
            return GetCasterAnimator();

        if (bindingSource == gameObject)
            return GetCasterAnimator();

        Animator directAnimator = bindingSource.GetComponent<Animator>();
        if (directAnimator != null)
            return directAnimator;

        Animator nestedAnimator = bindingSource.GetComponentInChildren<Animator>(includeInactive: true);
        if (nestedAnimator != null)
            return nestedAnimator;

        return GetCasterAnimator();
    }

    private void Awake()
    {
        // S'assure qu'un CharacterController est présent pour gérer la physique.
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();

        // Centralise la récupération (ou l'ajout) du PlayableDirector afin que
        // chaque unité puisse lire ses propres timelines de MusicalMoves ou d'objets.
        battleDirector = GetComponent<PlayableDirector>();
        if (battleDirector == null)
            battleDirector = gameObject.AddComponent<PlayableDirector>();

        // Les timelines sont déclenchées manuellement, on désactive donc toute
        // lecture automatique pour éviter une répétition non désirée en scène.
        battleDirector.playOnAwake = false;

        RegisterBattleTimelineEvents();

        CacheConcentrationSystem();
    }

    private void CacheConcentrationSystem()
    {
        if (concentrationSystem == null)
            TryGetComponent(out concentrationSystem);
    }

    private AllegianceSide? allegianceOverride;

    public AllegianceSide EffectiveAllegiance => allegianceOverride ?? ResolveDefaultAllegiance();
    public bool IsPlayerControlled => EffectiveAllegiance == AllegianceSide.Player;

    public bool IsAllyOf(CharacterUnit other)
    {
        return other != null && EffectiveAllegiance == other.EffectiveAllegiance;
    }

    public bool IsEnemyOf(CharacterUnit other)
    {
        return other != null && EffectiveAllegiance != other.EffectiveAllegiance;
    }

    public CharacterType characterType => IsPlayerControlled ? CharacterType.SquadUnit : CharacterType.EnemyUnit;

    private AllegianceSide ResolveDefaultAllegiance()
    {
        if (Data == null)
            return AllegianceSide.Player;

        if (Data.isPlayerControlled || Data.characterType == CharacterType.SquadUnit)
            return AllegianceSide.Player;

        return AllegianceSide.Enemy;
    }

    #region Statistiques runtime (alignées sur CharacterData)
    private float _currentReflex;
    private float _currentMobility;
    private float _currentVitality;
    private float _currentPower;
    private float _currentStability;
    private float _currentSagacity;

    private float _currentHP;
    private float _currentStrength;
    private float _currentDefense;
    private float _currentSpeed;
    private float _currentRange;
    private float _currentInitiative;
    private float _currentInterceptionRange;
    private float _currentInterceptionChance;

    private float _currentRage;
    private float _currentFatigue;
    private int _currentHarmonicCharge;
    private float _currentMovementPoints;
    private float _maxMovementPoints;
    #endregion

    [Header("Critical Hit")]
    [Tooltip("Base critical chance applied when a QTE is perfect (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float baseCriticalChance = 0.01f;
    [Tooltip("Base critical rate before power scaling.")]
    [FormerlySerializedAs("baseCriticRate")]
    [SerializeField] private float baseCriticalRate = 1f;
    [Tooltip("Runtime bonus added directly to the critical rate.")]
    [FormerlySerializedAs("criticRateBonus")]
    [SerializeField] private float criticalRateBonus = 0f;
    [Tooltip("Runtime bonus added to base power for critical rate scaling.")]
    [SerializeField] private float basePowerBonus = 0f;

    public float currentReflex { get => _currentReflex; set => _currentReflex = value; }
    public float currentMobility { get => _currentMobility; set => _currentMobility = value; }
    public float CurrentMovementPoints => _currentMovementPoints;
    public float MaxMovementPoints => _maxMovementPoints;
    public float currentVitality
    {
        get => _currentVitality;
        set
        {
            _currentVitality = value;
            // La vitalité influence directement les PV max : on rafraîchit l'affichage et l'événement.
            NotifyHealthChanged(refreshMax: true);
        }
    }
    public float currentPower { get => _currentPower; set => _currentPower = value; }
    public float currentStability { get => _currentStability; set => _currentStability = value; }
    public float currentSagacity { get => _currentSagacity; set => _currentSagacity = value; }

    public float currentHP
    {
        get => _currentHP;
        set
        {
            bool wasDead = _currentHP <= 0f;

            // On s'assure que la valeur stockée reste positive et ne dépasse pas le maximum connu.
            float maxHP = MaxHP;
            float clampedValue = maxHP > 0f ? Mathf.Clamp(value, 0f, maxHP) : Mathf.Max(0f, value);

            _currentHP = clampedValue;

            // Préviens immédiatement toutes les interfaces (dont la timeline) que les PV ont changé.
            NotifyHealthChanged();

            if (wasDead && _currentHP > 0f && Data != null && Data.characterName == "Lucian")
            {
                BattleCameraShatter shatter = FindFirstObjectByType<BattleCameraShatter>();
                shatter?.ResetEffect();
            }
        }
    }
    public float currentStrength { get => _currentStrength; set => _currentStrength = value; }
    public float currentDefense { get => _currentDefense; set => _currentDefense = value; }
    public float currentSpeed { get => _currentSpeed; set => _currentSpeed = value; }
    public float currentRange { get => _currentRange; set => _currentRange = value; }
    public float currentInitiative { get => _currentInitiative; set => _currentInitiative = value; }
    public float currentInterceptionRange { get => _currentInterceptionRange; set => _currentInterceptionRange = value; }
    public float currentInterceptionChance { get => _currentInterceptionChance; set => _currentInterceptionChance = value; }

    public float currentRage { get => _currentRage; set => _currentRage = value; }
    public float currentFatigue { get => _currentFatigue; set => _currentFatigue = value; }

    public float BasePower => (Data != null ? Data.basePower : 0f) + basePowerBonus;
    public float CriticalRate => Mathf.Max(0f, baseCriticalRate + Mathf.Floor(BasePower / 10f) + criticalRateBonus);
    public float CriticalChance => Mathf.Clamp01(baseCriticalChance * CriticalRate);

    public void AddBasePowerBonus(float delta)
    {
        basePowerBonus += delta;
        currentPower += delta;
    }

    public void AddCriticalRateBonus(float delta)
    {
        criticalRateBonus += delta;
    }

    // Ressources runtime
    public float currentMP;
    public float currentMusicalGauge;
    // Nouvelle réserve d'harmoniques par type
    public Dictionary<HarmonicType, int> harmonicReserve = new();
    public Dictionary<MusicalMoveSO, int> moveCooldowns = new();
    // Compteurs d'utilisation des attaques musicales
    // Clé : move, Valeur : nombre d'utilisations
    public Dictionary<MusicalMoveSO, int> moveUsesThisTurn = new();
    public Dictionary<MusicalMoveSO, int> moveUsesThisBattle = new();

    // Gestion de l'ATB
    public float currentATB = 0f;
    public float ATBMax = 100f;
    public bool IsReady => currentATB >= ATBMax && currentHP > 0;

    /// <summary>
    /// Evénement déclenché à chaque modification des points de vie de l'unité.
    /// Les deux valeurs flottantes correspondent respectivement aux PV actuels et aux PV max
    /// afin de simplifier le binding côté UI.
    /// </summary>
    public event Action<CharacterUnit, float, float> OnHealthChanged;

    /// <summary>
    /// Renvoie les points de vie maximums actuels en tenant compte des bonus/malus de vitalité.
    /// </summary>
    public float MaxHP => Data != null ? Data.baseHP + currentVitality : Mathf.Max(_currentHP, 0f);

    /// <summary>
    /// Force un rafraîchissement manuel de la barre de vie ainsi que des abonnés à <see cref="OnHealthChanged"/>.
    /// Utile si un autre système manipule directement les statistiques sans passer par les propriétés publiques.
    /// </summary>
    /// <param name="refreshMax">Indique si la valeur max doit être réappliquée à la barre.</param>
    public void RefreshHealthDisplay(bool refreshMax = false) => NotifyHealthChanged(refreshMax);

    /// <summary>
    /// Centralise la notification des variations de PV afin de tenir à jour la barre monde et les UI associées.
    /// </summary>
    /// <param name="refreshMax">Forcer la réapplication de la valeur maximale sur la barre.</param>
    private void NotifyHealthChanged(bool refreshMax = false)
    {
        float maxHP = MaxHP;
        float clampedValue = maxHP > 0f ? Mathf.Clamp(_currentHP, 0f, maxHP) : Mathf.Max(_currentHP, 0f);

        if (hpBar != null)
        {
            if (refreshMax)
                hpBar.SetMaxValue(maxHP);

            hpBar.SetValue(clampedValue);
        }

        OnHealthChanged?.Invoke(this, clampedValue, maxHP);
    }

    [Header("Etat de mort")]
    [Tooltip("Si true, l'unité est retirée définitivement du combat lorsqu'elle tombe à 0 PV.")]
    [SerializeField] private bool removeFromBattleOnKnockOut = false;
    private bool deathTriggered;
    private bool permanentDeathTriggered;
    /// <summary>
    /// Indique si l'unité est définitivement morte
    /// </summary>
    public bool IsDead => permanentDeathTriggered || deathTriggered || currentHP <= 0f;
    public bool IsPermanentlyDead => permanentDeathTriggered;
    public bool IsKnockedOut => currentHP <= 0f && !permanentDeathTriggered;
    public event System.Action<CharacterUnit> OnDeath;
    public event System.Action<CharacterUnit, AllegianceSide> OnAllegianceChanged;
    public bool isReadyToParry;
    // Indique si l'unité est immunisée à l'interception. Visible pour faciliter
    // le débogage pendant le combat.
    public bool isInterceptionImmune = false;
    // Nombre de tours restants pour l'immunité à l'interception. Visible pour
    // suivre précisément la durée de l'effet.
    public int interceptionImmunityTurns = 0;

    [Header("Récompenses de combat")]
    public List<ItemData> lootItems = new();
    public int experienceReward = 0;

    #region Cycle de Vie
    /// <summary>
    /// Initialise toutes les statistiques du personnage selon sa fiche.
    /// </summary>
    public void Initialize(CharacterData characterData)
    {
        Data = characterData;
        Data.owner = this;
        AllegianceManager.Instance?.ApplyToUnit(this);

        InitializeLoadoutSets();

        // Initialisation des stats
        currentReflex = Data.baseReflex;
        currentMobility = Data.baseMobility;
        currentVitality = Data.baseVitality;
        currentPower = BasePower;
        currentStability = Data.baseStability;
        currentSagacity = Data.baseSagacity;
        currentHP = Data.baseHP + currentVitality;
        currentStrength = Data.baseStrength;
        currentDefense = Data.baseDefense;
        currentSpeed = Data.baseSpeed;
        currentRange = Data.baseRange;
        currentInitiative = Data.baseInitiative;
        currentInterceptionRange = Data.baseInterceptionRange;
        currentInterceptionChance = 0f;
        currentRage = Data.baseRage;
        currentFatigue = Data.baseFatigue;
        ResetMovementPoints();

        harmonicReserve.Clear();
        _currentHarmonicCharge = 0;
        if (Data.baseHarmonicCharge > 0)
        {
            // Applique la réserve initiale définie dans le CharacterData pour aider les nouveaux joueurs à démarrer avec un capital clair.
            AddHarmonic(Data.harmonicType, Data.baseHarmonicCharge);
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        // Recherche proactive de l'Animator dédié aux timelines dans les enfants (même inactifs).
        animator = GetCasterAnimator(forceRefresh: true);
        ConfigureRootMotionApplier(animator);
        ApplyMovementAnimationOverrides();
        awakeState = GetComponent<AwakeState>();
        isIdleActive = false; // L'unité n'est pas encore en Idle : permet de jouer le son d'entrée lors du premier appel.
        CacheConcentrationSystem();

        // Setup graphique
        if (spriteRenderer != null && Data.portrait != null)
            spriteRenderer.sprite = Data.portrait;

        // UI HP : un rafraîchissement explicite garantit que les valeurs max et courantes
        // sont cohérentes sur toutes les interfaces (monde + timeline) immédiatement après l'init.
        NotifyHealthChanged(refreshMax: true);

        if (customBar != null)
        {
            var concentration = concentrationSystem;
            if (concentration != null)
            {
                customBar.SetMaxValue(concentration.maxConcentration);
                customBar.SetValue(concentration.currentConcentration);
            }
            else
            {
                customBar.SetMaxValue(Data.maxFatigue);
                customBar.SetValue(currentFatigue);
            }
        }

        // Vérifie immédiatement l'état harmonique pour déclencher Awake/Dissonant si nécessaire au lancement du combat.
        CheckDissonance();
    }

    private void ApplyMovementAnimationOverrides()
    {
        if (Data == null || animator == null)
            return;

        if (Data.walkAnimation == null && Data.runAnimation == null)
            return;

        movementOverrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
        if (movementOverrideController == null)
        {
            if (animator.runtimeAnimatorController == null)
                return;

            movementOverrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = movementOverrideController;
        }

        if (movementWalkClipKey == null || movementRunClipKey == null)
            CacheMovementOverrideKeys(movementOverrideController);

        if (Data.walkAnimation != null && movementWalkClipKey != null)
            movementOverrideController[movementWalkClipKey] = Data.walkAnimation;

        if (Data.runAnimation != null && movementRunClipKey != null)
            movementOverrideController[movementRunClipKey] = Data.runAnimation;
    }

    private void CacheMovementOverrideKeys(AnimatorOverrideController overrideController)
    {
        if (overrideController == null)
            return;

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);

        movementWalkClipKey = FindMovementClipKey(overrides, MovementWalkClipHints);
        movementRunClipKey = FindMovementClipKey(overrides, MovementRunClipHints);
    }

    private static AnimationClip FindMovementClipKey(List<KeyValuePair<AnimationClip, AnimationClip>> overrides, string[] hints)
    {
        if (overrides == null || hints == null)
            return null;

        foreach (string hint in hints)
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip key = overrides[i].Key;
                if (key == null)
                    continue;

                if (IsMovementClipNameMatch(key.name, hint))
                    return key;
            }
        }

        return null;
    }

    private static bool IsMovementClipNameMatch(string clipName, string hint)
    {
        if (string.IsNullOrEmpty(clipName) || string.IsNullOrEmpty(hint))
            return false;

        if (clipName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        foreach (string exclusion in MovementClipExclusions)
        {
            if (clipName.IndexOf(exclusion, StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
        }

        return true;
    }

    public void PlayMovementAnimation(float distance)
    {
        Animator movementAnimator = GetCasterAnimator();
        if (movementAnimator == null)
            return;

        float blend = distance <= 0f ? 0f : Mathf.Clamp01(distance / MovementBlendDistance);
        if (HasAnimatorParameter(movementAnimator, AnimatorMovementBlendHash))
            movementAnimator.SetFloat(AnimatorMovementBlendHash, blend);

        if (movementAnimator.HasState(0, AnimatorMovementDashHash))
            movementAnimator.CrossFade(AnimatorMovementDashHash, IdleCrossFadeDurationSeconds, 0);
        else if (movementAnimator.HasState(0, AnimatorMovementBlendStateHash))
            movementAnimator.CrossFade(AnimatorMovementBlendStateHash, IdleCrossFadeDurationSeconds, 0);
    }

    public void StopMovementAnimation()
    {
        Animator movementAnimator = GetCasterAnimator();
        if (movementAnimator == null)
            return;

        if (HasAnimatorParameter(movementAnimator, AnimatorMovementBlendHash))
            movementAnimator.SetFloat(AnimatorMovementBlendHash, 0f);
    }

    private static bool HasAnimatorParameter(Animator targetAnimator, int parameterHash)
    {
        if (targetAnimator == null)
            return false;

        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.nameHash == parameterHash)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Change l'allégeance runtime de l'unité (utilisable par Timeline Signals).
    /// </summary>
    public void SetAllegiance(AllegianceSide side)
    {
        ApplyAllegianceOverride(side, notifyManagers: true, notifyBattle: true);
    }

    public void SetAllegianceToAlly()
    {
        SetAllegiance(AllegianceSide.Player);
    }

    public void SetAllegianceToEnemy()
    {
        SetAllegiance(AllegianceSide.Enemy);
    }

    internal void ApplyAllegianceOverride(AllegianceSide side, bool notifyManagers, bool notifyBattle)
    {
        if (Data == null)
            return;

        bool wasPlayerControlled = IsPlayerControlled;
        allegianceOverride = side;

        if (notifyManagers)
            AllegianceManager.EnsureInstance().SetAllegiance(Data, side, this);

        if (notifyBattle && wasPlayerControlled != IsPlayerControlled)
            NewBattleManager.Instance?.HandleAllegianceChanged(this, wasPlayerControlled);

        OnAllegianceChanged?.Invoke(this, side);
    }

    /// <summary>
    /// Assure la cohérence des liens entre une timeline et le PlayableDirector
    /// de l'unité avant de déclencher sa lecture. Les pistes portant la mention
    /// "Caster" sont reliées en priorité à l'Animator du lanceur afin de
    /// préserver l'intention chorégraphique des MusicalMoves.
    /// </summary>
    /// <param name="timeline">Timeline à jouer via le PlayableDirector local.</param>
    /// <param name="casterBinding">
    /// Objet de référence pour les pistes d'animation. S'il est omis, l'Animator
    /// principal de l'unité est utilisé automatiquement.
    /// </param>
    public void PlayBattleTimeline(TimelineAsset timeline, GameObject casterBinding = null)
    {
        if (timeline == null)
            return;

        // Sécurise l'accès au PlayableDirector même si le composant a été ajouté
        // dynamiquement après l'Awake (cas de duplication à l'éditeur par exemple).
        if (battleDirector == null)
            battleDirector = GetComponent<PlayableDirector>();

        if (battleDirector == null)
        {
            Debug.LogError($"[CharacterUnit] Aucun PlayableDirector disponible sur {name}. La timeline '{timeline.name}' ne peut pas être jouée.");
            return;
        }

        RegisterBattleTimelineEvents();

        NotifyIdleStateExit(); // On quitte l'Idle : joue le son associé et réinitialise l'état sonore.

        // Évite que plusieurs timelines se chevauchent sur la même unité.
        battleDirector.Stop();

        battleDirector.playableAsset = timeline;

        // Détermine les cibles de binding prioritaires.
        GameObject bindingRoot = casterBinding ?? GetCasterBindingTarget();
        // Sélectionne l'Animator le plus pertinent pour les pistes "Caster".
        Animator bindingAnimator = ResolveAnimatorForBinding(bindingRoot);

        if (bindingAnimator == null && animator != null)
            bindingAnimator = animator;

        foreach (var output in timeline.outputs)
        {
            BindTimelineOutput(output, bindingRoot, bindingAnimator);
        }

        battleDirector.time = 0d;
        battleDirector.Play();
    }

    /// <summary>
    /// Fournit un accès en lecture au <see cref="PlayableDirector"/> pilotant les timelines de combat.
    /// Cette propriété reste protégée afin d'éviter toute modification extérieure non contrôlée tout
    /// en autorisant les gestionnaires spécialisés (UI, TimelineManager, systèmes de QTE) à vérifier
    /// l'état du director lorsque des Signaux mettent temporairement en pause une timeline.
    /// </summary>
    public PlayableDirector BattleDirector => battleDirector;

    /// <summary>
    /// Met en pause la timeline courante si elle est en lecture. Ce comportement reste utilisé par
    /// certains Signaux hérités pour figer la mise en scène pendant les transitions critiques.
    /// </summary>
    public void PauseBattleTimeline()
    {
        if (battleDirector != null && battleDirector.state == PlayState.Playing)
            battleDirector.Pause();
    }

    /// <summary>
    /// Reprend la lecture d'une timeline précédemment mise en pause via <see cref="PauseBattleTimeline"/>.
    /// </summary>
    public void ResumeBattleTimeline()
    {
        if (battleDirector != null && battleDirector.state == PlayState.Paused)
            battleDirector.Resume();
    }

    /// <summary>
    /// Prépare la timeline d'introduction de combat en reliant explicitement
    /// les pistes "Root" et "Model" du <see cref="PlayableDirector"/> local.
    /// Les autres pistes potentiellement présentes sont nettoyées afin d'éviter
    /// qu'un binding issu d'une précédente timeline ne perturbe la mise en scène.
    /// </summary>
    /// <param name="introTimeline">Timeline d'introduction à jouer.</param>
    /// <returns>
    /// Le <see cref="PlayableDirector"/> prêt à être lancé, ou <c>null</c> si la
    /// configuration n'a pas pu être réalisée.
    /// </returns>
    public PlayableDirector PrepareIntroTimeline(TimelineAsset introTimeline)
    {
        if (introTimeline == null)
            return null;

        // Sécurise la récupération du PlayableDirector, indispensable pour jouer la timeline.
        if (battleDirector == null)
            battleDirector = GetComponent<PlayableDirector>();

        // Si pour une raison quelconque le PlayableDirector est absent, on le recrée afin de garantir la lecture.
        if (battleDirector == null)
            battleDirector = gameObject.AddComponent<PlayableDirector>();

        if (battleDirector == null)
        {
            Debug.LogError($"[CharacterUnit] Aucun PlayableDirector disponible sur {name}. La timeline d'introduction '{introTimeline.name}' ne peut pas être préparée.");
            return null;
        }

        RegisterBattleTimelineEvents();

        // --- Préparation des Animator pour la timeline d'introduction --------------------------------------------
        // L'Animator du "Root" doit impérativement provenir du GameObject portant le CharacterUnit.
        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator == null && animator != null && animator.gameObject == gameObject)
        {
            // Si le champ "animator" référence déjà celui du root (cas de certaines unités), on le réutilise.
            rootAnimator = animator;
        }

        // L'Animator du "Model" doit être recherché exclusivement dans les enfants pour éviter toute confusion.
        Animator modelAnimator = null;

        if (animator != null && animator.gameObject != gameObject)
        {
            // On dispose déjà d'un Animator enfant enregistré via Initialize : on l'exploite directement.
            modelAnimator = animator;
        }
        else
        {
            // Recherche explicite parmi les enfants (y compris inactifs) tout en excluant l'objet racine.
            modelAnimator = GetComponentsInChildren<Animator>(includeInactive: true)
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject != gameObject);

            if (modelAnimator != null)
            {
                // On mémorise l'Animator du modèle pour les usages ultérieurs (attaques, VFX, etc.).
                animator = modelAnimator;
            }
        }

        // Coupe toute éventuelle lecture en cours pour éviter qu'une timeline précédente ne continue.
        battleDirector.Stop();

        battleDirector.playableAsset = introTimeline;

        // Parcourt toutes les pistes afin de ne conserver que les bindings nécessaires.
        foreach (var track in introTimeline.GetOutputTracks())
        {
            // On se concentre sur les AnimationTrack car seules les pistes "Root" et "Model" doivent être reliées.
            if (track is AnimationTrack)
            {
                string trackName = track.name;

                // La piste "Root" manipule le GameObject principal portant le CharacterUnit.
                if (string.Equals(trackName, "Root", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (rootAnimator != null)
                    {
                        // Binding explicite sur l'Animator du GameObject principal du CharacterUnit.
                        battleDirector.SetGenericBinding(track, rootAnimator);
                    }
                    else
                    {
                        // Aucun Animator sur le root : on nettoie pour éviter une référence fantôme.
                        battleDirector.ClearGenericBinding(track);
                    }

                    continue; // Binding traité, on passe à la piste suivante.
                }

                // La piste "Model" anime exclusivement un Animator situé dans les enfants (mesh, armature, ...).
                if (string.Equals(trackName, "Model", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (modelAnimator != null)
                    {
                        // On relie la piste à l'Animator enfant dédié au modèle visuel.
                        battleDirector.SetGenericBinding(track, modelAnimator);
                    }
                    else
                    {
                        // Aucun Animator enfant disponible : on nettoie pour éviter d'hériter d'un ancien binding.
                        battleDirector.ClearGenericBinding(track);
                    }

                    continue; // Binding géré, inutile de poursuivre les vérifications sur cette piste.
                }
            }

            // Pour toutes les autres pistes, on supprime tout binding résiduel pour éviter des références incohérentes.
            battleDirector.ClearGenericBinding(track);
        }

        // S'assure que la timeline repart du début lorsque le BattleManager la lancera.
        battleDirector.time = 0d;

        return battleDirector;
    }

    /// <summary>
    /// Arrête proprement la timeline de combat en cours sur cette unité. Cette
    /// méthode est utilisée notamment lors de la fermeture des menus d'objets.
    /// </summary>
    public void StopBattleTimeline()
    {
        if (battleDirector != null && battleDirector.state == PlayState.Playing)
            battleDirector.Stop();

        SuppressRootMotion(false);
    }

    /// <summary>
    /// Indique si la timeline de combat locale est actuellement en cours
    /// d'exécution. Utile pour synchroniser les différentes phases d'un move.
    /// </summary>
    public bool IsBattleTimelinePlaying => battleDirector != null && battleDirector.state == PlayState.Playing;

    /// <summary>
    /// Indique si la timeline locale est simplement en pause (ni arrêtée, ni en lecture).
    /// Cette information complète <see cref="IsBattleTimelinePlaying"/> pour distinguer un arrêt
    /// normal d'un gel volontaire orchestré par les timelines Performing.
    /// </summary>
    public bool IsBattleTimelinePaused => battleDirector != null && battleDirector.state == PlayState.Paused;

    /// <summary>
    /// Réalise le binding d'une piste individuelle d'une timeline vers les
    /// cibles adéquates (Animator, GameObject ou composant spécialisé).
    /// </summary>
    private void BindTimelineOutput(PlayableBinding output, GameObject bindingRoot, Animator bindingAnimator)
    {
        // Les pistes caméra restent gérées par le BattleCameraManager pour
        // conserver les transitions cinématographiques écrites dans la Bible du jeu.
        string streamName = output.streamName ?? string.Empty;
        if (streamName.IndexOf("camera", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        System.Type targetType = output.outputTargetType;

        // Les pistes de signaux doivent pointer vers les récepteurs attachés au director.
        if (targetType != null && typeof(Component).IsAssignableFrom(targetType) && targetType.Name.Contains("SignalReceiver"))
        {
            Component receiver = battleDirector.GetComponent(targetType);
            if (receiver != null)
                battleDirector.SetGenericBinding(output.sourceObject, receiver);
            else
                Debug.LogWarning($"[CharacterUnit] SignalReceiver {targetType.Name} introuvable sur {battleDirector.gameObject.name} pour la timeline '{battleDirector.playableAsset.name}'.");
            return;
        }

        // Priorité absolue : toute piste mentionnant explicitement le caster suit l'Animator principal.
        if (streamName.IndexOf("caster", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            streamName.IndexOf("pnj", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (bindingAnimator != null)
                battleDirector.SetGenericBinding(output.sourceObject, bindingAnimator);
            else if (bindingRoot != null)
                battleDirector.SetGenericBinding(output.sourceObject, bindingRoot);
            else
                battleDirector.SetGenericBinding(output.sourceObject, gameObject);
            return;
        }

        // Les pistes "Model" visent généralement l'objet contenant l'Animator.
        if (streamName.IndexOf("model", System.StringComparison.OrdinalIgnoreCase) >= 0 && bindingAnimator != null)
        {
            battleDirector.SetGenericBinding(output.sourceObject, bindingAnimator.gameObject);
            return;
        }

        // Fallback : on s'appuie sur le type de sortie pour trouver un composant compatible.
        if (targetType != null)
        {
            if (typeof(Animator).IsAssignableFrom(targetType))
            {
                if (bindingAnimator != null)
                {
                    battleDirector.SetGenericBinding(output.sourceObject, bindingAnimator);
                    return;
                }
            }
            else if (typeof(GameObject).IsAssignableFrom(targetType))
            {
                battleDirector.SetGenericBinding(output.sourceObject, bindingRoot ?? gameObject);
                return;
            }
            else if (typeof(Component).IsAssignableFrom(targetType))
            {
                Component component = null;

                if (bindingRoot != null)
                {
                    component = bindingRoot.GetComponent(targetType);
                    if (component == null)
                        component = bindingRoot.GetComponentInChildren(targetType);
                }

                if (component == null && bindingAnimator != null)
                    component = bindingAnimator.GetComponent(targetType);

                if (component == null)
                    component = battleDirector.GetComponent(targetType);

                if (component != null)
                {
                    battleDirector.SetGenericBinding(output.sourceObject, component);
                    return;
                }
            }
        }

        // Dernier recours : on relie la piste à l'objet principal de l'unité.
        battleDirector.SetGenericBinding(output.sourceObject, bindingRoot ?? gameObject);
    }

    /// <summary>
    /// Vérifie régulièrement l'état de mort du personnage.
    /// </summary>
    void Update()
    {
        HandleDeath();
        HandleCustomBarValue();
        ApplyGravity();
    }

    void HandleCustomBarValue()
    {
        if (customBar != null)
        {
            CacheConcentrationSystem();
            var concentration = concentrationSystem;
            if (concentration != null)
                customBar.SetValue(concentration.currentConcentration);
            else
                customBar.SetValue(currentFatigue);
        }
    }

    /// <summary>
    /// Applique une gravité basique aux unités terrestres afin qu'elles tombent
    /// naturellement lorsqu'elles ne sont plus soutenues.
    /// </summary>
    private void ApplyGravity()
    {
        // Les unités aériennes ne sont pas soumises à la gravité.
        if (IsAirUnit || controller == null)
            return;

        if (controller.isGrounded)
        {
            // Lorsque l'unité touche le sol, on réinitialise la vitesse de chute
            // afin d'éviter une accumulation négative.
            if (fallVelocity.y < 0f)
                fallVelocity.y = -2f; // Petite force vers le bas pour coller au sol
        }
        else
        {
            // Accumule la gravité au fil du temps lorsque l'unité est en l'air.
            fallVelocity.y += gravity * Time.deltaTime;
            controller.Move(fallVelocity * Time.deltaTime);
        }
    }

    /// <summary>
    /// Implémentation minimale de <see cref="IDamageable"/> pour assurer la
    /// compatibilité avec l'interface. On redirige vers la version complète
    /// prenant en compte l'attaquant.
    /// </summary>
    /// <param name="amount">Quantité de dégâts subis.</param>
    public void TakeDamage(float amount)
    {
        // Redirige vers la version complète en autorisant par défaut
        // les éventuelles redirections de dégâts (LoyaltyMark, etc.).
        TakeDamage(amount, null);
    }

    /// <summary>
    /// Inflige des dégâts et met à jour l'UI correspondante.
    /// </summary>
    /// <param name="amount">Quantité de dégâts subis.</param>
    /// <param name="attacker">Transform de l'attaquant pour déterminer la direction.</param>
    public void TakeDamage(float amount, Transform attacker = null, bool allowRedirect = true)
    {
        if (permanentDeathTriggered)
            return;

        // Si autorisé, on vérifie la présence d'une marque de loyauté qui
        // pourrait rediriger les dégâts vers un protecteur.
        if (allowRedirect)
        {
            var mark = GetComponent<LoyaltyMark>();
            if (mark != null && mark.RedirectDamage(amount))
            {
                // Lorsque la redirection fonctionne, la cible ne subit aucun dégât.
                // On en profite pour jouer le remerciement adéquat si le lanceur
                // est un allié et qu'un clip a été configuré.
                TryPlayLoyaltyMarkAllyClip(mark);
                return;
            }
        }

        currentHP = Mathf.Max(currentHP - amount, 0);

        // Calcul de la gravité du coup pour déterminer le son et le message
        float maxHP = Data.baseHP + currentVitality;
        bool devastating = amount > maxHP * 0.2f;

        // Affiche le nombre de dégâts au-dessus de cette unité
        DamagePopupManager.Instance?.ShowDamage(transform, Mathf.RoundToInt(amount));
        PlayDamageFeedback(devastating);

        // Message indiquant la gravité des dégâts subis
        if (ActionUIDisplayManager.Instance != null)
        {
            ActionUIDisplayManager.Instance.DisplayDamage(Data.characterName, devastating);
        }

        // Si les PV tombent à zéro ou moins, on déclenche immédiatement la mort
        // pour éviter que l'animation de blessure n'écrase l'animation de mort
        if (currentHP <= 0 && !deathTriggered)
        {
            PlayDeath();
            return;
        }

        // Lance l'animation de blessure adaptée à la direction de l'attaquant
        PlayHurtAnimation(attacker);
        GetComponent<SleepStatus>()?.OnDamageTaken();
        CacheConcentrationSystem();
        concentrationSystem?.OnDamageTaken(amount);
        if (Data != null && Data.gameplayType == GameplayType.Rage)
        {
            GetComponent<RageSystem>()?.AddRage(amount);
        }
    }

    /// <summary>
    ///     Point d'extension pour appliquer des modificateurs de dégâts contextuels.
    ///     À ce stade du projet, nous nous contentons de normaliser la valeur afin
    ///     d'éviter toute quantité négative, mais le hook restera disponible pour
    ///     intégrer facilement d'autres systèmes (talents, états temporaires...).
    /// </summary>
    public float ApplyDamageModifiers(float baseValue)
    {
        return Mathf.Max(0f, baseValue);
    }

    /// <summary>
    ///     Point d'extension équivalent pour les soins. Cela garantit que les
    ///     MusicalMoves et Items disposent d'un pipeline commun pour ajuster la
    ///     valeur finale selon les buffs/malus futurs.
    /// </summary>
    public float ApplyHealingModifiers(float baseValue)
    {
        return Mathf.Max(0f, baseValue);
    }

    /// <summary>
    /// Appelé quand la cible pare une attaque.
    /// </summary>
    public void TakeParry()
    {
        // Affiche un message de parade via l'UI
        ActionUIDisplayManager.Instance?.DisplayParry(Data.characterName);
    }

    /// <summary>
    ///     Déclenche le clip vocal prévu lorsque la marque de loyauté protège
    ///     un allié. Aucune lecture n'est effectuée si le protecteur est ennemi
    ///     ou si aucun son n'a été configuré dans les données de personnage.
    /// </summary>
    /// <param name="mark">Marque de loyauté actuellement active sur cette unité.</param>
    private void TryPlayLoyaltyMarkAllyClip(LoyaltyMark mark)
    {
        if (mark == null || Data == null)
            return;

        CharacterUnit markProtector = mark.protector;
        if (markProtector == null || markProtector.Data == null)
            return;

        // On vérifie que la victime et le protecteur appartiennent bien à la même équipe.
        if (markProtector.characterType != characterType)
            return;

        AudioClipSO clip = Data.loyaltyMarkTargetAlly;
        if (clip == null)
            return;

        AudioManager.Instance?.PlayVoice(clip, Data.characterName);
    }

    /// <summary>
    /// Appelé quand la cible esquive une attaque.
    /// </summary>
    public void TakeDodge()
    {
        ActionUIDisplayManager.Instance?.DisplayDodge(Data.characterName);
    }

    /// <summary>
    /// Déclenche la mort lorsque les PV atteignent zéro.
    /// </summary>
    void HandleDeath()
    {
        if (currentHP <= 0 && !deathTriggered)
        {
            PlayDeath();
        }
    }

    /// <summary>
    /// Joue l'animation et les effets de mort, puis retire l'unité du combat.
    /// </summary>
    void PlayDeath()
    {
        deathTriggered = true;
        if (Data.deathEffect != null)
        {
            Instantiate(Data.deathEffect, transform.position, Quaternion.identity);
        }
        Animator animator = GetCasterAnimator();
        Debug.Log(this + " handleDeath called, playing death animation.");
        if (animator != null)
        {
            if (!TryPlayStateOnAllLayers(animator, DeathStateName, DeathStateShortHash))
                Debug.LogWarning($"[CharacterUnit] Etat Animator '{DeathStateName}' introuvable pour {name}.");
        }
        NewBattleManager.Instance.RemoveFromTimeline(this);
        NewBattleManager.Instance.activeCharacterUnits.Remove(this); // facultatif

        if (characterType == CharacterType.EnemyUnit)
        {
            GameManager.Instance?.IncrementEnemiesDefeated();
            NewBattleManager.Instance?.OnEnemyDefeated(this);
        }

        if (IsPlayerControlled)
        {
            PlayAllyWeep();

            if (Data.characterName == "Lucian")
            {
                BattleCameraShatter shatter = FindFirstObjectByType<BattleCameraShatter>();
                if (shatter != null)
                    shatter.Break();
            }
        }

        if (removeFromBattleOnKnockOut)
            FinalizeDeath();

        OnDeath?.Invoke(this);
    }

    public void MarkAsPermanentlyDead()
    {
        if (permanentDeathTriggered)
            return;

        if (!deathTriggered && currentHP > 0f)
        {
            currentHP = 0f;
            PlayDeath();
        }
        else
        {
            FinalizeDeath();
        }
    }

    public void PlayResurection()
    {
        if (permanentDeathTriggered)
            return;

        deathTriggered = false;
        NewBattleManager.Instance?.RestoreUnitToBattle(this);

        Animator animator = GetCasterAnimator();
        if (animator == null)
            return;

        if (!TryPlayStateOnAllLayers(animator, ResurectionStateName, ResurectionStateShortHash))
            Debug.LogWarning($"[CharacterUnit] Etat Animator '{ResurectionStateName}' introuvable pour {name}.");
    }

    private void FinalizeDeath()
    {
        if (permanentDeathTriggered)
            return;

        permanentDeathTriggered = true;
        NewBattleManager.Instance?.RemoveFromBattle(this);
    }

    private bool TryPlayStateOnAllLayers(Animator animator, string stateName, int shortHash)
    {
        if (animator == null)
            return false;

        bool played = false;
        int layerCount = animator.layerCount;
        for (int layer = 0; layer < layerCount; layer++)
        {
            string layerName = animator.GetLayerName(layer);
            int fullHash = Animator.StringToHash($"{layerName}.{stateName}");
            if (animator.HasState(layer, fullHash))
            {
                animator.CrossFade(fullHash, 0.05f, layer, 0f);
                played = true;
                continue;
            }

            if (animator.HasState(layer, shortHash))
            {
                animator.CrossFade(shortHash, 0.05f, layer, 0f);
                played = true;
            }
        }

        return played;
    }

    void PlayAllyWeep()
    {
        var allies = NewBattleManager.Instance.activeCharacterUnits
            .Where(u => u.IsPlayerControlled && u != this && u.currentHP > 0)
            .ToList();
        if (allies.Count == 0)
            return;

        CharacterUnit randomAlly = allies[UnityEngine.Random.Range(0, allies.Count)];
        AudioClipSO clip = GetWeepClip(randomAlly.Data, Data.characterName);
        if (clip != null)
            AudioManager.Instance?.PlayVoice(clip, randomAlly.Data.characterName);
    }

    AudioClipSO GetWeepClip(CharacterData allyData, string deadName)
    {
        return deadName switch
        {
            "Lucian" => allyData.weepForLucianDeath,
            "Thalia" => allyData.weepForThaliaDeath,
            "Kael" => allyData.weepForKaelDeath,
            "Link" => allyData.weepForLinkDeath,
            "Luna" => allyData.weepForLunaDeath,
            _ => null,
        };
    }

    /// <summary>
    /// Soigne l'unité et met à jour la barre de vie.
    /// </summary>
    public void Heal(float amount)
    {
        float previousHP = currentHP;
        currentHP = Mathf.Min(currentHP + amount, Data.baseHP + currentVitality);

        int healed = Mathf.RoundToInt(currentHP - previousHP);
        if (healed > 0)
            DamagePopupManager.Instance?.ShowHeal(transform, healed);
    }

    /// <summary>
    ///     Gestion centralisée des statuts temporaires résolus en fin de tour.
    ///     Aujourd'hui cela se limite aux overrides d'altitude, mais la méthode
    ///     servira également de point d'entrée pour d'autres effets persistants.
    /// </summary>
    public void ProcessEndOfTurnStatuses()
    {
        var altitudeOverride = GetAltitudeOverrideStatus();
        if (altitudeOverride == null)
        {
            TickPersistentStatusComponents();
            return;
        }

        bool stillActive = altitudeOverride.TickTurn();
        if (!stillActive)
            altitudeOverrideStatus = null;

        TickPersistentStatusComponents();
    }

    private void TickPersistentStatusComponents()
    {
        // Les buffs/débuffs sont décrémentés en tours via le contrôleur dédié.
        if (TryGetComponent(out CharacterStatusEffectController statusController))
            statusController.TickTurn();

        var sleep = GetComponent<SleepStatus>();
        sleep?.TickTurn();

        var stunned = GetComponent<StunnedStatus>();
        stunned?.TickTurn();

        var loyalty = GetComponent<LoyaltyMark>();
        loyalty?.Tick();

        var link = GetComponent<LinkMark>();
        link?.Tick();
    }

    public void ApplyBuff(float value)
    {
        // Cette interface est conservée pour compatibilité rétroactive.
        // Utiliser CharacterStatusEffectController pour appliquer un buff ciblé.
    }
    public void RemoveBuff(float value)
    {
        // Voir ApplyBuff : l'API principale passe par CharacterStatusEffectController.
    }

    public void ApplyDebuff(float value)
    {
        // Voir ApplyBuff : l'API principale passe par CharacterStatusEffectController.
    }
    public void RemoveDebuff(float value)
    {
        // Voir ApplyBuff : l'API principale passe par CharacterStatusEffectController.
    }

    /// <summary>
    /// Joue le son d'impact. Utilise une voix différente si le coup est
    /// considéré comme dévastateur.
    /// </summary>
    /// <param name="isDevastating">True si le coup est dévastateur.</param>
    public void PlayHitSound(bool isDevastating = false)
    {
        // Sélection du clip approprié
        AudioClipSO clip = (isDevastating && Data.criticalHitSound != null)
            ? Data.criticalHitSound
            : Data.hitSound;

        if (clip != null)
            AudioManager.Instance?.PlaySfx(clip);
    }

    /// <summary>
    /// Joue le son spécifique lorsqu'une interception touche cette unité.
    /// </summary>
    public void PlayInterceptedSound()
    {
        if (Data.interceptedSound != null)
            AudioManager.Instance?.PlaySfx(Data.interceptedSound);
    }

    /// <summary>
    /// Joue le son spécifique lorsqu'une interception réussit et que
    /// cette unité est celle qui intercepte.
    /// </summary>
    public void PlayInterceptionSound()
    {
        if (Data.interceptionSound != null)
            AudioManager.Instance?.PlaySfx(Data.interceptionSound);
    }

    public void PlayMoveStartSound()
    {
        if (Data.moveStartClip != null)
        {
            AudioManager.Instance?.PlayTempSfx(Data.moveStartClip);
        }
    }

    public void PlayMoveEndSound()
    {
        if (Data.moveEndClip != null)
        {
            AudioManager.Instance?.PlayTempSfx(Data.moveEndClip);
        }
    }

    public IEnumerator PlayDamageFlash()
    {
        if (spriteRenderer == null) yield break;
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = originalColor;
    }

    public IEnumerator PlayShake(float duration = 0.15f, float magnitude = 0.1f)
    {
        Vector3 originalPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = UnityEngine.Random.Range(-magnitude, magnitude);
            float y = UnityEngine.Random.Range(-magnitude, magnitude);
            transform.localPosition = originalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;
    }

    public IEnumerator PlayKnockback(Vector3 direction, float distance = 0.5f, float duration = 0.1f)
    {
        Vector3 start = transform.position;
        Vector3 end = start + direction.normalized * distance;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(start, end, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = start;
    }

    void PlayAnimationClip(AnimationClip clip)
    {
        // Ne rien jouer si l'unité est morte
        if (IsDead)
            return;

        if (animator != null && clip != null)
        {
            // CrossFade pour éviter d'interrompre brutalement l'animation en cours
            animator.CrossFade(clip.name, 0.05f);
        }
    }

    /// <summary>
    /// Lance l'animation de préparation associée au <see cref="MusicalMoveSO"/> sélectionné.
    /// Cette étape se déroule avant la validation de la cible afin que le joueur
    /// perçoive immédiatement la posture préparatoire du personnage actif, comme
    /// décrit dans l'Histoire de Symphonie.
    /// </summary>
    /// <param name="clip">
    /// AnimationClip fourni par le move en cours de sélection. Peut être nul
    /// si le move ne définit pas d'animation personnalisée.
    /// </param>
    public void PlayPreparingAnimation(AnimationClip clip)
    {
        // Les moves d'entrée de gamme n'ont pas toujours de pose dédiée :
        // on ignore simplement l'appel lorsque le clip est absent pour
        // éviter toute erreur et conserver un feedback cohérent.
        if (clip == null)
            return;

        // On s'assure de disposer de l'Animator enfant adéquat avant de
        // lancer la transition, notamment lorsque l'unité vient juste
        // d'apparaître sur le champ de bataille.
        GetCasterAnimator();

        // Réutilise la méthode centralisée afin de bénéficier de tous les
        // garde-fous existants (vérification d'état mort, CrossFade, etc.).
        PlayAnimationClip(clip);
    }

    /// <summary>
    /// Lance l'animation principale d'exécution d'un move.
    /// </summary>
    /// <param name="clip">AnimationClip à jouer sur le caster.</param>
    public void PlayPerformingAnimation(AnimationClip clip)
    {
        if (clip == null)
            return;

        GetCasterAnimator();
        PlayAnimationClip(clip);
    }

    /// <summary>
    /// Lance en boucle l'animation de preparation au ciblage definie sur le CharacterData.
    /// Retourne true si un clip a effectivement ete lance.
    /// </summary>
    public bool StartPrepareToTargetAnimation()
    {
        AnimationClip clip = Data != null ? Data.prepareToTargetAnimation : null;
        if (clip == null)
            return false;

        StopPrepareToTargetAnimation();

        Animator casterAnimator = GetCasterAnimator(forceRefresh: true);
        if (casterAnimator == null)
            return false;

        int stateHash = Animator.StringToHash(clip.name);
        if (casterAnimator.HasState(0, stateHash))
        {
            PlayAnimationClip(clip);
            return true;
        }

        StartPrepareToTargetPlayable(casterAnimator, clip);
        return true;
    }

    /// <summary>
    /// Arrete la preparation au ciblage si elle est active.
    /// </summary>
    public void StopPrepareToTargetAnimation()
    {
        if (prepareToTargetGraph.IsValid())
            prepareToTargetGraph.Destroy();
        prepareToTargetGraph = default;
        prepareToTargetPlayable = default;
    }

    private void StartPrepareToTargetPlayable(Animator targetAnimator, AnimationClip clip)
    {
        if (targetAnimator == null || clip == null)
            return;

        prepareToTargetGraph = PlayableGraph.Create($"{name}_PrepareToTarget");
        prepareToTargetGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        prepareToTargetPlayable = AnimationClipPlayable.Create(prepareToTargetGraph, clip);
        prepareToTargetPlayable.SetApplyFootIK(true);
        prepareToTargetPlayable.SetApplyPlayableIK(true);

        var output = AnimationPlayableOutput.Create(prepareToTargetGraph, "PrepareToTarget", targetAnimator);
        output.SetSourcePlayable(prepareToTargetPlayable);
        prepareToTargetGraph.Play();
    }

    public void PlayHurtAnimation(Transform attacker = null)
    {
        // Si l'unité est morte ou qu'aucun Animator n'est disponible, on ne fait rien
        if (IsDead || animator == null)
            return;

        // Lorsque l'attaquant est connu, on calcule le côté touché
        if (attacker != null)
        {
            // Direction normalisée allant de cette unité vers l'attaquant
            Vector3 direction = (attacker.position - transform.position).normalized;

            // Angle signé autour de l'axe Y pour savoir si l'attaque vient de la
            // gauche (< 0) ou de la droite (> 0). La valeur absolue permet ensuite de
            // distinguer l'avant ou l'arrière.
            float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);

            // Nom de l'état à jouer. Par défaut on considère une attaque de face.
            string state = HitFrontStateName;
            int stateHash = HitFrontStateShortHash;

            // Avant : angle proche de 0°
            if (Mathf.Abs(angle) <= 45f)
            {
                state = HitFrontStateName;
                stateHash = HitFrontStateShortHash;
            }
            // Arrière : angle > 135° ou < -135°
            else if (Mathf.Abs(angle) > 135f)
            {
                state = HitBackStateName;
                stateHash = HitBackStateShortHash;
            }
            // Droite : angle positif (attaque venant de la droite)
            else if (angle > 0f)
            {
                state = HitRightStateName;
                stateHash = HitRightStateShortHash;
            }
            // Gauche : angle négatif
            else
            {
                state = HitLeftStateName;
                stateHash = HitLeftStateShortHash;
            }

            // Lance l'animation correspondante dans l'Animator sur tous les layers
            if (!TryPlayStateOnAllLayers(animator, state, stateHash))
                Debug.LogWarning($"[CharacterUnit] Etat Animator '{state}' introuvable pour {name}.");
            return;
        }

        // Si aucun attaquant n'est fourni, on se rabat sur l'animation générique
        PlayAnimationClip(Data?.hitAnimation ?? hurtAnimation);
    }
    public void PlayInterceptedAnimation() => PlayAnimationClip(interceptedAnimation);
    public void PlayInterceptionAnimation() => PlayAnimationClip(interceptionAnimation);
    public void PlayPrepareToUndergoAnimation()
    {
        // Pas d'animation si l'unité est morte
        if (IsDead)
            return;
        // On force l'actualisation du cache pour récupérer l'Animator enfant même si
        // l'objet racine avait été retenu auparavant (par exemple lorsque le modèle
        // n'était pas encore actif). L'objectif est de jouer l'animation sur le rig
        // enfant qui porte réellement les poses défensives.
        Animator anim = GetCasterAnimator(forceRefresh: true);

        // Si le cache renvoie toujours l'Animator du GameObject racine, on tente une
        // récupération directe de l'Animator enfant. Cela évite de lancer l'animation
        // sur le mauvais Animator, ce qui bloquerait la pose de garde.
        if (anim != null && anim.gameObject == gameObject)
        {
            Animator childAnimator = FindChildAnimatorForBindings();
            if (childAnimator != null && childAnimator.gameObject != gameObject)
            {
                animator = childAnimator; // Maintien du cache synchronisé.
                anim = childAnimator;
            }
            else
            {
                // Sans Animator enfant il est inutile d'aller plus loin : on préserve
                // l'état actuel plutôt que de déclencher un CrossFade sur un mauvais
                // contrôleur.
                return;
            }
        }

        // Même si le cache renvoie null (absence d'Animator valide) on évite toute
        // NullReferenceException en contrôlant Data et le clip demandé.
        if (anim != null && Data != null && Data.prepareToUndergoAnimation != null)
        {
            // Utilise CrossFade pour garantir la bonne transition
            anim.CrossFade(Data.prepareToUndergoAnimation.name, 0.05f);
        }
    }

    void PlayDamageFeedback(bool isDevastating)
    {
        // Joue le son correspondant à la gravité du coup
        PlayHitSound(isDevastating);

        if (Data.hitEffect != null)
            Instantiate(Data.hitEffect, transform.position, Quaternion.identity);

        StartCoroutine(PlayDamageFlash());
        StartCoroutine(PlayShake());
        StartCoroutine(PlayKnockback(Vector3.zero)); // Tu peux adapter la direction
    }

    public MusicalMoveSO GetRandomMusicalAttack()
    {
        var availableAttacks = Data.musicalAttacks
            .Where(m => !m.onlyAwake || IsAwake)
            .Where(m => !m.enterAwake || !IsAwake)
            .Where(m => !m.enterAwake || GetHarmonicCount(Data.harmonicType) >= Data.awakeHarmonicThreshold)
            .Where(m => CanUseMove(m))
            .ToArray();

        if (availableAttacks == null || availableAttacks.Length == 0)
        {
            Debug.LogWarning($"[CharacterUnit] {Data.characterName} n'a aucune attaque musicale disponible pour l'état actuel !");
            return null;
        }

        int index = UnityEngine.Random.Range(0, availableAttacks.Length);
        return availableAttacks[index];
    }

    /// <summary>
    ///     Renvoie l'attaque basique utilisée pour les effets automatiques (comme Presto).
    ///     On privilégie une attaque offensive accessible dans l'état courant.
    /// </summary>
    public MusicalMoveSO GetBasicAttack()
    {
        if (Data == null)
            return null;

        MusicalMoveSO FindFirstUsable(IEnumerable<MusicalMoveSO> moves, bool requireOffensive)
        {
            if (moves == null)
                return null;

            foreach (var move in moves)
            {
                if (move == null)
                    continue;

                bool canUse = (!move.onlyAwake || IsAwake)
                              && (!move.enterAwake || !IsAwake)
                              && (!move.enterAwake || GetHarmonicCount(Data.harmonicType) >= Data.awakeHarmonicThreshold)
                              && GetAvailableHarmonicsForCost(move.consumedHarmonicType) >= move.harmonicCost
                              && !IsMoveOnCooldown(move);

                if (!canUse)
                    continue;

                if (!requireOffensive || (move.moveType == MoveType.Attack && move.HasEffect(MusicalEffectType.Damage)))
                    return move; // Premier candidat valide trouvé.
            }

            return null;
        }

        // 1) Priorité : attaques basiques explicitement configurées sur le personnage.
        var basicOffensive = FindFirstUsable(Data.basicAttack, requireOffensive: true);
        if (basicOffensive != null)
            return basicOffensive;

        var basicAny = FindFirstUsable(Data.basicAttack, requireOffensive: false);
        if (basicAny != null)
            return basicAny;

        // 2) Aucun move basique disponible : on laisse la résolution globale gérer un fallback (defaultBasicAttackMove).
        return null;
    }

    public CharacterUnit SelectTargetFromSquad()
    {
        var squad = NewBattleManager.Instance.activeCharacterUnits
            .Where(u => u.IsPlayerControlled && u.currentHP > 0)
            .ToList();

        if (squad == null || squad.Count == 0)
        {
            Debug.LogWarning("[EnemyAI] Aucun joueur valide à cibler.");
            return null;
        }

        // Priorité : cible ayant infligé le plus de dégâts au cours du combat
        var topDamageDealer = NewBattleManager.Instance.GetTopDamageDealer();
        if (topDamageDealer != null)
            return topDamageDealer;

        // Sinon, cible avec le moins de PV
        var lowestHPUnit = squad.OrderBy(u => u.currentHP).FirstOrDefault();
        if (lowestHPUnit != null)
            return lowestHPUnit;

        // Fallback aléatoire
        return squad[UnityEngine.Random.Range(0, squad.Count)];
    }

    public void AddHarmonic(HarmonicType type, int amount = 1)
    {
        if (amount <= 0)
            return; // Évite les écritures inutiles et les dérives numériques.

        if (!harmonicReserve.ContainsKey(type))
            harmonicReserve[type] = 0;
        harmonicReserve[type] += amount;
        if (Data != null && type == Data.harmonicType)
            _currentHarmonicCharge = harmonicReserve[type]; // Maintient un suivi runtime sans modifier le ScriptableObject.
        CheckDissonance();
    }

    public bool ConsumeHarmonic(HarmonicType type, int amount = 1)
    {
        if (amount <= 0)
            return true;

        if (type == HarmonicType.Lumiere)
        {
            if (!harmonicReserve.ContainsKey(type) || harmonicReserve[type] < amount)
                return false;

            harmonicReserve[type] -= amount;
            if (Data != null && type == Data.harmonicType)
                _currentHarmonicCharge = harmonicReserve[type];
            CheckDissonance();
            return true;
        }

        int availableType = GetHarmonicCount(type);
        int availableLumiere = GetHarmonicCount(HarmonicType.Lumiere);
        if (availableType + availableLumiere < amount)
            return false;

        if (!harmonicReserve.ContainsKey(type))
            harmonicReserve[type] = 0;
        if (!harmonicReserve.ContainsKey(HarmonicType.Lumiere))
            harmonicReserve[HarmonicType.Lumiere] = 0;

        int fromType = Mathf.Min(availableType, amount);
        harmonicReserve[type] -= fromType;
        int remaining = amount - fromType;
        if (remaining > 0)
            harmonicReserve[HarmonicType.Lumiere] -= remaining;

        if (Data != null)
        {
            if (type == Data.harmonicType)
                _currentHarmonicCharge = harmonicReserve[type];
            else if (Data.harmonicType == HarmonicType.Lumiere)
                _currentHarmonicCharge = harmonicReserve[HarmonicType.Lumiere];
        }

        CheckDissonance();
        return true;
    }

    public int GetHarmonicCount(HarmonicType type)
    {
        return harmonicReserve.ContainsKey(type) ? harmonicReserve[type] : 0;
    }

    public int GetAvailableHarmonicsForCost(HarmonicType type)
    {
        int count = GetHarmonicCount(type);
        if (type != HarmonicType.Lumiere)
            count += GetHarmonicCount(HarmonicType.Lumiere);
        return count;
    }

    public int GetTotalHarmonicCount()
    {
        int total = 0;
        foreach (var entry in harmonicReserve)
            total += entry.Value;
        return total;
    }

    public void ClearAllHarmonics()
    {
        var keys = harmonicReserve.Keys.ToList();
        foreach (var key in keys)
            harmonicReserve[key] = 0;
        if (Data != null)
            _currentHarmonicCharge = 0;
        CheckDissonance();
    }

    public void ReduceHarmonicsByHalf()
    {
        if (harmonicReserve.Count == 0)
            return;

        var keys = harmonicReserve.Keys.ToList();
        foreach (var key in keys)
            harmonicReserve[key] = harmonicReserve[key] / 2;

        if (Data != null)
            _currentHarmonicCharge = GetHarmonicCount(Data.harmonicType);
        CheckDissonance();
    }

    public void ReduceCooldowns()
    {
        var keys = moveCooldowns.Keys.ToList();
        foreach (var key in keys)
            moveCooldowns[key] = Mathf.Max(0, moveCooldowns[key] - 1);
    }

    public bool IsMoveOnCooldown(MusicalMoveSO move)
    {
        return moveCooldowns.ContainsKey(move) && moveCooldowns[move] > 0;
    }

    public void SetMoveCooldown(MusicalMoveSO move)
    {
        if (move.cooldown > 0)
            moveCooldowns[move] = move.cooldown;
    }

    // ---------------------------------------------------------------------
    // Gestion des limitations d'utilisation des attaques musicales
    // ---------------------------------------------------------------------

    /// <summary>
    /// Vérifie si ce move peut être utilisé en fonction des limites par tour
    /// et par combat.
    /// </summary>
    public bool CanUseMove(MusicalMoveSO move)
    {
        if (move == null)
            return false;

        if (move.maxUsesPerTurn > 0)
        {
            moveUsesThisTurn.TryGetValue(move, out int usedTurn);
            if (usedTurn >= move.maxUsesPerTurn)
                return false;
        }

        if (move.maxUsesPerBattle > 0)
        {
            moveUsesThisBattle.TryGetValue(move, out int usedBattle);
            if (usedBattle >= move.maxUsesPerBattle)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Enregistre l'utilisation d'un move pour tenir à jour les compteurs.
    /// </summary>
    public void RegisterMoveUse(MusicalMoveSO move)
    {
        if (move.maxUsesPerTurn > 0)
        {
            moveUsesThisTurn.TryGetValue(move, out int usedTurn);
            moveUsesThisTurn[move] = usedTurn + 1;
        }

        if (move.maxUsesPerBattle > 0)
        {
            moveUsesThisBattle.TryGetValue(move, out int usedBattle);
            moveUsesThisBattle[move] = usedBattle + 1;
        }
    }

    /// <summary>
    /// Réinitialise les compteurs par tour. À appeler au début de chaque tour.
    /// </summary>
    public void ResetTurnMoveUsage()
    {
        moveUsesThisTurn.Clear();
    }

    /// <summary>
    /// Réinitialise les compteurs globaux du combat. À appeler au début du combat.
    /// </summary>
    public void ResetBattleMoveUsage()
    {
        moveUsesThisTurn.Clear();
        moveUsesThisBattle.Clear();
    }

    public void ResetMovementPoints()
    {
        float max = Mathf.Max(0f, currentMobility);
        _maxMovementPoints = max;
        _currentMovementPoints = max;
    }

    public bool CanSpendMovementDistance(float distance)
    {
        if (distance <= 0f)
            return true;

        return _currentMovementPoints >= distance - 0.01f;
    }

    public bool ConsumeMovementDistance(float distance)
    {
        if (distance <= 0f)
            return true;

        if (_currentMovementPoints < distance - 0.01f)
            return false;

        _currentMovementPoints = Mathf.Max(0f, _currentMovementPoints - distance);
        return true;
    }

    public void ConsumeAllMovementPoints()
    {
        _currentMovementPoints = 0f;
    }

    /// <summary>
    /// Vérifie les seuils harmoniques pour orchestrer automatiquement les transitions Awake/Dissonant.
    /// </summary>
    private void CheckDissonance()
    {
        if (Data == null)
            return;

        int currentHarmonics = GetHarmonicCount(Data.harmonicType);

        // 1) Seuil d'éveil atteint : priorité absolue à l'état Awake.
        if (currentHarmonics >= Data.awakeHarmonicThreshold)
        {
            if (IsDissonant)
                ExitDissonantState();
            if (!IsAwake)
                EnterAwakeState();
            return;
        }

        // 2) Seuil inférieur atteint : on perd l'éveil et on passe en dissonance.
        if (currentHarmonics < Data.dissonancePoint)
        {
            if (IsAwake)
                ExitAwakeState();
            if (!IsDissonant)
                EnterDissonantState();
        }
        else
        {
            // 3) Zone tampon entre les deux seuils : aucun état spécial ne doit rester actif.
            if (IsAwake)
                ExitAwakeState();
            if (IsDissonant)
                ExitDissonantState();
        }
    }

    /// <summary>
    /// Active l'état Awake et applique les bonus correspondants.
    /// </summary>
    public void EnterAwakeState()
    {
        if (IsDissonant)
            ExitDissonantState(); // L'éveil annule immédiatement toute dissonance résiduelle.
        awakeState?.EnterAwake();
    }

    /// <summary>
    /// Désactive l'état Awake et retire les bonus.
    /// </summary>
    public void ExitAwakeState()
    {
        awakeState?.ExitAwake();
    }

    /// <summary>
    /// Active l'état Dissonant lorsque le seuil inférieur d'harmonie est franchi.
    /// </summary>
    public void EnterDissonantState()
    {
        awakeState?.EnterDissonant();
    }

    /// <summary>
    /// Désactive l'état Dissonant afin de revenir à un comportement classique.
    /// </summary>
    public void ExitDissonantState()
    {
        awakeState?.ExitDissonant();
    }

    /// <summary>
    /// Calcule les dégâts bruts d'une attaque basique en tenant compte des modificateurs contextuels.
    /// </summary>
    /// <returns>Valeur de dégâts prête à être appliquée à une cible.</returns>
    public float GetBaseAttackDamage()
    {
        if (Data == null)
            return 0f;

        float baseValue = Mathf.Max(0f, BasePower);
        return CombatPipeline.ResolveDamageValue(this, baseValue, new CombatPipeline.DamageOptions
        {
            includePower = false,
            applyAttackMultiplier = true,
            applyModifiers = true,
            clampToBaseValue = false,
            registerDamage = false,
            allowRedirect = true,
            valueMultiplier = 1f
        });
    }

    public float GetAttackMultiplier()
    {
        if (TryGetComponent<SleepStatus>(out var sleep) && sleep.IsAsleep && Data != null && Data.gameplayType == GameplayType.Fatigue)
            return 2f;
        if (TryGetComponent<FatigueSystem>(out var fatigue) && fatigue.IsAsleep && Data != null && Data.gameplayType == GameplayType.Fatigue)
            return 2f;
        return 1f;
    }

    public void PlayIdleAnimation()
    {
        if (currentHP <= 0)
            return;

        if (TryGetComponent<SleepStatus>(out var sleep) && sleep.IsAsleep)
            return;

        if (TryGetComponent<StunnedStatus>(out var stunned) && stunned.IsStunned)
            return;

        if (TryGetComponent<FatigueSystem>(out var fatigue) && fatigue.IsAsleep)
            return;

        // Si on atteint réellement l'Idle, on déclenche son éventuel son d'entrée.
        if (!isIdleActive)
        {
            PlayStateClip(Data != null ? Data.idleEnterClip : null);
            isIdleActive = true;
        }

        if (animator != null)
        {
            const int baseLayerIndex = 0;

            // Afin d'éviter tout changement brutal d'animation, on favorise un CrossFade lorsque l'état existe.
            if (animator.HasState(baseLayerIndex, AnimatorStateIdleBattle))
            {
                animator.CrossFade(AnimatorStateIdleBattle, IdleCrossFadeDurationSeconds, baseLayerIndex, 0f);
            }
            else
            {
                // En dernier recours, on conserve Play pour les rigs spéciaux n'ayant pas l'état Idle_Battle déclaré.
                animator.Play("Idle_Battle");
            }
        }
    }

    /// <summary>
    /// Informe le système que l'unité quitte son Idle actuel afin de jouer le son de sortie une seule fois.
    /// </summary>
    public void NotifyIdleStateExit()
    {
        if (!isIdleActive)
            return; // Aucun son à jouer si l'Idle n'était pas actif.

        isIdleActive = false;
        PlayStateClip(Data != null ? Data.idleExitClip : null);
    }

    /// <summary>
    /// Joue un clip stocké dans un <see cref="AudioClipSO"/> en respectant son volume interne.
    /// </summary>
    private void PlayStateClip(AudioClipSO clip)
    {
        if (clip != null)
            AudioManager.Instance?.PlaySfx(clip);
    }

    #endregion
}
