using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Nécessaire pour utiliser les actions d'input
using UnityEngine.Playables;
using Unity.Cinemachine; // Pour gérer les caméras Cinemachine

public class BattleTransitionManager : MonoBehaviour
{
    public static BattleTransitionManager Instance { get; private set; }

    private PlayerDetection playerDetection;

    /// <summary>
    /// Identifiant shader mis en cache pour remettre à zéro les dissolves appliqués via MaterialPropertyBlock.
    /// </summary>
    private static readonly int DissolveStrengthId = Shader.PropertyToID("_DissolveStrength");

    [Header("Ressources Visuals")]
    [SerializeField] private Image worldFadeOverlay;
    [SerializeField] private ParticleSystem maskRingParticles;

    [Header("SFX")]
    [SerializeField] private List<AudioClipSO> transitionSFXClips = new();
    [SerializeField] private AudioClipSO brokenGlassSound;
    [SerializeField] private AudioClipSO versusThunder;

    [Header("Music")]
    /// <summary>
    /// Référence de la coroutine jouant les sons de transition. Permet de l'interrompre
    /// proprement lors d'un nouveau combat ou d'une annulation.
    /// </summary>
    private Coroutine transitionAudioCoroutine;

    [SerializeField] private GameObject battleCamera;
    [SerializeField] private Camera battleCameraCam;
    [SerializeField] private GameObject battleSceneTransitionCanvas;
    [SerializeField] private Transform enemyPosition01;
    [SerializeField] private GameObject playerRoot;
    private Transform battleSceneTransitionPanel;
    private CharacterController cachedPlayerController;

    /// <summary>
    /// Indique si une sortie de combat est déjà en cours. Ce garde-fou évite
    /// d'empiler plusieurs coroutines ExitVictoryScreenAndBattle lorsque le
    /// joueur matraque la touche de validation pendant l'écran de victoire.
    /// </summary>
    private bool exitBattleRoutineRunning = false;
    private Coroutine dissolveResetRoutine = null;

    [Header("Scenes")]
    [SerializeField] private GameObject worldScene; // GameObject racine de la scène du monde à désactiver pendant les combats

    [Header("UI")]
    [SerializeField] private GameObject qteCircle;
    [SerializeField] private GameObject battleTimeline;
    [SerializeField] private GameObject passTurnButton;
    [SerializeField] private GameObject actionDisplayPanel;

    [Header("Versus Screen")]
    [SerializeField] private GameObject versusCameraCanvas;
    [SerializeField] private GameObject brokenGlass;
    [SerializeField] private GameObject versusTransition; // Transition du Versus
    [SerializeField] private Image SU1; // Portrait du premier allié
    [SerializeField] private Image SU2; // Portrait du second allié
    [SerializeField] private Image SU3; // Portrait du troisième allié
    [SerializeField] private Image EU1; // Portrait du premier ennemi
    [SerializeField] private Image EU2; // Portrait du second ennemi
    [SerializeField] private Image EU3; // Portrait du troisième ennemi
    [SerializeField] private Animator unitSpawnPointsAnimator; // Animator jouant l'animation Versus
    [SerializeField] private List<GameObject> continuePrompts = new(); // Éléments "Continuer" à afficher après chargement

    private bool battleUIShown = false; // Suivi de l'affichage initial de l'UI
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject gameOverScreen;

    // Mise en cache du rendu Versus pour l'effet de verre brisé
    [SerializeField] private Camera versusCamera;
    private RenderTexture versusRenderTexture;
    private RenderTexture frozenVersusFrame;
    private Renderer[] brokenGlassRenderers = Array.Empty<Renderer>();
    private RawImage[] brokenGlassRawImages = Array.Empty<RawImage>();

    /// <summary>
    /// Cache les éléments d'interface de combat au début de la transition.
    /// </summary>
    public void HideBattleUI()
    {
        // Désactive le cercle de QTE s'il est présent pour éviter son apparition précoce
        if (qteCircle != null)
            qteCircle.SetActive(false);

        if (battleTimeline != null)
            battleTimeline.SetActive(false);
        if (passTurnButton != null)
            passTurnButton.SetActive(false);
        if (actionDisplayPanel != null)
            actionDisplayPanel.SetActive(false);

        battleUIShown = false;
    }

    /// <summary>
    /// Indique si l'interface de combat est actuellement visible.
    /// </summary>
    public bool IsBattleUiVisible => battleUIShown;

    /// <summary>
    /// Affiche les éléments d'interface lorsque le joueur commence son premier tour.
    /// </summary>
    public void ShowBattleUIIfNeeded()
    {
        if (battleUIShown)
            return;

        if (qteCircle != null)
            qteCircle.SetActive(true);

        if (battleTimeline != null)
            battleTimeline.SetActive(true);
        if (passTurnButton != null)
            passTurnButton.SetActive(true);
        if (actionDisplayPanel != null)
            actionDisplayPanel.SetActive(true);

        battleUIShown = true;
    }

    /// <summary>
    /// Met à jour l'écran de Versus avec les portraits des unités alliées et ennemies.
    /// Les emplacements vides deviennent totalement transparents.
    /// </summary>
    private void UpdateVersusPortraits()
    {
        // Récupération des listes d'unités
        var squad = SquadManager.Instance != null ? SquadManager.Instance.SquadCharacters : new List<CharacterData>();
        var enemies = NewBattleManager.Instance != null ? NewBattleManager.Instance.enemyTemplates : new List<CharacterData>();

        // Tableau des images alliées et traitement
        Image[] squadImages = { SU1, SU2, SU3 };
        for (int i = 0; i < squadImages.Length; i++)
        {
            if (i < squad.Count && squad[i] != null)
            {
                // Récupère la liste des sprites de Versus définie dans le CharacterData
                List<Sprite> sprites = squad[i].versusSprites;
                if (sprites != null && sprites.Count > 0)
                {
                    // Choisit un sprite aléatoire parmi la liste fournie
                    squadImages[i].sprite = sprites[UnityEngine.Random.Range(0, sprites.Count)];
                    // Assure une opacité totale pour le sprite sélectionné
                    Color c = squadImages[i].color; c.a = 1f; squadImages[i].color = c;
                }
                else
                {
                    // Liste nulle ou vide : on rend l'image totalement transparente
                    // pour éviter d'afficher un portrait par défaut ou un sprite incorrect
                    squadImages[i].sprite = null;
                    Color c = squadImages[i].color; c.a = 0f; squadImages[i].color = c;
                }
            }
            else
            {
                // Aucune unité : image totalement transparente
                squadImages[i].sprite = null;
                Color c = squadImages[i].color; c.a = 0f; squadImages[i].color = c;
            }
        }

        // Tableau des images ennemies et traitement identique
        Image[] enemyImages = { EU1, EU2, EU3 };
        for (int i = 0; i < enemyImages.Length; i++)
        {
            if (i < enemies.Count && enemies[i] != null)
            {
                List<Sprite> sprites = enemies[i].versusSprites;
                if (sprites != null && sprites.Count > 0)
                {
                    // Sélection d'un sprite aléatoire pour l'ennemi courant
                    enemyImages[i].sprite = sprites[UnityEngine.Random.Range(0, sprites.Count)];
                    // Opacité maximale pour garantir la visibilité du portrait
                    Color c = enemyImages[i].color; c.a = 1f; enemyImages[i].color = c;
                }
                else
                {
                    // Liste nulle ou vide : le portrait doit être complètement transparent
                    enemyImages[i].sprite = null;
                    Color c = enemyImages[i].color; c.a = 0f; enemyImages[i].color = c;
                }
            }
            else
            {
                enemyImages[i].sprite = null;
                Color c = enemyImages[i].color; c.a = 0f; enemyImages[i].color = c;
            }
        }
    }

    #region Initialisation
    /// <summary>
    /// Prépare les références globales du gestionnaire de transition.
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        worldFadeOverlay ??= GameObject.Find("WorldFadeOverlayPanel")?.GetComponent<Image>();
        playerDetection ??= FindFirstObjectByType<PlayerDetection>();
        ResolveBattleCamera();
        ResolveVersusCamera();
        ResolveTransitionCanvas();
        ResolveBattleCameraCam();
        versusRenderTexture = versusCamera != null ? versusCamera.targetTexture : null;
        if (brokenGlass != null)
        {
            brokenGlassRenderers = brokenGlass.GetComponentsInChildren<Renderer>(true);
            brokenGlassRawImages = brokenGlass.GetComponentsInChildren<RawImage>(true);
        }

        // Tente de récupérer automatiquement le GameObject "WorldScene" si l'on n'a rien assigné dans l'inspecteur
        worldScene ??= GameObject.Find("WorldScene");

        // Recherche automatique des éléments "Continuer" si aucun n'est assigné
        if (continuePrompts == null || continuePrompts.Count == 0)
        {
            continuePrompts = new List<GameObject>();
            if (versusTransition != null)
            {
                // Parcourt tous les enfants du Versus pour trouver ceux contenant "Continuer"
                foreach (Transform t in versusTransition.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Contains("Continuer"))
                        continuePrompts.Add(t.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// S'assure que l'interface de combat est masquée au chargement de la scène.
    /// </summary>
    private void Start()
    {
        // On cache toutes les UI liées au combat tant que le joueur n'a pas débuté son premier tour.
        HideBattleUI();
    }

    #endregion

    #region Transition
    /// <summary>
    /// Prépare et lance un combat initié depuis une Timeline en spécifiant
    /// explicitement les ennemis à affronter via un ScriptableObject.
    /// Cette surcharge facilite l'appel depuis une Timeline qui ne peut
    /// transmettre qu'un seul paramètre dans ses signaux.
    /// </summary>
    /// <param name="config">ScriptableObject contenant les ennemis et les timelines associées.</param>
    public void StartTimelineBattle(TimelineBattleConfigSO config)
    {
        // ✅ Dès qu'une timeline demande un combat, on arrête immédiatement
        // la timeline en cours pour éviter tout chevauchement d'actions.
        TimelineManager.Instance?.StopTimeline(); // Met fin à la cinématique en cours

        if (config == null)
        {
            Debug.LogWarning("[BattleTransitionManager] TimelineBattleConfigSO non fourni pour StartTimelineBattle.");
            return;
        }

        // ------------------------------------------------------------------
        // 1) Rassemble les ennemis définis dans le ScriptableObject
        // ------------------------------------------------------------------
        List<CharacterData> enemies = new();
        if (config.enemy1 != null) enemies.Add(config.enemy1);
        if (config.enemy2 != null) enemies.Add(config.enemy2);
        if (config.enemy3 != null) enemies.Add(config.enemy3);

        if (enemies.Count == 0)
        {
            Debug.LogWarning("[BattleTransitionManager] Aucun ennemi spécifié dans TimelineBattleConfigSO.");
            return;
        }

        ApplyTimelineEnemyAllegiance(enemies);

        // Stocke les séquences post-combat directement dans le gestionnaire de combat
        if (NewBattleManager.Instance != null)
        {
            NewBattleManager.Instance.victoryTimelineSequence.Clear();
            if (config.victoryTimelineSequence != null)
                NewBattleManager.Instance.victoryTimelineSequence.AddRange(config.victoryTimelineSequence);

            NewBattleManager.Instance.defeatTimelineSequence.Clear();
            if (config.defeatTimelineSequence != null)
                NewBattleManager.Instance.defeatTimelineSequence.AddRange(config.defeatTimelineSequence);

            NewBattleManager.Instance.activeTimelineBattleConfig = config;
            NewBattleManager.Instance.gameOverOnDefeat = !config.HasDefeatTimeline; // Game Over si aucune séquence de défaite
            NewBattleManager.Instance.lastBattleOutcome = BattleOutcome.None; // Réinitialisation de l'issue du combat
        }

        // --------------------------------------------------------------
        // 2) Simule la détection classique en remplissant PlayerDetection
        // --------------------------------------------------------------
        playerDetection ??= FindFirstObjectByType<PlayerDetection>();
        if (playerDetection != null)
        {
            playerDetection.detectedEnemies.Clear();
            playerDetection.detectedEnemies.AddRange(enemies);
            playerDetection.detectionOn = false; // Évite toute nouvelle détection
            playerDetection.battleEngaged = true; // Marque le combat comme en cours
        }
        else
        {
            Debug.LogWarning("[BattleTransitionManager] PlayerDetection introuvable, état non synchronisé.");
        }

        // --------------------------------------------------------------
        // 3) Transmet la liste au NewBattleManager pour le combat
        // --------------------------------------------------------------
        if (NewBattleManager.Instance != null)
        {
            NewBattleManager.Instance.enemyTemplates.Clear();
            NewBattleManager.Instance.enemyTemplates.AddRange(enemies);
        }
        else
        {
            Debug.LogWarning("[BattleTransitionManager] NewBattleManager introuvable, combat annulé.");
            return;
        }

        // ------------------------------------------------------------------
        // 4) Lancement de la transition de combat classique
        // ------------------------------------------------------------------
        StartCombatTransition();
    }

    private static void ApplyTimelineEnemyAllegiance(IEnumerable<CharacterData> enemies)
    {
        if (enemies == null)
            return;

        var allegianceManager = AllegianceManager.EnsureInstance();
        foreach (var enemy in enemies)
        {
            if (enemy == null)
                continue;

            if (allegianceManager.GetEffectiveAllegiance(enemy) == AllegianceSide.Player)
                allegianceManager.SetAllegiance(enemy, AllegianceSide.Enemy);
        }
    }

    /// <summary>
    /// Lance toutes les étapes de la transition vers le mode combat.
    /// </summary>
    public void StartCombatTransition()
    {
        // ✅ Sécurité supplémentaire : s'assure qu'aucune timeline ne
        // continue pendant la transition de combat.
        TimelineManager.Instance?.StopTimeline();

        ResolveBattleCamera();

        GameObject playerObject = ResolvePlayerRoot();
        if (playerObject != null)
            GameManager.Instance?.EnsureCheckpointInitialized(playerObject.transform);

        // Sauvegarde la position actuelle de la WorldCamera pour la restituer après le combat
        CameraController.Instance?.SaveWorldCameraTransform();

        CombatSkyboxManager.Instance?.ApplyBattleSkybox();

        // Masque l'interface jusqu'au premier tour du joueur
        HideBattleUI();

        //// Désactive également le GameObject principal du monde pour empêcher toute interaction
        if (worldScene != null)
            worldScene.SetActive(false);

        //Bloquer la position du joueur pour éviter qu'il ne chute quand worldScene est temporairement désactivée
        CharacterController cC = ResolvePlayerController();
        if (cC != null)
            cC.gameObject.SetActive(false);
        else
            Debug.LogWarning("[BattleTransitionManager] Impossible de localiser le joueur pour la transition de combat.");

        // Active uniquement les caméras nécessaires au lancement du combat
        CameraActivationManager.Instance?.ActivateBattleAndVersusCameras();

        AudioClipSO randomClip = null;
        ZoneSO currentZone = ZoneManager.Instance != null ? ZoneManager.Instance.currentZone : null;
        if (currentZone != null && currentZone.battleMusic != null && currentZone.battleMusic.Length > 0)
        {
            randomClip = currentZone.battleMusic[UnityEngine.Random.Range(0, currentZone.battleMusic.Length)];
        }

        if (randomClip != null)
            AudioManager.Instance.TransitionToCombat(randomClip);
        else
            Debug.LogWarning("[BattleTransitionManager] Aucune musique de combat trouvée pour la zone actuelle.");

        // 👉 Lancement sécurisé de la séquence audio de transition.
        //    On interrompt d'abord toute lecture précédente (par exemple si un
        //    autre combat démarre pendant que les sons se jouent encore) afin
        //    d'éviter la superposition de plusieurs voix/VFX.
        StopTransitionAudioRoutine();
        transitionAudioCoroutine = StartCoroutine(PlayTransitionSoundsSequentially());

        StartCoroutine(TransitionRoutine());

        Debug.Log("[BattleTransitionManager] Transition de combat démarrée.");
    }

    /// <summary>
    /// Enchaîne les différentes étapes visuelles et logiques de la transition.
    /// </summary>
    private IEnumerator TransitionRoutine()
    {
        // 🛡️ Assure que les filtres BlackScreen et WhiteScreen sont totalement
        //    transparents avant même l'affichage de l'écran de Versus.
        //    Cette précaution évite tout flash résiduel provenant d'une
        //    transition précédente.
        var fader = FadeChildrenOpacity.Instance;
        if (fader != null)
        {
            // Indice 0 = BlackScreen, indice 1 = WhiteScreen.
            // La durée est fixée à 0 pour appliquer instantanément la
            // transparence complète.
            fader.EnsureTransparency(0, 0f);
            fader.EnsureTransparency(1, 0f);
        }

        // Affiche immédiatement l'écran de Versus pour masquer les chargements restants.
        battleCamera.SetActive(true);
        versusTransition.SetActive(true);
        versusCameraCanvas.SetActive(true);
        CameraActivationManager.Instance?.ActivateBattleAndVersusCameras();

        // Cache le bouton "Continuer" tant que le chargement du champ de bataille n'est pas terminé
        foreach (var go in continuePrompts)
            if (go != null)
                go.SetActive(false);

        // Laisse une frame pour que l'écran Versus s'affiche avant les traitements plus lourds (sélection du battlefield, chargement, etc.).
        yield return null;

        // Détermine quel champ de bataille doit être chargé en fonction de
        // l'ennemi détecté par le joueur.
        // 
        // Dans certains cas (ex. zone non initialisée correctement), l'objet
        // PlayerDetection peut être désactivé ou sa liste d'ennemis vide.
        // Afin d'éviter une NullReference et de garantir l'affichage de l'écran
        // de Versus, on tente d'abord de récupérer l'indice depuis
        // PlayerDetection. Si cela échoue, on se replie sur la liste copiée
        // dans le NewBattleManager.

        // Récupération (ou création) de la référence vers PlayerDetection
        playerDetection ??= FindFirstObjectByType<PlayerDetection>();

        // Valeur par défaut au cas où aucun ennemi valide n'est trouvé
        int battlefieldIndex = 0;

        if (playerDetection != null && playerDetection.detectedEnemies.Count > 0 &&
            playerDetection.detectedEnemies[0] != null)
        {
            // ✅ Cas nominal : utilisation de l'ennemi détecté pour choisir le champ de bataille
            battlefieldIndex = playerDetection.detectedEnemies[0].battlefieldIndex;
        }
        else if (NewBattleManager.Instance.enemyTemplates.Count > 0 &&
                 NewBattleManager.Instance.enemyTemplates[0] != null)
        {
            // 🔁 Repli : on s'appuie sur la liste d'ennemis déjà copiée dans le
            // NewBattleManager lors de la détection. Cela évite un blocage de la
            // transition si PlayerDetection n'est plus accessible.
            battlefieldIndex = NewBattleManager.Instance.enemyTemplates[0].battlefieldIndex;
        }
        else
        {
            // ⚠️ Aucun ennemi valide trouvé : on loggue un avertissement afin de
            // faciliter le débogage et on conserve l'indice par défaut (0).
            Debug.LogWarning("[BattleTransitionManager] Impossible de déterminer l'indice du battlefield ; utilisation de 0 par défaut.");
        }

        // Effet de distorsion pour accentuer le passage en mode combat
        if (PostProcessManager.Instance != null)
            StartCoroutine(PostProcessManager.Instance.PulseLensDistortion(-1f, 0.5f));

        if (versusThunder != null)
            AudioManager.Instance.PlaySound(versusThunder);
        StartCoroutine(VersusThunderFlash()); // Déclenche un flash blanc synchronisé avec le son

        // Met à jour les portraits du Versus et lance l'animation d'apparition
        UpdateVersusPortraits();
        unitSpawnPointsAnimator?.Play("VersusLaunched");

        // Pendant que l'écran de Versus est affiché, on charge uniquement le
        // champ de bataille correspondant à l'ennemi rencontré. L'action
        // "Confirm" ne sera disponible qu'une fois ce chargement terminé afin
        // d'éviter toute validation prématurée.
        yield return StartCoroutine(BattlefieldManager.Instance.LoadBattlefield(battlefieldIndex));

        // Affiche à présent le bouton "Continuer" puisque le joueur peut confirmer
        foreach (var go in continuePrompts)
            if (go != null)
                go.SetActive(true);

        // --- Attente de la confirmation du joueur ---
        // L'écran de Versus reste jusqu'à ce que le joueur appuie sur
        // "Confirm". Cette action vient d'être rendue disponible car le
        // battlefield est prêt.
        InputAction confirmAction = InputsManager.Instance.playerInputs.Battle.Confirm;
        confirmAction.Enable();
        yield return new WaitUntil(() => confirmAction.triggered);
        confirmAction.Disable();

        // Capture la frame du Versus avant de masquer la transition pour l'effet de verre brise.
        yield return CaptureVersusFrameForGlass();

        versusTransition.SetActive(false);
        brokenGlass.SetActive(true);
        Animator glass = brokenGlass.GetComponent<Animator>();
        glass.Play("Glass_Explode");

        // À la fin de l'animation, la VersusCam doit se désactiver
        if (CameraActivationManager.Instance != null)
            StartCoroutine(CameraActivationManager.Instance.DisableVersusAfterAnimation(glass));

        if (brokenGlassSound != null)
            AudioManager.Instance.PlaySound(brokenGlassSound);

        // Les unités apparaissent après l'explosion
        NewBattleManager.Instance.SpawnAll();

        if (battleCamera != null)
        {
            battleCamera.SetActive(true); // S'assure que la camera de combat est active

            // Au lieu de jouer une timeline d'introduction, on priorise la Cinemachine dédiée
            // à l'intro de combat. Si elle est indisponible, on conserve l'ancien plan large.
            if (BattleCameraManager.Instance != null &&
                BattleCameraManager.Instance.TryGetCameraByName("CMV_BattleIntro", out _))
            {
                // Toutes les transitions caméra étant désormais lissées, on s'appuie sur la durée par défaut
                // fournie par le BlendSwitcher plutôt que d'imposer un cut immédiat.
                BattleCameraManager.Instance.SwitchToCamera("CMV_BattleIntro");

                // Munin pilote désormais la dynamique de la caméra d'introduction.

            }
            else
            {
                BattleCameraManager.Instance?.SwitchToCamera("CMV_OverHead_CasterLookTarget");
            }
        }
        else
        {
            Debug.LogWarning("[BattleTransitionManager] BattleScene_Camera_BattleIntro introuvable.");
        }

        SetupBattleCameraAndUI();

        GameManager.Instance.CurrentState = GameState.BattleTransition;
        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.Battle.Get());

        NewBattleManager.Instance.ChangeBattleState(BattleState.Initialization);

        while (NewBattleManager.Instance.unitsInBattle.Count <= 0)
            yield return null;

        // ⏳ laisser les timelines d'intro se jouer avant le début réel du combat.
        

        // 🎮 Lancement du combat et affichage du menu principal après la pause.
        yield return NewBattleManager.Instance.StartBattle();
    }


    public IEnumerator ExitVictoryScreenAndBattle()
    {
        if (exitBattleRoutineRunning)
        {
            // En cas de double validation, on logue et on ignore pour conserver
            // l'état courant : la première coroutine va se charger d'achever la sortie.
            Debug.LogWarning("[BattleTransitionManager] Une transition de sortie de combat est déjà en cours.");
            yield break;
        }

        exitBattleRoutineRunning = true;

        // On fige immédiatement le temps pour empêcher toute frame supplémentaire
        // du combat avant que l'interface ne soit complètement masquée.
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f; // La physique est figée elle aussi pour éliminer toute avance discrète des animations.

        yield return ExitVictoryScreenAndBattleSequence();

        exitBattleRoutineRunning = false;
    }

    /// <summary>
    /// Corps principal de la sortie de combat. Cette méthode suppose que le temps a
    /// déjà été figé par l'appelant pour éviter toute reprise momentanée du gameplay.
    /// </summary>
    private IEnumerator ExitVictoryScreenAndBattleSequence()
    {
        CombatSkyboxManager.Instance?.RestoreDefaultSkybox();

        bool shouldRespawnAtCheckpoint = NewBattleManager.Instance != null
            && NewBattleManager.Instance.ShouldRespawnAtCheckpoint;

        //yield return FadeToBlack(2f);

        // Nous récupérons ici le composant PlayerDetection afin de pouvoir
        // manipuler la liste temporaire des ennemis en combat.
        // ⚠️ Lorsque l'on quitte le combat, la scène d'exploration est encore désactivée.
        //     PlayerDetection vit dans cette scène : il est donc inactif et invisible
        //     pour une recherche classique. Sans ce paramètre, l'appel suivant
        //     (playerDetection.enemiesInFight) provoquait une NullReferenceException
        //     et bloquait la sortie de l'écran de victoire.
        playerDetection ??= FindFirstObjectByType<PlayerDetection>(FindObjectsInactive.Include);

        if (playerDetection == null)
        {
            Debug.LogWarning("[BattleTransitionManager] PlayerDetection introuvable lors de la sortie de combat.");
        }

        bool removeWorldEnemies = NewBattleManager.Instance != null
            && NewBattleManager.Instance.lastBattleOutcome == BattleOutcome.Victory;

        // Suppression des ennemis vaincus présents dans le monde
        // La liste enemiesInFight contient déjà uniquement les ennemis engagés
        if (playerDetection != null)
        {
            var worldEnemies = playerDetection.enemiesInFight.ToList();
            foreach (var enemy in worldEnemies)
            {
                // On retire l'ennemi de la liste afin qu'il ne puisse plus être redétecté
                playerDetection.enemiesInFight.Remove(enemy);
                // Destruction complète de l'objet ennemi dans le World pour
                // éviter qu'il ne persiste après la victoire du joueur
                if (removeWorldEnemies && enemy != null)
                    Destroy(enemy.transform.parent.gameObject);
            }

            // Par sécurité, on vide la liste au cas où il resterait des références
            playerDetection.enemiesInFight.Clear();
            playerDetection.detectedEnemies.Clear(); // Nettoyage complet des ennemis détectés
        }

        GameManager.Instance.ChangeGameState(GameState.Exploration);

        // Réactive la détection après un court délai (en temps réel pour ne pas dépendre du timeScale)
        playerDetection?.ResetDetection(1f);

        // Réactive également le GameObject principal du monde pour reprendre l'exploration
        if (worldScene != null)
            worldScene.SetActive(true);
        else
            Debug.LogWarning("[BattleTransitionManager] worldScene n'est pas assigné, la scène d'exploration risque de rester masquée.");

        GameObject playerObject = ResolvePlayerRoot();
        CharacterController cC = ResolvePlayerController();
        if (cC != null)
            cC.gameObject.SetActive(true);
        else if (playerObject == null)
            Debug.LogWarning("[BattleTransitionManager] Impossible de localiser le joueur lors de la sortie de combat.");
        else
            Debug.LogWarning("[BattleTransitionManager] Aucun CharacterController trouvé sur l'objet Player lors de la sortie de combat.");

        if (shouldRespawnAtCheckpoint && GameManager.Instance != null && playerObject != null)
            GameManager.Instance.RespawnPlayerAtCheckpoint(playerObject);

        // Retour à l'exploration : seule la WorldCam doit être active
        CameraActivationManager.Instance?.ActivateWorldCamera();
        // Replace la caméra du monde à l'endroit où le joueur l'avait laissée
        CameraController.Instance?.RestoreWorldCameraTransform();

        if (maskRingParticles != null)
        {
            maskRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            maskRingParticles.Play();
        }

        // Réinitialise les effets de dissolve ayant pu rester figes apres le combat
        StartDissolveReset(2f);

        HideVictoryPanel();
        HideGameOverPanel();

        AudioManager.Instance.ReturnFromBattle();

        // Nettoie les éléments visuels ou caméras activés pendant la transition de combat
        ResetTransitionObjects();

        // Cache l'interface de combat pour le retour à l'exploration
        HideBattleUI();

        // Sécurise l'accès aux inputs pour éviter une NullReference si l'InputsManager
        // n'est pas encore initialisé (cas limite lors de chargements manuels).
        if (InputsManager.Instance != null && InputsManager.Instance.playerInputs != null)
        {
            InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.World.Get());
        }
        else
        {
            Debug.LogWarning("[BattleTransitionManager] InputsManager ou son PlayerInputs est manquant lors du retour à l'exploration.");
        }

        // Une fois l'UI fermée et les inputs réactivés, on peut relancer le temps de jeu.
        yield return StartCoroutine(RestoreTimeScale(0f, 1f, 2f));
        Time.fixedDeltaTime = 0.02f; // Garantit la valeur par défaut même si le RestoreTimeScale a été interrompu.
        InputsManager.Instance?.RestoreInputUpdateMode();

        if (Application.isPlaying && !Application.isEditor && SaveAndLoadManager.Instance != null)
        {
            SaveAndLoadManager.Instance.AutoSave();
        }

        // Attente avant de supprimer le champ de bataille pour laisser le temps
        // au joueur de revenir visuellement dans le monde.
        yield return new WaitForSecondsRealtime(1f);

        // Détermine si une séquence post-combat doit être jouée avant de réinitialiser
        List<TimelineSequenceEntry> postBattleSequence = BuildPostBattleSequence();
        TimelineBattleConfigSO postBattleConfig = NewBattleManager.Instance != null
            ? NewBattleManager.Instance.activeTimelineBattleConfig
            : null;
        PostBattleTimelineContext postBattleContext = ResolvePostBattleContext(postBattleConfig);

        // Destruction complète du battlefield et remise à zéro des infos du combat
        BattlefieldManager.Instance?.UnloadCurrentBattlefield();
        NewBattleManager.Instance?.ResetBattleInfos();

        // Joue la séquence correspondante si elle est définie
        if (postBattleSequence != null && postBattleSequence.Count > 0)
        {
            yield return WaitForVictoryScreenClosed();
            yield return StartCoroutine(PlayPostBattleSequence(postBattleSequence, postBattleContext));
        }

        //yield return FadeToTransparent(1f);

        yield return new WaitForSecondsRealtime(0);

        // Si pour une raison quelconque le timeScale reste figé (ex : Restore interrompu),
        // on force une remise à la valeur attendue pour éviter de bloquer le monde.
        if (Time.timeScale < 0.999f)
        {
            Debug.LogWarning("[BattleTransitionManager] timeScale forcé à 1 après la transition de combat.");
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    private readonly struct PostBattleTimelineContext
    {
        public readonly GameObject caster;
        public readonly string cameraTag;

        public PostBattleTimelineContext(GameObject caster, string cameraTag)
        {
            this.caster = caster;
            this.cameraTag = cameraTag;
        }
    }

    private List<TimelineSequenceEntry> BuildPostBattleSequence()
    {
        if (NewBattleManager.Instance == null)
            return null;

        List<TimelineSequenceEntry> source = null;

        if (NewBattleManager.Instance.lastBattleOutcome == BattleOutcome.Victory)
        {
            source = NewBattleManager.Instance.victoryTimelineSequence;
        }
        else if (NewBattleManager.Instance.lastBattleOutcome == BattleOutcome.Defeat)
        {
            source = NewBattleManager.Instance.defeatTimelineSequence;
        }
        else
        {
            return null;
        }

        List<TimelineSequenceEntry> sequence = new();
        if (source != null && source.Count > 0)
        {
            for (int i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry != null && entry.HasPlayable)
                    sequence.Add(entry);
            }
        }

        return sequence.Count > 0 ? sequence : null;
    }

    private static PostBattleTimelineContext ResolvePostBattleContext(TimelineBattleConfigSO config)
    {
        GameObject caster = null;
        string cameraTag = "WorldCamera";

        if (config != null)
        {
            if (!string.IsNullOrWhiteSpace(config.postBattleCasterTag))
                caster = FindGameObjectByTagSafe(config.postBattleCasterTag);

            if (!string.IsNullOrWhiteSpace(config.postBattleCameraTag))
                cameraTag = config.postBattleCameraTag;
        }

        if (caster == null && InteractionManager.Instance != null)
            caster = InteractionManager.Instance.currentInteractable;

        return new PostBattleTimelineContext(caster, cameraTag);
    }

    private static GameObject FindGameObjectByTagSafe(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        try
        {
            return GameObject.FindGameObjectWithTag(tag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[BattleTransitionManager] Tag '{tag}' is not defined.");
            return null;
        }
    }

    private IEnumerator PlayPostBattleSequence(List<TimelineSequenceEntry> sequence, PostBattleTimelineContext context)
    {
        if (sequence == null || sequence.Count == 0)
            yield break;

        if (TimelineManager.Instance == null)
        {
            Debug.LogWarning("[BattleTransitionManager] TimelineManager introuvable pour jouer la séquence post-combat.");
            yield break;
        }

        int lastPlayableIndex = -1;
        for (int i = 0; i < sequence.Count; i++)
        {
            var entry = sequence[i];
            if (entry != null && entry.HasPlayable)
                lastPlayableIndex = i;
        }

        for (int i = 0; i < sequence.Count; i++)
        {
            var entry = sequence[i];
            if (entry == null || !entry.HasPlayable)
                continue;

            bool autoRestore = i == lastPlayableIndex;

            if (entry.directorPrefab != null)
            {
                if (!TryPlayDirectorEntry(entry, autoRestore, out PlayableDirector playedDirector, out bool destroyAfter))
                    continue;

                yield return WaitForTimelineToFinish();

                if (destroyAfter && playedDirector != null)
                    Destroy(playedDirector.gameObject);

                continue;
            }

            if (entry.timelineAsset != null)
            {
                TimelineManager.Instance.PlayTimeline(entry.timelineAsset, context.caster, context.cameraTag, autoRestore: autoRestore);
                yield return WaitForTimelineToFinish();
            }
        }
    }

    private bool TryPlayDirectorEntry(
        TimelineSequenceEntry entry,
        bool autoRestore,
        out PlayableDirector director,
        out bool destroyAfter)
    {
        director = null;
        destroyAfter = false;

        if (entry == null || entry.directorPrefab == null)
            return false;

        PlayableDirector directorToPlay = entry.directorPrefab;
        if (!directorToPlay.gameObject.scene.IsValid())
        {
            directorToPlay = Instantiate(entry.directorPrefab);
            destroyAfter = entry.destroyAfterPlay;
        }

        if (directorToPlay == null)
            return false;

        directorToPlay.Stop();
        directorToPlay.time = 0d;
        directorToPlay.Evaluate();
        TimelineManager.Instance.PlayTimeline(directorToPlay, autoRestore: autoRestore, requiresWorldCamera: false);
        director = directorToPlay;
        return true;
    }

    private IEnumerator WaitForTimelineToFinish()
    {
        if (TimelineManager.Instance == null)
            yield break;

        yield return null;

        while (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying)
            yield return null;
    }

    private IEnumerator WaitForVictoryScreenClosed()
    {
        var battleManager = NewBattleManager.Instance;
        if (battleManager == null || battleManager.victoryScreen == null)
            yield break;

        Transform panel = battleManager.victoryScreen.transform.childCount > 0
            ? battleManager.victoryScreen.transform.GetChild(0)
            : null;

        while (battleManager != null
               && battleManager.victoryScreen != null
               && battleManager.victoryScreen.activeInHierarchy)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy)
                yield break;

            yield return null;
        }
    }

    /// <summary>
    /// Réinitialise tous les éléments activés lors du lancement du combat afin
    /// d'éviter qu'ils restent visibles ou actifs une fois le combat terminé.
    /// </summary>
    private void ResetTransitionObjects()
    {
        // 🧹 Nettoyage audio : interrompt la séquence si elle est encore en cours
        //    (par exemple si le joueur quitte prématurément l'écran Versus).
        StopTransitionAudioRoutine();

        versusTransition.SetActive(false); // Bouclage pour être sûr
        brokenGlass.SetActive(false);
        versusCameraCanvas.SetActive(false);

        // Le canvas de transition est désactivé pour éviter tout résidu visuel
        ResolveTransitionCanvas();
        if (battleSceneTransitionPanel != null)
            battleSceneTransitionPanel.gameObject.SetActive(false);
    }

    void SetupBattleCameraAndUI()
    {
        ResolveBattleCamera();
        ResolveBattleCameraCam();
        ResolveTransitionCanvas();

        // Active la hiérarchie caméra
        battleCamera.SetActive(true);
        foreach (Transform child in battleCamera.transform)
            child.gameObject.SetActive(true);

        // Récupère la Camera Unity via son GameObject "BattleCamera_Cam"
        if (battleCameraCam == null)
        {
            Debug.LogError("Impossible de trouver BattleCamera_Cam ou son composant Camera.");
            return;
        }

        // Assigne la caméra au Canvas en ScreenSpace-Camera
        var battleUICanvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.renderMode == RenderMode.ScreenSpaceCamera);

        if (battleUICanvas != null)
        {
            battleUICanvas.worldCamera = battleCameraCam;
        }

        // Active le premier enfant du TransitionCanvas si trouvé
        if (battleSceneTransitionPanel != null)
            battleSceneTransitionPanel.gameObject.SetActive(true);
    }

    public IEnumerator SlowTimeScale(float to, float speed)
    {
        float epsilon = 0.001f; // petit seuil pour éviter les flottants imprécis

        while (Time.timeScale - to > epsilon)
        {
            float newScale = Time.timeScale - Time.unscaledDeltaTime * speed;
            if (newScale <= to + epsilon)
                newScale = to;

            Time.timeScale = Mathf.Max(0f, newScale);
            Time.fixedDeltaTime = Time.timeScale * 0.02f;

            yield return null;
        }

        Time.timeScale = to;
        Time.fixedDeltaTime = Time.timeScale * 0.02f;
    }

    private IEnumerator RestoreTimeScale(float from, float to, float speed)
    {
        float epsilon = 0.001f;

        while (to - Time.timeScale > epsilon)
        {
            Time.timeScale += Time.unscaledDeltaTime * speed;
            if (Time.timeScale > to)
                Time.timeScale = to;

            Time.fixedDeltaTime = Time.timeScale * 0.02f;
            yield return null;
        }

        Time.timeScale = to;
        Time.fixedDeltaTime = 0.02f;
    }

    private void StartDissolveReset(float duration)
    {
        if (dissolveResetRoutine != null)
            StopCoroutine(dissolveResetRoutine);

        dissolveResetRoutine = StartCoroutine(ResetDissolveShaderValues(duration));
    }

    /// <summary>
    /// Parcourt tous les renderers presents dans la scene afin de remettre a zero la propriete
    /// de dissolve utilisee durant les transitions de combat. La transition se fait en temps reel.
    /// </summary>
    private IEnumerator ResetDissolveShaderValues(float duration)
    {
        // Inclut les renderers inactifs car certains decors peuvent etre masques pendant le combat
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var propertyBlock = new MaterialPropertyBlock();
        var targets = new List<(Renderer renderer, int index, float startValue)>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                continue;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                bool supportsDissolve = material != null && material.HasProperty(DissolveStrengthId);

                propertyBlock.Clear();
                renderer.GetPropertyBlock(propertyBlock, i);
                float startValue = propertyBlock.GetFloat(DissolveStrengthId);

                if (!supportsDissolve && Mathf.Approximately(startValue, 0f))
                    continue;

                if (Mathf.Approximately(startValue, 0f) && supportsDissolve)
                    startValue = material.GetFloat(DissolveStrengthId);

                targets.Add((renderer, i, startValue));
            }
        }

        if (targets.Count == 0)
        {
            dissolveResetRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            foreach (var target in targets)
            {
                if (target.renderer == null)
                    continue;

                float value = Mathf.Lerp(target.startValue, 0f, t);
                target.renderer.GetPropertyBlock(propertyBlock, target.index);
                propertyBlock.SetFloat(DissolveStrengthId, value);
                target.renderer.SetPropertyBlock(propertyBlock, target.index);
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        foreach (var target in targets)
        {
            if (target.renderer == null)
                continue;

            target.renderer.GetPropertyBlock(propertyBlock, target.index);
            propertyBlock.SetFloat(DissolveStrengthId, 0f);
            target.renderer.SetPropertyBlock(propertyBlock, target.index);
        }

        dissolveResetRoutine = null;
    }

    /// <summary>
    /// Stoppe immédiatement la séquence audio de transition.
    /// </summary>
    private void StopTransitionAudioRoutine()
    {
        if (transitionAudioCoroutine != null)
        {
            StopCoroutine(transitionAudioCoroutine);
            transitionAudioCoroutine = null;
        }
    }

    private void ResolveBattleCamera()
    {
        if (battleCamera == null)
            battleCamera = GameObject.FindGameObjectWithTag("BattleCamera");
    }

    private void ResolveVersusCamera()
    {
        if (versusCamera == null)
            versusCamera = GameObject.FindGameObjectWithTag("VersusCamera")?.GetComponent<Camera>();
    }

    private void ResolveBattleCameraCam()
    {
        if (battleCameraCam != null)
            return;

        if (battleCamera != null)
        {
            Camera[] cameras = battleCamera.GetComponentsInChildren<Camera>(true);
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam.name == "BattleCamera_Cam")
                {
                    battleCameraCam = cam;
                    return;
                }
            }
        }

        GameObject camObj = GameObject.Find("BattleCamera_Cam");
        if (camObj != null)
            battleCameraCam = camObj.GetComponent<Camera>();
    }

    private void ResolveTransitionCanvas()
    {
        if (battleSceneTransitionCanvas == null)
            battleSceneTransitionCanvas = GameObject.Find("BattleScene_TransitionCanvas");

        if (battleSceneTransitionPanel == null && battleSceneTransitionCanvas != null)
        {
            Transform root = battleSceneTransitionCanvas.transform;
            if (root.childCount > 0)
                battleSceneTransitionPanel = root.GetChild(0);
        }
    }

    private Transform ResolveEnemyPosition01()
    {
        if (enemyPosition01 != null)
            return enemyPosition01;

        enemyPosition01 = GameObject.Find("EnemyPosition_01")?.transform;
        return enemyPosition01;
    }

    private GameObject ResolvePlayerRoot()
    {
        if (playerRoot != null)
            return playerRoot;

        playerRoot = GameObject.FindGameObjectWithTag("Player");
        return playerRoot;
    }

    private CharacterController ResolvePlayerController()
    {
        if (cachedPlayerController != null)
            return cachedPlayerController;

        cachedPlayerController = FindFirstObjectByType<CharacterController>(FindObjectsInactive.Include);
        if (cachedPlayerController != null)
            return cachedPlayerController;

        GameObject playerObject = ResolvePlayerRoot();
        if (playerObject != null)
            cachedPlayerController = playerObject.GetComponent<CharacterController>();

        return cachedPlayerController;
    }

    private IEnumerator PlayTransitionSoundsSequentially()
    {
        if (transitionSFXClips == null || transitionSFXClips.Count == 0)
        {
            Debug.LogWarning("[TransitionAudio] Aucun clip configuré pour la transition de combat.");
            transitionAudioCoroutine = null;
            yield break;
        }

        foreach (var clipAsset in transitionSFXClips)
        {
            if (clipAsset == null || clipAsset.Clip == null)
                continue; // Clip absent : on ignore l'entrée mais on poursuit la séquence.

            AudioManager audioManager = AudioManager.Instance;
            if (audioManager == null)
            {
                Debug.LogWarning("[TransitionAudio] AudioManager absent, impossible de jouer la transition audio.");
                transitionAudioCoroutine = null;
                yield break;
            }

            if (clipAsset.Loop)
            {
                // Les boucles n'ont pas de sens pendant l'intro de combat : on loggue et force un passage unique.
                Debug.LogWarning($"[TransitionAudio] Le clip '{clipAsset.name}' est configuré en boucle. " +
                                 "La lecture sera forcée sur une seule itération pour éviter un blocage de la transition.");
            }

            audioManager.PlaySfx(clipAsset.Clip, clipAsset.Volume, false);

            float waitDuration = clipAsset.Length;
            if (waitDuration <= 0f && clipAsset.Clip != null)
                waitDuration = clipAsset.Clip.length;
            if (waitDuration > 0f)
            {
                yield return new WaitForSeconds(waitDuration);
            }
        }

        transitionAudioCoroutine = null;
    }

    /// <summary>
    /// Crée un flash blanc rapide synchronisé avec le son "versusThunder".
    /// Utilise l'overlay existant en le colorant temporairement en blanc puis en le faisant disparaître.
    /// </summary>
    private IEnumerator VersusThunderFlash()
    {
        if (worldFadeOverlay == null)
        {
            Debug.LogWarning("[BattleTransitionManager] worldFadeOverlay manquant pour le flash blanc.");
            yield break;
        }

        // Sauvegarde l'état initial pour éviter des effets indésirables
        bool wasActive = worldFadeOverlay.gameObject.activeSelf;
        Color originalColor = worldFadeOverlay.color;

        // Prépare l'overlay en blanc totalement transparent
        worldFadeOverlay.gameObject.SetActive(true);
        worldFadeOverlay.color = new Color(1f, 1f, 1f, 0f);

        // Phase d'apparition très rapide simulant l'éclair
        float flashInDuration = 0.05f;
        float elapsed = 0f;
        while (elapsed < flashInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / flashInDuration);
            worldFadeOverlay.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        // Phase de disparition progressive pour un retour doux
        float flashOutDuration = 0.25f;
        elapsed = 0f;
        while (elapsed < flashOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / flashOutDuration);
            worldFadeOverlay.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        // Restaure l'overlay en forçant sa transparence.
        //
        // Sans cette étape, si un fondu noir précédent (par exemple issu d'une
        // timeline passée) a laissé l'overlay actif et opaque, le flash se
        // terminerait sur un écran noir persistant. En imposant un alpha nul,
        // on garantit que l'effet visuel est toujours correctement réinitialisé.
        worldFadeOverlay.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        worldFadeOverlay.gameObject.SetActive(wasActive);
    }

    //private IEnumerator FadeToBlack(float duration)
    //{
    //    yield return FadeAlpha(0f, 1f, duration);
    //}

    //private IEnumerator FadeToTransparent(float duration)
    //{
    //    yield return FadeAlpha(worldFadeOverlay?.color.a ?? 1f, 0f, duration);
    //}

    //private IEnumerator FadeAlpha(float from, float to, float duration)
    //{
    //    if (worldFadeOverlay == null)
    //    {
    //        Debug.LogWarning("WorldFadeOverlay manquant !");
    //        yield break;
    //    }

    //    float elapsed = 0f;
    //    while (elapsed < duration)
    //    {
    //        elapsed += Time.unscaledDeltaTime;
    //        float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

    //        Color col = worldFadeOverlay.color;
    //        col.a = Mathf.Lerp(from, to, t);
    //        worldFadeOverlay.color = col;

    //        yield return null;
    //    }

    //    Color final = worldFadeOverlay.color;
    //    final.a = to;
    //    worldFadeOverlay.color = final;
    //}

    private void HideVictoryPanel()
    {
        var battleManager = NewBattleManager.Instance;
        if (battleManager == null || battleManager.victoryScreen == null)
            return;

        if (battleManager.victoryScreen.transform.childCount > 0)
            battleManager.victoryScreen.transform.GetChild(0).gameObject.SetActive(false);

        battleManager.victoryScreen.SetActive(false);
    }
    private void HideGameOverPanel() => NewBattleManager.Instance.gameOverScreen?.transform.GetChild(0).gameObject.SetActive(false);

    private IEnumerator CaptureVersusFrameForGlass()
    {
        if (versusCamera != null)
            versusRenderTexture = versusCamera.targetTexture;

        if (versusRenderTexture == null)
            yield break;

        if (frozenVersusFrame == null)
        {
            var desc = versusRenderTexture.descriptor;
            frozenVersusFrame = new RenderTexture(desc)
            {
                name = "RT_VersusCaptureRuntime"
            };
        }

        if (!versusRenderTexture.IsCreated())
            versusRenderTexture.Create();

        bool wasEnabled = versusCamera != null && versusCamera.enabled;
        if (versusCamera != null && !wasEnabled)
            versusCamera.enabled = true;

        // Attend une frame complete pour laisser HDRP rendre la VersusCam sans appel imbrique.
        yield return null;
        yield return new WaitForEndOfFrame();

        Graphics.Blit(versusRenderTexture, frozenVersusFrame);
        ApplyGlassTexture(frozenVersusFrame);

        // Désactive la VersusCam pour éviter qu'elle ne nettoie la RT aux frames suivantes.
        if (versusCamera != null)
            versusCamera.enabled = false;
    }

    private void ApplyGlassTexture(RenderTexture texture)
    {
        if (texture == null)
            return;

        foreach (var r in brokenGlassRenderers)
        {
            if (r == null)
                continue;

            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;
                mat.SetTexture("_MainTex", texture);
                mat.SetTexture("_BaseMap", texture);
                mat.SetTexture("_BASE_COLOR_MAP", texture);
            }
        }

        foreach (var img in brokenGlassRawImages)
        {
            if (img != null)
                img.texture = texture;
        }
    }

    private void OnDestroy()
    {
        if (frozenVersusFrame != null)
        {
            frozenVersusFrame.Release();
            frozenVersusFrame = null;
        }
    }

    #endregion
}
