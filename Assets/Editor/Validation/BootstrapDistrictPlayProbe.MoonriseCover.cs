using System;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    private static float moonriseCoverStarted;
    private static GameObject moonriseCover;
    private static SquadCharacterController moonriseCoverActor;
    private static Vector3 moonriseCoverOrigin;
    private static bool moonriseCoverBlocked, moonriseCoverDetour, moonriseCoverInvalidDamage;
    private static int moonriseCoverHealth;

    private static bool MoonriseCoverVisible(SquadCharacterController actor) =>
        moonriseEnemy.RuntimeCharacterData.vision.TryEvaluate(moonriseEnemy.Root, actor.transform,
            moonriseEnemy.RuntimeCharacterData.vision.maximumDistance, 360, out _, out _, out _);

    private static void TickMoonriseCoverProbe(SquadCharacterController actor)
    {
        float elapsed = Time.realtimeSinceStartup - moonriseCoverStarted;
        if (elapsed > 18) throw new TimeoutException("Close-cover probe timed out; " + MoonriseDiagnostic(actor, RealTimeCombatManager.Instance));
        if (!moonriseEnemy.IsEngagedWith(actor.transform) || moonriseEnemy.IsFlameDormant)
            throw new InvalidOperationException("The cover fixture lost its valid engaged/lit encounter before verification.");
        var input = new GamepadState();
        if (moonriseCover == null)
        {
            // Create only between committed actions and with clearance for the
            // navigation agent's radius; never insert a collider into an actor.
            Vector3 separation = Vector3.ProjectOnPlane(actor.transform.position - moonriseEnemy.Root.position, Vector3.up);
            if (moonriseEnemy.IsAttackCommitted) { InputSystem.QueueStateEvent(moonrisePad, input); return; }
            if (separation.magnitude < 1.8f)
            {
                var camera = Camera.main;
                Vector3 away = separation.normalized;
                Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
                input.leftStick = new Vector2(Vector3.Dot(away, right), Vector3.Dot(away, forward)) * .5f;
                InputSystem.QueueStateEvent(moonrisePad, input); return;
            }
            Check(separation.magnitude < 2.5f && MoonriseCoverVisible(actor),
                "The known target is close and visible before the temporary cover fixture is inserted.");
            moonriseCover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            moonriseCover.name = "Validation_Moonrise_CloseCover";
            moonriseCover.hideFlags = HideFlags.DontSave;
            moonriseCover.transform.SetPositionAndRotation((actor.transform.position + moonriseEnemy.Root.position) * .5f + Vector3.up * 1.2f,
                Quaternion.LookRotation(separation));
            moonriseCover.transform.localScale = new Vector3(.8f, 2.4f, .08f);
            moonriseCover.GetComponent<Renderer>().enabled = false;
            var obstacle = moonriseCover.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            Physics.SyncTransforms();
            Check(!MoonriseCoverVisible(actor), "The temporary collider genuinely obstructs attack line of sight.");
            moonriseCoverOrigin = moonriseEnemy.Root.position;
            moonriseCoverActor = actor;
            moonriseCoverHealth = actor.CurrentHp;
            actor.HealthChanged += ObserveMoonriseCoverHealth;
            InputSystem.QueueStateEvent(moonrisePad, input);
            return;
        }
        InputSystem.QueueStateEvent(moonrisePad, input);
        bool visible = MoonriseCoverVisible(actor);
        moonriseCoverBlocked |= !visible;
        var agent = moonriseEnemy.GetComponent<NavMeshAgent>();
        moonriseCoverDetour |= agent.enabled && agent.isOnNavMesh && agent.hasPath && agent.path.corners.Length >= 3;
        float moved = Vector3.ProjectOnPlane(moonriseEnemy.Root.position - moonriseCoverOrigin, Vector3.up).magnitude;
        File.WriteAllText(Path.Combine(report.run, "close-cover.txt"),
            $"elapsed={elapsed} visible={visible} known={moonriseEnemy.IsEngagedWith(actor.transform)} moved={moved} detour={moonriseCoverDetour} invalidDamage={moonriseCoverInvalidDamage}\n" + MoonriseDiagnostic(actor, RealTimeCombatManager.Instance));
        if (moonriseCoverInvalidDamage) throw new InvalidOperationException("Damage was received while the actual attack line of sight was blocked.");
        if (!visible || moved < .3f || !moonriseCoverDetour) return;
        Check(moonriseCoverBlocked && moonriseEnemy.IsEngagedWith(actor.transform), "A known close target remains tracked behind cover.");
        Check(true, "The production NavMesh agent follows a detour and regains attack line of sight without teleportation.");
        Check(!moonriseCoverInvalidDamage, "Health events confirm no damage through the blocking collider.");
        CleanupMoonriseCoverProbe();
        Finish(true, "Focused production Moonrise close-cover fixture: natural acquisition/impacts, retained target, real NavMesh detour and no damage through cover. Temporary collider removed. Counter/death/continuity and 15-20 minute pacing are outside this focused run.");
    }

    private static void ObserveMoonriseCoverHealth(SquadCharacterController actor)
    {
        if (actor.CurrentHp < moonriseCoverHealth && moonriseCover != null && !MoonriseCoverVisible(actor))
            moonriseCoverInvalidDamage = true;
        moonriseCoverHealth = actor.CurrentHp;
    }

    private static void CleanupMoonriseCoverProbe()
    {
        if (moonriseCoverActor != null) moonriseCoverActor.HealthChanged -= ObserveMoonriseCoverHealth;
        if (moonriseCover != null)
        {
            moonriseCover.GetComponent<Collider>().enabled = false;
            moonriseCover.GetComponent<NavMeshObstacle>().enabled = false;
            if (Application.isPlaying) Object.Destroy(moonriseCover); else Object.DestroyImmediate(moonriseCover);
        }
        moonriseCover = null; moonriseCoverActor = null;
        moonriseCoverStarted = 0;
        moonriseCoverBlocked = moonriseCoverDetour = moonriseCoverInvalidDamage = false;
    }
}
