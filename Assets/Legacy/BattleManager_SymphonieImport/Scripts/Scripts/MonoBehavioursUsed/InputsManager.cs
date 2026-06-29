using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
// Directives réservées à l'éditeur pour l'inspecteur personnalisé.
// En production, UnityEditor n'est pas disponible, d'où l'encapsulation.
using UnityEditor;
#endif

public class InputsManager : MonoBehaviour
{
    public static InputsManager Instance { get; private set; }
    public PlayerInputs playerInputs;
    private CharacterController3D controller;

    [Header("Pass Turn")]
    public float passHoldDuration = 2f;
    private Pulse passTurnPulse;
    private Coroutine passRoutine;

    private InputActionMap[] allMaps;

    /// <summary>
    /// Mémorise la liste des ActionMaps actives avant d'entrer dans l'inventaire afin de les restaurer ensuite.
    /// </summary>
    private readonly List<InputActionMap> mapsBeforeInventory = new();

    /// <summary>
    /// Indique si l'on se trouve actuellement dans un contexte où seule l'ActionMap Inventory doit rester active.
    /// </summary>
    private bool inventoryModeActive;

    /// <summary>
    /// Liste temporaire des actions du mapping Battle mises en pause pendant
    /// l'introduction du combat. Elle permet de restaurer précisément l'état
    /// des contrôles une fois la cinématique terminée.
    /// </summary>
    private readonly List<InputAction> battleActionsDisabledDuringIntro = new();

    /// <summary>
    /// Indique si la restriction "Confirm uniquement" est active. Ce drapeau
    /// évite d'empiler plusieurs appels successifs et garantit une remise à
    /// zéro propre lorsque les menus sont à nouveau disponibles.
    /// </summary>
    private bool battleIntroRestrictionActive = false;


    /// <summary>
    /// Indique si les validations doivent être temporairement ignorées.
    /// Cette variable ne passe à <c>true</c> que lorsqu'une sélection (compétence ou
    /// objet) est déclenchée par un contrôle également lié à l'action "Confirm".
    /// Dans ce cas précis, toutes les tentatives de validation sont bloquées jusqu'au
    /// relâchement de la touche afin d'empêcher un lancement immédiat du mouvement.
    /// </summary>
    private bool ignorerProchaineValidation = false;

    private InputSettings.UpdateMode? previousInputUpdateMode;
    private bool forcedDynamicInputUpdate = false;

    #region Initialisation
    /// <summary>
    /// Instancie l'asset d'inputs et configure le singleton.
    /// </summary>
    void Awake()
    {
        playerInputs = new PlayerInputs();

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

        // --- Détection des périphériques au démarrage ---
        // Force un rafraîchissement de l'Input System pour
        // recenser les périphériques déjà branchés lorsque
        // le jeu se lance (évite de devoir les rebrancher).
        InputSystem.Update();

        // Affiche dans la console les périphériques détectés
        // afin de faciliter le débogage et la maintenance.
        foreach (var device in InputSystem.devices)
        {
            Debug.Log($"Périphérique détecté au lancement : {device.displayName}");
        }

        // Écoute les changements de périphériques pour
        // gérer les branchements/débranchements à chaud.
        InputSystem.onDeviceChange += OnDeviceChange;

        allMaps = new[]
        {
            playerInputs.World.Get(),
            playerInputs.Inventory.Get(),
            playerInputs.Battle.Get(),
            playerInputs.InfoBox.Get(),
            playerInputs.Menu.Get()
        };

        // Les actions LeftShoulder et RightShoulder sont désormais définies dans
        // l'asset PlayerInputs. Elles seront reliées aux callbacks dans SetInputs().
    }

    /// <summary>
    /// Active le mapping World au démarrage et trouve le contrôleur.
    /// </summary>
    void Start()
    {
        ActivateOnly(playerInputs.World.Get());
        SetInputs();
        controller = FindFirstObjectByType<CharacterController3D>();
        passTurnPulse = PassTurnUI.Instance.gameObject.GetComponentInChildren<Pulse>();
    }

    /// <summary>
    /// Abonne les actions aux différents callbacks.
    /// </summary>
    public void SetInputs()
    {
        var battle = playerInputs.Battle;
        battle.Select1.performed += OnSelect1;
        battle.Select2.performed += OnSelect2;
        battle.Select3.performed += OnSelect3;
        battle.Awake.performed += OnAwake; // Appui sur la touche d'éveil
        battle.BaseAttack.performed += OnBaseAttack; // Attaque de base directe depuis le SkillsMenu
        battle.Menu.performed += OnMenu;
        battle.Back.started += OnBackStarted;
        battle.Back.performed += OnBackInput;
        battle.Back.canceled += OnBackCanceled;
        // "Confirm" est également utilisé pour sélectionner la 3e compétence.
        // On écoute donc l'évènement "canceled" afin de savoir quand la touche est relâchée.
        battle.Confirm.performed += OnConfirm;
        battle.Confirm.canceled += OnConfirmCanceled;
        battle.EnemiesGroupSelection.performed += OnEnemiesGroupSelection;
        battle.SquadGroupSelection.performed += OnSquadGroupSelection;
        // Naviguer entre les pages de compétences avec les boutons d'épaules
        battle.LeftShoulder.performed += OnPreviousSkillPage;
        battle.RightShoulder.performed += OnNextSkillPage;

        var world = playerInputs.World;
        world.Inventory.performed += OnWorldInventory;

    }

    /// <summary>
    /// Désabonne tous les callbacks des actions.
    /// </summary>
    public void ResetInputs()
    {
        var battle = playerInputs.Battle;
        battle.Select1.performed -= OnSelect1;
        battle.Select2.performed -= OnSelect2;
        battle.Select3.performed -= OnSelect3;
        battle.Awake.performed -= OnAwake; // Désabonnement de la touche d'éveil
        battle.BaseAttack.performed -= OnBaseAttack; // Retire le binding de l'attaque basique
        battle.Menu.performed -= OnMenu;
        battle.Back.started -= OnBackStarted;
        battle.Back.performed -= OnBackInput;
        battle.Back.canceled -= OnBackCanceled;
        // On se désabonne également de l'évènement "canceled" lié à Confirm.
        battle.Confirm.performed -= OnConfirm;
        battle.Confirm.canceled -= OnConfirmCanceled;
        battle.EnemiesGroupSelection.performed -= OnEnemiesGroupSelection;
        battle.SquadGroupSelection.performed -= OnSquadGroupSelection;
        battle.LeftShoulder.performed -= OnPreviousSkillPage;
        battle.RightShoulder.performed -= OnNextSkillPage;

        var world = playerInputs.World;
        world.Inventory.performed -= OnWorldInventory;
    }

    /// <summary>
    /// Active uniquement les maps données et désactive les autres.
    /// </summary>
    public void ActivateOnly(params InputActionMap[] mapsToEnable)
    {
        // 1) on désactive tout
        foreach (var m in allMaps)
            m.Disable();

        // 2) on ré-active le sous-ensemble voulu
        foreach (var m in mapsToEnable)
            m.Enable();
    }

    /// <summary>
    /// Active exclusivement l'ActionMap Inventory tout en mémorisant l'état précédent pour pouvoir le restaurer ensuite.
    /// </summary>
    public void EnterInventoryMode()
    {
        if (playerInputs == null)
            return;

        if (inventoryModeActive)
            return;

        mapsBeforeInventory.Clear();
        foreach (var map in allMaps)
        {
            if (map != null && map.enabled)
                mapsBeforeInventory.Add(map);
        }

        ActivateOnly(playerInputs.Inventory.Get());
        inventoryModeActive = true;

        // On s'assure que l'action Inventory est disponible pour permettre la fermeture du menu.
        var toggleAction = playerInputs.Inventory.Inventory;
        if (toggleAction != null && !toggleAction.enabled)
            toggleAction.Enable();
    }

    /// <summary>
    /// Restaure les ActionMaps actives avant l'ouverture de l'inventaire.
    /// </summary>
    public void ExitInventoryMode()
    {
        if (playerInputs == null)
            return;

        if (!inventoryModeActive)
            return;

        if (mapsBeforeInventory.Count > 0)
        {
            ActivateOnly(mapsBeforeInventory.ToArray());
        }
        else
        {
            ActivateOnly(playerInputs.World.Get());
        }

        mapsBeforeInventory.Clear();
        inventoryModeActive = false;
    }

    /// <summary>
    /// Restreint temporairement les contrôles au seul bouton "Confirm" du mapping Battle.
    /// Cette méthode est invoquée au début d'un combat pour éviter toute interaction
    /// prématurée avec les menus tant que les unités terminent leurs animations
    /// d'introduction.
    /// </summary>
    public void RestrictInputsToBattleConfirm()
    {
        if (playerInputs == null)
            return;

        if (battleIntroRestrictionActive)
            return;

        battleIntroRestrictionActive = true;

        // On s'assure que seul le mapping Battle est actif afin de bloquer immédiatement
        // les contrôles d'exploration ou des menus annexes.
        ActivateOnly(playerInputs.Battle.Get());

        var confirmAction = playerInputs.Battle.Confirm;
        var battleMap = playerInputs.Battle.Get();

        if (battleMap == null)
        {
            battleIntroRestrictionActive = false;
            return;
        }

        // Dans le doute, on garantit que Confirm est bien opérationnel pour permettre
        // au joueur de valider les messages affichés pendant la transition (ex : écran Versus).
        if (confirmAction != null && !confirmAction.enabled)
            confirmAction.Enable();

        battleActionsDisabledDuringIntro.Clear();

        // Chaque action du mapping, à l'exception de Confirm, est désactivée jusqu'à la fin
        // de l'introduction. On mémorise la liste pour pouvoir la restaurer fidèlement.
        foreach (var action in battleMap.actions)
        {
            if (action == null || action == confirmAction)
                continue;

            if (action.enabled)
            {
                action.Disable();
                battleActionsDisabledDuringIntro.Add(action);
            }
        }
    }

    /// <summary>
    /// Restaure l'intégralité du mapping Battle après la cinématique d'introduction.
    /// Les actions précédemment suspendues sont réactivées une par une pour retrouver
    /// un contrôle complet des menus de combat.
    /// </summary>
    public void RestoreBattleInputsAfterIntro()
    {
        if (playerInputs == null)
            return;

        if (!battleIntroRestrictionActive)
            return;

        var battleMap = playerInputs.Battle.Get();
        if (battleMap == null)
        {
            battleIntroRestrictionActive = false;
            battleActionsDisabledDuringIntro.Clear();
            return;
        }

        // On conserve l'exclusivité du mapping Battle afin de respecter le cahier des charges.
        ActivateOnly(battleMap);

        // Réactive chaque action mise en pause pendant la cinématique.
        foreach (var action in battleActionsDisabledDuringIntro)
        {
            if (action != null && !action.enabled)
                action.Enable();
        }

        battleActionsDisabledDuringIntro.Clear();
        battleIntroRestrictionActive = false;
    }

    public void ForceDynamicInputUpdate()
    {
        if (forcedDynamicInputUpdate)
            return;

        if (InputSystem.settings == null)
            return;

        previousInputUpdateMode = InputSystem.settings.updateMode;
        if (previousInputUpdateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;

        forcedDynamicInputUpdate = true;
    }

    public void RestoreInputUpdateMode()
    {
        if (!forcedDynamicInputUpdate)
            return;

        if (InputSystem.settings != null && previousInputUpdateMode.HasValue)
            InputSystem.settings.updateMode = previousInputUpdateMode.Value;

        previousInputUpdateMode = null;
        forcedDynamicInputUpdate = false;
    }

    #endregion

    #region Gestion des périphériques
    /// <summary>
    /// Désabonne les événements lors de la destruction du singleton.
    /// </summary>
    void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        // Nettoyage des abonnements aux boutons d'épaules
        if (playerInputs != null)
        {
            var battle = playerInputs.Battle;
            battle.LeftShoulder.performed -= OnPreviousSkillPage;
            battle.RightShoulder.performed -= OnNextSkillPage;
        }
    }

    /// <summary>
    /// Callback appelé lors du branchement ou de la reconnexion d'un périphérique.
    /// </summary>
    /// <param name="device">Le périphérique concerné.</param>
    /// <param name="change">Le type de changement détecté.</param>
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
        {
            Debug.Log($"Périphérique (re)connecté : {device.displayName}");
        }
    }

    #endregion

    #region Inputs
    /// <summary>
    /// Récupère le <see cref="NewBattleManager"/> uniquement si les menus ne sont pas verrouillés
    /// par la cinématique d'introduction de combat. Centraliser la vérification permet
    /// d'éviter la duplication de logique dans chaque callback d'entrée utilisateur.
    /// </summary>
    /// <param name="bm">Instance active du gestionnaire de combat lorsque disponible.</param>
    /// <returns>Vrai si les menus sont accessibles et que l'instance est valide, faux sinon.</returns>
    private bool TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm)
    {
        bm = NewBattleManager.Instance;

        // Si aucune instance n'est disponible ou que les menus sont verrouillés, on ignore l'entrée.
        if (bm == null || bm.AreBattleMenusLocked)
            return false;

        return true;
    }

    /// <summary>
    /// Callback de validation des actions de combat.
    /// </summary>
    private void OnConfirm(InputAction.CallbackContext ctx)
    {
        // Tant que la BattleIntro est active, on ne valide aucune action de menu pour éviter les chevauchements.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        bool isVictoryOrGameOverScreen = bm.currentBattleState == BattleState.VictoryScreen_CanContinue
            || bm.currentBattleState == BattleState.GameOverScreen_CanContinue;

        // Si une sélection vient juste d'être effectuée, on ignore cette validation
        // pour éviter d'exécuter immédiatement le mouvement sans choix de cible.
        // On autorise néanmoins la validation lorsqu'on se trouve sur l'écran de victoire/game over
        // afin de ne jamais bloquer le joueur sur ce panneau.
        if (!isVictoryOrGameOverScreen && ignorerProchaineValidation)
        {
            // Le joueur vient de sélectionner une compétence ou un objet
            // avec la même touche que la validation. On ignore donc toute
            // tentative de confirmation tant que la touche n'a pas été relâchée.
            return;
        }

        if (bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill
            || bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill
            || bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad
            || bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies
            )
        {
            // Liste des messages d'erreurs à afficher au joueur.
            // Cette liste permet de communiquer plusieurs problèmes en même temps
            // (distance trop grande, cible au mauvais endroit, etc.).
            var instructions = new System.Collections.Generic.List<string>();

            // Vérifie si la cible est à portée de l'attaque.
            // Si elle ne l'est pas, on l'ajoute à la liste des messages.
            if (!bm.TryGetMovementRequirement(bm.currentCharacterUnit, bm.currentTargetCharacter, bm.currentMove, out float requiredDistance))
            {
                instructions.Add("Aucune cible valide");
            }
            else if (requiredDistance > 0.01f && !bm.currentCharacterUnit.CanSpendMovementDistance(requiredDistance))
            {
                instructions.Add("Pas assez de points de mouvement");
            }

            // Vérifie que l'altitude de la cible correspond aux exigences du mouvement.
            // Exemple : un mouvement terrestre ne peut pas toucher une cible en l'air.
            if (!bm.IsTargetAltitudeValid(bm.currentTargetCharacter, bm.currentMove))
            {
                if (bm.currentMove.altitudeCondition == AltitudeCondition.AirOnly)
                {
                    // Indique que la cible doit être en l'air sans aucun sol sous elle.
                    instructions.Add("La cible doit être en l'air sans sol sous elle");
                }
                else if (bm.currentMove.altitudeCondition == AltitudeCondition.GroundOnly)
                {
                    // Indique que la cible doit être au sol.
                    instructions.Add("La cible doit être au sol");
                }
            }

            // Si une ou plusieurs erreurs ont été détectées, on les affiche
            // toutes en même temps, puis on quitte la méthode.
            if (instructions.Count > 0)
            {
                ActionUIDisplayManager.Instance.DisplayInstruction(string.Join("\n", instructions));
                return;
            }

            // Empêche le lancement si la position relative est déjà occupée par une autre unité.
            if (!bm.HasSpaceForMove(bm.currentCharacterUnit, bm.currentTargetCharacter, bm.currentMove))
            {
                ActionUIDisplayManager.Instance.DisplayInstruction_TargetPositionOccupied();
                return;
            }

            bm.ChangeBattleState(BattleState.SquadUnit_PerformingMusicalMove);
            bm.StartCoroutine(bm.ExecuteMoveOnTarget(bm.currentMove, bm.currentCharacterUnit, bm.currentTargetCharacter));
            bm.ToggleMenuContainers(false, false, false);
        }
        else if (IsItemTargetSelectionState(bm.currentBattleState))
        {
            if (bm.currentItem == null || bm.currentCharacterUnit == null)
                return;

            var targets = bm.ResolveItemTargetsForCurrentSelection();
            if (targets.Count == 0)
            {
                ActionUIDisplayManager.Instance.DisplayInstruction("Aucune cible valide");
                return;
            }

            CharacterUnit rangeTarget = bm.currentTargetCharacter ?? targets[0];
            if (!bm.IsTargetInRange(bm.currentCharacterUnit, rangeTarget, bm.currentItem))
            {
                ActionUIDisplayManager.Instance.DisplayInstruction_TargetTooFar();
                return;
            }

            bm.ChangeBattleState(BattleState.SquadUnit_Item_Use);
            if (bm.IsCurrentItemMultiTargetSelection())
                bm.StartCoroutine(bm.UseItemOnTargets(bm.currentItem, bm.currentCharacterUnit, targets));
            else
                bm.StartCoroutine(bm.UseItemOnTarget(bm.currentItem, bm.currentCharacterUnit, rangeTarget));
            bm.ToggleMenuContainers(false, false, false);
        }

        if (isVictoryOrGameOverScreen)
        {
            bm.ChangeBattleState(BattleState.None);
            var transitionManager = BattleTransitionManager.Instance;
            if (transitionManager != null)
            {
                // Même logique que le bouton Continue : on laisse le manager centraliser la sortie.
                transitionManager.StartCoroutine(transitionManager.ExitVictoryScreenAndBattle());
            }
            else
            {
                Debug.LogWarning("[InputsManager] BattleTransitionManager introuvable à la validation de l'écran de victoire.");
            }
        }
    }

    /// <summary>
    /// Réinitialise l'indicateur d'ignorance lorsque la touche "Confirm" est relâchée.
    /// </summary>
    private void OnConfirmCanceled(InputAction.CallbackContext ctx)
    {
        // Dès que le joueur relâche la touche, la validation redevient possible.
        ignorerProchaineValidation = false;
    }

    /// <summary>
    /// Analyse le contrôle ayant déclenché une sélection pour déterminer si la prochaine
    /// validation doit être temporairement bloquée.
    /// </summary>
    /// <param name="selectionContext">Contexte d'entrée fourni par l'Input System.</param>
    private void MettreAJourIgnorerValidationApresSelection(InputAction.CallbackContext selectionContext)
    {
        bool boutonPartageAvecConfirm = false;

        // L'Input System peut ne pas fournir de contrôle (cas très rare). On vérifie donc que
        // l'information est bien disponible avant toute comparaison.
        var selectionControl = selectionContext.control;
        if (selectionControl != null && playerInputs != null)
        {
            var confirmAction = playerInputs.Battle.Confirm;

            if (confirmAction != null)
            {
                var confirmControls = confirmAction.controls;

                // On recherche un contrôle strictement identique entre la sélection et l'action Confirm.
                // Tant que le joueur n'utilise pas la même touche, la validation doit rester possible.
                for (int i = 0; i < confirmControls.Count; i++)
                {
                    if (confirmControls[i] == selectionControl)
                    {
                        boutonPartageAvecConfirm = true;
                        break;
                    }
                }

                // Si aucun contrôle actif ne correspond, on vérifie également les liaisons déclarées afin de
                // couvrir les cas où l'action Confirm est configurée sur la même touche mais n'a pas encore été
                // évaluée comme "performed" dans ce même frame.
                if (!boutonPartageAvecConfirm)
                {
                    var bindings = confirmAction.bindings;
                    for (int i = 0; i < bindings.Count; i++)
                    {
                        var effectivePath = bindings[i].effectivePath;
                        if (string.IsNullOrEmpty(effectivePath))
                            continue;

                        if (InputControlPath.Matches(effectivePath, selectionControl))
                        {
                            boutonPartageAvecConfirm = true;
                            break;
                        }
                    }
                }
            }
        }

        // L'indicateur est activé uniquement lorsque le même bouton assure à la fois la sélection et la confirmation.
        // Ainsi, on continue de bloquer les validations indésirables sans imposer de double pression inutile.
        ignorerProchaineValidation = boutonPartageAvecConfirm;
    }

    /// <summary>
    /// Sélectionne l'option 1 dans les menus.
    /// </summary>
    private void OnSelect1(InputAction.CallbackContext ctx)
    {
        // On ignore immédiatement toute tentative si la cinématique empêche encore l'accès aux menus.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        if (bm.currentBattleState == BattleState.SquadUnit_MainMenu)
        {
            bm.OpenSkillsMenu();
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_SkillsMenu)
        {
            // Dans le SkillsPanel, les entrées Select1/2/3 doivent exclusivement cibler les MusicalMoves paginés.
            // On confie donc la sélection à une méthode dédiée qui ignore volontairement l'attaque de base
            // et le Special Musical Move.
            TrySelectMusicalMoveFromSkillsPanel(bm, ctx, 0, nameof(OnSelect1));
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_ItemsMenu)
        {
            if (bm.itemChoices.Count > 0)
            {
                bm.currentItem = bm.itemChoices[0];
                bm.ToggleMenuContainers(false, false, false);
                // Application de la même règle que pour les compétences : si la touche diffère, la validation
                // reste possible immédiatement afin de ne pas ralentir inutilement le joueur.
                MettreAJourIgnorerValidationApresSelection(ctx);
                bm.HandleTargetSelection(bm.currentItem);
                // La Timeline de préparation gère maintenant les animations liées à l'objet
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect1 ignoré : pas d'item disponible !");
            }
        }
    }

    /// <summary>
    /// Sélectionne l'option 2 dans les menus.
    /// </summary>
    private void OnSelect2(InputAction.CallbackContext ctx)
    {
        // Tant que l'introduction verrouille les menus, aucune sélection ne doit être prise en compte.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        if (bm.currentBattleState == BattleState.SquadUnit_MainMenu)
        {
            bm.OpenItemMenu();
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_SkillsMenu)
        {
            // Ce bouton correspond désormais au deuxième slot paginé (offset 1) :
            // les MusicalMoves restent ainsi indépendants de l'attaque basique fixe.
            TrySelectMusicalMoveFromSkillsPanel(bm, ctx, 1, nameof(OnSelect2));
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_ItemsMenu)
        {
            if (bm.itemChoices.Count > 1)
            {
                bm.currentItem = bm.itemChoices[1];
                bm.ToggleMenuContainers(false, false, false);
                // Mise à jour de l'indicateur : seule une touche commune avec Confirm impose une temporisation.
                MettreAJourIgnorerValidationApresSelection(ctx);
                bm.HandleTargetSelection(bm.currentItem);
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect2 ignoré : pas assez d'items !");
            }
        }
    }

    /// <summary>
    /// Sélectionne l'option 3 dans les menus.
    /// </summary>
    private void OnSelect3(InputAction.CallbackContext ctx)
    {
        // Même règle pour le troisième bouton : l'entrée est ignorée si la BattleIntro n'est pas terminée.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        if (bm.currentBattleState == BattleState.SquadUnit_SkillsMenu)
        {
            // Le troisième bouton atteint le troisième slot paginé (offset 2) afin de couvrir les configurations
            // où plusieurs MusicalMoves sont disponibles sur la même page.
            TrySelectMusicalMoveFromSkillsPanel(bm, ctx, 2, nameof(OnSelect3));
        }
        else if (bm.currentBattleState == BattleState.SquadUnit_ItemsMenu)
        {
            if (bm.itemChoices.Count > 2)
            {
                bm.currentItem = bm.itemChoices[2];
                bm.ToggleMenuContainers(false, false, false);
                // On réutilise la même logique conditionnelle pour la sélection d'objets.
                MettreAJourIgnorerValidationApresSelection(ctx);
                bm.HandleTargetSelection(bm.currentItem);
            }
            else
            {
                Debug.LogWarning("[InputsManager] OnSelect3 ignoré : pas assez d'items !");
            }
        }
    }

    /// <summary>
    ///     Tente de sélectionner un MusicalMove affiché dans le SkillsPanel.
    ///     Les offsets se basent sur les slots paginés (hors attaque de base et hors Special Musical Move).
    /// </summary>
    /// <param name="bm">Référence vers le gestionnaire de combat en cours.</param>
    /// <param name="ctx">Contexte de l'input, utilisé notamment pour détecter les touches partagées avec Confirm.</param>
    /// <param name="paginatedOffset">Décalage au sein de la page courante (0 = premier slot paginé, etc.).</param>
    /// <param name="debugContext">Nom de l'action, uniquement pour les logs de diagnostic.</param>
    /// <returns><c>true</c> si une compétence musicale a été sélectionnée avec succès ; sinon <c>false</c>.</returns>
    private bool TrySelectMusicalMoveFromSkillsPanel(NewBattleManager bm, InputAction.CallbackContext ctx, int paginatedOffset, string debugContext)
    {
        // Sécurise l'offset attendu : un nombre négatif indiquerait un mauvais appel et doit être ignoré.
        if (paginatedOffset < 0)
        {
            Debug.LogWarning($"[InputsManager] {debugContext} ignoré : offset négatif ({paginatedOffset}).");
            return false;
        }

        // Sans slots paginés disponibles, impossible d'adresser une compétence musicale.
        int paginatedSlots = bm.GetPaginatedSkillSlotCount();
        if (paginatedSlots <= 0)
        {
            Debug.LogWarning($"[InputsManager] {debugContext} ignoré : aucun slot de compétence musicale disponible !");
            return false;
        }

        // Si l'offset dépasse la capacité de la page, on prévient afin de détecter rapidement une mauvaise configuration UI.
        if (paginatedOffset >= paginatedSlots)
        {
            Debug.LogWarning($"[InputsManager] {debugContext} ignoré : pas assez de slots paginés pour atteindre l'offset {paginatedOffset}.");
            return false;
        }

        // Calcule l'index global dans la liste des compétences musicales réellement disponibles.
        int pageSize = paginatedSlots;
        int baseIndex = bm.currentSkillPageIndex * pageSize;
        int globalIndex = baseIndex + paginatedOffset;

        if (bm.skillChoices.Count <= globalIndex)
        {
            Debug.LogWarning($"[InputsManager] {debugContext} ignoré : pas assez de skills pour indexer {globalIndex}.");
            return false;
        }

        MusicalMoveSO selectedMove = bm.skillChoices[globalIndex];
        HarmonicType requiredType = selectedMove.consumedHarmonicType;

        // Validation des ressources : on vérifie d'abord la quantité d'harmoniques disponible.
        if (bm.currentCharacterUnit.GetAvailableHarmonicsForCost(requiredType) < selectedMove.harmonicCost)
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
            return false;
        }

        // Les compétences en recharge sont signalées visuellement et ne doivent pas être lancées.
        if (bm.currentCharacterUnit.IsMoveOnCooldown(selectedMove))
        {
            ActionUIDisplayManager.Instance.DisplayInstruction_MoveOnCooldown();
            bm.ShowMainMenu();
            return false;
        }

        // Tout est prêt : on mémorise la compétence choisie et on masque les menus pour passer à la sélection de cible.
        bm.currentMove = selectedMove;
        bm.ToggleMenuContainers(false, false, false);

        // Comme pour les autres sélections, on bloque temporairement la validation si la touche est partagée avec Confirm.
        MettreAJourIgnorerValidationApresSelection(ctx);

        bm.HandleTargetSelection(selectedMove);
        return true;
    }

    /// <summary>
    /// Tente d'activer l'état Awake lorsque le joueur est dans le SkillsMenu.
    /// </summary>
    private void OnAwake(InputAction.CallbackContext ctx)
    {
        // Blocage de l'éveil tant que les introductions empêchent toute interaction.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        if (bm.currentBattleState != BattleState.SquadUnit_SkillsMenu)
            return; // Ignore l'input si le menu des compétences n'est pas actif

        CharacterUnit unit = bm.currentCharacterUnit;
        if (unit == null)
            return;

        // Vérifie la réserve d'harmonique nécessaire pour l'éveil
        if (unit.GetHarmonicCount(unit.Data.harmonicType) < unit.Data.awakeHarmonicThreshold)
        {
            // Feedback si le joueur n'a pas assez d'harmonique
            ActionUIDisplayManager.Instance.DisplayInstruction_NotEnoughHarmonics();
            return;
        }

        // Active l'état Awake et rafraîchit les compétences disponibles
        unit.EnterAwakeState();
        bm.OpenSkillsMenu();
    }

    /// <summary>
    /// Déclenche la sélection d'une attaque de base lorsque le joueur se trouve dans le SkillsMenu.
    /// </summary>
    private void OnBaseAttack(InputAction.CallbackContext ctx)
    {
        // Impossible d'initier une attaque tant que la BattleIntro verrouille les menus.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        // On limite strictement l'action au SkillsMenu pour éviter toute incohérence dans les autres états.
        if (bm.currentBattleState != BattleState.SquadUnit_SkillsMenu)
            return;

        // L'appel à TryStartBaseAttackSelection masque automatiquement les menus et prépare la cible.
        if (bm.TryStartBaseAttackSelection())
        {
            // Même logique que pour les autres sélections : si la touche utilisée correspond également
            // à Confirm, on patiente une frame pour éviter une validation instantanée du move.
            MettreAJourIgnorerValidationApresSelection(ctx);
        }
    }

    /// <summary>
    /// Appelé lorsque le joueur presse l'épaule droite pour afficher la page suivante de compétences.
    /// </summary>
    private void OnNextSkillPage(InputAction.CallbackContext ctx)
    {
        // Ne bascule pas de page tant que la cinématique d'introduction verrouille les menus.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        // On ne réagit que si le joueur se trouve bien dans le menu des compétences,
        // afin d'éviter tout comportement inattendu dans les autres états de combat.
        if (bm.currentBattleState != BattleState.SquadUnit_SkillsMenu)
            return;

        // Calcule le nombre de cases disponibles pour les attaques musicales classiques
        // (le premier slot étant l'attaque basique fixe et le dernier le move spécial).
        int pageSize = bm.GetPaginatedSkillSlotCount();

        // Sans slot paginé, aucune page supplémentaire ne peut être affichée.
        if (pageSize <= 0)
            return;

        // Si le total des attaques disponibles tient sur une seule page, inutile de tenter
        // une navigation : on quitte simplement la méthode.
        if (bm.skillChoices == null || bm.skillChoices.Count <= pageSize)
            return;

        // Toutes les conditions sont réunies : on peut afficher la page suivante.
        bm.NextSkillPage();
    }

    /// <summary>
    /// Appelé lorsque le joueur presse l'épaule gauche pour afficher la page précédente de compétences.
    /// </summary>
    private void OnPreviousSkillPage(InputAction.CallbackContext ctx)
    {
        // Même garde-fou pour la navigation arrière.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        // De la même manière que pour l'épaule droite, on vérifie d'abord que le menu
        // des compétences est bien actif avant de traiter l'entrée.
        if (bm.currentBattleState != BattleState.SquadUnit_SkillsMenu)
            return;

        // Détermination du nombre d'attaques affichables par page (hors attaque basique et move spécial).
        int pageSize = bm.GetPaginatedSkillSlotCount();

        // Sans slot paginé, il est impossible de reculer.
        if (pageSize <= 0)
            return;

        // Si une seule page suffit à afficher toutes les compétences, il n'y a rien
        // à afficher de plus et l'on quitte la méthode.
        if (bm.skillChoices == null || bm.skillChoices.Count <= pageSize)
            return;

        // Navigation vers la page précédente.
        bm.PreviousSkillPage();
    }

    private void OnBackInput(InputAction.CallbackContext ctx)
    {
        // Aucun retour arrière n'est traité durant la séquence d'introduction.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        if (bm.currentBattleState == BattleState.SquadUnit_SkillsMenu ||
            bm.currentBattleState == BattleState.SquadUnit_ItemsMenu)
        {
            bm.ShowMainMenu();
            return;
        }

        if (IsSkillTargetSelectionState(bm.currentBattleState))
        {
            bm.OpenSkillsMenu();
            bm.currentCharacterUnit.GetCasterAnimator()?.SetTrigger("exitAction");
        }
        else if (IsItemTargetSelectionState(bm.currentBattleState))
        {
            bm.OpenItemMenu();
        }
    }

    private bool IsSkillTargetSelectionState(BattleState state)
    {
        return state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForSkill ||
               state == BattleState.SquadUnit_TargetSelectionAmongSquadForSkill ||
               (state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && NewBattleManager.Instance.currentMove != null) ||
               (state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && NewBattleManager.Instance.currentMove != null);
    }

    private bool IsItemTargetSelectionState(BattleState state)
    {
        return state == BattleState.SquadUnit_TargetSelectionAmongEnemiesForItem ||
               state == BattleState.SquadUnit_TargetSelectionAmongSquadForItem ||
               (state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad && NewBattleManager.Instance.currentItem != null) ||
               (state == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies && NewBattleManager.Instance.currentItem != null);
    }

    private void OnBackStarted(InputAction.CallbackContext ctx)
    {
        // Démarrer un passage de tour pendant l'intro pourrait couper des animations : on bloque donc la commande.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;
        if (bm.currentBattleState == BattleState.SquadUnit_MainMenu && passRoutine == null)
        {
            if (passTurnPulse != null)
                passTurnPulse.pulseSpeed = 0f;
            passRoutine = StartCoroutine(PassTurnRoutine());
        }
    }

    private void OnBackCanceled(InputAction.CallbackContext ctx)
    {
        // Si le verrou est actif, aucune routine n'a dû démarrer : on peut sortir immédiatement.
        if (!TryGetBattleManagerWhileMenusUnlocked(out _))
            return;

        if (passTurnPulse != null)
            passTurnPulse.pulseSpeed = 2f;

        if (passRoutine != null)
        {
            StopCoroutine(passRoutine);
            passRoutine = null;
        }
        // Vérifie que l'interface de passage de tour existe toujours
        // avant de tenter de réinitialiser sa progression.
        PassTurnUI.Instance?.ResetProgressSmooth();
    }

    private void OnMenu(InputAction.CallbackContext ctx)
    {
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;

        if (bm.currentBattleState == BattleState.VictoryScreen_Await
            || bm.currentBattleState == BattleState.VictoryScreen_CanContinue
            || bm.currentBattleState == BattleState.GameOverScreen_Await
            || bm.currentBattleState == BattleState.GameOverScreen_CanContinue)
            return;

        bm.ShowHarmonicStatusMenu();
    }

    private IEnumerator PassTurnRoutine()
    {
        float elapsed = 0f;
        while (elapsed < passHoldDuration)
        {
            // Surveille en continu l'état du verrou afin d'éviter tout lancement forcé.
            if (!TryGetBattleManagerWhileMenusUnlocked(out _))
            {
                passRoutine = null;
                yield break;
            }

            if (!playerInputs.Battle.Back.IsPressed())
            {
                passRoutine = null;
                // L'UI peut avoir été détruite si le combat s'est terminé.
                PassTurnUI.Instance?.ResetProgressSmooth();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // Met à jour la jauge uniquement si elle est toujours présente.
            PassTurnUI.Instance?.SetProgress(elapsed / passHoldDuration);
            yield return null;
        }

        passRoutine = null;
        NewBattleManager.Instance.EndTurn();
    }

    private void OnEnemiesGroupSelection(InputAction.CallbackContext ctx)
    {
        // Aucune bascule de groupe pendant l'intro pour préserver la cohérence des caméras.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;
        if (bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad)
        {
            bool allowed = false;
            if (bm.currentMove != null)
            {
                if (bm.currentMove.targetType == TargetType.SingleAllyOrEnemy)
                {
                    bm.ApplyMoveTargetSelection(TargetType.SingleEnemy);
                    allowed = true;
                }
                else if (bm.currentMove.targetType == TargetType.AllEnemies
                         || bm.currentMove.targetType == TargetType.AllAllies)
                {
                    bm.ApplyMoveTargetSelection(TargetType.AllEnemies);
                    allowed = true;
                }
            }
            if (bm.currentItem != null)
            {
                TargetType desired = TargetType.SingleEnemy;
                bool itemAllowed = false;
                if (bm.currentItem.targetTypes.Contains(TargetType.SingleEnemy))
                {
                    desired = TargetType.SingleEnemy;
                    itemAllowed = true;
                }
                else if (bm.currentItem.targetTypes.Contains(TargetType.AllEnemies))
                {
                    desired = TargetType.AllEnemies;
                    itemAllowed = true;
                }
                else if (bm.currentItem.targetTypes.Contains(TargetType.All))
                {
                    desired = TargetType.All;
                    itemAllowed = true;
                }
                if (itemAllowed)
                    bm.currentItemTargetType = desired;
                allowed = allowed || itemAllowed;
            }
            if (!allowed)
                return;

            bm.ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies);
            if (bm.currentMove == null)
                bm.SetCurrentTargetToFirst(CharacterType.EnemyUnit);

        }
    }

    private void OnSquadGroupSelection(InputAction.CallbackContext ctx)
    {
        // Même logique côté alliés : les entrées sont ignorées tant que l'introduction est active.
        if (!TryGetBattleManagerWhileMenusUnlocked(out NewBattleManager bm))
            return;
        if (bm.currentBattleState == BattleState.SquadUnit_SkillsMenu)
        {
            if (bm.TryUseRepli())
                MettreAJourIgnorerValidationApresSelection(ctx);
            return;
        }
        if (bm.currentBattleState == BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnEnemies)
        {
            bool allowed = false;
            if (bm.currentMove != null)
            {
                if (bm.currentMove.targetType == TargetType.SingleAllyOrEnemy)
                {
                    bm.ApplyMoveTargetSelection(TargetType.SingleAlly);
                    allowed = true;
                }
                else if (bm.currentMove.targetType == TargetType.AllEnemies
                         || bm.currentMove.targetType == TargetType.AllAllies)
                {
                    bm.ApplyMoveTargetSelection(TargetType.AllAllies);
                    allowed = true;
                }
            }
            if (bm.currentItem != null)
            {
                TargetType desired = TargetType.SingleAlly;
                bool itemAllowed = false;
                if (bm.currentItem.targetTypes.Contains(TargetType.SingleAlly))
                {
                    desired = TargetType.SingleAlly;
                    itemAllowed = true;
                }
                else if (bm.currentItem.targetTypes.Contains(TargetType.AllAllies))
                {
                    desired = TargetType.AllAllies;
                    itemAllowed = true;
                }
                else if (bm.currentItem.targetTypes.Contains(TargetType.All))
                {
                    desired = TargetType.All;
                    itemAllowed = true;
                }
                if (itemAllowed)
                    bm.currentItemTargetType = desired;
                allowed = allowed || itemAllowed;
            }
            if (!allowed)
                return;

            bm.ChangeBattleState(BattleState.SquadUnit_TargetSelectionAmongSquadOrEnemies_OnSquad);
            if (bm.currentMove == null)
                bm.SetCurrentTargetToFirst(CharacterType.SquadUnit);

        }
    }

    /// <summary>
    /// Ouvre l'inventaire lorsque l'action World/Inventory est pressée.
    /// </summary>
    private void OnWorldInventory(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;

        InventoryManager.Instance?.OpenInventoryFromWorldInput();
    }
    #endregion
}

#if UNITY_EDITOR
// ----- Inspecteur personnalisé pour InputsManager (éditeur uniquement) -----
[CustomEditor(typeof(InputsManager))]
[CanEditMultipleObjects]
public class InputsManagerEditor : Editor
{
    private void OnEnable()
    {
        // Mise à jour régulière de l'inspector pour afficher l'état des maps.
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // Rafraîchit l'Inspector en temps réel
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        // Sécurité : on vérifie que l'objet sérialisé existe toujours
        if (serializedObject == null)
            return;

        serializedObject.Update();

        // Affiche l'inspecteur par défaut
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("🎮 Input Action Maps Status", EditorStyles.boldLabel);

            // Pour chaque instance sélectionnée
            foreach (var obj in targets)
            {
                if (obj == null) continue;
                var mgr = obj as InputsManager;
                if (mgr == null) continue;

                EditorGUILayout.LabelField($"-- {mgr.gameObject.name} --", EditorStyles.miniBoldLabel);
                DrawMapStatus("World", mgr.playerInputs.World.Get());
                DrawMapStatus("Inventory", mgr.playerInputs.Inventory.Get());
                DrawMapStatus("Battle", mgr.playerInputs.Battle.Get());
                DrawMapStatus("InfoBox", mgr.playerInputs.InfoBox.Get());
                DrawMapStatus("Menu", mgr.playerInputs.Menu.Get());
                EditorGUILayout.Space();
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Passez en Play Mode pour voir l'état des Input Action Maps.",
                MessageType.Info
            );
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMapStatus(string label, InputActionMap map)
    {
        bool isEnabled = map.enabled;
        string statusText = isEnabled ? "Enabled" : "Disabled";

        var style = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = isEnabled ? Color.green : Color.red }
        };

        EditorGUILayout.LabelField(label, statusText, style);
    }
}
#endif
