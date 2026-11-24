using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lit.DreamFractures
{
    public sealed partial class DreamFractureRunController : IInputModeHandler
    {
        private bool catalogOpen, ownsFocus, abandonConfirm;
        private Vector2 catalogScroll;
        private int navigationIndex;

        public void OpenCatalog() => catalogOpen = true;
        private void CloseCatalog() => catalogOpen = false;

        private void UpdateFocus()
        {
            bool modal = catalogOpen || State.phase == DreamFracturePhase.Results;
            bool lockCountdown = State.phase == DreamFracturePhase.Countdown;
            if (!lockCountdown && countdownLocked)
            {
                InputModeCoordinator.Exit(this);
                countdownLocked = false;
            }
            if (modal && !ownsFocus)
            {
                InputFocusStack.Push(this);
                InputModeCoordinator.Enter(this, InputMode.UserInterface);
                ownsFocus = true;
            }
            else if (!modal && ownsFocus) ReleaseFocus();
            if (lockCountdown && !countdownLocked)
            {
                InputModeCoordinator.Enter(this, InputMode.Disabled);
                countdownLocked = true;
            }
        }

        public bool HandleInputModeAction(InputModeAction action, InputAction.CallbackContext context)
        {
            if (!catalogOpen && State.phase != DreamFracturePhase.Results) return false;
            if (action == InputModeAction.Cancel && catalogOpen) { CloseCatalog(); return true; }
            var visible = Catalog != null ? Catalog.fractures
                .Where(f => f != null && f.IsVisible && f.IsValid && f.available).ToArray()
                : Array.Empty<DreamFractureDefinitionSO>();
            if (action == InputModeAction.Navigate && visible.Length > 0)
            {
                float direction = context.ReadValue<Vector2>().y;
                if (Math.Abs(direction) > .5f)
                    navigationIndex = (navigationIndex + (direction < 0 ? 1 : visible.Length - 1)) % visible.Length;
                return true;
            }
            if (action != InputModeAction.Submit) return false;
            if (State.phase == DreamFracturePhase.Results) { Request("ready"); return true; }
            if (Authority && visible.Length > 0)
            {
                var candidate = visible[Mathf.Clamp(navigationIndex, 0, visible.Length - 1)];
                if (State.definitionId != candidate.id) { Request("select", candidate.id); return true; }
            }
            if (State.phase == DreamFracturePhase.Preparing) Request("ready");
            return true;
        }

        private void ReleaseFocus()
        {
            InputFocusStack.Pop(this);
            InputModeCoordinator.Exit(this);
            ownsFocus = false;
            countdownLocked = false;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (State.phase == DreamFracturePhase.Running || State.phase == DreamFracturePhase.Countdown)
            {
                var definition = Definition;
                if (definition == null) return;
                var targets = definition.TargetsFor(State.initialPlayers);
                GUILayout.BeginArea(new Rect(20, 20, 330, 145), GUI.skin.box);
                GUILayout.Label(definition.displayName);
                GUILayout.Label($"Score  {State.score} / {targets.score}");
                GUILayout.Label($"Temps  {DisplaySeconds:0.0} s / {targets.seconds:0} s");
                if (State.phase == DreamFracturePhase.Countdown)
                    GUILayout.Label($"Départ dans {Math.Ceiling(RemainingCountdown)}");
                else if (Authority)
                {
                    if (GUILayout.Button("Abandonner")) abandonConfirm = true;
                    if (abandonConfirm)
                    {
                        if (GUILayout.Button("Confirmer l’abandon")) { Request("abandon"); abandonConfirm = false; }
                        if (GUILayout.Button("Continuer")) abandonConfirm = false;
                    }
                }
                GUILayout.EndArea();
            }
            if (State.phase == DreamFracturePhase.Loading || State.phase == DreamFracturePhase.Returning)
                catalogOpen = false;
            if (State.phase == DreamFracturePhase.Preparing && !catalogOpen && !InputFocusStack.HasAnyFocus())
            {
                GUILayout.BeginArea(new Rect(20, 180, 330, 90), GUI.skin.box);
                GUILayout.Label("Une fracture onirique est proposée.");
                if (GUILayout.Button("Voir la proposition")) OpenCatalog();
                GUILayout.EndArea();
            }
            if (State.phase == DreamFracturePhase.Results) DrawResult();
            else if (catalogOpen) DrawCatalog();
        }

        private void DrawCatalog()
        {
            GUILayout.BeginArea(new Rect(Math.Max(10, (Screen.width - 620) / 2), 50, 620,
                Math.Max(200, Screen.height - 100)), GUI.skin.box);
            GUILayout.Label("Portail onirique");
            catalogScroll = GUILayout.BeginScrollView(catalogScroll);
            int players = State.phase == DreamFracturePhase.Preparing ? State.initialPlayers :
                (Online ? NetworkManager.Singleton.ConnectedClientsIds.Count : 1);
            int visible = 0;
            foreach (var definition in Catalog != null ? Catalog.fractures : Array.Empty<DreamFractureDefinitionSO>())
            {
                if (definition == null || !definition.IsVisible) continue;
                visible++;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(definition.displayName);
                GUILayout.Label(definition.description);
                if (definition.IsValid)
                {
                    var targets = definition.TargetsFor(players);
                    GUILayout.Label($"{players} joueur(s) · Score cible {targets.score} · Temps cible {targets.seconds:0} s");
                    var record = Records?.Find(definition.id, definition.rulesVersion, players);
                    if (record != null && record.hasSuccess)
                        GUILayout.Label($"Vos records · {record.bestScore} points · {record.bestSeconds:0.0} s");
                    if (Authority && definition.available)
                    {
                        if (GUILayout.Button(State.definitionId == definition.id ? "Sélectionné" : "Choisir"))
                            Request("select", definition.id);
                    }
                    else if (!definition.available) GUILayout.Label(definition.unavailableReason);
                }
                else GUILayout.Label("Configuration incomplète");
                GUILayout.EndVertical();
            }
            if (visible == 0) GUILayout.Label("Les prochaines fractures seront proposées ici.");
            GUILayout.EndScrollView();
            if (State.phase == DreamFracturePhase.Preparing)
            {
                GUILayout.Label($"Choix de l’hôte · {Definition?.displayName} · Prêts {State.ready.Length}/{State.participants.Length}");
                if (GUILayout.Button(State.ready.Contains(LocalId) ? "Retirer ma confirmation" : "Je suis prêt"))
                    Request("ready");
                if (GUILayout.Button("Annuler la proposition")) Request("cancel");
            }
            else if (!Authority) GUILayout.Label("L’hôte choisit la fracture.");
            if (!string.IsNullOrEmpty(State.message)) GUILayout.Label(State.message);
            if (GUILayout.Button("Fermer")) CloseCatalog();
            GUILayout.EndArea();
        }

        private void DrawResult()
        {
            GUILayout.BeginArea(new Rect(Math.Max(10, (Screen.width - 500) / 2), 80, 500, 370), GUI.skin.box);
            GUILayout.Label(Definition != null ? Definition.displayName : "Fracture onirique");
            var result = State.result;
            if (result != null)
            {
                GUILayout.Label(result.outcome == DreamFractureOutcome.Success ? "Défi réussi" :
                    result.outcome == DreamFractureOutcome.Abandoned ? "Tentative abandonnée" : "Défi échoué");
                GUILayout.Label($"Score · {result.score} / {result.targetScore} · {(result.scoreReached ? "atteint" : "à améliorer")}");
                GUILayout.Label($"Temps · {result.seconds:0.0} s / {result.targetSeconds:0} s · {(result.timeReached ? "atteint" : "à améliorer")}");
                if (result.hadDisconnect) GUILayout.Label("Un participant s’est déconnecté pendant la tentative.");
            }
            if (!string.IsNullOrEmpty(State.message)) GUILayout.Label(State.message);
            GUILayout.Label($"Nouvelle tentative · Prêts {State.ready.Length}");
            if (State.participants.Contains(LocalId) &&
                GUILayout.Button(State.ready.Contains(LocalId) ? "Retirer ma confirmation" : "Prêt à recommencer"))
                Request("ready");
            if (Authority && GUILayout.Button("Retour à la Maison")) Request("return");
            if (!Authority) GUILayout.Label("L’hôte peut ramener le groupe à la Maison.");
            GUILayout.EndArea();
        }
    }
}
