using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class CombatBootstrapPlayValidation
{
    private static bool ZoneRoundTrip => Environment.GetCommandLineArgs().Contains("-combatMigrationZoneRoundTrip");
    private static int zoneTransferStage, zoneTransferHealth, zoneTransferGeneration;
    private static float zoneTransferStarted;
    private static string zoneTransferActorId, zoneTransferTeam;
    private static Pose zoneTransferArrival;
    private static ZoneManifest zoneTransferDestination;

    private static void ResetZoneTransferProbe()
    {
        zoneTransferStage = 0;
        zoneTransferDestination = null;
    }

    private static bool ValidateZoneTransfer(GameFlowService flow, RealTimeCombatManager manager, SquadCharacterController actor)
    {
        if (zoneTransferStage == 5) return true;
        if (zoneTransferStage > 0 && Time.realtimeSinceStartup - zoneTransferStarted > 90)
            throw new TimeoutException("Zone transfer stage=" + zoneTransferStage + " transitioning=" + flow.IsTransitioning +
                " scene=" + SceneManager.GetActiveScene().name + " navigation=" + NavMeshWorldService.Instance?.State);
        if (zoneTransferStage == 0)
        {
            zoneTransferDestination = AssetDatabase.LoadAssetAtPath<ZoneManifest>(DefaultManifestPath);
            var portals = UnityEngine.Object.FindObjectsByType<PortalController>().Where(portal => portal.IsSceneTransition &&
                new SerializedObject(portal).FindProperty("destinationZone").objectReferenceValue == zoneTransferDestination).ToArray();
            Require(portals.Length == 1, "Maison contains one authored portal to the actual District 1 manifest");
            var portal = portals[0];
            var poses = (List<Pose>)typeof(PortalController).GetMethod("CaptureDestinationPoses", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(portal, null);
            Require(poses.Count > 0, "The actual portal supplies its authored destination poses");
            zoneTransferArrival = poses[0];
            Require(TryFindPortalFixture(actor, portal, out var near), "A clear physical interaction fixture exists near the authored portal");
            Require(actor.TrySetUccExternalPositionAndRotation(near, actor.transform.rotation, true),
                "Only Lucian approaches the actual portal through the existing UCC placement API");
            Physics.SyncTransforms();
            zoneTransferActorId = actor.GetEntityId().ToString();
            zoneTransferHealth = Mathf.Max(1, actor.MaxHp - 9);
            actor.SetCurrentHp(zoneTransferHealth);
            zoneTransferTeam = TeamState();
            Require(portal.ServerTryUse(actor.gameObject, out _, out _), "Actual portal accepts solo interaction and starts normal zone travel");
            AdvanceZoneTransfer(1);
            return false;
        }
        if (flow.IsTransitioning) return false;
        if (zoneTransferStage == 1)
        {
            ValidateZoneArrival(manager, actor, zoneTransferDestination, AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath));
            Require(Vector3.ProjectOnPlane(actor.transform.position - zoneTransferArrival.position, Vector3.up).magnitude < .25f &&
                Mathf.Abs(actor.transform.position.y - zoneTransferArrival.position.y) < .5f,
                "District arrival uses the portal's captured destination, not a replacement spawn: " + actor.transform.position);
            StartTransferBasic(manager, actor);
            AdvanceZoneTransfer(2);
            return false;
        }
        if (zoneTransferStage == 2)
        {
            if (TransferBasicPending(actor)) return false;
            if (!GamepadProbe || probeInputStage == 0)
                Require(!actor.GetComponent<PlayerActionPresentationController>().IsActionActive, "District Basic completes without an orphan action");
            if (GamepadProbe && !ValidateGamepadProbe(manager, actor)) return false;
            Require(GameFlowService.ReturnToHub("Default"), "Existing return-to-hub API accepts the authored Maison spawn ID");
            AdvanceZoneTransfer(3);
            return false;
        }
        if (zoneTransferStage == 3)
        {
            var maison = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
            ValidateZoneArrival(manager, actor, maison, zoneTransferDestination);
            var spawn = ZoneSpawnPoint.FindFirstInScene(maison.PrimarySceneName);
            Require(spawn != null && Vector3.ProjectOnPlane(actor.transform.position - spawn.transform.position, Vector3.up).magnitude < .25f &&
                Mathf.Abs(actor.transform.position.y - spawn.transform.position.y) < .5f,
                "Return places the same actor at the existing authored Maison spawn");
            StartTransferBasic(manager, actor);
            AdvanceZoneTransfer(4);
            return false;
        }
        if (TransferBasicPending(actor)) return false;
        if (!GamepadProbe || probeInputStage == 0)
            Require(!actor.GetComponent<PlayerActionPresentationController>().IsActionActive, "Maison Basic completes after the full zone roundtrip");
        if (GamepadProbe && !ValidateGamepadProbe(manager, actor)) return false;
        ValidateTransferActor(manager, actor);
        zoneTransferStage = 5;
        return true;
    }

    private static void AdvanceZoneTransfer(int next)
    {
        zoneTransferStage = next;
        zoneTransferStarted = Time.realtimeSinceStartup;
    }

    private static void StartTransferBasic(RealTimeCombatManager manager, SquadCharacterController actor)
    {
        var basic = actor.CharacterData.groundBasicSkills?.FirstOrDefault(skill => skill != null);
        Require(basic != null && manager.TryUseSkill(basic), "Common combat facade accepts an authored Basic after zone transfer");
        zoneTransferGeneration = actor.GetComponent<PlayerActionPresentationController>().ActionGeneration;
        ResetGamepadProbeCycle();
    }

    private static bool TransferBasicPending(SquadCharacterController actor)
    {
        if (!actor.GetComponent<PlayerActionPresentationController>().IsCurrentSession(zoneTransferGeneration)) return false;
        if (Time.realtimeSinceStartup - zoneTransferStarted > 8)
            throw new TimeoutException("Basic retained its session after zone transfer: " +
                DescribeAction(actor.GetComponent<PlayerActionPresentationController>()));
        return true;
    }

    private static void ValidateZoneArrival(RealTimeCombatManager manager, SquadCharacterController actor, ZoneManifest current, ZoneManifest previous)
    {
        foreach (string scene in current.LoadingSceneNames.Concat(new[] { current.PrimarySceneName }))
            Require(SceneManager.GetSceneByName(scene).isLoaded, "Transfer loaded required scene: " + scene);
        foreach (string scene in previous.LoadingSceneNames.Concat(new[] { previous.PrimarySceneName }).Except(current.LoadingSceneNames))
            Require(!SceneManager.GetSceneByName(scene).isLoaded, "Solo transfer unloaded previous scene: " + scene);
        var world = NavMeshWorldService.Instance;
        Require(world != null && world.IsReady && world.LastReport.zoneId == current.PrimarySceneName,
            "Navigation is Ready for the actual current zone, not a residual previous world");
        ValidateTransferActor(manager, actor);
    }

    private static void ValidateTransferActor(RealTimeCombatManager manager, SquadCharacterController actor)
    {
        Require(actor != null && actor.GetEntityId().ToString() == zoneTransferActorId && manager.PlayerRoot == actor.transform,
            "Zone transfer preserves the same controlled actor and combat binding");
        Require(actor.CurrentHp == zoneTransferHealth && TeamState() == zoneTransferTeam, "Health and existing companion progression survive zone transfer");
        Require(UnityEngine.Object.FindObjectsByType<CombatWorldRuntime>().Length == 1 &&
            UnityEngine.Object.FindObjectsByType<SquadCharacterController>().Count(candidate => candidate.CharacterData == actor.CharacterData) == 1,
            "One common combat world and one controlled-character instance remain");
        Require(actor.IsGrounded && !actor.GetComponent<LitOpsiveLocomotionBridge>().IsInputSuppressedByUcc &&
            actor.GetComponent<LitUccInteractionBridge>().CanEvaluateLitInteractions &&
            InputModeCoordinator.CurrentMode == InputMode.Exploration && !InputFocusStack.HasAnyFocus() && !manager.HasLockedCombatTarget,
            "Grounding, exploration input and interaction evaluation are restored without a camera lock");
    }

    private static bool TryFindPortalFixture(SquadCharacterController actor, PortalController portal, out Vector3 position)
    {
        var anchor = portal.GetInteractionAnchor();
        foreach (var direction in new[] { anchor.forward, -anchor.forward, anchor.right, -anchor.right })
        foreach (float distance in new[] { 1f, 1.5f, 2f })
        {
            Vector3 expected = anchor.position + Vector3.ProjectOnPlane(direction, Vector3.up).normalized * distance;
            foreach (var hit in Physics.RaycastAll(expected + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
            {
                if (hit.transform.GetComponentInParent<SquadCharacterController>() != null || hit.normal.y < .7f ||
                    Mathf.Abs(hit.point.y - actor.transform.position.y) > .75f) continue;
                Vector3 candidate = hit.point + Vector3.up * .02f;
                if (Physics.OverlapCapsule(candidate + Vector3.up * .3f, candidate + Vector3.up * 1.5f, .25f,
                    ~0, QueryTriggerInteraction.Ignore).Any(collider => !collider.transform.IsChildOf(actor.transform))) continue;
                position = candidate;
                return true;
            }
        }
        position = default;
        return false;
    }
}
