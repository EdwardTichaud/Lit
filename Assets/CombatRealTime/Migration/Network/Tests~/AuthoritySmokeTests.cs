using System;
using System.Reflection;
using Lit.Combat.Migration.Network;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class AuthoritySmokeTests
{
    private sealed class Target : ICombatTarget
    {
        public Transform Root { get; set; }
        public bool IsDead { get; set; }
    }
    private sealed class Executor : ICombatCommandExecutor
    {
        public bool AllowStart = true, AllowImpact = true, ThrowStart;
        public int Ends;
        public CombatActionEndReason Reason;
        public bool CanStart(CombatCommandContext context) => AllowStart;
        public bool TryStart(CombatCommandContext context) => ThrowStart ? throw new Exception("start failure") : AllowStart;
        public bool CanApplyImpact(CombatCommandContext context, NetworkObject target, int impactEvent) => AllowImpact;
        public void End(CombatCommandContext context, CombatActionEndReason reason) { Ends++; Reason = reason; }
    }
    private sealed class World : IDisposable
    {
        public readonly NetworkManager Manager;
        public readonly NavMeshWorldService WorldService;
        public readonly CombatCommandAuthority Authority;
        public readonly NetworkObject Player, Enemy, Other;
        public readonly Target EnemyTarget;
        public readonly Executor Execution = new Executor();
        public readonly ScriptableObject Skill = new ScriptableObject();
        public bool Alive = true, Assigned = true;
        public World(bool cinematic = false)
        {
            Time.unscaledTimeAsDouble = 10;
            WorldService = new NavMeshWorldService(); NavMeshWorldService.Instance = WorldService;
            var root = new GameObject();
            Manager = root.AddComponent<NetworkManager>(); NetworkManager.Singleton = Manager;
            Manager.ConnectedClients.Add(7, new object());
            Authority = root.AddComponent<CombatCommandAuthority>(); Invoke(Authority, "OnEnable");
            Player = Spawn(1, 7); Enemy = Spawn(2, 0); Other = Spawn(3, 7);
            Enemy.transform.position = new Vector3(2, 0, 0);
            Other.transform.position = new Vector3(0, 0, 1);
            EnemyTarget = new Target { Root = Enemy.transform };
            Check(Authority.RegisterActor(Player, () => Alive, Execution, id => Assigned && id == 7), "register source");
            Check(Authority.RegisterActor(Enemy, () => !EnemyTarget.IsDead), "register target");
            Check(Authority.RegisterActor(Other, () => true, Execution, id => id == 7), "register other");
            Check(Authority.RegisterSkill(Player, 1, Skill, Policy(cinematic)), "register skill");
        }
        private NetworkObject Spawn(ulong id, ulong owner)
        {
            var root = new GameObject(); root.AddComponent<SquadCharacterController>();
            var value = root.AddComponent<NetworkObject>();
            value.NetworkManager = Manager; value.NetworkObjectId = id; value.OwnerClientId = owner;
            Manager.SpawnManager.SpawnedObjects.Add(id, value); return value;
        }
        public CombatCommandRejection Execute(out CombatActionTicket ticket, ulong sequence = 1, int skill = 1,
            ulong sender = 7, NetworkObject target = null) =>
            Authority.TryExecute(sender, Player, sequence, skill, target ?? Enemy, default, out ticket);
        public void Tick(double seconds = 0) { Time.unscaledTimeAsDouble += seconds; Invoke(Authority, "Update"); }
        public void Dispose() { Invoke(Authority, "OnDisable"); NetworkManager.Singleton = null; NavMeshWorldService.Instance = null; }
    }

    private static CombatCommandPolicy Policy(bool pair = false, float cooldown = 2) =>
        new CombatCommandPolicy(CombatCommandTargetMode.Required, 0, 3, 2, cooldown, 5, pair);
    private static void Invoke(object value, string method) => value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
    private static void Is(CombatCommandRejection actual, CombatCommandRejection expected) => Check(actual == expected, $"Expected {expected}, got {actual}");

    private static int Main()
    {
        int passed = 0, failed = 0;
        void Test(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
        }
        Test("server-only authority and standalone lease", () =>
        {
            using var w = new World(); w.Manager.IsServer = false;
            Is(w.Execute(out _), CombatCommandRejection.NotServer);
            Check(!w.Authority.RegisterActor(w.Player, () => true), "client registration");
            Check(!CombatCinematicReservations.TryAcquire(w, w.Player.transform, w.EnemyTarget, out _), "client lease");
        });
        Test("ownership, assignment, allowlist, registration", () =>
        {
            using var w = new World();
            Is(w.Execute(out _, sender: 8), CombatCommandRejection.Ownership);
            w.Assigned = false; Is(w.Execute(out _), CombatCommandRejection.Assignment); w.Assigned = true;
            Is(w.Execute(out _, skill: 999), CombatCommandRejection.UnknownSkill);
            Check(!w.Authority.RegisterActor(w.Player, () => true), "duplicate registration reset");
            w.Authority.UnregisterActor(w.Player); Is(w.Execute(out _, sequence: 2), CombatCommandRejection.UnregisteredActor);
        });
        Test("range and nonfinite input", () =>
        {
            using var w = new World(); w.Enemy.transform.position = new Vector3(4, 0, 0);
            Is(w.Execute(out _), CombatCommandRejection.OutOfRange); w.Tick(.1);
            w.Enemy.transform.position = new Vector3(1, 3, 0);
            Is(w.Execute(out _, 2), CombatCommandRejection.OutOfRange); w.Tick(.1);
            Is(w.Authority.TryExecute(7, w.Player, 3, 1, w.Enemy, new Vector3(float.NaN, 0, 0), out _), CombatCommandRejection.InvalidCommand);
        });
        Test("missing target, unloaded scene, foreign identity", () =>
        {
            using var w = new World();
            Is(w.Authority.TryExecute(7, w.Player, 1, 1, null, default, out _), CombatCommandRejection.InvalidTarget); w.Tick(.1);
            w.Enemy.gameObject.scene = new Scene(2, false); Is(w.Execute(out _, 2), CombatCommandRejection.InvalidTarget); w.Tick(.1);
            w.Enemy.gameObject.scene = new Scene(1); w.Manager.SpawnManager.SpawnedObjects.Remove(2);
            Is(w.Execute(out _, 3), CombatCommandRejection.InvalidTarget);
        });
        Test("replay, rate limiting and busy actor", () =>
        {
            using var w = new World(); Is(w.Execute(out _), CombatCommandRejection.None);
            Is(w.Execute(out _), CombatCommandRejection.Replay);
            Is(w.Execute(out _, 2), CombatCommandRejection.RateLimited); w.Tick(.1);
            Is(w.Execute(out _, 3), CombatCommandRejection.Busy);
        });
        Test("cooldown survives aliases and completion", () =>
        {
            using var w = new World(); Check(w.Authority.RegisterSkill(w.Player, 2, w.Skill, Policy()), "alias");
            Is(w.Execute(out var ticket), CombatCommandRejection.None); Check(w.Authority.Complete(ticket), "complete"); w.Tick(.1);
            Is(w.Execute(out _, 2, skill: 2), CombatCommandRejection.Cooldown); w.Tick(2);
            Is(w.Execute(out _, 3), CombatCommandRejection.None);
        });
        Test("ticket generation, epoch and duplicate impacts", () =>
        {
            using var w = new World(); Is(w.Execute(out var old), CombatCommandRejection.None);
            Check(!w.Authority.TryConsumeImpact(old, w.Other), "captured target bypass");
            w.Execution.AllowImpact = false; Check(!w.Authority.TryConsumeImpact(old, w.Enemy), "contact gate");
            w.Execution.AllowImpact = true; Check(w.Authority.TryConsumeImpact(old, w.Enemy), "first hit");
            Check(!w.Authority.TryConsumeImpact(old, w.Enemy), "duplicate hit");
            Check(!w.Authority.TryConsumeImpact(old, w.Enemy, 1), "invalid event");
            Check(!w.Authority.IsCurrent(new CombatActionTicket(old.AuthorityEpoch + 1, old.ActorId, old.Generation)), "foreign epoch");
            w.Authority.Complete(old); w.Tick(2); Is(w.Execute(out var next, 2), CombatCommandRejection.None);
            Check(next.Generation > old.Generation && !w.Authority.Cancel(old) && w.Authority.IsCurrent(next), "stale generation");
        });
        Test("command pair and standalone lease contend both participants", () =>
        {
            using var w = new World(true); Is(w.Execute(out var ticket), CombatCommandRejection.None);
            Check(w.Authority.IsReserved(w.Player) && w.Authority.IsReserved(w.Enemy), "pair lock");
            Check(!CombatCinematicReservations.TryAcquire(w, w.Other.transform, w.EnemyTarget, out _), "target overlap");
            w.Authority.Cancel(ticket); Check(!w.Authority.IsReserved(w.Enemy), "cancel release");
            Check(CombatCinematicReservations.TryAcquire(w, w.Other.transform, w.EnemyTarget, out var lease), "standalone lease");
            w.Tick(2); Is(w.Execute(out _, 2), CombatCommandRejection.Busy);
            int releases = 0; lease.Released += () => releases++; lease.Release(); lease.Release();
            Check(releases == 1 && !lease.IsActive && !w.Authority.IsReserved(w.Other) && !w.Authority.IsReserved(w.Enemy), "idempotent release");
        });
        Test("death invalidates command and both locks", () =>
        {
            using var w = new World(true); Is(w.Execute(out var ticket), CombatCommandRejection.None);
            w.EnemyTarget.IsDead = true;
            Check(!w.Authority.TryConsumeImpact(ticket, w.Enemy) && !w.Authority.IsReserved(w.Player), "death cleanup");
            Check(w.Execution.Ends == 1 && w.Execution.Reason == CombatActionEndReason.Death, "end exactly once");
        });
        Test("disconnect cleans command and lease", () =>
        {
            using var w = new World(true); Is(w.Execute(out var ticket), CombatCommandRejection.None);
            w.Manager.Disconnect(7); Check(!w.Authority.IsCurrent(ticket) && w.Execution.Reason == CombatActionEndReason.Disconnect, "disconnect command");
        });
        Test("ownership transfer, despawn, assignment loss and expiry", () =>
        {
            foreach (int kind in new[] { 0, 1, 2, 3 })
            {
                using var w = new World(true); Is(w.Execute(out var ticket), CombatCommandRejection.None);
                if (kind == 0) w.Player.OwnerClientId = 9;
                if (kind == 1) w.Enemy.IsSpawned = false;
                if (kind == 2) w.Assigned = false;
                w.Tick(kind == 3 ? 6 : 0);
                Check(!w.Authority.IsCurrent(ticket) && !w.Authority.IsReserved(w.Player), "lifecycle " + kind);
            }
        });
        Test("additive scene and persistent root accept commands and impacts", () =>
        {
            using var w = new World(); w.Enemy.gameObject.scene = new Scene(2); w.Player.gameObject.scene = new Scene(99);
            Is(w.Execute(out var ticket), CombatCommandRejection.None);
            Check(w.Authority.TryConsumeImpact(ticket, w.Enemy), "cross-scene impact");
        });
        Test("additive churn preserves leases; participant unload retires them", () =>
        {
            using var w = new World();
            Check(CombatCinematicReservations.TryAcquire(w, w.Player.transform, w.EnemyTarget, out var lease), "acquire");
            SceneManager.Change(); SceneManager.Unload();
            Check(lease.IsActive && w.Authority.IsReserved(w.Player), "unrelated scene churn");
            SceneManager.UnloadParticipant(); Check(!lease.IsActive, "participant unload");
            Is(w.Execute(out _), CombatCommandRejection.UnregisteredActor);
        });
        Test("explicit and NavMesh world invalidation clear registrations and leases", () =>
        {
            foreach (bool unload in new[] { false, true })
            {
                using var w = new World();
                Check(CombatCinematicReservations.TryAcquire(w, w.Player.transform, w.EnemyTarget, out var lease), "acquire");
                if (unload) w.WorldService.Invalidate(); else w.Authority.ClearForSceneTransition();
                Check(!lease.IsActive && !w.Authority.IsReserved(w.Player), "scene release");
                Is(w.Execute(out _), CombatCommandRejection.UnregisteredActor);
            }
        });
        Test("replaced world rejects old ticket before impact", () =>
        {
            using var w = new World(); Is(w.Execute(out var ticket), CombatCommandRejection.None);
            NavMeshWorldService.Instance = new NavMeshWorldService();
            Check(!w.Authority.TryConsumeImpact(ticket, w.Enemy), "old world ticket");
            Is(w.Execute(out _, 2), CombatCommandRejection.UnregisteredActor);
        });
        Test("lease target death, timeout, disconnect and disable", () =>
        {
            foreach (int kind in new[] { 0, 1, 2, 3 })
            {
                using var w = new World();
                Check(CombatCinematicReservations.TryAcquire(w, w.Player.transform, w.EnemyTarget, out var lease), "acquire");
                if (kind == 0) w.EnemyTarget.IsDead = true;
                if (kind == 1) w.Tick(61);
                if (kind == 2) w.Manager.Disconnect(7);
                if (kind == 3) Invoke(w.Authority, "OnDisable");
                w.Tick(); Check(!lease.IsActive, "lease cleanup " + kind);
            }
        });
        Test("throwing executor fails closed and releases pair", () =>
        {
            using var w = new World(true); w.Execution.ThrowStart = true;
            Is(w.Execute(out _), CombatCommandRejection.ExecutorFailed);
            Check(w.Execution.Ends == 1 && !w.Authority.IsReserved(w.Enemy), "failed start release");
            w.Tick(.1); Is(w.Execute(out _, 2), CombatCommandRejection.Cooldown);
        });
        Test("policy rejects invalid values", () =>
        {
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, -1f })
            {
                bool rejected = false;
                try { Policy(cooldown: bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "invalid cooldown");
            }
        });
        Test("explicit self-capable policy does not loosen required-enemy actions", () =>
        {
            using var w = new World();
            Is(w.Execute(out _, target: w.Player), CombatCommandRejection.InvalidTarget); w.Tick(.1);
            var ownSkill = new ScriptableObject();
            Check(w.Authority.RegisterSkill(w.Player, 2, ownSkill, new CombatCommandPolicy(CombatCommandTargetMode.RequiredOrSelf, 0, 6, 2, 0, 2)), "self-capable policy");
            Is(w.Execute(out _, sequence: 2, skill: 2, target: w.Player), CombatCommandRejection.None);
        });
        Test("executor attachment preserves an existing target registration", () =>
        {
            using var w = new World();
            Check(w.Authority.TryAttachExecutor(w.Enemy, w.Execution, id => id == 0), "attach");
            Check(w.Authority.IsRegistered(w.Enemy, w.Execution), "executor binding");
            Check(!w.Authority.TryAttachExecutor(w.Enemy, new Executor(), id => true), "foreign executor replacement");
        });
        Test("explicit despawn cleanup works after identity IsSpawned becomes false", () =>
        {
            using var w = new World(true); Is(w.Execute(out var ticket), CombatCommandRejection.None);
            w.Player.IsSpawned = false; w.Authority.UnregisterActor(w.Player);
            Check(w.Execution.Ends == 1 && !w.Authority.IsCurrent(ticket), "despawn callback cleanup");
        });
        Test("cooldown survives actor unregister/re-register within the same world", () =>
        {
            using var w = new World(); Is(w.Execute(out var ticket), CombatCommandRejection.None); w.Authority.Complete(ticket);
            w.Authority.UnregisterActor(w.Player, CombatActionEndReason.OwnershipChanged);
            Check(w.Authority.RegisterActor(w.Player, () => true, w.Execution, id => id == 7), "re-register");
            Check(w.Authority.RegisterSkill(w.Player, 1, w.Skill, Policy()), "skill re-register");
            Check(w.Authority.GetCooldownRemaining(w.Player, w.Skill) > 0, "cooldown reset");
            Is(w.Execute(out _), CombatCommandRejection.Cooldown);
        });
        CatalogSmokeTests.Run(Test);
        Console.WriteLine($"{passed} passed; {failed} failed. Managed doubles only; Unity/NGO runtime not exercised.");
        return failed == 0 ? 0 : 1;
    }
}
