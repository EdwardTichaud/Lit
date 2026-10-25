using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public sealed partial class CycleRuntimeSmoke : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private string role, scenario, resultPath;
    private NetworkManager manager;
    private CycleDefinition definition;
    private CycleController controller;
    private readonly List<CycleAutoDialogueSession> sessions = new();
    private readonly List<string> evidence = new();
    private float started, spawnedAt, firstUiAt = -1f;
    private bool interrupted, earlyCheck, priorFinished, priorPreserved, done;
    private bool disabledOnce, resumed, sawStoppedPresentation, lastAutomaticShowing;
    private float disabledAt;
    private int shownAttempts;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (Application.companyName != "LitValidation" || Application.productName != "CycleProtocolSmoke") return;
        var root = new GameObject("Isolated cycle protocol"); root.SetActive(false);
        var bootstrap = root.AddComponent<NetcodeBootstrap>();
        foreach (string field in new[] { "dontDestroyOnLoad", "autoCreateNetworkManager", "autoCreateLauncher", "autoCreateSpawner",
            "autoCreateConnectionApproval", "autoCreatePersistentWorldSystems", "enableSceneManagement" })
            typeof(NetcodeBootstrap).GetField(field, Private).SetValue(bootstrap, false);
        root.AddComponent<CycleRuntimeSmoke>();
        root.SetActive(true);
        DontDestroyOnLoad(root);
    }

    private static string Arg(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-" + name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true; Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        role = Arg("role", "host"); scenario = Arg("scenario", "normal");
        resultPath = Arg("result", Path.Combine(Application.persistentDataPath, "cycle-smoke.txt"));
        Application.logMessageReceived += (message, stack, kind) =>
        {
            if (kind == LogType.Exception && !done) Finish(false, "Unhandled runtime exception: " + message + "\n" + stack);
        };
        started = Time.realtimeSinceStartup;
        yield return null; // Finish ordinary AfterSceneLoad initializers first.
        if (scenario.StartsWith("timeline", StringComparison.Ordinal))
        {
            yield return RunTimelineFixture();
            yield break;
        }
        if (scenario.StartsWith("sequence", StringComparison.Ordinal))
        {
            yield return RunNetworkSequenceFixture();
            yield break;
        }
        definition = Resources.Load<CycleDefinition>("Narrative/CycleSmokeDefinition");
        if (definition == null) { Finish(false, "Missing authored smoke definition."); yield break; }
        NetcodeRuntimeUtilities.EnsureNetworkObjectHash(Resources.Load<GameObject>("CycleSmokePrefab").GetComponent<NetworkObject>(), 0x51C1E001u);
        foreach (var automatic in Resources.Load<GameObject>("CycleSmokePrefab").GetComponent<CycleController>().autoDialogues)
            automatic.durationSeconds = scenario == "disconnect" || scenario == "allgone" || scenario == "disable" ? 2f : .5f;
        manager = new GameObject("Protocol NetworkManager").AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig();
        var transport = manager.gameObject.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", ushort.Parse(Arg("port", "27841")), "127.0.0.1");
        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.EnableSceneManagement = false;
        manager.NetworkConfig.ConnectionApproval = false;
        manager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
        manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = Resources.Load<GameObject>("CycleSmokePrefab") });
        new GameObject("Protocol WorldRules").AddComponent<WorldRulesStateManager>();
        CycleProgressionService.Ensure(FindAnyObjectByType<WorldRulesStateManager>());
        if (role == "client" && (scenario == "busy" || scenario == "late"))
        {
            DialoguePanelUI.TryShowTimedConversation("Prior owner's conversation", 5f, success => { priorFinished = success; }, this);
            evidence.Add("Prior conversation opened before connection.");
        }
        if (role == "late") yield return new WaitForSecondsRealtime(2f);
        bool listening = role == "host" ? manager.StartHost() : role == "server" ? manager.StartServer() : manager.StartClient();
        if (!listening) { Finish(false, "Transport start refused."); yield break; }
        evidence.Add("Transport started role=" + role + " scenario=" + scenario);
        if (role == "host" || role == "server")
        {
            int minimum = role == "host" ? 2 : 2;
            while (!done && manager.ConnectedClientsIds.Count < minimum && Time.realtimeSinceStartup - started < 20f) yield return null;
            if (manager.ConnectedClientsIds.Count < minimum) { Finish(false, "Not enough real peers connected."); yield break; }
            yield return new WaitForSecondsRealtime(1f);
            controller = Instantiate(Resources.Load<GameObject>("CycleSmokePrefab")).GetComponent<CycleController>();
            NetcodeRuntimeUtilities.EnsureNetworkObjectHash(controller.GetComponent<NetworkObject>(), 0x51C1E001u);
            controller.GetComponent<NetworkObject>().Spawn();
            spawnedAt = Time.realtimeSinceStartup;
            evidence.Add("Controller spawned with connected=" + manager.ConnectedClientsIds.Count);
        }
    }

    private void Update()
    {
        if (scenario != null && scenario.StartsWith("sequence", StringComparison.Ordinal)) return;
        if (done || manager == null || definition == null) return;
        if (Time.realtimeSinceStartup - started > 35f) { Finish(false, "Protocol timed out."); return; }
        if (controller == null) controller = FindAnyObjectByType<CycleController>();
        if (controller == null || !controller.IsSpawned) return;
        bool server = role == "host" || role == "server";
        var ui = DialoguePanelUI.Instance;
        bool automaticShowing = ui != null && ui.IsShowing && ui.dialogueText != null && ui.dialogueText.text.Contains("Smoke automatic");
        if (automaticShowing && !lastAutomaticShowing) shownAttempts++;
        if (!automaticShowing && lastAutomaticShowing) sawStoppedPresentation = true;
        lastAutomaticShowing = automaticShowing;
        if (automaticShowing && firstUiAt < 0) { firstUiAt = Time.realtimeSinceStartup; evidence.Add("Automatic text really shown."); }
        if (scenario == "skip" && automaticShowing && !interrupted && Time.realtimeSinceStartup - firstUiAt > .1f)
        {
            interrupted = true;
            typeof(DialoguePanelUI).GetMethod("HideManually", Private).Invoke(ui, null);
            evidence.Add("Viewer intentionally passed the line before its minimum duration.");
        }
        if (!server && role != "late" && (scenario == "late" || scenario == "busy"))
            if (!priorFinished && ui != null && ui.IsShowing && ui.dialogueText != null)
            {
                if (!ui.dialogueText.text.Contains("Prior owner")) { Finish(false, "Automatic text replaced another owner's conversation."); return; }
                priorPreserved = true;
            }
        if (!server && role != "late" && scenario == "interrupt" && automaticShowing && !interrupted && Time.realtimeSinceStartup - firstUiAt > .1f)
        {
            interrupted = true; ui.HideImmediate(); evidence.Add("First automatic conversation interrupted.");
        }
        if (!server && (scenario == "disconnect" || scenario == "allgone") && automaticShowing && Time.realtimeSinceStartup - firstUiAt > .15f)
        {
            manager.Shutdown(); Finish(true, "Peer deliberately disconnected before natural completion."); return;
        }
        bool completed = CycleProgressionService.Instance != null && CycleProgressionService.Instance.IsStepCompleted(definition, "intro");
        if (server)
        {
            var session = (CycleAutoDialogueSession)typeof(CycleController).GetField("autoDialogueSession", Private).GetValue(controller);
            if (session != null && !sessions.Contains(session)) { sessions.Add(session); evidence.Add("Attempt=" + session.Token + " audience=" + session.Participants.Count); }
            float elapsed = Time.realtimeSinceStartup - spawnedAt;
            if (scenario == "disable" && !disabledOnce && automaticShowing && Time.realtimeSinceStartup - firstUiAt > .15f)
            {
                disabledOnce = true; disabledAt = Time.realtimeSinceStartup; controller.enabled = false;
                evidence.Add("Controller disabled during its first presentation.");
            }
            if (scenario == "disable" && disabledOnce && !resumed && Time.realtimeSinceStartup - disabledAt > 1f)
            {
                if (completed || ui != null && ui.IsShowing) { Finish(false, "Disabled controller kept its UI or committed progress."); return; }
                resumed = true; controller.enabled = true;
                evidence.Add("No committed progress or leaked UI; controller reenabled.");
            }
            if (!earlyCheck && elapsed > 1.1f && (scenario == "busy" || scenario == "late" || scenario == "disconnect"))
            {
                earlyCheck = true;
                if (completed) { Finish(false, "Progress committed before the delayed viewer's natural completion."); return; }
                evidence.Add("No early progress at +1.1 seconds.");
            }
            if (scenario == "allgone" && elapsed > 4f)
            {
                if (completed || sessions.Any(s => s.CompletedCount != 0)) Finish(false, "Unseen dialogue completed after everyone left.");
                else Finish(true, "All viewers left: no completion committed.");
                return;
            }
            if (completed)
            {
                if (scenario == "skip" && (!interrupted || shownAttempts != 1 || sessions.Count != 1))
                { Finish(false, "Passing the line must complete the first attempt without replaying it."); return; }
                if ((scenario == "interrupt" || scenario == "disable") && (sessions.Count < 2 || sessions[0].Status != CycleAutoDialogueStatus.Failed))
                { Finish(false, "Interruption did not require a fresh successful attempt."); return; }
                if (scenario == "late" && (sessions.Count != 1 || sessions[0].Participants.Count != 2))
                { Finish(false, "A late join changed the frozen audience."); return; }
                if (scenario != "allgone" && (sessions.Count == 0 || sessions.Last().Status != CycleAutoDialogueStatus.Completed))
                { Finish(false, "Completion lacks a naturally completed acknowledgement barrier."); return; }
                StartCoroutine(FinishAfterReplication()); done = true;
            }
        }
        else if (completed)
        {
            if (scenario == "skip" && (!interrupted || shownAttempts != 1))
            { Finish(false, "Client's passed line was replayed or never acknowledged."); return; }
            if (scenario == "disable" && (shownAttempts < 2 || !sawStoppedPresentation))
            { Finish(false, "Client did not observe cancellation and a fresh presentation after reenable."); return; }
            if (role == "late" && firstUiAt >= 0) { Finish(false, "Late viewer was forced into an already-started presentation."); return; }
            if (role != "late" && firstUiAt < 0) { Finish(false, "Client received progress without ever displaying the line."); return; }
            if ((scenario == "busy" || scenario == "late") && role != "late" && (!priorFinished || !priorPreserved))
            { Finish(false, "The prior conversation did not finish naturally and remain intact."); return; }
            Finish(true, "Client received authoritative progression after presentation.");
        }
    }

    private IEnumerator FinishAfterReplication()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        done = false; Finish(true, "Host committed progression after the required natural completions.");
    }

    private void Finish(bool success, string message)
    {
        if (done) return;
        done = true; evidence.Add((success ? "PASS " : "FAIL ") + message);
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
        File.WriteAllLines(resultPath, evidence);
        Debug.Log(string.Join("\n", evidence));
        Application.Quit(success ? 0 : 1);
    }
}
