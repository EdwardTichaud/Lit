using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Playables;
using UnityEngine.Timeline; // Gestion des TimelineAsset et bindings
using UnityEngine.InputSystem; // Nécessaire pour manipuler les InputAction du joueur
using UnityEngine.UI; // 🖼️ Gestion des éléments d'interface (fillAmount)
using System.Collections; // Requis pour l'utilisation des coroutines

public class TimelineManager : MonoBehaviour
{
    public static TimelineManager Instance { get; private set; }

    /// <summary>
    /// Référence de la Timeline en cours.
    /// </summary>
    private PlayableDirector currentDirector;

    /// <summary>
    /// PlayableDirector générique utilisé pour jouer les <see cref="TimelineAsset"/>.
    /// Autrefois géré par <c>TimelineLauncher</c>, il est maintenant centralisé ici pour
    /// simplifier la maintenance.
    /// </summary>
    [Header("Lecture de Timeline")]
    [SerializeField] private PlayableDirector reusableDirector;


    /// <summary>
    /// Indique si une Timeline est en train de jouer.
    /// </summary>
    public bool IsTimelinePlaying { get; private set; }

    /// <summary>
    /// Alias conservé pour l'ancien <c>TimelineLauncher</c>.
    /// </summary>
    public bool IsTimelineActive => IsTimelinePlaying;

    /// <summary>
    /// Durée nécessaire de maintien de l'input <c>Cancel</c> pour passer la Timeline.
    /// Configurable dans l'inspecteur pour s'adapter facilement aux besoins de gameplay.
    /// </summary>
    [SerializeField]
    private float skipHoldDuration = 3f;

    /// <summary>
    /// Référence locale vers l'action <c>Cancel</c> afin de pouvoir l'activer
    /// même lorsque toute la map <c>World</c> est désactivée.
    /// </summary>
    private InputAction cancelAction;

    /// <summary>
    /// Coroutine utilisée pour attendre le maintien continu de l'input Cancel.
    /// Si elle arrive à son terme, la Timeline est interrompue immédiatement.
    /// </summary>
    private Coroutine skipCoroutine;

    /// <summary>
    /// Image de l'interface affichant la progression du maintien de la touche.
    /// Doit pointer vers l'enfant "Frame" de "TimelineManagerCanvas".
    /// </summary>
    [SerializeField] private Image skipFillImage;

    /// <summary>
    /// Référence vers le Canvas dédié à l'affichage des informations de Timeline.
    /// Ce Canvas doit rester inactif lorsque aucune Timeline ne tourne ou qu'elle a été passée.
    /// </summary>
    [SerializeField] private GameObject timelineCanvas;

    /// <summary>
    /// Bouton permettant de passer la cinématique. Il est rendu visible uniquement
    /// lorsque qu'une Timeline est en cours de lecture afin d'éviter toute
    /// interaction inutile en dehors des cinématiques.
    /// </summary>
    [SerializeField] private GameObject passButton;

    /// <summary>
    /// Vrai si la timeline a été accélérée via maintien de Cancel.
    /// Permet d'éviter un double fondu au noir.
    /// </summary>
    private bool timelineSkipped = false;

    // Empêche un double traitement de fin de Timeline (arrêt + fallback).
    private bool endingTimeline = false;

    // Distingue une pause manuelle (QTE) d'une fin naturelle en mode Hold.
    private bool timelineManuallyPaused = false;

    /// <summary>
    /// Indique si la timeline en cours doit être entourée d'un fondu noir.
    /// Peut être désactivé pour certaines cinématiques courtes ou contextuelles.
    /// </summary>
    private bool useFade = true;

    /// <summary>
    /// Précise si la lecture d'une timeline doit interrompre la musique en cours.
    /// Lorsque <c>false</c>, la bande-son actuelle est conservée pendant la cinématique.
    /// </summary>
    private bool interruptMusic = true;

    /// <summary>
    /// Indique si la timeline en cours peut être passée par le joueur.
    /// Lorsque <c>false</c>, aucune option de passage n'est proposée.
    /// </summary>
    private bool allowSkip = true;

    /// <summary>
    /// Contrôle la restauration automatique de la caméra et des entrées
    /// à la fin d'une Timeline. Utile pour enchaîner plusieurs timelines
    /// (préparation/attaque/repli) sans repositionnement brusque de la caméra.
    /// </summary>
    private bool autoRestore = true;

    private int battlePauseToken = -1;

    private readonly Dictionary<string, GameObject> cameraTagCache = new();
    private bool overrideDirectorWrapMode;
    private DirectorWrapMode storedDirectorWrapMode;

    private const double k_TimelineEndTolerance = 0.05d;

    [Header("Audio")]
    [Tooltip("Musique de fond à jouer pendant les timelines.")]
    [SerializeField] private AudioClipSO timelineMusicClip;

    /// <summary>
    /// Active ou désactive l'ensemble des entrées de la map <c>World</c> pendant
    /// l'exécution d'une Timeline. Cela garantit qu'aucune action du joueur ne
    /// vient perturber une cinématique en cours.
    /// </summary>
    /// <param name="enable">True pour réactiver les inputs, false pour les bloquer.</param>
    private void ToggleWorldInputs(bool enable)
    {
        // Vérifie que l'InputsManager et la map World existent avant de manipuler les actions.
        if (InputsManager.Instance == null) return;

        // Récupère la map complète afin de pouvoir l'activer ou la désactiver en un seul appel.
        var worldMap = InputsManager.Instance.playerInputs.World;

        if (enable)
        {
            // Réactivation globale de toutes les actions World une fois la Timeline terminée.
            worldMap.Enable();
        }
        else
        {
            // Désactive immédiatement toutes les actions World pour empêcher toute interaction.
            worldMap.Disable();
        }
    }

    /// <summary>
    /// Active l'écoute de l'input <c>Cancel</c> pour permettre au joueur de passer
    /// la cinématique en maintenant le bouton pendant une durée définie.
    /// </summary>
    private void EnableTimelineSkip()
    {
        // Sécurise l'accès au singleton et à l'action Cancel
        if (InputsManager.Instance == null) return;

        cancelAction = InputsManager.Instance.playerInputs.World.Cancel;

        // On s'assure que seule l'action Cancel soit active : même si la map
        // World est désactivée, l'action individuelle peut être réactivée.
        cancelAction.started += OnCancelStarted;
        cancelAction.canceled += OnCancelCanceled;
        cancelAction.Enable();
    }

    /// <summary>
    /// Désactive l'écoute de l'input Cancel et arrête la coroutine éventuelle.
    /// Appelé à la fin de la Timeline ou si elle est stoppée prématurément.
    /// </summary>
    private void DisableTimelineSkip()
    {
        if (cancelAction == null) return;

        cancelAction.started -= OnCancelStarted;
        cancelAction.canceled -= OnCancelCanceled;
        cancelAction.Disable();
        cancelAction = null;

        if (skipCoroutine != null)
        {
            StopCoroutine(skipCoroutine);
            skipCoroutine = null;
        }
    }

    /// <summary>
    /// Déclenché lors de l'appui initial sur Cancel. Lance une coroutine
    /// qui attend la durée complète de maintien pour passer la Timeline.
    /// </summary>
    private void OnCancelStarted(InputAction.CallbackContext ctx)
    {
        // Si une attente était déjà en cours, on la relance proprement.
        if (skipCoroutine != null)
            StopCoroutine(skipCoroutine);

        skipCoroutine = StartCoroutine(SkipTimelineAfterHold());
    }

    /// <summary>
    /// Appelé lorsque l'utilisateur relâche l'input Cancel avant la fin de
    /// la durée requise. On annule donc le passage de la timeline.
    /// </summary>
    private void OnCancelCanceled(InputAction.CallbackContext ctx)
    {
        if (skipCoroutine != null)
        {
            StopCoroutine(skipCoroutine);
            skipCoroutine = null;
        }

        // Réinitialise la barre de progression si l'utilisateur relâche trop tôt
        if (skipFillImage != null)
        {
            skipFillImage.fillAmount = 0f;
            skipFillImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Coroutine qui attend <see cref="skipHoldDuration"/> secondes.
    /// Si l'attente se termine, la Timeline est accélérée afin de se terminer en
    /// une seule frame tout en laissant s'exécuter les événements restants.

    /// </summary>
    private IEnumerator SkipTimelineAfterHold()
    {
        float elapsed = 0f;

        // Affiche et réinitialise la barre de progression
        if (skipFillImage != null)
        {
            skipFillImage.fillAmount = 0f;
            skipFillImage.gameObject.SetActive(true);
        }

        // Incrémente le fillAmount pendant tout le maintien du bouton
        while (elapsed < skipHoldDuration)
        {
            // Utilise le temps non-scalé pour que la jauge de skip fonctionne même quand la timeline fige le jeu.
            elapsed += Time.unscaledDeltaTime;
            if (skipFillImage != null)
                skipFillImage.fillAmount = Mathf.Clamp01(elapsed / skipHoldDuration);
            yield return null;
        }

        // Une fois la durée atteinte, on délègue le passage de la timeline
        // à une routine dédiée pour pouvoir la réutiliser ailleurs.
        yield return ExecuteTimelineSkip();
    }

    /// <summary>
    /// Routine commune pour passer la timeline avec fondu au noir et
    /// accélération de la fin. Elle est appelée après le maintien de Cancel
    /// mais peut également être invoquée directement via <see cref="SkipCurrentTimeline"/>.
    /// </summary>
    private IEnumerator ExecuteTimelineSkip()
    {
        // Marque la timeline comme passée afin d'éviter un second fondu
        timelineSkipped = true;

        // Cache la jauge de progression si elle est affichée
        if (skipFillImage != null)
            skipFillImage.gameObject.SetActive(false);

        // Le Canvas n'a plus lieu d'être lorsque la timeline est passée
        if (timelineCanvas != null)
            timelineCanvas.SetActive(false);
        // Le bouton Passer est également désactivé immédiatement
        if (passButton != null)
            passButton.SetActive(false);

        // Ferme immédiatement tout dialogue encore affiché afin
        // d'éviter qu'il ne persiste après le passage de la Timeline.
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.ForceCloseDialogue();

        // Arrête d'écouter l'entrée Cancel pendant l'accélération
        skipCoroutine = null;
        DisableTimelineSkip();

        // Fondu au noir pour masquer la fin accélérée
        yield return FadeBlackRoutine(true);

        // Lance la lecture ultra-rapide de la Timeline plutôt qu'un arrêt brutal.
        // Cela garantit que tous les signaux et animations prévus soient exécutés.
        StartCoroutine(FastForwardTimeline());
    }

    /// <summary>
    /// Permet à d'autres scripts de forcer le passage de la timeline en cours
    /// tout en conservant le fondu au noir. Utile pour proposer un bouton de
    /// skip immédiat dans une UI, par exemple.
    /// </summary>
    public void SkipCurrentTimeline()
    {
        if (!IsTimelinePlaying)
            return; // Rien à passer

        // Si une attente de maintien était en cours, on la stoppe proprement
        if (skipCoroutine != null)
        {
            StopCoroutine(skipCoroutine);
            skipCoroutine = null;
        }

        // Lance la routine de passage
        StartCoroutine(ExecuteTimelineSkip());
    }

    /// <summary>
    /// Accélère fortement la Timeline courante pour qu'elle atteigne sa fin en un
    /// clin d'œil tout en jouant ses derniers événements.
    /// </summary>
    private IEnumerator FastForwardTimeline()
    {
        // On garde une référence locale car currentDirector peut être modifié ailleurs
        var director = currentDirector;
        if (director == null)
            yield break; // Aucun directeur, rien à accélérer

        // Récupère le Playable racine pour manipuler sa vitesse.
        var rootPlayable = director.playableGraph.GetRootPlayable(0);
        double originalSpeed = rootPlayable.GetSpeed();

        // Vitesse extrêmement élevée : la Timeline est parcourue quasi instantanément.
        // Même une longue cinématique est ainsi exécutée en une seule frame afin que
        // tous les signaux et animations prévus soient déclenchés malgré le passage.
        const double ultraFastSpeed = 1e6; // Valeur gigantesque mais finie pour éviter l'infini.
        rootPlayable.SetSpeed(ultraFastSpeed);

        // Attend que la Timeline signale sa fin sans provoquer d'exception
        // si le directeur est détruit entre-temps.
        while (director != null && director.state == PlayState.Playing)
            // Une seule itération suffit généralement : Unity stoppe la timeline à la frame suivante.
            yield return null;

        // Restaure la vitesse par sécurité pour les prochaines timelines
        // uniquement si le Playable existe toujours.
        if (rootPlayable.IsValid())
            rootPlayable.SetSpeed(originalSpeed);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;

            // Recherche ou création du PlayableDirector dédié à la lecture des TimelineAsset.
            if (reusableDirector == null)
            {
                // Tente de récupérer l'ancien objet "TimelineLauncher" s'il existe encore dans la scène.
                var launcherGO = GameObject.Find("TimelineLauncher");
                if (launcherGO != null)
                {
                    reusableDirector = launcherGO.GetComponent<PlayableDirector>();

                    // Rattache l'objet trouvé au TimelineManager pour centraliser la hiérarchie.
                    if (reusableDirector != null)
                        launcherGO.transform.SetParent(transform);
                }

                // Si aucune référence n'est trouvée, on en crée une nouvelle sur ce GameObject.
                if (reusableDirector == null)
                    reusableDirector = gameObject.AddComponent<PlayableDirector>();
            }

            // Recherche et désactivation du Canvas de gestion des timelines
            if (timelineCanvas == null)
            {
                timelineCanvas = GameObject.Find("TimelineManagerCanvas");
            }
            if (timelineCanvas != null)
            {
                // Le Canvas ne doit pas être visible tant qu'aucune Timeline n'est jouée
                timelineCanvas.SetActive(false);

                // Récupère le bouton "Passer" pour le contrôler dynamiquement
                if (passButton == null)
                {
                    var passTransform = timelineCanvas.transform.Find("Passer");
                    if (passTransform != null)
                        passButton = passTransform.gameObject;
                }
                if (passButton != null)
                    passButton.SetActive(false); // caché par défaut hors cinématique

                // Recherche automatique de l'image de remplissage si elle n'est pas assignée dans l'inspecteur
                if (skipFillImage == null)
                {
                    var frame = timelineCanvas.transform.Find("Passer/Frame");
                    if (frame != null)
                        skipFillImage = frame.GetComponent<Image>();
                }
            }

            // Initialise l'image pour éviter une barre visible au démarrage
            if (skipFillImage != null)
            {
                skipFillImage.fillAmount = 0f;
                skipFillImage.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (!IsTimelinePlaying || endingTimeline)
            return;

        if (currentDirector == null)
        {
            endingTimeline = true;
            StartCoroutine(HandleTimelineEnd(null));
            return;
        }

        if (currentDirector.state != PlayState.Playing && !timelineManuallyPaused)
        {
            double duration = currentDirector.duration;
            if (duration > 0d && currentDirector.time >= duration - k_TimelineEndTolerance)
            {
                endingTimeline = true;
                StartCoroutine(HandleTimelineEnd(currentDirector));
            }
        }
    }

    /// <summary>
    /// Assigne explicitement un <see cref="PlayableDirector"/> externe
    /// à utiliser pour la lecture des timelines.
    /// Utilisé notamment par le <c>BattleTimelineManager</c> pour
    /// centraliser la lecture des timelines de combat.
    /// </summary>
    /// <param name="externalDirector">Le PlayableDirector à utiliser.</param>
    public void SetExternalDirector(PlayableDirector externalDirector)
    {
        // Aucune vérification complexe : si null est fourni, la lecture
        // échouera proprement lors de l'appel à PlayTimeline.
        reusableDirector = externalDirector;
    }

    /// <summary>
    /// Modifie dynamiquement le comportement de restauration automatique
    /// de la caméra et des entrées à la fin de la Timeline en cours.
    /// Permet notamment au <see cref="BattleTimelineManager"/> de chaîner
    /// plusieurs timelines sans repositionnement intempestif.
    /// </summary>
    /// <param name="restore">True pour restaurer, False pour désactiver la restauration.</param>
    public void SetAutoRestore(bool restore)
    {
        autoRestore = restore;
    }

    private GameObject ResolveCameraByTag(string cameraTag)
    {
        if (string.IsNullOrEmpty(cameraTag))
            return null;

        if (cameraTagCache.TryGetValue(cameraTag, out GameObject cached) && cached != null)
            return cached;

        GameObject found = GameObject.FindGameObjectWithTag(cameraTag);
        if (found != null)
            cameraTagCache[cameraTag] = found;

        return found;
    }

    /// <summary>
    /// Joue une nouvelle Timeline. Arrête proprement la précédente.
    /// </summary>
    /// <param name="withFade">
    ///     True pour entourer la timeline d'un fondu noir,
    ///     False pour la jouer directement sans transition.
    /// </param>
    /// <param name="interruptMusic">True pour couper la musique en cours, false pour la laisser jouer.</param>
    /// <param name="allowSkip">False pour empêcher l'utilisateur de passer la timeline.</param>
    public void PlayTimeline(PlayableDirector newDirector, bool withFade = true, bool interruptMusic = true, bool allowSkip = true, bool autoRestore = true, bool requiresWorldCamera = true)
    {
        if (newDirector == null)
        {
            Debug.LogWarning("[TimelineManager] PlayTimeline appelé avec null !");
            return;
        }

        // Arrête la timeline en cours si elle est différente
        if (currentDirector != null && currentDirector != newDirector && currentDirector.state == PlayState.Playing)
        {
            Debug.Log("[TimelineManager] Arrêt de la Timeline en cours avant de jouer la nouvelle.");
            currentDirector.Stop();
        }

        // Abonnement aux events
        newDirector.played -= OnPlayed;
        newDirector.stopped -= OnStopped;
        newDirector.played += OnPlayed;
        newDirector.stopped += OnStopped;

        currentDirector = newDirector;
        endingTimeline = false;
        timelineManuallyPaused = false;

        if (requiresWorldCamera)
        {
            RequestWorldCameraControl();
        }
        overrideDirectorWrapMode = false;
        if (requiresWorldCamera)
        {
            storedDirectorWrapMode = newDirector.extrapolationMode;
            newDirector.extrapolationMode = DirectorWrapMode.None;
            overrideDirectorWrapMode = true;
        }

        // Enregistre les préférences de lecture pour cette timeline.
        useFade = withFade;
        this.interruptMusic = interruptMusic;
        this.allowSkip = allowSkip; // Mémorise si le joueur peut passer la cinématique
        this.autoRestore = autoRestore; // Indique si la caméra/inputs sont restaurés en fin de timeline
        EnsureCameraBinding(newDirector);
        currentDirector.Play();
    }

    /// <summary>
    /// Joue un <see cref="TimelineAsset"/> en effectuant dynamiquement les bindings
    /// nécessaires. Cette logique provenait initialement de <c>TimelineLauncher</c>.
    /// </summary>
    /// <param name="timelineAsset">Timeline à jouer.</param>
    /// <param name="caster">GameObject jouant la timeline (binding des tracks "Caster" ou "PNJ").</param>
    /// <param name="cameraTag">Tag de la caméra à animer. Peut être null pour n'animer que le caster.</param>
    /// <param name="withFade">True pour jouer la timeline avec fondu, false pour la jouer instantanément.</param>
    /// <param name="interruptMusic">True pour couper ou remplacer la musique actuelle, false pour la conserver.</param>
    /// <param name="allowSkip">False pour interdire au joueur de passer la timeline.</param>
    /// <param name="autoRestore">True pour restaurer automatiquement la caméra et les entrées à la fin.</param>
    /// <param name="fixedRotation">Rotation imposée pour la caméra. Lorsque renseignée, elle remplace la rotation
    /// du caster et reste utilisée pour toutes les timelines successives d'un même move.</param>
    public void PlayTimeline(
        TimelineAsset timelineAsset,
        GameObject caster,
        string cameraTag,
        bool withFade = true,
        bool interruptMusic = true,
        bool allowSkip = true,
        bool autoRestore = true,
        Quaternion? fixedRotation = null,
        GameObject cameraTarget = null)
    {
        if (timelineAsset == null || reusableDirector == null)
        {
            Debug.LogError("[TimelineManager] TimelineAsset ou PlayableDirector manquant !");
            return;
        }

        // Associe l'asset au PlayableDirector générique
        reusableDirector.playableAsset = timelineAsset;

        GameObject cameraGO = null;
        Transform cameraParent = null;   // Parent direct de la caméra pour récupérer son Animator
        bool requiresWorldCamera = !string.IsNullOrEmpty(cameraTag) && cameraTag.Equals("WorldCamera", System.StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(cameraTag))
        {
            cameraGO = ResolveCameraByTag(cameraTag);
            cameraParent = cameraGO != null ? cameraGO.transform.parent : null;

            if (requiresWorldCamera)
            {
                // Sauvegarde la position actuelle de la WorldCamera avant que la Timeline ne la déplace
                CameraController.Instance?.SaveWorldCameraTransform();
            }

            // 📌 Positionne l'origine de la caméra uniquement sur la cible fournie.
            // Aucun point spécifique n'est recherché : on se contente du GameObject de la CharacterUnit.
            if (cameraTarget != null && cameraParent != null)
            {
                cameraParent.position = cameraTarget.transform.position;   // 🡆 BattleCamera_Origin sur la cible
                cameraParent.rotation = fixedRotation ?? cameraTarget.transform.rotation;
            }
            else if (caster != null && cameraParent != null)
            {
                // 🕴️ Fallback : si aucune cible n'est renseignée, on se cale sur le lanceur.
                cameraParent.position = caster.transform.position;
                cameraParent.rotation = fixedRotation ?? caster.transform.rotation;
            }
        }

        foreach (var output in timelineAsset.outputs)
        {
            string trackName = output.streamName;
            string lower = trackName.ToLower();
            System.Type type = output.outputTargetType;

            // Gestion des pistes liées au lanceur (Caster ou PNJ)
            if (lower.Contains("caster") || lower.Contains("pnj"))
            {
                if (caster != null)
                {
                    BindObjectToTrack(output, caster);
                }
                else
                {
                    // Aucune référence valide : on ignore cette piste
                    Debug.LogWarning($"[TimelineManager] Caster introuvable pour la track : {trackName}, piste ignorée.");
                }
            }
            // Gestion des pistes liées à la caméra
            else if (lower.Contains("camera"))
            {
                if (cameraGO != null)
                {
                    // L'animation de la caméra doit utiliser l'Animator situé sur le parent de la WorldCamera.
                    Animator camAnimator = cameraParent != null
                        ? cameraParent.GetComponent<Animator>()
                        : cameraGO.GetComponent<Animator>();

                    if (camAnimator != null)
                    {
                        reusableDirector.SetGenericBinding(output.sourceObject, camAnimator);
                    }
                    else
                    {
                        Debug.LogWarning($"[TimelineManager] Animator manquant pour la caméra {cameraTag}");
                    }
                }
                else
                {
                    // Aucune caméra correspondante : on ignore la piste
                    Debug.LogWarning($"[TimelineManager] Caméra introuvable pour la track : {trackName}, piste ignorée.");
                }
            }
            // Gestion des receveurs de signaux
            else if (type != null && typeof(Component).IsAssignableFrom(type) && type.Name.Contains("SignalReceiver"))
            {
                // Récupère le SignalReceiver présent sur le même GameObject que le PlayableDirector
                Component receiver = reusableDirector.GetComponent(type);
                if (receiver != null)
                {
                    reusableDirector.SetGenericBinding(output.sourceObject, receiver);
                }
                else
                {
                    Debug.LogWarning($"[TimelineManager] {type.Name} manquant sur {reusableDirector.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[TimelineManager] Aucun binding pour la track : {trackName}");
            }
        }

        // Binding des signaux placés directement sur la timeline sans track dédiée
        SignalReceiver markerReceiver = reusableDirector.GetComponent<SignalReceiver>();
        if (markerReceiver != null && timelineAsset.markerTrack != null)
        {
            reusableDirector.SetGenericBinding(timelineAsset.markerTrack, markerReceiver);
        }

        // Joue la timeline en profitant de toute la gestion centralisée (skip, fondu, musique...)
        // La caméra est positionnée une seule fois ; aucun suivi continu n'est effectué.
        PlayTimeline(reusableDirector, withFade, interruptMusic, allowSkip, autoRestore, requiresWorldCamera);
    }

    /// <summary>
    /// Joue une timeline en ciblant automatiquement le PNJ actuellement en interaction.
    /// </summary>
    /// <param name="timelineAsset">Timeline à jouer sur le PNJ courant.</param>
    /// <param name="withFade">True pour inclure un fondu, false pour une transition immédiate.</param>
    /// <param name="interruptMusic">True pour interrompre la musique en cours, false pour la conserver.</param>
    /// <param name="allowSkip">False pour interdire au joueur de passer la cinématique.</param>
    public void PlayTimelineOnCurrentNPC(TimelineAsset timelineAsset, bool withFade = true, bool interruptMusic = true, bool allowSkip = true)
    {
        // Récupère le PNJ en cours d'interaction via l'InteractionManager.
        GameObject npc = InteractionManager.Instance != null ? InteractionManager.Instance.currentInteractable : null;

        if (npc == null)
        {
            // Avertit si aucun PNJ n'est en interaction lorsque la méthode est appelée.
            Debug.LogWarning("[TimelineManager] Aucun PNJ courant pour jouer la timeline.");
            return;
        }

        // Utilise la WorldCamera : la track "Camera" ira chercher l'Animator du parent de la WorldCamera.
        PlayTimeline(timelineAsset, npc, "WorldCamera", withFade, interruptMusic, allowSkip);
    }

    /// <summary>
    /// Lie dynamiquement un GameObject à une piste de Timeline.
    /// </summary>
    private void BindObjectToTrack(PlayableBinding output, GameObject go)
    {
        if (output.outputTargetType == typeof(Animator))
        {
            Animator animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
                reusableDirector.SetGenericBinding(output.sourceObject, animator);
            else
                Debug.LogWarning($"[TimelineManager] Animator manquant sur {go.name}");
        }
        else
        {
            reusableDirector.SetGenericBinding(output.sourceObject, go);
        }
    }

    /// <summary>
    /// Arrête immédiatement la timeline en cours si elle joue encore.
    /// </summary>
    public void StopTimeline()
    {
        if (currentDirector != null &&
            (currentDirector.state == PlayState.Playing || currentDirector.state == PlayState.Paused))
        {
            currentDirector.Stop();
        }
    }

    /// <summary>
    /// Arrête explicitement la Timeline en cours.
    /// </summary>
    public void StopCurrentTimeline()
    {
        if (currentDirector != null && currentDirector.state == PlayState.Playing)
        {
            currentDirector.Stop();
        }
    }

    /// <summary>
    /// Met en pause la Timeline en cours (appelé par un Signal).
    /// </summary>
    public void PauseCurrentTimeline()
    {
        if (currentDirector == null) return;
        if (currentDirector.state != PlayState.Playing) return;

        currentDirector.Pause();
        // Stabilise l'état visuel/audio au frame exact de pause.
        currentDirector.Evaluate();
        timelineManuallyPaused = true;

        // On reste en mode "cinématique" pendant la pause :
        // => on NE réactive PAS les déplacements ici.
        // (si tu veux autoriser un prompt/QTE, gère-le dans ton UI/Input dédié)
        Debug.Log("[TimelineManager] PauseCurrentTimeline()");
    }

    /// <summary>
    /// Reprend la Timeline en cours (appelé par une action joueur / QTE / autre).
    /// </summary>
    public void ResumeCurrentTimeline()
    {
        if (currentDirector == null) return;
        if (currentDirector.state != PlayState.Paused) return;

        currentDirector.Resume();
        timelineManuallyPaused = false;
        Debug.Log("[TimelineManager] ResumeCurrentTimeline()");
    }

    /// <summary>
    /// Callback quand une Timeline démarre.
    /// </summary>
    private void OnPlayed(PlayableDirector pd)
    {
        IsTimelinePlaying = true;
        RequestBattlePauseIfNeeded();
        // Bloque immédiatement toutes les actions "World" pendant l'exécution de la timeline.
        ToggleWorldInputs(false);
        // Active les options de passage uniquement si autorisé
        if (allowSkip)
        {
            // Réactive uniquement l'action Cancel pour permettre un éventuel passage de la Timeline.
            EnableTimelineSkip();
            // Affiche le Canvas de gestion des timelines pendant la lecture
            if (timelineCanvas != null)
                timelineCanvas.SetActive(true);
            // Active le bouton Passer seulement pendant une Timeline
            if (passButton != null)
                passButton.SetActive(true);
        }
        else
        {
            // S'assure que les éléments de skip restent cachés lorsque l'option est désactivée
            DisableTimelineSkip();
            if (timelineCanvas != null)
                timelineCanvas.SetActive(false);
            if (passButton != null)
                passButton.SetActive(false);
        }
        // Met le CameraController en pause pour que seule la Timeline anime la caméra.
        if (CameraController.Instance != null)
            CameraController.Instance.PauseController();
        Debug.Log($"[TimelineManager] Timeline jouée : {pd.name}");

        // Bascule la musique vers le thème de timeline si nécessaire.
        // Si l'option est désactivée, on conserve la musique de fond actuelle.
        if (interruptMusic && AudioManager.Instance != null)
            AudioManager.Instance.TransitionToTimeline(timelineMusicClip);

        // Lance un fondu noir pour encadrer le début de la timeline si demandé.
        if (useFade)
            StartCoroutine(FadeBlackRoutine());
    }

    /// <summary>
    /// Callback quand une Timeline s'arrête.
    /// </summary>
    private void OnStopped(PlayableDirector pd)
    {
        if (currentDirector == pd)
        {
            // On délègue la fin de timeline à une coroutine afin de chaîner le fondu
            if (endingTimeline)
                return;

            endingTimeline = true;
            StartCoroutine(HandleTimelineEnd(pd));
        }
    }

    /// <summary>
    /// Gère la séquence de fin de timeline avec fondu noir, masquage des repositionnements
    /// puis restitution progressive des commandes au joueur.
    /// </summary>
    private IEnumerator HandleTimelineEnd(PlayableDirector pd)
    {
        string directorName = pd != null ? pd.name : "Unknown";
        Debug.Log($"[TimelineManager] Timeline stoppée : {directorName}");
        // On n'a plus besoin d'écouter l'input Cancel une fois la cinématique terminée.
        DisableTimelineSkip();
        allowSkip = true; // Réinitialise l'autorisation de passage pour les prochaines timelines

        // Masque le Canvas car la timeline est terminée
        if (timelineCanvas != null)
            timelineCanvas.SetActive(false);
        // Cache aussi le bouton Passer pour les prochaines cinématiques
        if (passButton != null)
            passButton.SetActive(false);

        // S'assure qu'aucun dialogue résiduel ne reste affiché à la fin
        // d'une Timeline, qu'elle ait été jouée jusqu'au bout ou passée.
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.ForceCloseDialogue();

        // 1) Fondu vers le noir pour cacher le "snap" de fin de Timeline.
        //    Si la timeline a déjà été accélérée, l'écran est noir : on évite un second fondu.
        if (!timelineSkipped)
        {
            // N'exécute le fondu que si l'option est active.
            if (useFade)
                yield return FadeBlackRoutine(true);
        }
        else
        {
            timelineSkipped = false; // Réinitialisation pour la prochaine timeline
        }

        // Restaure l'ancienne musique maintenant que la timeline est terminée.
        // Ne s'applique que si la timeline avait interrompu la bande-son précédente.
        if (interruptMusic && AudioManager.Instance != null)
            AudioManager.Instance.ReturnFromTimeline();

        // 2) La cinématique est terminée.
        IsTimelinePlaying = false;
        currentDirector = null;
        timelineManuallyPaused = false;

        if (overrideDirectorWrapMode && pd != null)
        {
            pd.extrapolationMode = storedDirectorWrapMode;
            overrideDirectorWrapMode = false;
        }

        // Restauration optionnelle : certains enchaînements de timelines doivent
        // conserver le contrôle caméra pour éviter des transitions brutales.
        if (autoRestore)
        {
            // On redonne le contrôle de Lucian en réactivant les inputs "World".
            ToggleWorldInputs(true);

            // Réactivation du CameraController pour rendre la main après la cinématique
            if (CameraController.Instance != null)
            {
                CameraController.Instance.ResumeController();
                // ↩️ Replace la WorldCamera à sa dernière position sauvegardée pour garder la continuité
                CameraController.Instance.RestoreWorldCameraTransform();
            }
        }

        // 4) Une fois tout rétabli en arrière-plan, on peut revenir progressivement à l'image.
        if (useFade)
            yield return FadeFromBlackRoutine();

        // 5) Vérification finale : on s'assure qu'aucun filtre noir ou blanc ne reste visible.
        //    Cette étape évite qu'une opacité résiduelle persiste après la timeline.
        var fader = FadeChildrenOpacity.Instance;
        if (fader != null)
        {
            // Indice 0 = BlackScreen, indice 1 = WhiteScreen
            fader.EnsureTransparency(0, 0.5f);
            fader.EnsureTransparency(1, 0.5f);
        }

        // Réinitialise le comportement de restauration pour les prochaines timelines
        autoRestore = true;

        ReleaseBattlePauseIfNeeded();
        ReleaseWorldCameraControl();
        endingTimeline = false;
    }

    private void RequestBattlePauseIfNeeded()
    {
        if (battlePauseToken >= 0)
            return;

        NewBattleManager battleManager = NewBattleManager.Instance;
        if (battleManager == null || !battleManager.IsBattleActive)
            return;

        battlePauseToken = battleManager.RequestBattlePause("TimelineManager");
    }

    private void ReleaseBattlePauseIfNeeded()
    {
        if (battlePauseToken < 0)
            return;

        NewBattleManager battleManager = NewBattleManager.Instance;
        if (battleManager != null)
            battleManager.ReleaseBattlePause(battlePauseToken);

        battlePauseToken = -1;
    }

    private bool worldCameraControlRequested;
    private Transform storedPlayerTransform;
    private Quaternion storedPlayerRotation;
    private bool hasStoredPlayerRotation;
    private Transform storedPlayerModelTransform;
    private Vector3 storedPlayerModelLocalPosition;
    private Quaternion storedPlayerModelLocalRotation;
    private bool hasStoredPlayerModelPose;

    private void RequestWorldCameraControl()
    {
        if (worldCameraControlRequested)
            return;

        CameraActivationManager.Instance?.BeginWorldCameraTimelineOverride();
        worldCameraControlRequested = true;
        StorePlayerRotation();
    }

    private void ReleaseWorldCameraControl()
    {
        if (!worldCameraControlRequested)
            return;

        CameraActivationManager.Instance?.EndWorldCameraTimelineOverride();
        worldCameraControlRequested = false;
        RestorePlayerRotation();
    }

    /// <summary>
    /// Garantit qu'une track caméra non bindée utilise au moins l'Animator de la WorldCamera.
    /// Évite les timelines jouées sans contrôle caméra effectif (ex : WakeUp!).
    /// </summary>
    private void EnsureCameraBinding(PlayableDirector director)
    {
        if (director == null)
            return;

        var timeline = director.playableAsset as TimelineAsset;
        if (timeline == null)
            return;

        var worldCam = GameObject.FindGameObjectWithTag("WorldCamera");
        Animator camAnimator = worldCam != null
            ? (worldCam.transform.parent != null
                ? worldCam.transform.parent.GetComponent<Animator>()
                : worldCam.GetComponent<Animator>())
            : null;

        if (camAnimator == null)
            return;

        foreach (var output in timeline.outputs)
        {
            string name = output.streamName ?? string.Empty;
            var targetType = output.outputTargetType;
            bool looksLikeCameraTrack = name.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0
                                        || (targetType != null && targetType.Name.IndexOf("CinemachineBrain", StringComparison.OrdinalIgnoreCase) >= 0);

            if (!looksLikeCameraTrack)
                continue;

            if (director.GetGenericBinding(output.sourceObject) != null)
                continue;

            director.SetGenericBinding(output.sourceObject, camAnimator);
        }
    }

    private void StorePlayerRotation()
    {
        if (hasStoredPlayerRotation)
            return;

        var player = FindFirstObjectByType<ThirdPersonPlayerController>();
        if (player == null)
            return;

        storedPlayerTransform = player.transform;
        storedPlayerRotation = storedPlayerTransform.rotation;
        hasStoredPlayerRotation = true;

        if (!hasStoredPlayerModelPose)
        {
            Transform modelRoot = FindPlayerModelRoot(storedPlayerTransform);

            if (modelRoot == null)
            {
                var playerAnimator = player.GetComponentInChildren<Animator>();
                if (playerAnimator != null && playerAnimator.transform != storedPlayerTransform)
                    modelRoot = playerAnimator.transform;
            }

            if (modelRoot != null)
            {
                storedPlayerModelTransform = modelRoot;
                storedPlayerModelLocalPosition = storedPlayerModelTransform.localPosition;
                storedPlayerModelLocalRotation = storedPlayerModelTransform.localRotation;
                hasStoredPlayerModelPose = true;
            }
        }
    }

    private void RestorePlayerRotation()
    {
        if (!hasStoredPlayerRotation)
            return;

        if (storedPlayerTransform != null)
            storedPlayerTransform.rotation = storedPlayerRotation;

        storedPlayerTransform = null;
        hasStoredPlayerRotation = false;

        if (hasStoredPlayerModelPose && storedPlayerModelTransform != null)
        {
            storedPlayerModelTransform.localPosition = storedPlayerModelLocalPosition;
            storedPlayerModelTransform.localRotation = storedPlayerModelLocalRotation;
        }

        storedPlayerModelTransform = null;
        hasStoredPlayerModelPose = false;
    }

    private Transform FindPlayerModelRoot(Transform playerRoot)
    {
        if (playerRoot == null)
            return null;

        var skinnedMeshes = playerRoot.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
        foreach (var skinned in skinnedMeshes)
        {
            if (skinned == null)
                continue;

            Transform candidate = skinned.transform;
            while (candidate != null && candidate.parent != playerRoot)
                candidate = candidate.parent;

            if (candidate != null && candidate != playerRoot)
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Sequence de fondu : transparent -> noir (1s), maintien (1s) puis optionnellement noir -> transparent (1s).
    /// Utilise le <see cref="FadeChildrenOpacity"/> dont l'enfant 0 doit être un panneau noir couvrant l'écran.
    /// </summary>
    /// <param name="stayBlack">
    ///     True pour rester sur un écran noir à la fin du fondu (utile en fin de timeline),
    ///     False pour revenir à l'image en fin de séquence.
    /// </param>
    private IEnumerator FadeBlackRoutine(bool stayBlack = false)
    {
        var fader = FadeChildrenOpacity.Instance;
        if (fader == null)
            yield break; // Aucun fader disponible, on quitte silencieusement

        // 1) transparent vers noir
        fader.ChangeOpacity(0, 1f, 1f);
        yield return new WaitForSeconds(1f);

        // 2) pause d'une seconde à opacité maximale
        yield return new WaitForSeconds(1f);

        // 3) noir vers transparent (sauf si on veut rester en noir)
        if (!stayBlack)
        {
            fader.ChangeOpacity(0, 0f, 1f);
            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// Fait revenir l'écran du noir vers la transparence en 1 seconde.
    /// Utilisé après <see cref="FadeBlackRoutine(bool)"/> lorsque <c>stayBlack</c> est vrai.
    /// </summary>
    private IEnumerator FadeFromBlackRoutine()
    {
        var fader = FadeChildrenOpacity.Instance;
        if (fader == null)
            yield break;

        fader.ChangeOpacity(0, 0f, 1f);
        yield return new WaitForSeconds(1f);
    }
}
