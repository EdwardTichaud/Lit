using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lit.Fractures
{
    /// <summary>Isolated solo exploration fixture. No campaign slot, combat result or network session is created.</summary>
    [DisallowMultipleComponent]
    public sealed partial class FractureGraybox : MonoBehaviour
    {
        public GameObject lucianPrefab;
        public KnowledgeSO networkKnowledge;
        public Transform spawn;
        public Camera previewCamera;
        public GameObject thresholdGate;
        public GameObject galleryGate;
        public GameObject networkGate;
        public GameObject startGate;
        [Tooltip("Jalon 1 uniquement : les gardes ne sont pas encore raccordes. Ne valide aucun combat.")]
        public bool allowUncontestedGrayboxInteractions = true;
        [Tooltip("Aide explicite de conception, propre à cette graybox. Aucune prise de focus.")]
        public bool showPuzzleHelp = true;
        [Min(1f)] public float puzzleHelpDistance = 5.5f;
        public SquadCharacterController Player { get; private set; }
        public FractureAttempt Attempt { get; } = new FractureAttempt();
        public FractureArchivePuzzle ArchivePuzzle { get; } = new FractureArchivePuzzle();
        public string ClueNotebook => "1 = " + Clue(0) + "   |   2 = " + Clue(1) + "   |   3 = " + Clue(2);
        public string GalleryCode => Glyph(ArchivePuzzle.GetInput(0)) + " / " + Glyph(ArchivePuzzle.GetInput(1)) + " / " + Glyph(ArchivePuzzle.GetInput(2));
        private string Clue(int index) => ArchivePuzzle.HasClue(index) ? Glyph(FractureArchivePuzzle.SymbolForNumber(index)) : "?";
        private static string Glyph(int value) => value == 0 ? "O" : value == 1 ? "Y" : "—";
        public string Feedback { get; private set; } = "Entrez dans la fracture depuis le pupitre de depart.";
        public bool ExplorationCompleted { get; private set; }
        public bool KnowsNetwork => KnowledgeManager.Instance != null && KnowledgeManager.Instance.HasKnowledge(networkKnowledge);
        public string CurrentObjective => districtIntegration && ExplorationCompleted ? "Énigmes résolues. Retournez au quartier par le même passage." :
            !Attempt.IsRunning ? "Commencez au pupitre d'entrée." :
            ArchivePuzzle.DoorOpen ? "Porte ouverte : traversez-la dans les Galeries et rejoignez le poste de fin." :
            ArchivePuzzle.FoundCount < 3 ? "Fouillez les bibliothèques des Archives : " + ArchivePuzzle.FoundCount + "/3 indices trouvés." :
            "Dans les Galeries : entrez les symboles dans l'ordre 1, 2, 3, puis essayez la combinaison.";
        private KnowledgeManager ownedKnowledge;
        private LitOpsiveLocomotionBridge bridge;
        private InputMode previousMode;
        private bool initialized;
        private bool started;
        private bool configuredMode;
        private bool cameraSuspended;
        private float announcementUntil;
        private string announcement;
        private FractureStation[] helpStations;
        private const string Source = "FractureGraybox";

        private void Start()
        {
            started = true;
            Initialize();
        }

        private void OnEnable()
        {
            if (started && Application.isPlaying) Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            if (districtIntegration) { InitializeDistrict(); return; }
            // This fixture must never attach itself to a live campaign or online game.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ||
                GameFlowService.Instance != null || SquadManager.Instance != null ||
                SaveSessionManager.Instance != null && SaveSessionManager.Instance.HasActiveSave ||
                LocalPlayerContext.LocalCharacterRoot != null)
            {
                Debug.LogError("[Fracture] Ouvrir cette scene seule, hors campagne et hors session reseau.", this);
                enabled = false;
                return;
            }
            if (lucianPrefab == null || networkKnowledge == null || spawn == null || previewCamera == null)
            {
                Debug.LogError("[Fracture] References de graybox incompletes.", this);
                enabled = false;
                return;
            }
            LocalPlayerInput.EnsureInstance();
            previousMode = InputModeCoordinator.CurrentMode;
            InputModeCoordinator.SetBaseMode(InputMode.Exploration);
            configuredMode = true;
            if (KnowledgeManager.Instance == null) ownedKnowledge = KnowledgeManager.GetOrCreate();
            var root = Instantiate(lucianPrefab, spawn.position, spawn.rotation);
            root.name = "Lucian_FractureGraybox";
            Player = root.GetComponent<SquadCharacterController>();
            bridge = root.GetComponent<LitOpsiveLocomotionBridge>();
            var info = root.GetComponent<CharacterInfo>();
            if (Player == null || bridge == null || info == null || info.SourceData == null)
            {
                Debug.LogError("[Fracture] Le prefab Lucian doit conserver sa pile UCC et ses donnees.", this);
                root.SetActive(false); Destroy(root); Player = null; enabled = false;
                return;
            }
            Player.BindCharacterData(info.SourceData, initializeInventory: false);
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            LocalPlayerContext.SetLocalCharacter(root.transform, source: Source);
            if (cameraSuspended)
            {
                previewCamera.GetComponent<LitUccCameraCharacterBinder>()?.EndExternalCameraControl(true);
                cameraSuspended = false;
            }
            LocalInputRouter.Jump += OnJump;
            helpStations = GetComponentsInChildren<FractureStation>(true);
            initialized = true;
            RefreshGates();
        }

        private bool AcceptsInput => initialized && Player != null && Player.CurrentHp > 0 &&
            InputModeCoordinator.CurrentMode == InputMode.Exploration && !InputFocusStack.HasAnyFocus();

        private void Update()
        {
            if (districtIntegration) { UpdateDistrict(); return; }
            if (!initialized || Player == null) return;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening || GameFlowService.Instance != null)
            {
                Debug.LogError("[Fracture] La graybox solo ne peut pas cohabiter avec une session active.", this);
                enabled = false;
                return;
            }
            if (AcceptsInput)
            {
                Player.SetSprintModifier(LocalInputRouter.SprintPressed);
                Player.Move(LocalInputRouter.MoveValue);
                Player.RefreshLocalInteractionDetectionForExternalLocomotion();
            }
            else bridge.StopBridgeInput();
            if (Player.CurrentHp <= 0) Attempt.End(FracturePhase.Lost);
            // In this discovery fixture no performance grade is awarded.
            if (Time.deltaTime > 0f) Attempt.Tick(Time.deltaTime);
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed && AcceptsInput) Player.Jump();
        }

        public bool TryInteract(FractureStation station, SquadCharacterController actor)
        {
            if (districtIntegration) return TryDistrictInteraction(station, actor);
            if (!AcceptsInput || station == null || station.labyrinth != this || actor != Player ||
                !station.isActiveAndEnabled || !station.IsInReach(actor) || !bridge.Grounded || bridge.IsInputSuppressedByUcc)
                return false;
            return ExecuteInteraction(station, actor);
        }

        private bool ExecuteInteraction(FractureStation station, SquadCharacterController actor)
        {
            bool changed = false;
            if (station.kind == FractureStation.Kind.Entry)
            {
                if (Attempt.CanReform && !districtIntegration)
                {
                    Attempt.Reform();
                    ArchivePuzzle.ResetAttempt();
                    foreach (var dial in GetComponentsInChildren<FractureStation>(true)) dial.dialValue = 0;
                    ExplorationCompleted = false;
                }
                changed = Attempt.Start(1, true);
                Feedback = changed ? "La porte de départ est ouverte. Fouillez les bibliothèques aux Archives, puis utilisez les indices dans les Galeries." : "La tentative est déjà en cours.";
            }
            else if (Attempt.Phase != FracturePhase.Exploring)
                Feedback = "Commencez une tentative au pupitre de l'entree.";
            else switch (station.kind)
            {
                case FractureStation.Kind.Bookshelf:
                    station.searched = true;
                    changed = ArchivePuzzle.Reveal(station.clueIndex);
                    Feedback = station.clueIndex < 0 ? "Aucun indice dans cette bibliothèque. Cherchez dans les autres." : "Indice : " + (station.clueIndex + 1) + " = " + Glyph(FractureArchivePuzzle.SymbolForNumber(station.clueIndex)) + ". Noté dans votre carnet (" + ArchivePuzzle.FoundCount + "/3).";
                    if (changed)
                    {
                        announcement = actor.CharacterData.characterName + " a trouvé : " + (station.clueIndex + 1) + " = " + Glyph(FractureArchivePuzzle.SymbolForNumber(station.clueIndex));
                        announcementUntil = Time.unscaledTime + 5f;
                        if (ArchivePuzzle.FoundCount == 3) KnowledgeReveal.Reveal(networkKnowledge, actor.gameObject, Source + "/archives");
                    }
                    break;
                case FractureStation.Kind.GalleryDial:
                    ArchivePuzzle.Cycle(station.dialIndex);
                    Feedback = "Case " + (station.dialIndex + 1) + " : " + Glyph(ArchivePuzzle.GetInput(station.dialIndex)) + ". Comparez au numéro correspondant dans votre carnet.";
                    break;
                case FractureStation.Kind.GalleryValidate:
                    changed = ArchivePuzzle.TryOpen();
                    Feedback = changed || ArchivePuzzle.DoorOpen ? "Bonne combinaison : la porte des Galeries est ouverte !" : ArchivePuzzle.FoundCount < 3 ? "Il manque des indices : fouillez les bibliothèques des Archives." : "Combinaison incorrecte. Placez les symboles dans l'ordre des numéros 1, 2, 3, puis réessayez.";
                    break;

                case FractureStation.Kind.Exit:
                    ExplorationCompleted = ArchivePuzzle.DoorOpen;
                    changed = ExplorationCompleted && Attempt.End(FracturePhase.Abandoned);
                    Feedback = changed ? districtIntegration ? "Les deux énigmes sont résolues. Retournez au quartier par le même passage." : "Les deux énigmes sont terminées ! Revenez à l'entrée pour recommencer." : "Ouvrez d'abord la porte des Galeries.";
                    break;
            }
            RefreshGates();
            return true; // Consume even a refused action: Interagir must not also jump.
        }

        private void RefreshGates()
        {
            // Keep the return path open after completion so the entry desk remains reachable.
            if (startGate != null) startGate.SetActive(districtIntegration ? !districtStarted : Attempt.AttemptNumber == 0);
            if (thresholdGate != null) thresholdGate.SetActive(true);
            if (galleryGate != null) galleryGate.SetActive(!ArchivePuzzle.DoorOpen);
            if (networkGate != null) networkGate.SetActive(true);
        }

        private void OnDisable()
        {
            if (districtIntegration)
            {
                initialized = false; Player = null; bridge = null; helpStations = null;
                requestedDistrictState = false; districtRules = null; lastDistrictState = -1;
                return; // Session owns the actor, camera, knowledge and input lifecycle.
            }
            LocalInputRouter.Jump -= OnJump;
            Attempt.End(FracturePhase.Abandoned);
            if (Player != null)
            {
                var cameraController = previewCamera != null ? previewCamera.GetComponent<Opsive.UltimateCharacterController.Camera.CameraController>() : null;
                if (cameraController != null && cameraController.Character == Player.gameObject)
                {
                    previewCamera.GetComponent<LitUccCameraCharacterBinder>()?.BeginExternalCameraControl();
                    cameraController.Character = null;
                    cameraSuspended = true;
                }
                Player.HideLocalInteractionPresentation();
                bridge.StopBridgeInput();
                LocalPlayerContext.ClearIfMatch(Player.transform, source: Source);
                Player.gameObject.SetActive(false);
                Destroy(Player.gameObject);
                Player = null;
            }
            if (ownedKnowledge != null)
            {
                ownedKnowledge.PrepareForRuntimeReset(Source + " cleanup");
                Destroy(ownedKnowledge.gameObject);
                ownedKnowledge = null;
            }
            if (configuredMode) InputModeCoordinator.SetBaseMode(previousMode);
            configuredMode = false;
            initialized = false;
        }

        private void OnGUI()
        {
            if (!initialized || Player == null || districtIntegration && !ContainsDistrictPoint(Player.transform.position)) return;
            var previousMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 760f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            var headerMatrix = GUI.matrix;
            if (districtIntegration) GUI.matrix = headerMatrix * Matrix4x4.Translate(new Vector3(180, 0, 0));
            GUI.Box(new Rect(12, 12, 850, 154), districtIntegration ? "Archives de la fracture — District 1" : "Fracture — graybox SOLO, jalon 1 (pas de boss/recompense/reseau)");
            GUI.Label(new Rect(25, 40, 820, 24), districtIntegration ? "Exploration continue du quartier | Progression partagée de l'énigme" : "Deplacement et saut habituels | Interagir sur un poste selectionne | aucune sauvegarde de campagne");
            GUI.Label(new Rect(25, 65, 820, 24), "CARNET — " + ClueNotebook + "     |     Porte des Galeries : " + (ArchivePuzzle.DoorOpen ? "ouverte" : GalleryCode));
            GUI.Label(new Rect(25, 90, 820, 24), "PROCHAINE ÉTAPE : " + CurrentObjective);
            GUI.Label(new Rect(25, 115, 820, 45), Feedback);
            GUI.matrix = headerMatrix;
            if (Time.unscaledTime < announcementUntil) GUI.Box(new Rect(12, 178, 850, 35), announcement);
            if (RuntimeOutlineSelectionManager.ActiveInteractable is FractureStation selected)
                GUI.Box(new Rect(12, height - 64, 850, 44), "Interagir : " + selected.label + selected.SelectionSuffix);
            var help = GetNearbyHelpStation(Player.transform.position);
            if (help != null && AcceptsInput)
            {
                var style = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16, richText = false };
                string text = help.label + "\n\n" + help.GetPrototypeHelp() +
                    "\n\nPour Interagir : approchez le bloc et regardez-le jusqu'à voir son invite.";
                const float panelWidth = 480;
                while (style.fontSize > 12 && style.CalcHeight(new GUIContent(text), panelWidth - 28) > height - 326)
                    style.fontSize--;
                float panelHeight = style.CalcHeight(new GUIContent(text), panelWidth - 28) + 28;
                var rect = new Rect(width - panelWidth - 12, 222, panelWidth, panelHeight);
                GUI.Box(rect, GUIContent.none);
                GUI.Label(new Rect(rect.x + 14, rect.y + 14, rect.width - 28, rect.height - 28), text, style);
            }
            GUI.matrix = previousMatrix;
        }

        public FractureStation GetNearbyHelpStation(Vector3 position)
        {
            if (!showPuzzleHelp || helpStations == null) return null;
            FractureStation nearest = null;
            float best = puzzleHelpDistance * puzzleHelpDistance;
            foreach (var station in helpStations)
            {
                if (station == null || !station.isActiveAndEnabled || station.labyrinth != this ||
                    Mathf.Abs(station.transform.position.y - position.y) >= 2f) continue;
                float distance = (station.transform.position - position).sqrMagnitude;
                if (distance > best) continue;
                nearest = station;
                best = distance;
            }
            if (RuntimeOutlineSelectionManager.ActiveInteractable is FractureStation selected &&
                selected.labyrinth == this && selected.isActiveAndEnabled &&
                (selected.transform.position - position).sqrMagnitude <= puzzleHelpDistance * puzzleHelpDistance)
                return selected;
            return nearest;
        }
    }
}
