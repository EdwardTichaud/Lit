using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    public static void InspectMoonriseWalkNavigationBatch()
    {
        var output = new StringBuilder();
        foreach (string scene in new[] { "District_1_Core", "District_1_Rooms_Environment", "District_1_Corridor_Environment", "District_1_Interactives" })
            EditorSceneManager.OpenScene("Assets/Scenes/District_1/" + scene + ".unity", scene == "District_1_Core" ? OpenSceneMode.Single : OpenSceneMode.Additive);
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
        var serialized = new SerializedObject(manifest);
        var data = serialized.FindProperty("bakedNavMeshData").objectReferenceValue as NavMeshData;
        var instance = NavMesh.AddNavMeshData(data);
        try
        {
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            Vector3 from = new(-1.42f, -93.97f, 55.15f), to = new(-4.69f, -94.29f, 44.10f);
            NavMesh.SamplePosition(from, out var start, 1f, filter);
            NavMesh.SamplePosition(to, out var end, 1f, filter);
            var path = new NavMeshPath();
            NavMesh.CalculatePath(start.position, end.position, filter, path);
            output.AppendLine($"start={start.position} end={end.position} status={path.status} corners=" + string.Join(";", path.corners.Select(p => p.ToString())));
            NavMesh.Raycast(start.position, end.position, out var edge, filter);
            output.AppendLine("direct nav edge=" + edge.position + " normal=" + edge.normal);
            Physics.SyncTransforms();
            foreach (var point in new[] { from, path.corners.LastOrDefault(), edge.position })
            {
                output.AppendLine("\nAT " + point);
                foreach (var collider in Physics.OverlapSphere(point + Vector3.up * .8f, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                    output.AppendLine($"near={collider.gameObject.scene.name}/{collider.name} bounds={collider.bounds} root={collider.transform.root.name}");
            }
            foreach (var hit in Physics.RaycastAll(from + Vector3.up, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
                output.AppendLine($"direct ray={hit.collider.gameObject.scene.name}/{hit.collider.name} point={hit.point}");
            foreach (var door in Object.FindObjectsByType<Door>(FindObjectsInactive.Include))
                if (Vector3.Distance(door.transform.position, from) < 15)
                    output.AppendLine($"door={door.name} position={door.transform.position} scene={door.gameObject.scene.name}");
            Vector3 candidate = new(-9f, -94f, 43f);
            bool sampled = NavMesh.SamplePosition(candidate, out var candidateHit, .4f, filter);
            output.AppendLine($"\nEncounter candidate={candidate} sampled={sampled} nav={candidateHit.position}");
            var blockers = Physics.OverlapCapsule(candidate + Vector3.up * .6f, candidate + Vector3.up * 1.4f,
                .5f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var blocker in blockers) output.AppendLine("candidate blocker=" + blocker.name);
            if (!sampled || blockers.Length != 0)
                throw new InvalidOperationException("The proposed encounter position is not a clear navigable capsule.");
            Directory.CreateDirectory("Library/MoonriseRoute");
            File.WriteAllText("Library/MoonriseRoute/navigation-inspection.txt", output.ToString());
        }
        finally { instance.Remove(); }
        EditorApplication.Exit(0);
    }

    private static Vector3 moonriseWalkGoal, moonriseWalkStart;
    private static float moonriseWalkStarted, moonriseWalkSubmitAt;
    private static Flame moonriseWalkingFlame;
    private static Flame moonriseCorridorFlame;
    private static Door moonriseRoomDoor;
    private static Vector3 moonriseRoomThreshold;

    private static void BeginMoonriseWalkingApproach(SquadCharacterController actor, Vector3 anchor, int stage)
    {
        var agent = moonriseEnemy.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(actor.transform.position, out var start, 1f, filter))
            throw new InvalidOperationException("No player NavMesh sample for the continuous approach: " + actor.transform.position);
        var path = new NavMeshPath();
        float bestLength = float.PositiveInfinity;
        float radius = stage == 276 ? .65f : 1.15f;
        // A Flame's anchor is its elevated Light, whereas locomotion queries
        // must sample the walkable floor, not the air beside that light.
        float groundHeight = stage == 275 || stage == 279 ? actor.transform.position.y : anchor.y;
        var rejected = new StringBuilder($"anchor={anchor} start={start.position} agentType={agent.agentTypeID}\n");
        for (int angle = 0; angle < 360; angle += 30)
        {
            Vector3 expected = anchor + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * radius;
            expected.y = groundHeight;
            if (!NavMesh.SamplePosition(expected, out var hit, .75f, filter))
            { rejected.AppendLine("No floor sample: " + expected); continue; }
            if (Mathf.Abs(hit.position.y - groundHeight) > 1f ||
                !NavMesh.CalculatePath(start.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
            { rejected.AppendLine($"Rejected floor={hit.position} path={path.status}"); continue; }
            float length = 0;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            if (length >= bestLength) continue;
            bestLength = length; moonriseWalkGoal = hit.position;
        }
        File.WriteAllText(Path.Combine(report.run, "walk-route-candidates-" + stage + ".txt"), rejected.ToString());
        Check(!float.IsInfinity(bestLength), "A complete authored NavMesh route exists to " +
            (stage == 275 || stage == 279 ? moonriseWalkingFlame.name : stage == 277 ? moonriseRoomDoor.name : "the post-victory ladder") + ".");
        moonriseWalkStart = actor.transform.position;
        moonriseWalkStarted = Time.realtimeSinceStartup;
        moonriseWalkSubmitAt = -1;
        File.AppendAllText(Path.Combine(report.run, "walked-approaches.txt"),
            $"stage={stage} from={moonriseWalkStart} goal={moonriseWalkGoal} pathLength={bestLength:F2}\n");
        Advance(stage);
    }

    private static void TickMoonriseWalkingApproach(SquadCharacterController actor)
    {
        float elapsed = Time.realtimeSinceStartup - moonriseWalkStarted;
        if (elapsed > 45)
            throw new TimeoutException("Continuous approach failed; " + MoonriseDiagnostic(actor, RealTimeCombatManager.Instance) +
                " goal=" + moonriseWalkGoal + " selected=" + (RuntimeOutlineSelectionManager.ActiveInteractable as Component)?.name);
        if (report.stage == 275 && moonriseWalkingFlame == moonriseCorridorFlame && !moonriseWalkingFlame.IsEffectivelyLit &&
            RealTimeCombatManager.Instance.IsCombatActive && moonriseEnemy.IsEngagedWith(actor.transform))
        {
            var contacts = moonriseEnemy.GetComponentsInChildren<Collider>().Where(collider => !collider.isTrigger).ToArray();
            var source = Flame.ActiveInfluenceFlames.FirstOrDefault(flame => flame != null && contacts.Any(flame.ProvidesEnemyActivationTo));
            Check(source != null, "Natural corridor acquisition occurs under an actual active fixed-Flame source: " + source?.FlameId);
            moonriseFlame = source;
            RecordMoonriseWalk(actor);
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
            Check(true, "The real route engages Juggernaut naturally before the corridor Flame; combat takes priority over world interaction, without an acquisition warp.");
            Advance(262); return;
        }
        if (report.stage == 279 && moonriseWalkingFlame.IsEffectivelyLit)
        {
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
            Check(moonriseTorchBefore - actor.FlameSecondsRemaining == 10 && moonriseWalkingFlame.TorchPointCostToLight == 10,
                "After victory, gamepad A lights the actual common Flame for exactly 10 torch points.");
            RecordMoonriseWalk(actor);
            BeginMoonriseWalkingApproach(actor, moonriseLadder.leafs1.position, 276);
            return;
        }
        if (report.stage == 275 && moonriseWalkingFlame.IsEffectivelyLit)
        {
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
            Check(true, "Lucian walks to " + moonriseWalkingFlame.name + " and lights the selected authored Flame with gamepad A; no approach warp or direct interaction handler.");
            RecordMoonriseWalk(actor);
            if (moonriseWalkingFlame != moonriseFlame)
            {
                Check(moonriseTorchBefore - actor.FlameSecondsRemaining == 20 && moonriseWalkingFlame.TorchPointCostToLight == 20,
                    "Maelle's Ancient Flame costs exactly 20 torch points on the real route.");
                if (MoonriseLucRouteRequested) BeginMoonriseLucPrologue(actor);
                else BeginMoonriseRoomDoorApproach(actor);
                return;
            }
            Advance(261); return;
        }
        if (report.stage == 277 && moonriseRoomDoor.IsOpen)
        {
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
            Check(true, "Gamepad A opens the actual Maelle door after its Ancient Flame reveals it.");
            RecordMoonriseWalk(actor);
            moonriseWalkGoal = moonriseRoomThreshold;
            moonriseWalkStart = actor.transform.position;
            moonriseWalkStarted = Time.realtimeSinceStartup;
            Advance(278); return;
        }
        Vector3 delta = Vector3.ProjectOnPlane(moonriseWalkGoal - actor.transform.position, Vector3.up);
        if (report.stage == 278 && delta.magnitude < .25f && actor.IsGrounded)
        {
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
            Check(true, "Lucian physically crosses the open room doorway using the UCC motor, without a teleport or collision bypass.");
            RecordMoonriseWalk(actor);
            moonriseWalkingFlame = moonriseFlame;
            BeginMoonriseWalkingApproach(actor, moonriseFlame.GetInteractionAnchor().position, 275);
            return;
        }
        if (report.stage == 276 && delta.magnitude < .25f && actor.IsGrounded &&
            Vector3.Distance(actor.transform.position, moonriseLadder.leafs1.position) < moonriseLadder.activationRadius)
        {
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
            Check(true, "After victory Lucian reaches the actual ladder through camera-relative gamepad movement, without repositioning.");
            RecordMoonriseWalk(actor);
            EquipMoonriseExplorationTorch(); return;
        }
        var input = new GamepadState();
        bool interact = (report.stage == 275 || report.stage == 279) && moonriseWalkingFlame.IsCharacterWithinInteractionDistance(actor.transform) &&
            ReferenceEquals(RuntimeOutlineSelectionManager.ActiveInteractable, moonriseWalkingFlame) ||
            report.stage == 277 && ReferenceEquals(RuntimeOutlineSelectionManager.ActiveInteractable, moonriseRoomDoor);
        if (interact)
        {
            if (moonriseWalkSubmitAt < 0) moonriseWalkSubmitAt = Time.realtimeSinceStartup;
            if ((Time.realtimeSinceStartup - moonriseWalkSubmitAt) % .7f < .12f) input = input.WithButton(GamepadButton.South);
        }
        else if (delta.magnitude > .18f)
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No gameplay camera for the continuous approach.");
            var agent = moonriseEnemy.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 direction = delta.normalized;
            if (NavMesh.SamplePosition(actor.transform.position, out var from, .75f, filter))
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(from.position, moonriseWalkGoal, filter, path) && path.status == NavMeshPathStatus.PathComplete)
                    foreach (var corner in path.corners.Skip(1))
                    {
                        Vector3 step = Vector3.ProjectOnPlane(corner - actor.transform.position, Vector3.up);
                        if (step.magnitude <= .25f) continue;
                        direction = step.normalized; break;
                    }
            }
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            input.leftStick = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)) * (delta.magnitude < .7f ? .4f : .75f);
        }
        InputSystem.QueueStateEvent(moonrisePad, input);
    }

    private static void BeginMoonriseRoomDoorApproach(SquadCharacterController actor)
    {
        moonriseTorchBefore = actor.FlameSecondsRemaining;
        BeginMoonriseWalkingApproach(actor, moonriseRoomDoor.GetInteractionAnchor().position, 277);
        // Capture the closed slab's centre/normal before its visual moves.
        Bounds slab = moonriseRoomDoor.GetInteractionDetectionCollider().bounds;
        Vector3 normal = slab.size.x < slab.size.z ? Vector3.right : Vector3.forward;
        float side = Mathf.Sign(Vector3.Dot(actor.transform.position - slab.center, normal));
        moonriseRoomThreshold = slab.center - normal * side * 1.5f;
        moonriseRoomThreshold.y = actor.transform.position.y;
    }

    private static void RecordMoonriseWalk(SquadCharacterController actor) =>
        File.AppendAllText(Path.Combine(report.run, "walked-approaches.txt"),
            $"stage={report.stage} arrived={actor.transform.position} realSeconds={Time.realtimeSinceStartup - moonriseWalkStarted:F2} directDisplacement={Vector3.Distance(actor.transform.position, moonriseWalkStart):F2}\n");

    private static void DriveMoonriseUnarmedApproach(SquadCharacterController actor)
    {
        bool known = moonriseEnemy.IsEngagedWith(actor.transform);
        // Initially walk into the actual vision cone. Stopping at melee range
        // on an unseen flank would test an idle bot rather than acquisition.
        Vector3 goal = moonriseEnemy.Root.position + (known ? Vector3.zero : moonriseEnemy.Root.forward * 2.5f);
        Vector3 delta = Vector3.ProjectOnPlane(goal - actor.transform.position, Vector3.up);
        var input = new GamepadState();
        var camera = Camera.main;
        if (camera != null && delta.magnitude > (known ? 1.8f : .3f))
        {
            Vector3 direction = MoonriseApproachDirection(actor, delta, goal);
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            input.leftStick = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)) * .5f;
        }
        InputSystem.QueueStateEvent(moonrisePad, input);
    }

    private static void WriteMoonriseVisibilityDiagnostic(SquadCharacterController actor, string label)
    {
        var camera = Camera.main;
        var mode = Object.FindAnyObjectByType<LitGameplayCameraModeController>();
        if (camera == null || mode == null) return;
        var mask = typeof(LitGameplayCameraModeController).GetField("visibilityMask", Private).GetValue(mode) as LitTacticalVisibilityMask;
        var output = new StringBuilder(MoonriseCameraDiagnostic());
        output.AppendLine("\nrenderedAnchor=" + mode.GetRenderedAnchor() + " ground=" + mode.GetRenderedGroundHeight());
        var renderers = Object.FindObjectsByType<MeshRenderer>();
        foreach (var pair in new[] { (name: "player", root: actor.transform), (name: "enemy", root: moonriseEnemy.Root) })
        foreach (float height in new[] { .25f, 1f, 1.7f })
        {
            Vector3 target = pair.root.position + Vector3.up * height;
            Vector3 delta = target - camera.transform.position;
            var ray = new Ray(camera.transform.position, delta.normalized);
            output.AppendLine($"\n{pair.name} height={height} target={target} viewport={camera.WorldToViewportPoint(target)}");
            foreach (var hit in Physics.RaycastAll(ray, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
            {
                if (hit.transform.IsChildOf(actor.transform) || hit.transform.IsChildOf(moonriseEnemy.Root)) continue;
                var group = hit.collider.GetComponentInParent<LitCameraOcclusionGroup>();
                output.AppendLine($"physics={hit.collider.gameObject.scene.name}/{hit.collider.name} at={hit.point} group={group?.name} covered={group != null && mask != null && mask.Covers(group, hit.point)}");
            }
            foreach (var renderer in renderers.Where(r => !r.transform.IsChildOf(actor.transform) && !r.transform.IsChildOf(moonriseEnemy.Root) &&
                         r.bounds.IntersectRay(ray, out float distance) && distance < delta.magnitude).OrderBy(r => Vector3.Distance(r.bounds.ClosestPoint(camera.transform.position), camera.transform.position)).Take(20))
            {
                var group = renderer.GetComponentInParent<LitCameraOcclusionGroup>();
                output.AppendLine($"bounds={renderer.gameObject.scene.name}/{renderer.name} group={group?.name} compatible={group != null && group.IsMaskCompatible(out _)} materials=" +
                    string.Join(";", renderer.sharedMaterials.Select(m => m != null ? m.name + ":" + m.shader.name : "null")));
            }
        }
        File.WriteAllText(Path.Combine(report.run, "visibility-" + label + ".txt"), output.ToString());
    }
}
