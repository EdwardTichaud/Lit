using System.Linq;
using UnityEngine;

public static partial class BootstrapDistrictPlayProbe
{
    private static LadderController lightJumpLadder;
    private static int lightJumpSource;
    private static Vector3 lightJumpDestination;
    private static AbilitySaveData lightJumpEquipment;

    private static void TickLightJump()
    {
        if (!ControlReady(out var actor)) return;
        var abilities = AbilityService.Instance;
        var skill = abilities.Resolve(LadderController.RequiredAbilityId);
        if (report.stage == 660)
        {
            Check(abilities.HasEquipped(actor.gameObject, skill), "New solo session equips Saut de lumière.");
            lightJumpEquipment = abilities.Capture();
            var without = abilities.Capture();
            without.slots.RemoveAll(slot => slot.abilityId == LadderController.RequiredAbilityId);
            abilities.Restore(without);
            foreach (var ladder in Object.FindObjectsByType<LadderController>().OrderBy(l => l.name))
            for (int endpoint = 0; endpoint < 2; endpoint++)
            {
                Transform origin = endpoint == 0 ? ladder.leafs1 : ladder.leafs2;
                Transform target = endpoint == 0 ? ladder.leafs2 : ladder.leafs1;
                if (origin == null || target == null) continue;
                foreach (Vector3 delta in new[] { Vector3.zero, Vector3.forward * .45f, Vector3.back * .45f,
                             Vector3.left * .45f, Vector3.right * .45f })
                foreach (RaycastHit floor in Physics.RaycastAll(origin.position + delta + Vector3.up, Vector3.down,
                             2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                {
                    Vector3 start = floor.point + Vector3.up * .025f;
                    if (floor.normal.y < .7f || Vector3.Distance(start, origin.position) > ladder.activationRadius * .8f ||
                        Physics.OverlapCapsule(start + Vector3.up * .35f, start + Vector3.up * 1.5f,
                            .24f, ~0, QueryTriggerInteraction.Ignore).Any(c => !c.transform.IsChildOf(actor.transform))) continue;
                    if (!actor.TrySetUccExternalPositionAndRotation(start, origin.rotation, true)) continue;
                    lightJumpLadder = ladder;
                    lightJumpSource = endpoint;
                    lightJumpDestination = target.TransformPoint(ladder.destinationLocalOffset);
                    Advance(661); return;
                }
            }
            throw new System.InvalidOperationException("No clear authored District_1 ladder approach.");
        }
        if (report.stage == 661)
        {
            if (!lightJumpLadder.CanBeDetectedBy(actor)) return;
            lightJumpLadder.SetDetectedCharacter(actor.gameObject);
            var label = typeof(LadderController).GetMethod("ResolveInteractionText", Private);
            Check((string)label.Invoke(lightJumpLadder, null) == "Saut de lumière requis", "Visible requirement when not equipped.");
            Check(lightJumpLadder.TryHandleLocalInteract() &&
                !lightJumpLadder.ServerTryBeginPassage(actor.gameObject, lightJumpSource, out _, out _),
                "Local interaction and authoritative passage both refuse the unequipped traveller.");
            abilities.Restore(lightJumpEquipment);
            Check(((string)label.Invoke(lightJumpLadder, null)).StartsWith("Saut de lumière —"),
                "Prompt updates to a directional jump.");
            Check(lightJumpLadder.TryHandleLocalInteract(), "Local interaction starts the existing passage.");
            Advance(662); return;
        }
        if (report.stage == 662 && Vector3.Distance(actor.transform.position, lightJumpDestination) < .65f)
            Finish(true, "Real District_1 light jump refused without equipment, then teleported and released movement with equipment.");
    }
}
