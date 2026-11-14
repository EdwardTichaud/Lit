using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class BootstrapDistrictPlayProbe
{
    private const string ChestProbe = "Lit.Validation.MaisonChest";
    [InitializeOnLoadMethod]
    private static void BindMaisonChestProbe() => EditorApplication.update += TickMaisonChestProbe;

    private static void TickMaisonChestProbe()
    {
        const string request = "Library/MaisonChestPlay.request";
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            string.IsNullOrEmpty(SessionState.GetString(Key, "")) && File.Exists(request))
        {
            File.Delete(request);
            // Reuse the existing isolated Maison startup, then divert before Enola's entry.
            Begin(false, enola: true);
            SessionState.SetString(ChestProbe, SessionState.GetString(Key, ""));
            return;
        }
        if (!EditorApplication.isPlaying || report == null || report.run != SessionState.GetString(ChestProbe, "") || report.status != "Playing") return;
        try
        {
            if (report.stage == 600)
            {
                report.scope = "Isolated Bootstrap/Maison, production chest selector and gamepad A/B. UCC approach fixture; no inventory mutation or multiplayer test.";
                var actor = LocalPlayerUtils.GetControlledCharacter().GetComponent<SquadCharacterController>();
                var chest = GameObject.FindWithTag("MaisonChest").GetComponent<InteractableItem>();
                var collider = chest.GetInteractionDetectionCollider();
                Vector3 anchor = chest.GetInteractionAnchor().position;
                foreach (float distance in new[] { 1.35f, 1.15f, 1.55f })
                for (int i = 0; i < 12; i++)
                {
                    Vector3 direction = Quaternion.Euler(0, i * 30, 0) * chest.transform.forward;
                    direction.y = 0; direction.Normalize();
                    Vector3 sample = anchor + direction * distance;
                    if (!Physics.Raycast(sample + Vector3.up * .8f, Vector3.down, out var floor, 2, ~0, QueryTriggerInteraction.Ignore) ||
                        floor.normal.y < .75f || Mathf.Abs(floor.point.y - anchor.y) > .65f) continue;
                    if (Physics.OverlapCapsule(floor.point + Vector3.up * .37f, floor.point + Vector3.up * 1.55f, .28f, ~0, QueryTriggerInteraction.Ignore)
                        .Any(c => !c.transform.IsChildOf(actor.transform))) continue;
                    if (!CharacterInteractionDetection.IsInteractionTargetVisibleFromCharacter(chest, collider, chest.GetInteractionAnchor(), floor.point + Vector3.up, actor.transform)) continue;
                    Check(actor.TrySetUccExternalPositionAndRotation(floor.point + Vector3.up * .04f, Quaternion.LookRotation(-direction), true), "Approached the authored chest on clear floor.");
                    Physics.SyncTransforms(); enolaPad = InputSystem.AddDevice<Gamepad>(); Advance(620); return;
                }
                throw new InvalidOperationException("No clear standing position found in reach of the authored Maison chest.");
            }
            if (report.stage == 620 && EditorApplication.timeSinceStartup - report.stageStarted > .5 && ControlReady(out _))
            {
                var chest = GameObject.FindWithTag("MaisonChest").GetComponent<InteractableItem>();
                Check(RuntimeOutlineSelectionManager.IsActiveInteractable(chest) && RuntimeOutlineSelectionManager.SelectedTargets.Count == 1,
                    "Actual detector selects the root container and its visible outline.");
                bool dispatched = false;
                void Observe(UnityEngine.InputSystem.InputAction.CallbackContext _) => dispatched = true;
                LocalInputRouter.Interact += Observe;
                try { PulseEnola(GamepadButton.South); }
                finally { LocalInputRouter.Interact -= Observe; }
                Check((bool)typeof(InteractableItem).GetField("lootOpen", Private).GetValue(chest),
                    $"A opens the original storage UI (dispatched={dispatched}, mode={MainMenuInputSettings.GetCurrentMode()}, focus={InputFocusStack.HasAnyFocus()}, character={typeof(InteractableItem).GetField("currentCharacter", Private).GetValue(chest)}, settings={LootUISettings.Instance}, locked={chest.isLocked}).");
                Advance(621);
            }
            else if (report.stage == 621 && EditorApplication.timeSinceStartup - report.stageStarted > .4)
            {
                var chest = GameObject.FindWithTag("MaisonChest").GetComponent<InteractableItem>();
                PulseEnola(GamepadButton.East);
                Check(!(bool)typeof(InteractableItem).GetField("lootOpen", Private).GetValue(chest), "B closes the storage UI.");
                Finish(true, "Maison chest selection, outline, open and close verified.");
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
}
