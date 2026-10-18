using System;
using System.Reflection;
using Lit.Combat.Migration.Network;
using Unity.Netcode;
using UnityEngine;

// Managed engine doubles exercise admission/lifetime only, not RPCs or playback.
internal static class NetworkQteSmokeTests
{
    private sealed class Target : ICombatTarget
    {
        public Transform Root { get; set; }
        public bool IsDead { get; set; }
    }

    private sealed class World : IDisposable
    {
        public NetworkManager Manager;
        public CombatCommandAuthority Authority;
        public NetworkObject Source, Enemy;
        public Target Victim;
        public bool Assigned = true;
        public CombatNetworkQteSession Session;

        public World()
        {
            Time.unscaledTimeAsDouble = 10;
            NavMeshWorldService.Instance = new NavMeshWorldService();
            var root = new GameObject();
            Manager = root.AddComponent<NetworkManager>();
            NetworkManager.Singleton = Manager;
            Manager.ConnectedClients.Add(7, new object());
            Authority = root.AddComponent<CombatCommandAuthority>();
            Invoke(Authority, "OnEnable");
            Source = Spawn(1, 7); Enemy = Spawn(2, 0);
            Victim = new Target { Root = Enemy.transform };
            Check(Authority.RegisterActor(Source, () => true, isAssigned: id => Assigned && id == 7));
            Check(Authority.RegisterActor(Enemy, () => !Victim.IsDead));
        }

        private NetworkObject Spawn(ulong id, ulong owner)
        {
            var root = new GameObject(); root.AddComponent<SquadCharacterController>();
            var actor = root.AddComponent<NetworkObject>();
            actor.NetworkManager = Manager; actor.NetworkObjectId = id; actor.OwnerClientId = owner;
            Manager.SpawnManager.SpawnedObjects.Add(id, actor);
            return actor;
        }

        public bool Create() => CombatNetworkQteSession.TryCreate(Authority, Source, Enemy, Victim,
            id => Assigned && id == 7, out Session);
        public void Open(bool release = false)
        {
            Check(Create());
            Check(Session.Source == Source);
            Check(Session.TryOpenWindow(CombatNetworkQteButton.Y, .5, release));
        }
        public CombatNetworkQteResult Press(ulong sender = 7, ulong sequence = 1,
            CombatNetworkQteButton button = CombatNetworkQteButton.Y, bool pressed = true) =>
            Session.Submit(sender, Session.Ticket, Session.Window, sequence, button, pressed);
        public void Dispose()
        {
            Session?.Dispose(); Invoke(Authority, "OnDisable");
            NetworkManager.Singleton = null; NavMeshWorldService.Instance = null;
        }
    }

    private static void Invoke(object value, string method) => value.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
    private static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void Is(CombatNetworkQteResult result, CombatNetworkQteResult expected) => Check(result == expected);

    private static int Main()
    {
        int passed = 0, failed = 0;
        void Test(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
        }
        Test("offline policy permits local controllers", () =>
        { NetworkManager.Singleton = null; Check(CombatLocalCinematicPolicy.AllowsLocalExecution); });
        Test("host client and shutdown reject local controllers", () =>
        {
            using var w = new World(); Check(!CombatLocalCinematicPolicy.AllowsLocalExecution);
            w.Manager.IsServer = false; Check(!CombatLocalCinematicPolicy.AllowsLocalExecution);
            w.Manager.IsListening = false; w.Manager.ShutdownInProgress = true;
            Check(!CombatLocalCinematicPolicy.AllowsLocalExecution);
        });
        Test("client cannot create authoritative session", () =>
        { using var w = new World(); w.Manager.IsServer = false; Check(!w.Create()); });
        Test("remote owner accepted host cannot substitute", () =>
        { using var w = new World(); w.Open(); Is(w.Press(sender: 0), CombatNetworkQteResult.Rejected); Is(w.Press(), CombatNetworkQteResult.Success); });
        Test("ticket and window mismatch leave real window intact", () =>
        {
            using var w = new World(); w.Open(); var t = w.Session.Ticket;
            Is(w.Session.Submit(7, new CombatActionTicket(t.AuthorityEpoch, 999, t.Generation), 1, 1, CombatNetworkQteButton.Y, true), CombatNetworkQteResult.Rejected);
            Is(w.Session.Submit(7, t, 2, 1, CombatNetworkQteButton.Y, true), CombatNetworkQteResult.Rejected);
            Is(w.Press(), CombatNetworkQteResult.Success);
            Is(w.Press(sequence: 2), CombatNetworkQteResult.Rejected);
        });
        Test("fresh release and sequence required", () =>
        {
            using var w = new World(); w.Open(true);
            Is(w.Press(), CombatNetworkQteResult.Rejected);
            Is(w.Press(sequence: 2, pressed: false), CombatNetworkQteResult.Released);
            Is(w.Press(sequence: 2), CombatNetworkQteResult.Rejected);
            Is(w.Press(sequence: 3), CombatNetworkQteResult.Success);
        });
        Test("wrong input fails once no guessing", () =>
        { using var w = new World(); w.Open(); Is(w.Press(button: CombatNetworkQteButton.X), CombatNetworkQteResult.Failure); Is(w.Press(sequence: 2), CombatNetworkQteResult.Rejected); });
        Test("exact deadline expires once by server time", () =>
        { using var w = new World(); w.Open(); Time.unscaledTimeAsDouble += .5; Is(w.Press(), CombatNetworkQteResult.Expired); Is(w.Session.Poll(), CombatNetworkQteResult.Rejected); });
        Test("invalid durations and overlapping windows rejected", () =>
        {
            using var w = new World(); Check(w.Create());
            foreach (double d in new[] { 0, -1, double.NaN, double.PositiveInfinity, 61 })
                Check(!w.Session.TryOpenWindow(CombatNetworkQteButton.Y, d));
            Check(!w.Session.TryOpenWindow((CombatNetworkQteButton)255, .5));
            Check(w.Session.TryOpenWindow(CombatNetworkQteButton.Y, .5, false));
            Check(!w.Session.TryOpenWindow(CombatNetworkQteButton.Y, .5));
        });
        Test("session replay cannot cross reservations", () =>
        {
            using var w = new World(); w.Open(); var old = w.Session.Ticket;
            w.Session.Dispose(); w.Open();
            Is(w.Session.Submit(7, old, 1, 1, CombatNetworkQteButton.Y, true), CombatNetworkQteResult.Rejected);
            Is(w.Press(), CombatNetworkQteResult.Success);
        });
        Test("window replay cannot cross steps", () =>
        {
            using var w = new World(); w.Open(); Is(w.Press(), CombatNetworkQteResult.Success);
            Check(w.Session.TryOpenWindow(CombatNetworkQteButton.Y, .5, false));
            Is(w.Session.Submit(7, w.Session.Ticket, 1, 2, CombatNetworkQteButton.Y, true), CombatNetworkQteResult.Rejected);
            Is(w.Press(sequence: 1), CombatNetworkQteResult.Rejected);
            Is(w.Press(sequence: 2), CombatNetworkQteResult.Success);
        });
        foreach (string loss in new[] { "assignment", "ownership", "disconnect", "death", "despawn", "world", "timeout", "shutdown", "authority" })
            Test(loss + " invalidates and releases both reservations", () =>
            {
                using var w = new World(); w.Open();
                switch (loss)
                {
                    case "assignment": w.Assigned = false; break;
                    case "ownership": w.Source.OwnerClientId = 8; break;
                    case "disconnect": w.Manager.ConnectedClients.Remove(7); break;
                    case "death": w.Victim.IsDead = true; break;
                    case "despawn": w.Source.IsSpawned = false; break;
                    case "world": w.Authority.ClearForSceneTransition(); break;
                    case "timeout": Time.unscaledTimeAsDouble += 60; break;
                    case "shutdown": w.Manager.ShutdownInProgress = true; break;
                    case "authority": w.Authority.isActiveAndEnabled = false; break;
                }
                Is(w.Press(), CombatNetworkQteResult.Rejected); Check(!w.Session.IsActive);
                w.Authority.isActiveAndEnabled = true;
                Check(!w.Authority.IsReserved(w.Source)); Check(!w.Authority.IsReserved(w.Enemy));
            });
        Test("contention and idempotent disposal", () =>
        {
            using var w = new World(); w.Open();
            Check(!CombatNetworkQteSession.TryCreate(w.Authority, w.Source, w.Enemy, w.Victim, id => true, out _));
            w.Session.Dispose(); w.Session.Dispose();
            Check(!w.Authority.IsReserved(w.Source) && !w.Authority.IsReserved(w.Enemy));
        });
        Console.WriteLine($"{passed} passed, {failed} failed. Managed doubles only; no transport or cinematic execution tested.");
        return failed == 0 ? 0 : 1;
    }
}
