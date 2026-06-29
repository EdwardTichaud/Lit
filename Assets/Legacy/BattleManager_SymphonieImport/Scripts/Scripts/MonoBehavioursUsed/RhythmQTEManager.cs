using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using UnityEngine.InputSystem;
using UnityEngine.Timeline; // Gestion des timelines de combat
using UnityEngine.UI;

public class RhythmQTEManager : MonoBehaviour
{
    public static RhythmQTEManager Instance { get; private set; }

    public MusicalMoveSO currentMove;
    private float startTime;
    private int currentBeatIndex = 0;
    /// <summary>
    /// Expose l'index du beat actuellement traité (pratique pour les outils de debug).
    /// </summary>
    public int CurrentBeatIndex => currentBeatIndex;
    private bool isActive = false;
    private List<bool> successResults;
    private List<bool> perfectResults;
    private int successfulNotes = 0;
    private Coroutine scheduledNotesRoutine;
    private bool ignoreAnimationNoteEvents;
    private float noteScheduleLeadInSeconds;
    private bool moveAppliedEffect;
    private bool hasCriticalHitThisMove;

    // Dernier résultat enregistré pour un QTE d'objet
    public bool LastItemSuccess { get; private set; }
    public bool LastItemCritical { get; private set; }
    public bool LastMoveAppliedEffect { get; private set; }

    [Header("QTE")]
    [Tooltip("Active ou desactive tout le systeme de QTE (affichage, input, resolution).")]
    [SerializeField] private bool qteEnabled = false;

    public bool IsQteEnabled => qteEnabled;

    // QTE
    private Coroutine beatRoutine;
    // Coroutine responsable d'un éventuel changement différé de caméra.
    // Permet de conserver le cadrage de préparation pendant un court instant
    // lorsque l'on enchaîne avec la phase de performing.
    private Coroutine pendingMotifSwitch;

    private float defaultFixedDeltaTime;

    [Header("QTE Visuel")]
    public GameObject qteCirclePrefab; // Prefab du cercle QTE historique
    public Transform qteUIParent; // Parent dans le canvas (facultatif, sinon instancié en world space)

    [Header("QTE Barre")]
    public GameObject qteBarPrefab; // Prefab legacy (conserve pour compat)

    [Header("Accessibilité QTE")]
    [Tooltip("Agrandit ou réduit la fenêtre temporelle des QTE (1 = valeur par défaut).")]
    [SerializeField] private float qteWindowScale = 1f;
    [Tooltip("Marge additionnelle (en pixels UI) appliquée à la zone de validation.")]
    [SerializeField] private float qteValidationPadding = 0f;
    [Tooltip("Seuil (0..1) pour considérer un QTE comme parfait par rapport à la demi-largeur de la zone.")]
    [Range(0f, 1f)]
    [SerializeField] private float qtePerfectThreshold = 0.35f;
    [Tooltip("Active l'affichage d'un feedback textuel après chaque QTE.")]
    [SerializeField] private bool showQteFeedback = true;
    [Tooltip("Facteur d'élargissement des fenêtres de parade/esquive pour la défense.")]
    [SerializeField] private float defenseWindowScale = 1f;

    private DefenseResult defenseResult;
    public DefenseResult GetDefenseResult() => defenseResult;

    // MoveTo

    // Durée par défaut utilisée si aucun délai n'est défini dans les données
    // Conserve l'ancien comportement en cas d'oubli de paramétrage
    private const float defaultTeleportDelay = 0.2f;
    private const float fixedMoveDurationSeconds = 1f;
    private const string ItemUseStateName = "Item_Use";
    private static readonly int AnimatorStateItemUseShortHash = Animator.StringToHash(ItemUseStateName);

    [Header("Déplacements")]
    [Tooltip("Particules instanciées lors d'un déplacement classique afin d'accentuer la sensation de dash.")]
    [SerializeField] private ParticleSystem dashParticlesPrefab;
    [Tooltip("Clip joué lorsqu'aucun son spécifique n'est défini sur le personnage pour l'effet de dash.")]
    [SerializeField] private AudioClipSO defaultDashSound;
    [Tooltip("Utilise le Root Motion si le clip en fournit, sinon fallback Lerp (compat clips Root/InPlace).")]
    [SerializeField] private bool preferRootMotionMoves = true;
    [Tooltip("Seuil minimal de delta pour considerer qu'un clip fournit du Root Motion.")]
    [SerializeField] private float rootMotionProbeThreshold = 0.0001f;
    private const int rootMotionProbeFrames = 3;

    // Tag de la caméra de combat à utiliser pour toutes les timelines

    // QTE Effect
    public AudioClipSO successSFX;
    public AudioClipSO failSFX;
    // Effets visuels pour indiquer le résultat du QTE
    public GameObject successEffectPrefab;
    public GameObject failEffectPrefab;

    // ------------------------------------------------------------------------------
    // Gestion des effets sonores
    // ------------------------------------------------------------------------------
    /// <summary>
    /// Joue un effet sonore à l'aide d'une source SFX disponible.
    /// </summary>
    /// <param name="clip">Clip audio à jouer</param>
    private void PlaySfx(AudioClipSO clip)
    {
        if (clip == null)
            return;

        // Centralise la lecture via l'AudioManager afin de profiter de la normalisation et
        // des volumes globaux configurés dans le projet.
        AudioManager.Instance?.PlaySfx(clip);
    }

    private CharacterUnit currentCaster;
    private CharacterUnit currentTarget;
    private int pendingNotes = 0;
    private bool qteActive = false;
    private int activeQteCount = 0;
    private readonly Dictionary<InputAction, int> qteActionUsage = new();
    private readonly HashSet<InputAction> qteActionsEnabledByQte = new();
    private readonly List<QTECircle> persistentQteCircles = new();
    /// <summary>
    /// Permet aux autres systèmes de connaître l'état d'activité d'un QTE en temps réel.
    /// </summary>
    public bool IsQteActive => qteActive;

    // Si activé, le temps de jeu est figé durant les QTE pour faciliter l'exécution
    [SerializeField] private bool easyMode = false;

    #region Initialisation
    /// <summary>
    /// Configure l'instance unique et mémorise le fixedDeltaTime de départ.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    /// <summary>
    /// Prépare la barre de QTE en l'affichant et en créant toutes les notes à l'avance.
    /// </summary>
    /// <param name="notes">Liste des données de notes à afficher</param>
    public void PrepareQTEBar(MusicalMoveSO move)
    {
        if (!qteEnabled)
            return;
        // QTE bar legacy: conservation des appels existants sans UI dédiée.
    }

    public void PrepareQTEBar(IList<MusicalMoveSO.NoteData> notes)
    {
        if (!qteEnabled)
            return;
    }

    private void PrepareQTEBar(IList<MusicalMoveSO.NoteData> notes, MusicalMoveSO move)
    {
        if (!qteEnabled)
            return;
    }

    /// <summary>
    /// Variante pour un motif simple d'item sans icônes spécifiques.
    /// </summary>
    public void PrepareQTEBar(IList<float> beatPattern)
    {
        if (!qteEnabled)
            return;
    }

    /// <summary>
    /// Détruit la barre de QTE active et vide la file des notes.
    /// </summary>
    public void ClearQTEBar()
    {
        foreach (var circle in persistentQteCircles)
        {
            if (circle == null)
                continue;
            circle.ForceDestroy();
        }
        persistentQteCircles.Clear();

        Transform parent = ResolveQteUIParent();
        FreeformLayoutGroup freeformLayout = parent != null ? parent.GetComponent<FreeformLayoutGroup>() : null;
        if (freeformLayout != null)
            freeformLayout.ClearRuntimeEntries();
    }

    private Transform ResolveQteUIParent()
    {
        if (qteUIParent != null)
            return qteUIParent;

        var freeformPanel = GameObject.Find("QTEPanel");
        if (freeformPanel != null)
        {
            qteUIParent = freeformPanel.transform;
            return qteUIParent;
        }

        var qtePanel = GameObject.Find("QTECirclesPanel");
        if (qtePanel != null)
        {
            qteUIParent = qtePanel.transform;
            return qteUIParent;
        }

        string[] preferredNames = { "Battle_UICanvas_BattleCamera", "BattleScene_UI_QTECircle", "BattleCameraCanvas" };
        foreach (string name in preferredNames)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                qteUIParent = go.transform;
                return qteUIParent;
            }
        }

        Canvas canvas = FindObjectOfType<Canvas>(true);
        if (canvas != null)
        {
            qteUIParent = canvas.transform;
            return qteUIParent;
        }

        return qteUIParent;
    }

    private void RegisterQteStart()
    {
        activeQteCount = Mathf.Max(0, activeQteCount + 1);
        qteActive = true;
    }

    private void RegisterQteEnd()
    {
        activeQteCount = Mathf.Max(0, activeQteCount - 1);
        qteActive = activeQteCount > 0;
    }

    private void AcquireInputAction(InputAction action)
    {
        if (action == null)
            return;

        qteActionUsage.TryGetValue(action, out int count);
        qteActionUsage[action] = count + 1;

        if (count == 0 && !action.enabled)
        {
            action.Enable();
            qteActionsEnabledByQte.Add(action);
        }
    }

    private void ReleaseInputAction(InputAction action)
    {
        if (action == null)
            return;

        if (!qteActionUsage.TryGetValue(action, out int count))
            return;

        count -= 1;
        if (count <= 0)
        {
            qteActionUsage.Remove(action);
            if (qteActionsEnabledByQte.Remove(action))
                action.Disable();
        }
        else
        {
            qteActionUsage[action] = count;
        }
    }

    private void EnsureQteBarForMove(MusicalMoveSO move)
    {
        if (!qteEnabled)
            return;
        // QTE bar legacy: aucun préchargement requis.
    }

    private void ApplyMoveEffect(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target, bool isCritical = false)
    {
        if (move == null)
            return;

        MusicalMoveExecutor.ApplyEffect(move, caster, target, isCritical);
        if (isCritical)
        {
            hasCriticalHitThisMove = true;
            BattleCameraManager.Instance?.TriggerCriticalFeedback(target);
        }
        moveAppliedEffect = true;
    }

    public void RegisterMoveEffectApplied(MusicalMoveSO move, bool isCritical = false)
    {
        if (move == null || currentMove != move)
            return;

        moveAppliedEffect = true;
        if (isCritical)
        {
            hasCriticalHitThisMove = true;
            BattleCameraManager.Instance?.TriggerCriticalFeedback(currentTarget);
        }
    }

    private bool RollCritical(CharacterUnit caster)
    {
        if (caster == null)
            return false;

        float chance = caster.CriticalChance;
        if (chance <= 0f)
            return false;

        return UnityEngine.Random.value < chance;
    }

    private void SetupNoteScheduleTiming(MusicalMoveSO move)
    {
        noteScheduleLeadInSeconds = 0f;
        if (move != null)
            noteScheduleLeadInSeconds = Mathf.Max(0f, move.qteDelay);
    }

    private float ResolveNoteSpacingSeconds(float beatSpacing)
    {
        return Mathf.Max(0f, beatSpacing);
    }

    private float EstimateQteLifetimeSeconds(float responseDelaySeconds)
    {
        if (responseDelaySeconds <= 0f)
            return 0f;

        if (qteCirclePrefab == null)
            return responseDelaySeconds;

        var circle = qteCirclePrefab.GetComponent<QTECircle>();
        if (circle == null)
            return responseDelaySeconds;

        return circle.EstimateLifetimeSeconds(responseDelaySeconds);
    }

    private void StartNoteSchedule(MusicalMoveSO move)
    {
        StopNoteSchedule();
        if (!qteEnabled || move == null || move.notes == null || move.notes.Count == 0)
            return;

        ignoreAnimationNoteEvents = true;
        SetupNoteScheduleTiming(move);
        scheduledNotesRoutine = StartCoroutine(ScheduleMoveNotes(move));
    }

    private void StopNoteSchedule()
    {
        if (scheduledNotesRoutine != null)
            StopCoroutine(scheduledNotesRoutine);

        scheduledNotesRoutine = null;
        ignoreAnimationNoteEvents = false;
        noteScheduleLeadInSeconds = 0f;
    }

    private IEnumerator ScheduleMoveNotes(MusicalMoveSO move)
    {
        if (move == null || move.notes == null || move.notes.Count == 0)
            yield break;

        if (noteScheduleLeadInSeconds > 0f)
            yield return new WaitForSecondsRealtime(noteScheduleLeadInSeconds);

        for (int i = 0; i < move.notes.Count; i++)
        {
            if (!isActive || currentMove != move)
                yield break;

            var note = move.notes[i];
            StartCoroutine(ResolveMoveNote(note));

            float delay = ResolveNoteSpacingSeconds(note.beatSpacing);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
        }
    }

    private IEnumerator ResolveMoveNote(MusicalMoveSO.NoteData note)
    {
        if (!qteEnabled)
            yield break;
        if (currentMove == null || currentCaster == null || currentTarget == null)
            yield break;

        if (currentCaster.characterType == CharacterType.EnemyUnit)
        {
            if (!currentCaster.Data.avoidable)
            {
                ApplyMoveEffect(currentMove, currentCaster, currentTarget);
                pendingNotes = Mathf.Max(0, pendingNotes - 1);
                yield break;
            }

            DefenseResult result = DefenseResult.Miss;
            yield return WaitForDefenseQTE(r => result = r);
            defenseResult = result;
            switch (result)
            {
                case DefenseResult.Parry:
                    currentTarget.TakeParry();
                    break;
                case DefenseResult.Dodge:
                    currentTarget.TakeDodge();
                    break;
                default:
                    ApplyMoveEffect(currentMove, currentCaster, currentTarget);
                    break;
            }

            pendingNotes = Mathf.Max(0, pendingNotes - 1);
            yield break;
        }

        float windowMs = Mathf.Max(1f, note.responseDelay * 1000f);
        bool success = false;
        QTEFeedback feedback = QTEFeedback.Miss;
        yield return WaitForQTE(windowMs, note.qteInput, null, Vector2.zero, (s, f) =>
        {
            success = s;
            feedback = f;
        }, true);

        bool isPerfect = feedback == QTEFeedback.Perfect;
        bool isCritical = success && isPerfect && RollCritical(currentCaster);

        if (success)
        {
            ApplyMoveEffect(currentMove, currentCaster, currentTarget, isCritical);
            successfulNotes++;
        }

        successResults?.Add(success);
        perfectResults?.Add(isPerfect);
        pendingNotes = Mathf.Max(0, pendingNotes - 1);
    }

    private float GetTotalRhythmSeconds(MusicalMoveSO move)
    {
        if (move == null || move.notes == null)
            return 0f;

        float sum = noteScheduleLeadInSeconds;
        float windowScale = Mathf.Max(0.1f, qteWindowScale);
        foreach (var note in move.notes)
        {
            sum += EstimateQteLifetimeSeconds(note.responseDelay * windowScale);
            sum += ResolveNoteSpacingSeconds(note.beatSpacing);
        }

        return sum;
    }

    // ------------------------------------------------------------------------------
    // Utilitaires Timeline
    // ------------------------------------------------------------------------------
    private void StartTimelinePhase(
        TimelineAsset timeline,
        bool overlay,
        CharacterUnit caster,
        GameObject animatorGO,
        GameObject cameraTarget,
        bool autoRestore,
        Quaternion initialRotation,
        CameraMotifSO motif = null,
        float motifSwitchDelay = 0f)
    {
        if (caster == null)
            return;

        ConfigureCameraForPhase(caster, animatorGO, cameraTarget, initialRotation, motif, motifSwitchDelay);

        if (timeline == null || BattleTimelineManager.Instance == null)
            return;

        // Lecture de la timeline via le PlayableDirector de l'unité concernée.
        GameObject defaultCasterTarget = caster.GetCasterBindingTarget();
        GameObject binding = animatorGO ?? defaultCasterTarget;
        BattleTimelineManager.Instance.PlayCasterTimeline(timeline, caster, binding);
    }

    /// <summary>
    /// Reprend une timeline déjà jouée précédemment (utilisée lorsque la Performing a été suspendue).
    /// </summary>
    private void ResumeTimelinePhase(
        TimelineAsset timeline,
        CharacterUnit caster,
        GameObject animatorGO,
        GameObject cameraTarget,
        Quaternion initialRotation,
        CameraMotifSO motif = null)
    {
        if (caster == null)
            return;

        ConfigureCameraForPhase(caster, animatorGO, cameraTarget, initialRotation, motif, 0f);

        if (timeline == null || BattleTimelineManager.Instance == null)
            return;

        BattleTimelineManager.Instance.ResumeCasterTimeline(caster);
    }

    /// <summary>
    /// Prépare la caméra et annule les délais résiduels avant de lancer ou reprendre une timeline.
    /// </summary>
    private void ConfigureCameraForPhase(
        CharacterUnit caster,
        GameObject animatorGO,
        GameObject cameraTarget,
        Quaternion initialRotation,
        CameraMotifSO motif,
        float motifSwitchDelay)
    {
        if (caster == null)
            return;

        if (motif != null)
        {
            CancelPendingMotifSwitch();

            if (motifSwitchDelay <= 0f)
                BattleCameraManager.Instance?.SetCameraMotif(motif);
            else
                pendingMotifSwitch = StartCoroutine(SwitchMotifAfterDelay(motif, motifSwitchDelay));
        }

    }

    /// <summary>
    /// Interrompt la coroutine responsable d'un changement de motif différé.
    /// </summary>
    private void CancelPendingMotifSwitch()
    {
        if (pendingMotifSwitch == null)
            return;

        StopCoroutine(pendingMotifSwitch);
        pendingMotifSwitch = null;
    }

    /// <summary>
    /// Active un motif de caméra après un délai personnalisé.
    /// </summary>
    /// <param name="motif">Motif à activer.</param>
    /// <param name="delay">Durée à attendre avant le basculement.</param>
    private IEnumerator SwitchMotifAfterDelay(CameraMotifSO motif, float delay)
    {
        // On attend patiemment la durée configurée afin que la phase en cours
        // puisse démarrer tout en conservant le cadrage précédent.
        yield return new WaitForSecondsRealtime(delay);

        // Si un autre StartTimelinePhase a été appelé durant l'attente, la coroutine
        // aura été stoppée et n'atteindra jamais ce point, évitant ainsi tout conflit.
        BattleCameraManager.Instance?.SetCameraMotif(motif);

        // Le champ est libéré pour accepter un nouveau délai si nécessaire.
        pendingMotifSwitch = null;
    }

    /// <summary>
    /// Attend la fin d'une timeline précédemment lancée.
    /// </summary>
    /// <param name="timeline">Timeline à surveiller.</param>
    /// <param name="overlay">Paramètre conservé pour compatibilité, sans effet.</param>
    private IEnumerator WaitForTimelinePhase(TimelineAsset timeline, bool overlay, CharacterUnit caster)
    {
        if (timeline == null || caster == null)
            yield break;

        float maxDuration = (float)timeline.duration;
        float timer = 0f;
        while (BattleTimelineManager.Instance != null &&
               BattleTimelineManager.Instance.IsCasterTimelinePlaying(caster) &&
               timer < maxDuration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (BattleTimelineManager.Instance != null && BattleTimelineManager.Instance.IsCasterTimelinePlaying(caster))
            Debug.LogWarning($"[RhythmQTEManager] Timeline '{timeline.name}' encore active après {maxDuration}s. Suite forcée.");
    }

    private IEnumerator KeepCasterFacingTarget(CharacterUnit caster, CharacterUnit target, float rotationSpeed = 360f)
    {
        while (caster != null && target != null && !caster.IsDead)
        {
            Vector3 direction = target.transform.position - caster.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
                targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
                Quaternion currentRotation = Quaternion.Euler(0f, caster.transform.eulerAngles.y, 0f);
                caster.transform.rotation = Quaternion.RotateTowards(
                    currentRotation,
                    targetRotation,
                    rotationSpeed * Time.unscaledDeltaTime);
            }

            yield return null;
        }
    }

    // Séquence du Musicalmove - Ajouter autant de méthodes que d'effets durant le move
    /// <summary>
    /// Indique si l'animation « PrepareToUndergo » doit être retardée pour la cible.
    /// Avec une timeline unique, ce drapeau reste désactivé.
    /// Cette information reste exposée afin que les autres gestionnaires (comme
    /// le NewBattleManager) puissent synchroniser leurs propres feedbacks visuels.
    /// </summary>
    public bool ShouldDelayTargetPreparationAnimation => delayTargetPreparationAnimationForCurrentMove;

    /// <summary>
    /// Prépare le comportement d'animation défensive de la cible avant même
    /// que la routine principale du <see cref="MusicalMoveSO"/> ne débute.
    /// </summary>
    /// <param name="upcomingMove">Move qui va être exécuté (peut être nul).</param>
    public void PrimeTargetPreparationAnimation(MusicalMoveSO upcomingMove)
    {
        // Avec une timeline unique, l'animation de préparation peut rester
        // immédiate : aucune phase préalable ne vient la décaler.
        delayTargetPreparationAnimationForCurrentMove = false;
    }

    /// <summary>
    /// Drapeau interne mémorisant l'état du retard à appliquer à l'animation
    /// de préparation de la cible. En évitant une variable locale, on rend
    /// l'information consultable par d'autres systèmes durant toute la routine.
    /// </summary>
    private bool delayTargetPreparationAnimationForCurrentMove = false;

    /// <summary>
    /// Orchestration complète d'un MusicalMove du déplacement à la résolution.
    /// </summary>
    public IEnumerator MusicalMoveRoutine(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        // Le caster peut éventuellement être détruit avant que la routine ne débute
        // On sécurise donc l'accès à son nom dans le log
        string casterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log("Début de la séquence du MusicalMove: " + move + " de " + casterName);
        isActive = true;
        successResults = new List<bool>();
        perfectResults = new List<bool>();
        successfulNotes = 0;
        moveAppliedEffect = false;
        hasCriticalHitThisMove = false;
        LastMoveAppliedEffect = false;

        // Position et rotation initiales du lanceur avant tout déplacement.
        // La rotation capturée ici (début de la séquence) servira de
        // référence pour la caméra jusqu'à la fin du move.
        Vector3 originPosition = caster != null ? caster.transform.position : Vector3.zero;
        // Prépare les variables globales avant toute animation ou téléportation.
        // Des événements d'animation peuvent survenir très tôt et doivent
        // pouvoir accéder à ces références immédiatement.
        currentMove = move;
        currentCaster = caster;
        currentTarget = target;
        Coroutine faceTargetCoroutine = null;
        if (move != null && move.stayFaceToTarget && caster != null && target != null)
            faceTargetCoroutine = StartCoroutine(KeepCasterFacingTarget(caster, target));
        if (qteEnabled)
        {
            pendingNotes = move.notes != null ? move.notes.Count : 0;
            StartNoteSchedule(move);
        }
        else
        {
            pendingNotes = 0;
        }

        bool tauntPlayed = false;
        System.Action<CharacterUnit> deathHandler = null;
        deathHandler = (dead) =>
        {
            if (!tauntPlayed && isActive && caster != null)
            {
                StartCoroutine(PlayTauntWithDelay(caster, caster.Data.prematureDeathTaunt, 1f));
                tauntPlayed = true;
            }
        };
        // La posture défensive de la cible peut démarrer immédiatement avec l'animation du move.
        delayTargetPreparationAnimationForCurrentMove = false;
        if (target != null)
        {
            target.OnDeath += deathHandler;
            target.PlayPrepareToUndergoAnimation();
        }

        // Configure le rig caméra avec les cibles du move avant de démarrer la mise en scène.
        BattleCameraManager.Instance?.ConfigureActionTargets(caster, target);

        // Active le motif dès le début de la séquence (utile aussi pour les moves IA sans sélection préalable).
        if (move != null)
        {
            CancelPendingMotifSwitch();
            if (move.cameraMotif != null)
                BattleCameraManager.Instance?.LockCameraMotif(move.cameraMotif);
            else
                BattleCameraManager.Instance?.ClearCameraMotif();
        }

        // Éventuel délai de pré-animation.
        // 🔄 Cette attente se produit désormais AVANT toute animation
        //     afin d'éviter un à-coup juste avant la téléportation.
        //     Elle laisse ainsi le temps de mettre en place des effets
        //     ou des annonces avant même le début du move.
        if (move.animationDelay > 0f)
        {
            // Délai en temps réel afin que la séquence commence au bon moment même en pause.
            yield return new WaitForSecondsRealtime(move.animationDelay);
        }

        // L'ancienne timeline globale de caméra est désactivée.

        // Rafraîchit la position de retour juste avant le déplacement effectif,
        // afin de revenir au point exact occupé au début de l'action.
        if (caster != null)
            originPosition = caster.transform.position;

        // Déplacement ou téléportation vers la cible si nécessaire.
        if (move.requiresMovement && caster != null && target != null)
            yield return MoveTo(caster, target, move);
        else if (caster != null && target != null)
        {
            // Sans déplacement, on oriente simplement le lanceur vers sa cible
            // Limitation de la rotation à l'axe Y pour éviter toute inclinaison
            Vector3 dir = target.transform.position - caster.transform.position;
            dir.y = 0f; // Ignore la différence de hauteur entre les deux unités
            dir = dir.normalized; // Normalisation après modification
            if (dir != Vector3.zero)
                caster.transform.forward = dir; // Applique uniquement la rotation horizontale
        }

        // --- Animation du move ---
        float animationStartTime = 0f;
        float animationDuration = 0f;
        EnsureQteBarForMove(move);
        if (move != null && caster != null && !caster.IsDead)
        {
            if (move.performingAnimation != null)
            {
                caster.PlayPerformingAnimation(move.performingAnimation);
                animationStartTime = Time.unscaledTime;
                animationDuration = move.performingAnimation.length;
            }
            else
            {
                Debug.LogWarning($"[MusicalMoveRoutine] performingAnimation manquante pour {move.moveName}.");
            }
        }

        if (qteEnabled)
        {
            if (pendingNotes == 0)
            {
                ApplyMoveEffect(move, caster, target);
            }
            else
            {
                while (pendingNotes > 0)
                {
                    float safeDelay = GetTotalRhythmSeconds(move);
                    float timer = 0f;
                    while (pendingNotes > 0 && timer < safeDelay)
                    {
                        // Le timer de sécurité utilise le temps réel pour rester fiable quelle que soit la vitesse globale.
                        timer += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (pendingNotes > 0)
                    {
                        Debug.LogWarning($"[MusicalMoveRoutine] {pendingNotes} note(s) non résolues pour {move.moveName}. Forçage de la suite.");
                        pendingNotes = 0;
                    }
                }

                TryApplyPerfectQteBonus(move, caster, target);
            }
        }

        if (animationDuration > 0f)
        {
            float elapsed = Time.unscaledTime - animationStartTime;
            float remaining = animationDuration - elapsed;
            if (remaining > 0f)
                yield return new WaitForSecondsRealtime(remaining);
        }

        bool critical = hasCriticalHitThisMove;

        // --- Retour ou téléportation de repli ---
        if (move.requiresMovement && !move.stayInPlace && caster != null && target != null)
            yield return ReturnToInitialPosition(move, caster, target, originPosition);

        if (faceTargetCoroutine != null)
            StopCoroutine(faceTargetCoroutine);

        if (caster != null)
            NewBattleManager.Instance?.OrientUnitTowardClosestOpponent(caster);

        if (move != null && move.cameraMotif != null)
            BattleCameraManager.Instance?.UnlockCameraMotif(move.cameraMotif);

        isActive = false;
        NewBattleManager.Instance.AfterMusicalMove(move, caster, critical);
        LastMoveAppliedEffect = moveAppliedEffect;

        if (target != null)
            target.OnDeath -= deathHandler;

        StopNoteSchedule();
        currentMove = null;
        currentCaster = null;
        currentTarget = null;
        delayTargetPreparationAnimationForCurrentMove = false; // ✅ Réinitialisation du drapeau de report

        BattleCameraManager.Instance?.ClearRigTargets();

        // Nettoie la barre de QTE une fois la séquence terminée
        ClearQTEBar();

        // Si le caster a été détruit durant la séquence, on évite une exception
        casterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log("Fin de la séquence du MusicalMove: " + move + " de " + casterName);
    }

    /// <summary>
    /// Gère la séquence d'utilisation d'un objet en combat.
    /// Cette routine reprend le même déroulé qu'un MusicalMove :
    /// lecture d'une éventuelle introduction, déplacement optionnel,
    /// animation principale puis retour à la position initiale.
    /// </summary>
    public IEnumerator ItemRoutine(ItemData item, CharacterUnit caster, CharacterUnit target)
    {
        // Préserve la sécurité si jamais le caster est détruit avant l'appel
        string itemCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log($"Début de la séquence d'utilisation de l'objet: {item.itemName} par {itemCasterName}");
        isActive = true;

        // Position et rotation de départ du lanceur avant toute animation.
        // La rotation enregistrée ici au tout début de la séquence
        // est réutilisée pour l'intégralité de l'utilisation de l'objet afin de
        // conserver une orientation de caméra stable.
        Vector3 originPosition = caster != null ? caster.transform.position : Vector3.zero;
        Quaternion initialRotation = caster != null ? caster.transform.rotation : Quaternion.identity;

        // Prépare la barre de QTE correspondant au motif de l'objet
        if (item.beatPattern != null && item.beatPattern.Count > 0)
            PrepareQTEBar(item.beatPattern);

        if (caster == null || caster.IsDead)
        {
            isActive = false;
            yield break;
        }

        
        Animator casterAnimator = caster.GetCasterAnimator();
        GameObject casterAnimatorGO = casterAnimator != null ? casterAnimator.gameObject : null;

        GameObject casterCameraTarget = casterAnimatorGO ?? caster.gameObject;
        GameObject performingCameraTarget = target != null
            ? (target.GetCasterBindingTarget() ?? target.gameObject)
            : casterCameraTarget;

        // L'ancien système de timeline caméra est remplacé par les caméras Cinemachine.
        bool useOverlay = false;
        // Lancement de timeline globale désactivé.

        BattleCameraManager.Instance?.ConfigureActionTargets(caster, target);

        // Les objets se jouent désormais sur place : on n'exécute plus de déplacement vers la cible.
        // On conserve uniquement l'orientation pour que la mise en scène reste cohérente.
        if (caster != null && target != null)
        {
            // Sans déplacement, on oriente simplement l'utilisateur vers sa cible
            // Limite la rotation à l'axe Y pour éviter les inclinaisons verticales
            Vector3 dir = target.transform.position - caster.transform.position;
            dir.y = 0f; // Neutralise l'écart de hauteur
            dir = dir.normalized; // Normalisation après suppression de la composante verticale
            if (dir != Vector3.zero)
                caster.transform.forward = dir; // Applique la rotation uniquement sur le plan horizontal
        }

        // --- Animation d'utilisation ---
        // Le motif reste unique pendant toute l'animation.
        StartTimelinePhase(
            null,
            useOverlay,
            caster,
            casterAnimatorGO,
            performingCameraTarget,
            true,
            initialRotation,
            item.cameraMotif);

        float animationStartTime = 0f;
        float animationDuration = 0f;
        bool itemUsePlayed = false;
        bool itemUseTriggered = false;
        int[] itemUseFullHashes = null;
        // L'animation d'item est toujours jouée sur le caster, même si le VFX cible une autre unité.
        if (caster != null && !caster.IsDead && casterAnimator != null)
        {
            int layerCount = casterAnimator.layerCount;
            itemUseFullHashes = new int[layerCount];
            for (int layer = 0; layer < layerCount; layer++)
            {
                string layerName = casterAnimator.GetLayerName(layer);
                int fullHash = Animator.StringToHash($"{layerName}.{ItemUseStateName}");
                itemUseFullHashes[layer] = fullHash;

                if (casterAnimator.HasState(layer, fullHash))
                {
                    casterAnimator.CrossFade(fullHash, 0.05f, layer, 0f);
                    itemUsePlayed = true;
                    continue;
                }

                if (casterAnimator.HasState(layer, AnimatorStateItemUseShortHash))
                {
                    casterAnimator.CrossFade(AnimatorStateItemUseShortHash, 0.05f, layer, 0f);
                    itemUsePlayed = true;
                }
            }

            foreach (var parameter in casterAnimator.parameters)
            {
                if (parameter.nameHash != AnimatorStateItemUseShortHash)
                    continue;

                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    casterAnimator.SetTrigger(parameter.nameHash);
                    itemUseTriggered = true;
                    itemUsePlayed = true;
                }
                break;
            }
        }

        if (itemUsePlayed)
        {
            yield return null;
            animationStartTime = Time.unscaledTime;
            animationDuration = 0f;
            int layerCount = casterAnimator.layerCount;
            for (int layer = 0; layer < layerCount; layer++)
            {
                var stateInfo = casterAnimator.GetCurrentAnimatorStateInfo(layer);
                int fullHash = itemUseFullHashes != null ? itemUseFullHashes[layer] : 0;
                bool isItemUseState = stateInfo.shortNameHash == AnimatorStateItemUseShortHash
                    || (fullHash != 0 && stateInfo.fullPathHash == fullHash);

                if (!isItemUseState)
                    continue;

                if (stateInfo.length > animationDuration)
                    animationDuration = stateInfo.length;
            }
        }
        else if (caster != null && !itemUseTriggered)
        {
            Debug.LogWarning($"[ItemRoutine] Etat Animator 'Item_Use' introuvable pour {caster.name}.");
        }

        if (item.itemVFX != null)
            StartCoroutine(SpawnItemVfxAfterDelay(item, target));

        // QTE associé à l'objet durant l'utilisation.
        LastItemSuccess = true;
        LastItemCritical = false;
        if (qteEnabled && item.beatPattern != null && item.beatPattern.Count > 0)
        {
            successResults = new List<bool>();
            List<bool> itemPerfectResults = new();
            foreach (float beat in item.beatPattern)
            {
                bool s = false;
                QTEFeedback feedback = QTEFeedback.Miss;
                yield return WaitForQTE(beat, null, Vector2.zero, (result, fb) =>
                {
                    s = result;
                    feedback = fb;
                });
                successResults.Add(s);
                itemPerfectResults.Add(feedback == QTEFeedback.Perfect);
            }
            LastItemSuccess = successResults.All(v => v);
            bool allPerfect = itemPerfectResults.All(v => v);
            if (allPerfect)
                LastItemCritical = RollCritical(caster);
        }

        if (animationDuration > 0f)
        {
            float elapsed = Time.unscaledTime - animationStartTime;
            float remaining = animationDuration - elapsed;
            if (remaining > 0f)
                yield return new WaitForSecondsRealtime(remaining);
        }

        bool timelinePaused =
            BattleTimelineManager.Instance != null && BattleTimelineManager.Instance.IsCasterTimelinePaused(caster);

        if (timelinePaused)
        {
            // 🎯 Même logique que pour les MusicalMoves : toute suspension résiduelle est levée
            //     immédiatement pour garantir une reprise fluide de la mise en scène.
            BattleTimelineManager.Instance?.ResumeCasterTimeline(caster);
        }

        isActive = false;
        // Protection en cas de destruction du lanceur avant la fin de la séquence
        itemCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log($"Fin de la séquence d'utilisation de l'objet: {item.itemName} par {itemCasterName}");

        // Nettoie la barre de QTE utilisée pour l'objet
        ClearQTEBar();

        // Retour à la caméra par défaut.
        // Comme pour les MusicalMoves, on neutralise toute attente résiduelle avant
        // de rendre la main à la caméra principale.
        CancelPendingMotifSwitch();
        BattleCameraManager.Instance?.ClearCameraMotif();
        BattleCameraManager.Instance?.ClearRigTargets();
    }

    private IEnumerator SpawnItemVfxAfterDelay(ItemData item, CharacterUnit target)
    {
        if (item == null || item.itemVFX == null)
            yield break;

        float delay = Mathf.Max(0f, item.vfxDelay);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (target == null)
            yield break;

        Transform anchor = target.GetCasterBindingTarget()?.transform ?? target.transform;
        if (anchor == null)
            yield break;

        GameObject instance = Instantiate(item.itemVFX, anchor.position, Quaternion.identity);
        instance.transform.SetParent(anchor, worldPositionStays: true);
    }

    /// <summary>
    /// Instancie (si disponible) le système de particules de dash et l'attache à l'unité.
    /// Cette méthode centralise l'initialisation pour s'assurer que le même comportement
    /// est appliqué lors des déplacements d'objets comme des MusicalMoves.
    /// </summary>
    /// <param name="caster">L'unité qui se déplace actuellement.</param>
    /// <returns>Une instance de particules prête à être suivie ou <c>null</c> si aucun prefab n'est configuré.</returns>
    private ParticleSystem SpawnDashParticles(CharacterUnit caster)
    {
        if (caster == null)
            return null; // Sécurité minimale : la méthode n'a pas vocation à gérer un déplacement sans unité.

        // Chaque personnage peut disposer de son propre effet de dash défini dans son CharacterData.
        // On récupère donc la référence à chaud pour respecter la personnalisation visuelle demandée.
        CharacterData casterData = caster.Data;
        ParticleSystem dashParticlesPrefab = casterData != null ? casterData.dashParticlesPrefab.GetComponentInChildren<ParticleSystem>() : null;

        if (dashParticlesPrefab == null)
            return null; // Aucun effet configuré pour cette unité : aucun système n'est créé.

        // Instancie le prefab directement à la position du lanceur pour éviter tout décalage visuel.
        ParticleSystem instance = Instantiate(dashParticlesPrefab, caster.transform.position, caster.transform.rotation);

        // Lecture optionnelle du son associé : priorité au clip personnalisé du personnage,
        // sinon utilisation d'un son par défaut défini dans le manager.
        AudioClipSO dashClip = casterData != null && casterData.dashSoundClip != null
            ? casterData.dashSoundClip
            : defaultDashSound;
        PlaySfx(dashClip);

        // Parentage temporaire : le système suit automatiquement l'unité pendant le déplacement.
        Transform instanceTransform = instance.transform;
        instanceTransform.SetParent(caster.transform, worldPositionStays: false);
        instanceTransform.localPosition = Vector3.zero; // Positionne l'effet au centre du personnage.
        instanceTransform.localRotation = Quaternion.identity; // Conserve l'orientation naturelle du prefab.
        instanceTransform.localScale = Vector3.one; // Évite tout effet d'échelle hérité inattendu.

        instance.Play(true); // Sécurise le lancement de l'effet (utile si le prefab est configuré sur "Stop").
        return instance;
    }

    /// <summary>
    /// Détache le système de particules du lanceur et lance une extinction progressive
    /// suivie de sa destruction pour éviter toute fuite de GameObject dans la scène.
    /// </summary>
    /// <param name="instance">Instance des particules précédemment créées.</param>
    private void ReleaseDashParticles(ParticleSystem instance)
    {
        if (instance == null)
            return; // Rien à relâcher : le déplacement s'est effectué sans effet visuel.

        Transform instanceTransform = instance.transform;
        if (instanceTransform != null)
        {
            // Le détachement permet de laisser l'effet se dissiper sur place même si l'unité repart immédiatement.
            instanceTransform.SetParent(null, worldPositionStays: true);
        }

        // La coroutine gère la réduction de l'émission sur 0,5 s puis la destruction après 2 s.
        StartCoroutine(FadeOutAndDestroyDashParticles(instance));
    }

    /// <summary>
    /// Réduit progressivement l'émission d'un système de particules puis le détruit
    /// afin de respecter les consignes de lisibilité en combat sans générer de déchets.
    /// </summary>
    /// <param name="instance">Système de particules à éteindre.</param>
    private IEnumerator FadeOutAndDestroyDashParticles(ParticleSystem instance)
    {
        if (instance == null)
            yield break; // Sécurité supplémentaire : l'effet peut déjà avoir été détruit ailleurs.

        // Multiplieurs de départ : ils sont utilisés pour conserver la configuration du prefab
        // (courbes, bursts, etc.) tout en la faisant décroître progressivement.
        var emission = instance.emission;
        float baseRateTimeMultiplier = emission.rateOverTimeMultiplier;
        float baseRateDistanceMultiplier = emission.rateOverDistanceMultiplier;

        const float fadeDuration = 0.5f; // Durée souhaitée pour atteindre une émission nulle.
        float timer = 0f;

        while (instance != null && timer < fadeDuration)
        {
            float factor = 1f - (timer / fadeDuration); // Interpolation linéaire de 1 vers 0.

            emission = instance.emission; // Récupération à chaque itération car il s'agit d'une struct.
            emission.rateOverTimeMultiplier = baseRateTimeMultiplier * factor;
            emission.rateOverDistanceMultiplier = baseRateDistanceMultiplier * factor;

            timer += Time.deltaTime;
            yield return null; // Attente d'une frame pour conserver une animation fluide.
        }

        if (instance == null)
            yield break; // L'effet a pu être détruit durant l'attente (ex : changement de scène).

        emission = instance.emission;
        emission.rateOverTimeMultiplier = 0f;
        emission.rateOverDistanceMultiplier = 0f;
        instance.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting); // Stoppe proprement l'émission.

        yield return new WaitForSecondsRealtime(2f); // Laisse le temps aux particules déjà générées de disparaître naturellement.

        if (instance != null)
            Destroy(instance.gameObject); // Suppression finale pour éviter toute accumulation en scène.
    }

    private bool TryEnableRootMotionMove(CharacterUnit caster, float desiredSpeed)
    {
        if (!preferRootMotionMoves || caster == null)
            return false;

        var rootMotion = caster.EnsureRootMotionApplier();
        if (rootMotion == null)
            return false;

        rootMotion.LockYToGround = true;
        rootMotion.ApplyRotation = true;
        rootMotion.SpeedScale = 1f;
        caster.SetRootMotionActive(true, desiredSpeed, scaleToSpeed: true);
        return true;
    }

    private void DisableRootMotionMove(CharacterUnit caster)
    {
        if (caster == null)
            return;

        caster.SetRootMotionActive(false);
    }

    private bool HasRootMotionDelta(Animator animator)
    {
        if (animator == null)
            return false;

        float threshold = Mathf.Max(0f, rootMotionProbeThreshold);
        return animator.deltaPosition.sqrMagnitude > threshold * threshold;
    }

    private IEnumerator SimpleMoveTo(CharacterUnit caster, CharacterUnit target, ItemData item)
    {
        if (caster == null || caster.IsDead)
            yield break;

        // Destination calculée selon la portée de l'objet
        Vector3 destination = target.transform.position + target.transform.forward * item.castDistance;
        Animator animator = caster.GetCasterAnimator();
        // Récupère le GameObject qui porte l'Animator pour pouvoir le masquer
        GameObject visualRoot = animator != null ? animator.gameObject : null;

        bool isTeleport = item.moveSpeed <= 0f; // Téléportation si vitesse nulle ou négative
        float distanceToDestination = Vector3.Distance(caster.transform.position, destination); // Distance à parcourir

        // Téléportation uniquement si la destination est différente de la position actuelle
        if (isTeleport && distanceToDestination > 0.01f)
        {
            // Lecture des effets de départ
            if (item.tpSFx_Start != null)
                PlaySfx(item.tpSFx_Start);
            if (item.tpVfx != null)
                Instantiate(item.tpVfx, caster.transform.position, Quaternion.identity);

            // Animation de déplacement
            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            // Cache le visuel le temps de la téléportation
            if (visualRoot != null)
                visualRoot.SetActive(false);

            // Délai configurable pour laisser apparaître l'effet de téléport
            float delay = item.teleportDelay >= 0f ? item.teleportDelay : defaultTeleportDelay;
            // Utilise une attente en temps réel pour que les téléportations fonctionnent même lorsque le jeu est en pause.
            yield return new WaitForSecondsRealtime(delay);

            // Téléportation instantanée
            caster.transform.position = destination;

            // Réaffiche le personnage à la nouvelle position
            if (visualRoot != null)
                visualRoot.SetActive(true);

            if (item.tpVfx != null)
                Instantiate(item.tpVfx, destination, Quaternion.identity);
            if (item.tpSFx_End != null)
                PlaySfx(item.tpSFx_End);
        }
        else if (!isTeleport)
        {
            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, destination);
            // Instancie les particules uniquement si un déplacement réel est nécessaire.
            ParticleSystem dashParticles = distance > 0.01f ? SpawnDashParticles(caster) : null;
            if (distance > 0.01f)
                caster.PlayMovementAnimation(distance);
            float duration = fixedMoveDurationSeconds;
            bool useRootMotion = distance > 0.01f && TryEnableRootMotionMove(caster, distance / Mathf.Max(duration, 0.0001f));

            if (useRootMotion)
            {
                Vector3 faceDir = destination - caster.transform.position;
                faceDir.y = 0f;
                if (faceDir.sqrMagnitude > 0.0001f)
                    caster.transform.forward = faceDir.normalized;

                bool hasRootMotion = false;
                for (int i = 0; i < rootMotionProbeFrames; i++)
                {
                    yield return null;
                    if (HasRootMotionDelta(animator))
                    {
                        hasRootMotion = true;
                        break;
                    }
                }

                if (!hasRootMotion)
                {
                    DisableRootMotionMove(caster);
                    useRootMotion = false;
                    caster.transform.position = startPos;
                }
            }

            if (useRootMotion)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                DisableRootMotionMove(caster);
                caster.transform.position = destination;
            }
            else
            {
                float t = 0f;
                // Mouvement progressif vers la destination
                while (t < 1f)
                {
                    caster.transform.position = Vector3.Lerp(startPos, destination, t);
                    t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                    yield return null;
                }
                caster.transform.position = destination;
            }

            caster.StopMovementAnimation();

            // Laisse l'effet se dissiper en douceur maintenant que l'unité est arrivée.
            ReleaseDashParticles(dashParticles);
        }
        // Si isTeleport mais distance nulle, on ne fait rien : aucune téléportation nécessaire

        // Orientation vers la cible
        // Conserve uniquement la rotation horizontale pour éviter les pivots en X
        Vector3 dir = target.transform.position - caster.transform.position;
        dir.y = 0f; // Ignore la composante verticale
        dir = dir.normalized; // Normalisation pour obtenir une direction unitaire
        if (dir != Vector3.zero)
            caster.transform.forward = dir; // Applique l'orientation sur l'axe Y uniquement

        yield return null;
    }

    // Retourne le lanceur à la position qu'il occupait au début de l'utilisation de l'objet
    private IEnumerator SimpleReturnToInitialPosition(CharacterUnit caster, CharacterUnit target, ItemData item, Vector3 originPosition)
    {
        if (caster == null || caster.IsDead)
            yield break;

        Vector3 origin = originPosition;
        Animator animator = caster.GetCasterAnimator();
        // GameObject contenant l'Animator à masquer pendant la téléportation
        GameObject visualRoot = animator != null ? animator.gameObject : null;
        bool isTeleport = item.moveSpeed <= 0f; // Téléportation si vitesse nulle
        float distanceToOrigin = Vector3.Distance(caster.transform.position, origin); // Distance de retour

        // Téléportation uniquement si le retour nécessite un déplacement
        if (isTeleport && distanceToOrigin > 0.01f)
        {
            if (item.tpSFx_Start != null)
                PlaySfx(item.tpSFx_Start);
            if (item.tpVfx != null)
                Instantiate(item.tpVfx, caster.transform.position, Quaternion.identity);

            if (!caster.IsDead)
                animator?.Play("Retreat_Battle");

            // Cache le GameObject visuel pendant l'attente
            if (visualRoot != null)
                visualRoot.SetActive(false);

            // Délai configurable avant la réapparition à la position initiale
            float delay = item.teleportDelay >= 0f ? item.teleportDelay : defaultTeleportDelay;
            // Utilise une attente en temps réel pour que les téléportations fonctionnent même lorsque le jeu est en pause.
            yield return new WaitForSecondsRealtime(delay);

            caster.transform.position = origin;

            // Réaffiche le personnage une fois la téléportation effectuée
            if (visualRoot != null)
                visualRoot.SetActive(true);

            if (item.tpVfx != null)
                Instantiate(item.tpVfx, origin, Quaternion.identity);
            if (item.tpSFx_End != null)
                PlaySfx(item.tpSFx_End);
        }
        else if (!isTeleport)
        {
            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, origin);
            ParticleSystem dashParticles = distance > 0.01f ? SpawnDashParticles(caster) : null;
            if (distance > 0.01f)
                caster.PlayMovementAnimation(distance);
            float duration = fixedMoveDurationSeconds;
            bool useRootMotion = distance > 0.01f && TryEnableRootMotionMove(caster, distance / Mathf.Max(duration, 0.0001f));

            if (useRootMotion)
            {
                Vector3 faceDir = origin - caster.transform.position;
                faceDir.y = 0f;
                if (faceDir.sqrMagnitude > 0.0001f)
                    caster.transform.forward = faceDir.normalized;

                bool hasRootMotion = false;
                for (int i = 0; i < rootMotionProbeFrames; i++)
                {
                    yield return null;
                    if (HasRootMotionDelta(animator))
                    {
                        hasRootMotion = true;
                        break;
                    }
                }

                if (!hasRootMotion)
                {
                    DisableRootMotionMove(caster);
                    useRootMotion = false;
                    caster.transform.position = startPos;
                }
            }

            if (useRootMotion)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                DisableRootMotionMove(caster);
                caster.transform.position = origin;
            }
            else
            {
                float t = 0f;
                while (t < 1f)
                {
                    caster.transform.position = Vector3.Lerp(startPos, origin, t);
                    t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                    yield return null;
                }
                caster.transform.position = origin;
            }

            caster.StopMovementAnimation();

            ReleaseDashParticles(dashParticles);
        }
        // Si isTeleport mais distance nulle, aucune action n'est nécessaire

        // Orientation optionnelle vers la cible
        // Limite la rotation au plan horizontal pour conserver l'orientation naturelle du personnage
        if (target != null)
        {
            Vector3 lookDir = target.transform.position - caster.transform.position;
            lookDir.y = 0f; // Ignore la différence de hauteur avec la cible
            lookDir = lookDir.normalized; // Normalisation après suppression de l'axe vertical
            if (lookDir != Vector3.zero)
                caster.transform.forward = lookDir; // Rotation uniquement autour de l'axe Y
        }

        yield return null;
    }

    private IEnumerator MoveTo(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        if (caster == null || caster.IsDead)
            yield break;

        // Journalisation sécurisée en cas de destruction d'une des unités
        string moveCasterName = caster != null ? caster.name : "(caster nul)";
        string moveTargetName = target != null ? target.name : "(cible nulle)";
        Debug.Log("Déplacement de " + moveCasterName + " vers " + moveTargetName);

        Vector3 startPosition = caster.transform.position;

        Vector3 targetBasePosition = target != null ? target.transform.position : startPosition;
        if (move.targetType == TargetType.SpawnPosition)
        {
            var battleManager = NewBattleManager.Instance;
            if (battleManager != null && battleManager.TryResolveUnitSpawnPosition(caster, out Vector3 spawnPosition))
                targetBasePosition = spawnPosition;
            else
                Debug.LogWarning("[MoveTo] Position de spawn introuvable pour le déplacement.");
        }

        // Calcul de la position cible en fonction de la position relative
        Vector3 offsetDir = Vector3.zero;
        if (move.relativePosition != RelativePosition.On)
        {
            Vector3 referenceDir = Vector3.zero;
            if (target != null)
            {
                referenceDir = caster.transform.position - targetBasePosition;
                referenceDir.y = 0f;
                if (referenceDir.sqrMagnitude <= 0.0001f)
                    referenceDir = target.transform.forward;
            }
            else
            {
                referenceDir = caster.transform.forward;
            }

            referenceDir.y = 0f;
            if (referenceDir.sqrMagnitude > 0.0001f)
                referenceDir = referenceDir.normalized;

            offsetDir = referenceDir;
            switch (move.relativePosition)
            {
                case RelativePosition.Back:
                    offsetDir = -referenceDir;
                    break;
                case RelativePosition.Left:
                    offsetDir = -Vector3.Cross(Vector3.up, referenceDir);
                    break;
                case RelativePosition.Right:
                    offsetDir = Vector3.Cross(Vector3.up, referenceDir);
                    break;
            }
        }

        Vector3 targetPos = targetBasePosition + offsetDir * move.castDistance;

        Animator animator = caster.GetCasterAnimator();
        // Stocke le GameObject visuel pour pouvoir le désactiver durant la téléportation
        GameObject visualRoot = animator != null ? animator.gameObject : null;
        bool isTeleport = move.ShouldTeleportToTarget(); // Téléportation si le temps demandé est nul ou si l'ancien move utilisait une vitesse nulle
        float distanceToTarget = Vector3.Distance(caster.transform.position, targetPos); // Distance à la cible

        // Téléportation uniquement si la cible est différente de la position actuelle
        if (isTeleport && distanceToTarget > 0.01f)
        {
            if (move.tpSFx_Start != null)
                PlaySfx(move.tpSFx_Start);
            if (move.tpVfx != null)
                Instantiate(move.tpVfx, caster.transform.position, Quaternion.identity);

            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            // Masque le visuel du lanceur pendant l'attente
            if (visualRoot != null)
                visualRoot.SetActive(false);

            // Délai configurable pour la téléportation du MusicalMove
            float delay = move.teleportDelay >= 0f ? move.teleportDelay : defaultTeleportDelay;
            // Utilise une attente en temps réel pour que les téléportations fonctionnent même lorsque le jeu est en pause.
            yield return new WaitForSecondsRealtime(delay);

            caster.transform.position = targetPos;

            // Réactive l'apparence du personnage après la téléportation
            if (visualRoot != null)
                visualRoot.SetActive(true);

            if (move.tpVfx != null)
                Instantiate(move.tpVfx, targetPos, Quaternion.identity);
            if (move.tpSFx_End != null)
                PlaySfx(move.tpSFx_End);
        }
        else if (!isTeleport)
        {
            Vector3 startPos = caster.transform.position;
            float distance = Vector3.Distance(startPos, targetPos);
            ParticleSystem dashParticles = distance > 0.01f ? SpawnDashParticles(caster) : null;
            if (distance > 0.01f)
                caster.PlayMovementAnimation(distance);
            float duration = fixedMoveDurationSeconds;
            bool useRootMotion = distance > 0.01f && TryEnableRootMotionMove(caster, distance / Mathf.Max(duration, 0.0001f));

            if (useRootMotion)
            {
                Vector3 faceDir = targetPos - caster.transform.position;
                faceDir.y = 0f;
                if (faceDir.sqrMagnitude > 0.0001f)
                    caster.transform.forward = faceDir.normalized;

                bool hasRootMotion = false;
                for (int i = 0; i < rootMotionProbeFrames; i++)
                {
                    yield return null;
                    if (HasRootMotionDelta(animator))
                    {
                        hasRootMotion = true;
                        break;
                    }
                }

                if (!hasRootMotion)
                {
                    DisableRootMotionMove(caster);
                    useRootMotion = false;
                    caster.transform.position = startPos;
                }
            }

            if (useRootMotion)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                DisableRootMotionMove(caster);
                caster.transform.position = targetPos;
            }
            else
            {
                float t = 0f;
                while (t < 1f)
                {
                    caster.transform.position = Vector3.Lerp(startPos, targetPos, t);
                    t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                    yield return null;
                }
                caster.transform.position = targetPos;
            }

            caster.StopMovementAnimation();

            ReleaseDashParticles(dashParticles);
        }
        // Si isTeleport mais aucune distance à parcourir, on ignore le déplacement

        Vector3 lookDir = Vector3.zero;
        if (target != null)
        {
            lookDir = targetBasePosition - caster.transform.position;
            lookDir.y = 0f; // Ignore l'écart de hauteur
            lookDir = lookDir.normalized;
        }

        // Ajuste également la direction d'offset pour éviter tout pivot vertical
        if (offsetDir != Vector3.zero)
        {
            offsetDir.y = 0f;
            offsetDir = offsetDir.normalized;
        }

        if (lookDir != Vector3.zero)
            caster.transform.forward = lookDir; // Priorité à la direction vers la cible
        else if (offsetDir != Vector3.zero)
            caster.transform.forward = offsetDir; // Sinon on utilise la direction relative (horizontalement)

        yield return null;
        moveCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log("Fin du déplacement de " + moveCasterName);
    }

    // Replace le lanceur exactement à la position occupée avant le début du move
    private IEnumerator ReturnToInitialPosition(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target, Vector3 originPosition)
    {
        // Peut être appelé alors que l'unité a été détruite
        string returnCasterName = caster != null ? caster.name : "(caster nul)";
        Debug.Log("Retour de " + returnCasterName + " vers sa position initiale");

        if (caster == null || caster.IsDead)
        {
            Debug.LogWarning("ReturnToInitialPosition : caster nul ou détruit à l'appel");
            yield break;
        }

        Vector3 startPos = caster.transform.position;
        Vector3 initialPosition = originPosition;
        Animator animator = caster.GetCasterAnimator();
        // GameObject contenant l'Animator à désactiver durant la téléportation
        GameObject visualRoot = animator != null ? animator.gameObject : null;
        bool isTeleport = move.ShouldTeleportToTarget(); // Téléportation si le temps demandé est nul ou si l'ancien move utilisait une vitesse nulle
        float distanceToInitial = Vector3.Distance(caster.transform.position, initialPosition); // Distance à parcourir

        // Téléportation uniquement si nécessaire
        if (isTeleport && distanceToInitial > 0.01f)
        {
            if (move.tpSFx_Start != null)
                PlaySfx(move.tpSFx_Start);
            if (move.tpVfx != null)
                Instantiate(move.tpVfx, caster.transform.position, Quaternion.identity);

            if (!caster.IsDead)
                animator?.Play("Dash_Battle");

            // Cache temporairement le GameObject visuel
            if (visualRoot != null)
                visualRoot.SetActive(false);

            // Délai configurable avant de revenir à la position de départ
            float delay = move.teleportDelay >= 0f ? move.teleportDelay : defaultTeleportDelay;
            // Utilise une attente en temps réel pour que les téléportations fonctionnent même lorsque le jeu est en pause.
            yield return new WaitForSecondsRealtime(delay);

            if (caster == null)
            {
                Debug.LogWarning("ReturnToInitialPosition : caster détruit pendant le délai");
                yield break;
            }

            caster.transform.position = initialPosition;

            // Réaffiche le personnage à sa position initiale
            if (visualRoot != null)
                visualRoot.SetActive(true);

            if (move.tpVfx != null)
                Instantiate(move.tpVfx, initialPosition, Quaternion.identity);
            if (move.tpSFx_End != null)
                PlaySfx(move.tpSFx_End);
        }
        else if (!isTeleport)
        {
            float distance = Vector3.Distance(startPos, initialPosition);
            ParticleSystem dashParticles = distance > 0.01f ? SpawnDashParticles(caster) : null;
            if (distance > 0.01f)
                caster.PlayMovementAnimation(distance);
            float duration = fixedMoveDurationSeconds;
            bool useRootMotion = distance > 0.01f && TryEnableRootMotionMove(caster, distance / Mathf.Max(duration, 0.0001f));

            if (useRootMotion)
            {
                Vector3 faceDir = initialPosition - caster.transform.position;
                faceDir.y = 0f;
                if (faceDir.sqrMagnitude > 0.0001f)
                    caster.transform.forward = faceDir.normalized;

                bool hasRootMotion = false;
                for (int i = 0; i < rootMotionProbeFrames; i++)
                {
                    yield return null;
                    if (HasRootMotionDelta(animator))
                    {
                        hasRootMotion = true;
                        break;
                    }
                }

                if (!hasRootMotion)
                {
                    DisableRootMotionMove(caster);
                    useRootMotion = false;
                    caster.transform.position = startPos;
                }
            }

            if (useRootMotion)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                DisableRootMotionMove(caster);
                caster.transform.position = initialPosition;
            }
            else
            {
                float t = 0f;
                while (t < 1f)
                {
                    caster.transform.position = Vector3.Lerp(startPos, initialPosition, t);
                    t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                    yield return null;
                }
                caster.transform.position = initialPosition;
            }

            caster.StopMovementAnimation();

            ReleaseDashParticles(dashParticles);
        }
        // Si isTeleport mais distance nulle, pas de retour nécessaire

        yield return null;

        if (move.stayFaceToTarget && target != null)
        {
            Vector3 finalDirToTarget = target.transform.position - caster.transform.position;
            finalDirToTarget.y = 0f; // Ignore la hauteur de la cible
            finalDirToTarget = finalDirToTarget.normalized;
            if (finalDirToTarget != Vector3.zero)
                caster.transform.forward = finalDirToTarget; // Rotation horizontale vers la cible
        }
        else
        {
            Vector3 finalDirToParent = initialPosition - caster.transform.position;
            finalDirToParent.y = 0f; // Conserve uniquement la composante horizontale
            finalDirToParent = finalDirToParent.normalized;
            if (finalDirToParent != Vector3.zero)
                caster.transform.forward = finalDirToParent; // Retourne vers sa position initiale sans inclinaison
        }
        Debug.Log("Le caster a terminé son retour.");
    }

    IEnumerator PlayMoveAnimations(string[] animationClips, CharacterUnit caster)
    {
        // Récupère une fois l’Animator plutôt que de l’appeler à chaque itération
        Animator animator = caster.GetCasterAnimator();

        foreach (string clip in animationClips)
        {
            // Lance le clip courant uniquement si l'unité est en vie
            if (!caster.IsDead)
                animator.Play(clip);
            Debug.Log("Animation jouée : " + clip);

            // On attend un frame pour laisser l’Animator passer à l’état du clip
            yield return null;

            // Récupère la longueur de l’état actuel (le clip qui vient d'être joué)
            float clipDuration = animator.GetCurrentAnimatorStateInfo(0).length;

            // Si tu veux être certain de prendre la longueur du AnimationClip lui-même,
            // tu peux aussi faire : float clipDuration = clip.length;
            yield return new WaitForSecondsRealtime(clipDuration);
        }

        Debug.Log("Toutes les animations sont terminées.");
    }

    private void TryApplyPerfectQteBonus(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        if (move == null || caster == null || target == null)
            return;

        if (move.notes == null || move.notes.Count != 4)
            return;

        if (successfulNotes != 4)
            return;

        if (!move.HasEffect(MusicalEffectType.Damage))
            return;

        int baseDamage = move.GetEffectValue(MusicalEffectType.Damage, move.PrimaryEffectValue);
        if (baseDamage <= 0)
            return;

        var options = new CombatPipeline.DamageOptions
        {
            includePower = true,
            applyAttackMultiplier = true,
            applyModifiers = true,
            clampToBaseValue = true,
            registerDamage = false,
            allowRedirect = true,
            valueMultiplier = 1f
        };

        float singleHitDamage = CombatPipeline.ResolveDamageValue(caster, baseDamage, options);
        float bonusDamage = singleHitDamage * successfulNotes * 0.2f;
        if (bonusDamage <= 0f)
            return;

        target.TakeDamage(bonusDamage, caster.transform, allowRedirect: true);
        NewBattleManager.Instance?.RegisterDamage(caster, bonusDamage);
    }

    public void TriggerNote(int index)
    {
        if (!qteEnabled)
            return;
        if (ignoreAnimationNoteEvents)
            return;
        if (currentMove == null || currentCaster == null || currentTarget == null)
            return;
        if (currentMove.notes == null || index < 0 || index >= currentMove.notes.Count)
            return;

        var note = currentMove.notes[index];

        // Si l'attaquant est un ennemi
        if (currentCaster.characterType == CharacterType.EnemyUnit)
        {
            // Si l'ennemi est marqué comme inévitable, aucun QTE défensif n'est proposé
            if (!currentCaster.Data.avoidable)
            {
                ApplyMoveEffect(currentMove, currentCaster, currentTarget);
                pendingNotes = Mathf.Max(0, pendingNotes - 1);
            }
            else
            {
                StartCoroutine(WaitForDefenseQTE(result =>
                {
                    defenseResult = result;
                    switch (result)
                    {
                        case DefenseResult.Parry:
                            currentTarget.TakeParry();
                            break;
                        case DefenseResult.Dodge:
                            currentTarget.TakeDodge();
                            break;
                        default:
                            ApplyMoveEffect(currentMove, currentCaster, currentTarget);
                            break;
                    }

                    pendingNotes = Mathf.Max(0, pendingNotes - 1);
                }));
            }
        }
        else
        {
            float windowMs = Mathf.Max(1f, note.responseDelay * 1000f);
            StartCoroutine(WaitForQTE(windowMs, note.qteInput, null, Vector2.zero, (success, feedback) =>
            {
                bool isPerfect = feedback == QTEFeedback.Perfect;
                bool isCritical = success && isPerfect && RollCritical(currentCaster);

                if (success)
                {
                    ApplyMoveEffect(currentMove, currentCaster, currentTarget, isCritical);
                    successfulNotes++;
                }
                successResults.Add(success);
                perfectResults?.Add(isPerfect);
                pendingNotes = Mathf.Max(0, pendingNotes - 1);
            }, true));
        }
    }

    public void TriggerQTE(float windowDelay)
    {
        if (!qteEnabled)
            return;
        // Appel historique sans icône
        StartCoroutine(WaitForQTE(windowDelay, null, Vector2.zero, (_, __) => { }));
    }

    public void TriggerQTE(float windowDelay, QTEInputSO qteInput)
    {
        if (!qteEnabled)
            return;
        StartCoroutine(WaitForQTE(windowDelay, qteInput, null, Vector2.zero, (_, __) => { }));
    }

    public void TriggerQTE(float windowDelay, QTEInputSO qteInput, Vector2 position)
    {
        if (!qteEnabled)
            return;
        StartCoroutine(WaitForQTE(windowDelay, qteInput, null, position, (_, __) => { }));
    }

    /// <summary>
    /// Lance un QTE en précisant l'icône à afficher au centre du cercle.
    /// </summary>
    /// <param name="windowDelay">Durée de la fenêtre en millisecondes</param>
    /// <param name="icon">Icône de l'input à afficher</param>
    public void TriggerQTE(float windowDelay, Sprite icon)
    {
        if (!qteEnabled)
            return;
        StartCoroutine(WaitForQTE(windowDelay, icon, Vector2.zero, (_, __) => { }));
    }

    /// <summary>
    /// Lance un QTE en précisant icône et position de l'affichage.
    /// </summary>
    /// <param name="windowDelay">Durée de la fenêtre en millisecondes</param>
    /// <param name="icon">Icône de l'input à afficher</param>
    /// <param name="position">Position du visuel dans le canvas</param>
    public void TriggerQTE(float windowDelay, Sprite icon, Vector2 position)
    {
        if (!qteEnabled)
            return;
        StartCoroutine(WaitForQTE(windowDelay, icon, position, (_, __) => { }));
    }

    private IEnumerator WaitForQTE(float windowDelay, Sprite icon, Vector2 position, System.Action<bool, QTEFeedback> callback, bool persistUntilMoveEnd = false)
    {
        return WaitForQTE(windowDelay, null, icon, position, callback, persistUntilMoveEnd);
    }

    private IEnumerator WaitForQTE(float windowDelay, QTEInputSO qteInput, Sprite icon, Vector2 position, System.Action<bool, QTEFeedback> callback, bool persistUntilMoveEnd = false)
    {
        if (!qteEnabled)
        {
            callback?.Invoke(false, QTEFeedback.Miss);
            yield break;
        }
        RegisterQteStart();
        float slowestTimeScale = 0f;
        float transitionDuration = 0.1f;
        float holdDuration = (windowDelay / 1000f) * Mathf.Max(0.1f, qteWindowScale);
        float normalTimeScale = 1f;

        // 🔻 Ralentissement progressif uniquement en mode facile
        float t = 0f;
        if (easyMode)
        {
            while (t < transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                float blend = t / transitionDuration;
                Time.timeScale = Mathf.Lerp(normalTimeScale, slowestTimeScale, blend);
                Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            // 🎯 Temps ralenti & instanciation du visuel
            Time.timeScale = slowestTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * slowestTimeScale;
        }

        GameObject qteVisualGO = null;
        QTECircle qteCircle = null;

        if (qteCirclePrefab != null)
        {
            Transform parent = ResolveQteUIParent();
            qteVisualGO = Instantiate(qteCirclePrefab, parent);
            qteVisualGO.transform.SetAsLastSibling();
            var visualRect = qteVisualGO.GetComponent<RectTransform>();
            FreeformLayoutGroup freeformLayout = parent != null ? parent.GetComponent<FreeformLayoutGroup>() : null;
            bool parentHasLayout = parent != null && parent.GetComponent<LayoutGroup>() != null;
            if (visualRect != null)
            {
                bool applied = false;
                if (freeformLayout != null)
                    applied = freeformLayout.RegisterRuntimeChild(visualRect);

                if (!applied && (freeformLayout != null || !parentHasLayout))
                    visualRect.anchoredPosition = position;
            }

            qteCircle = qteVisualGO.GetComponent<QTECircle>();
            if (qteCircle != null)
            {
                qteCircle.Initialize(qteInput, BattleInputType.Confirm, icon, holdDuration);
                if (persistUntilMoveEnd)
                {
                    qteCircle.SetAutoDestroyAfterFade(false);
                    persistentQteCircles.Add(qteCircle);
                }
            }
        }

        BattleInputType requiredInput = qteInput != null ? qteInput.BattleInput : BattleInputType.Confirm;
        InputAction requiredAction = null;
        if (InputsManager.Instance != null)
        {
            var battle = InputsManager.Instance.playerInputs.Battle;
            requiredAction = BattleInputResolver.Resolve(battle, requiredInput);
            AcquireInputAction(requiredAction);
        }

        bool success = false;
        QTEFeedback feedback = QTEFeedback.Miss;

        if (qteCircle != null)
        {
            while (!qteCircle.IsResolved)
                yield return null;

            success = qteCircle.WasSuccessful;
            feedback = qteCircle.Feedback;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < holdDuration)
            {
                if (requiredAction != null && requiredAction.triggered)
                {
                    success = true;
                    feedback = QTEFeedback.Good;
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        ReleaseInputAction(requiredAction);

        Vector3 effectPosition = qteVisualGO != null ? qteVisualGO.transform.position : Vector3.zero;

        callback?.Invoke(success, feedback);

        if (showQteFeedback)
            ActionUIDisplayManager.Instance?.DisplayQTEResult(feedback);

        if (qteVisualGO == null)
        {
            GameObject effect = success ? successEffectPrefab : failEffectPrefab;
            if (effect != null && effectPosition != Vector3.zero)
                Instantiate(effect, effectPosition, Quaternion.identity, qteUIParent);
        }

        if (success)
            AudioManager.Instance?.PlaySound(successSFX);
        else
            AudioManager.Instance?.PlaySound(failSFX);

        // 🔺 Retour au temps normal uniquement si le temps a été modifié
        if (easyMode)
        {
            t = 0f;
            while (t < transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                float blend = t / transitionDuration;
                Time.timeScale = Mathf.Lerp(slowestTimeScale, normalTimeScale, blend);
                Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            Time.timeScale = normalTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }
        RegisterQteEnd();
    }

    /// <summary>
    /// Variante de QTE dédiée à la défense contre une attaque ennemie.
    /// Retourne Parade, Esquive ou Échec selon le timing de l'input.
    /// </summary>
    private IEnumerator WaitForDefenseQTE(System.Action<DefenseResult> callback)
    {
        if (!qteEnabled)
        {
            callback?.Invoke(DefenseResult.Miss);
            yield break;
        }
        RegisterQteStart();
        float windowScale = Mathf.Max(0.1f, defenseWindowScale);
        float parryWindow = 0.1f * windowScale;
        float dodgeWindow = 0.2f * windowScale;
        float parryRatio = dodgeWindow > 0f ? parryWindow / dodgeWindow : 0.5f;

        float slowestTimeScale = 0f;
        float transitionDuration = 0.1f;
        float normalTimeScale = 1f;

        // 🔻 Ralentissement progressif uniquement en mode facile
        float t = 0f;
        if (easyMode)
        {
            while (t < transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                float blend = t / transitionDuration;
                Time.timeScale = Mathf.Lerp(normalTimeScale, slowestTimeScale, blend);
                Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            // 🎯 Temps ralenti & instanciation du visuel
            Time.timeScale = slowestTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * slowestTimeScale;
        }

        parryWindow = dodgeWindow * parryRatio;

        GameObject qteVisualGO = null;
        QTECircle qteCircle = null;
        if (qteCirclePrefab != null)
        {
            Transform parent = ResolveQteUIParent();
            qteVisualGO = Instantiate(qteCirclePrefab, parent);
            qteVisualGO.transform.SetAsLastSibling();
            var visualRect = qteVisualGO.GetComponent<RectTransform>();
            FreeformLayoutGroup freeformLayout = parent != null ? parent.GetComponent<FreeformLayoutGroup>() : null;
            if (visualRect != null)
                freeformLayout?.RegisterRuntimeChild(visualRect);

            qteCircle = qteVisualGO.GetComponent<QTECircle>();
            if (qteCircle != null)
                qteCircle.Initialize(null, BattleInputType.Confirm, null, dodgeWindow);
        }

        InputAction confirm = null;
        if (InputsManager.Instance != null)
        {
            confirm = InputsManager.Instance.playerInputs.Battle.Confirm;
            AcquireInputAction(confirm);
        }

        DefenseResult result = DefenseResult.Miss;
        QTEFeedback feedback = QTEFeedback.Miss;
        if (qteCircle != null)
        {
            while (!qteCircle.IsResolved)
                yield return null;

            feedback = qteCircle.Feedback;
            result = feedback switch
            {
                QTEFeedback.Perfect => DefenseResult.Parry,
                QTEFeedback.Good => DefenseResult.Dodge,
                _ => DefenseResult.Miss
            };
        }
        else
        {
            float elapsed = 0f;
            bool pressed = false;
            while (elapsed < dodgeWindow)
            {
                elapsed += Time.unscaledDeltaTime;
                if (confirm != null && confirm.triggered)
                {
                    pressed = true;
                    break;
                }
                yield return null;
            }

            if (!pressed)
                result = DefenseResult.Miss;
            else if (elapsed <= parryWindow)
                result = DefenseResult.Parry;
            else
                result = DefenseResult.Dodge;

            feedback = result switch
            {
                DefenseResult.Parry => QTEFeedback.Perfect,
                DefenseResult.Dodge => QTEFeedback.Good,
                _ => QTEFeedback.Miss
            };
        }

        ReleaseInputAction(confirm);

        callback?.Invoke(result);

        if (showQteFeedback)
            ActionUIDisplayManager.Instance?.DisplayQTEResult(feedback);

        // 🔺 Retour au temps normal uniquement si le temps a été modifié
        if (easyMode)
        {
            t = 0f;
            while (t < transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                float blend = t / transitionDuration;
                Time.timeScale = Mathf.Lerp(slowestTimeScale, normalTimeScale, blend);
                Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            Time.timeScale = normalTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }
        RegisterQteEnd();
    }

    /// <summary>
    /// Exécute un MusicalMove sur toutes les cibles marquées par Link.
    /// </summary>
    public IEnumerator MusicalMoveOnMarkedTargets(MusicalMoveSO move, CharacterUnit caster)
    {
        var targets = NewBattleManager.Instance.activeCharacterUnits
            .Where(u => u.GetComponent<LinkMark>() != null && u.currentHP > 0 && u != caster)
            .OrderBy(u => Vector3.Distance(caster.transform.position, u.transform.position))
            .ToList();

        foreach (var target in targets)
        {
            yield return MusicalMoveRoutine(move, caster, target);
        }
    }


    private Transform FindTargetForMove(MusicalMoveSO move)
    {
        var first = NewBattleManager.Instance.currentTargetCharacter;
        return first != null ? first.transform : null;
    }

    private IEnumerator PlayTauntWithDelay(CharacterUnit caster, AudioClipSO clip, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        string speakerName = caster != null && caster.Data != null ? caster.Data.characterName : null;
        AudioManager.Instance?.PlayVoice(clip, speakerName);
    }

    /// <summary>
    /// Recherche récursivement un enfant portant un nom donné.
    /// Utilisée ici pour trouver "Camera_TargetedPoint" sur la cible suivie.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null) return null;

        foreach (Transform child in parent)
        {
            if (child.name == targetName)
                return child;

            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    #endregion
}

public enum DefenseResult { Parry, Dodge, Jump, Miss }
