using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lit.Timeline;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using TimelineManager = Lit.Timeline.TimelineManager;

public sealed partial class CycleRuntimeSmoke
{
    private IEnumerator RunNetworkSequenceFixture()
    {
        definition = Resources.Load<CycleDefinition>("Narrative/CycleSequenceSmokeDefinition");
        var prefab = Resources.Load<GameObject>("CycleSequenceSmokePrefab");
        if (definition == null || prefab == null) { Finish(false, "Missing native network sequence fixture."); yield break; }
        NetcodeRuntimeUtilities.EnsureNetworkObjectHash(prefab.GetComponent<NetworkObject>(), 0x51C1E002u);
        var services = new GameObject("Sequence fixture application root"); services.AddComponent<ApplicationRoot>();
        services.AddComponent<TimelineManager>();
        var rules = new GameObject("Sequence fixture world rules").AddComponent<WorldRulesStateManager>();
        CycleProgressionService.Ensure(rules);
        manager = new GameObject("Sequence fixture network manager").AddComponent<NetworkManager>();
        var transport = manager.gameObject.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", ushort.Parse(Arg("port", "27841")), "127.0.0.1");
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false,
            ConnectionApproval = false, AutoSpawnPlayerPrefabClientSide = false };
        manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
        bool server = role == "host";
        if (!(server ? manager.StartHost() : manager.StartClient())) { Finish(false, "Sequence transport failed to start."); yield break; }
        evidence.Add("Real NGO sequence transport role=" + role);
        if (server)
        {
            while (manager.ConnectedClientsIds.Count < 2 && Time.realtimeSinceStartup - started < 20) yield return null;
            if (manager.ConnectedClientsIds.Count < 2) { Finish(false, "Sequence requires a connected real client."); yield break; }
            yield return new WaitForSecondsRealtime(.5f);
            controller = Instantiate(prefab).GetComponent<CycleController>();
            NetcodeRuntimeUtilities.EnsureNetworkObjectHash(controller.GetComponent<NetworkObject>(), 0x51C1E002u);
            controller.GetComponent<NetworkObject>().Spawn();
        }
        while (controller == null && Time.realtimeSinceStartup - started < 22)
        {
            controller = FindAnyObjectByType<CycleController>();
            yield return null;
        }
        if (controller == null) { Finish(false, "Client did not receive the sequence controller."); yield break; }
        int failedOutcomes = 0, successfulOutcomes = 0;
        controller.SequencePresentationFinished += (id, success) =>
        {
            if (id != "memory") return;
            if (success) successfulOutcomes++; else failedOutcomes++;
            evidence.Add("Shared sequence outcome success=" + success);
        };
        var effect = controller.GetComponentInChildren<TimelineDissolveTarget>();
        var visual = controller.GetComponentInChildren<MeshRenderer>();
        var observed = new HashSet<TimelinePlaybackHandle>();
        bool sawAnimation = false, stoppedOnce = false, retryRequested = false, restoredAfterFailure = false;
        double retryAt = -1;
        float completedAt = -1;
        while (Time.realtimeSinceStartup - started < 30)
        {
            var handle = (TimelinePlaybackHandle)typeof(CycleController).GetField("playback", Private).GetValue(controller);
            if (handle != null && observed.Add(handle)) evidence.Add("Local native playback attempt=" + observed.Count);
            if (effect.strength > .05f && effect.strength < .95f) sawAnimation = true;
            if (!server && scenario == "sequence_interrupt" && !stoppedOnce && handle != null && effect.strength > .15f)
            {
                stoppedOnce = true; handle.Stop(); evidence.Add("Client deliberately stopped its first native graph.");
            }
            bool completed = CycleProgressionService.Instance.IsStepCompleted(definition, "memory_seen");
            if (scenario == "sequence_interrupt" && failedOutcomes > 0 && !retryRequested &&
                successfulOutcomes == 0 && observed.Count == 1)
            {
                if (completed) { Finish(false, "Interrupted shared sequence committed a milestone."); yield break; }
                if (visual.enabled && effect.strength == 0) restoredAfterFailure = true;
                if (server)
                {
                    if (retryAt < 0) retryAt = Time.realtimeSinceStartupAsDouble + .4;
                    if (Time.realtimeSinceStartupAsDouble >= retryAt)
                    {
                        if (!restoredAfterFailure || !controller.TryRetrySceneSequence("memory"))
                        { Finish(false, "Failed sequence did not restore or could not be retried."); yield break; }
                        retryRequested = true; evidence.Add("Server explicitly retried the failed sequence without granting progress.");
                    }
                }
            }
            if (completed && completedAt < 0) completedAt = Time.realtimeSinceStartup;
            if (completedAt >= 0 && Time.realtimeSinceStartup - completedAt > .6f)
            {
                int expectedAttempts = scenario == "sequence_interrupt" ? 2 : 1;
                int expectedFailures = scenario == "sequence_interrupt" ? 1 : 0;
                bool valid = sawAnimation && observed.Count == expectedAttempts && failedOutcomes == expectedFailures &&
                    successfulOutcomes == 1 && !visual.enabled && observed.All(value => value.IsDone) &&
                    (scenario != "sequence_interrupt" || restoredAfterFailure && (server ? retryRequested : stoppedOnce));
                evidence.Add("Attempts=" + observed.Count + " animated=" + sawAnimation + " restored=" + restoredAfterFailure +
                    " failures=" + failedOutcomes + " successes=" + successfulOutcomes);
                Finish(valid, "Shared native sequence must complete once on both peers, with no milestone on interruption.");
                yield break;
            }
            yield return null;
        }
        Finish(false, "Network sequence did not reach a verified final state.");
    }
}
