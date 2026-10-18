using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        private CombatNetworkQteSession serverThreshold;
        private ulong thresholdEpoch, thresholdGeneration, thresholdWindow, thresholdInputSequence;
        private double thresholdDeadline;
        private bool thresholdPromptOpen;
        private int thresholdReplicaPresentation;
        private AnimatorOverrideController thresholdReplicaOverride;
        private RuntimeAnimatorController thresholdReplicaOriginal;
        private LitOpsiveLocomotionBridge.ExternalLockHandle thresholdReplicaLock;
        private Coroutine thresholdLifetime;
        private readonly List<Action> thresholdInputUnbind = new List<Action>();
        private readonly HashSet<byte> thresholdHeldAtOpen = new HashSet<byte>();
        private QTEPanelController thresholdPanel;
        private InputAction thresholdExpectedAction;
        private readonly NetworkVariable<CombatNetworkThresholdSnapshot> thresholdReplicaState = new NetworkVariable<CombatNetworkThresholdSnapshot>();
        private bool thresholdWasOwner;
        private bool thresholdReplicaActive;
        private bool thresholdFeedbackResolved;
        private readonly object thresholdInputOwner = new object();

        internal bool TryAcquireThreshold(ICombatTarget enemy, out CombatNetworkQteSession session)
        {
            session = null;
            if (!IsServer || !IsAssignedPlayer || authority == null || serverThreshold != null || enemy?.Root == null) return false;
            var identity = enemy.Root.GetComponentInParent<NetworkObject>();
            if (identity == null || !identity.IsSpawned || !authority.IsRegistered(identity) || enemy.IsDead ||
                authority.IsCinematicParticipant(NetworkObject) || authority.IsCinematicParticipant(identity)) return false;
            // Called only from the threshold controller's deferred admission, never inside an impact callback.
            // Revoke this source's attack ticket; unrelated actors and participant reservations stay untouched.
            if (hasAction && !authority.Cancel(active.Ticket)) return false;
            if (!CombatNetworkQteSession.TryCreate(authority, NetworkObject, identity, enemy,
                    owner => IsAssignedPlayer && OwnerClientId == owner, out session)) return false;
            serverThreshold = session;
            return true;
        }

        internal bool IsThresholdEncounterActive(ICombatTarget enemy)
        {
            if (!IsServer || combatWorld == null || actor == null || enemy?.Root == null) return false;
            foreach (var threat in combatWorld.GetThreats(actor))
                if (threat?.Root == enemy.Root) return true;
            return false;
        }

        internal void PublishThresholdPresentation(CombatNetworkQteSession session, ICombatTarget enemy,
            int stage, int step, bool success)
        {
            if (!IsServer || !IsSpawned || session != serverThreshold || enemy?.Root == null) return;
            var identity = enemy.Root.GetComponentInParent<NetworkObject>();
            if (identity == null || !identity.IsSpawned) return;
            var ticket = session.Ticket;
            double startedAt = NetworkManager.ServerTime.Time;
            thresholdReplicaState.Value = new CombatNetworkThresholdSnapshot
            {
                Active = true, Epoch = ticket.AuthorityEpoch, Generation = ticket.Generation,
                Target = new NetworkObjectReference(identity), Stage = stage, Step = step,
                Success = success, StartedAt = startedAt
            };
            ThresholdPresentationClientRpc(ticket.AuthorityEpoch, ticket.Generation,
                new NetworkObjectReference(identity), stage, step, success, startedAt);
        }

        internal void PublishThresholdWindow(CombatNetworkQteSession session, CombatThresholdQteInput button, float duration)
        {
            if (!IsServer || !IsSpawned || session != serverThreshold) return;
            var ticket = session.Ticket;
            ThresholdWindowClientRpc(ticket.AuthorityEpoch, ticket.Generation, session.Window, (byte)button,
                NetworkManager.ServerTime.Time + duration, OwnerOnlyThresholdRpc());
        }

        internal void PublishThresholdResolution(CombatNetworkQteSession session, bool success)
        {
            if (!IsServer || !IsSpawned || session != serverThreshold) return;
            ThresholdResolutionClientRpc(session.Ticket.AuthorityEpoch, session.Ticket.Generation,
                session.Window, success, OwnerOnlyThresholdRpc());
        }

        internal void EndThresholdPresentation(CombatNetworkQteSession session)
        {
            if (session == null || session != serverThreshold) return;
            serverThreshold = null;
            if (IsServer && IsSpawned)
            {
                var snapshot = thresholdReplicaState.Value; snapshot.Active = false; thresholdReplicaState.Value = snapshot;
                ThresholdEndClientRpc(session.Ticket.AuthorityEpoch, session.Ticket.Generation);
            }
        }

        internal void PublishThresholdExit(CombatNetworkQteSession session, float blend)
        {
            if (IsServer && IsSpawned && session == serverThreshold)
            {
                var snapshot = thresholdReplicaState.Value; snapshot.Exiting = true; snapshot.ExitBlend = blend;
                thresholdReplicaState.Value = snapshot;
                ThresholdExitClientRpc(session.Ticket.AuthorityEpoch, session.Ticket.Generation, blend);
            }
        }

        [ClientRpc]
        private void ThresholdExitClientRpc(ulong epoch, ulong generation, float blend)
        {
            if (IsServer || !thresholdReplicaActive || epoch != thresholdEpoch || generation != thresholdGeneration || animator == null) return;
            int idle = Animator.StringToHash("CombatIdle");
            if (animator.HasState(0, idle)) animator.CrossFade(idle, Mathf.Clamp(blend, .02f, .35f), 0, 0);
        }

        private ClientRpcParams OwnerOnlyThresholdRpc() => new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };

        [ClientRpc]
        private void ThresholdPresentationClientRpc(ulong epoch, ulong generation, NetworkObjectReference enemy,
            int stageIndex, int stepIndex, bool success, double startedAt)
        {
            ApplyThresholdPresentation(epoch, generation, enemy, stageIndex, stepIndex, success, startedAt);
        }

        private void ApplyThresholdPresentation(ulong epoch, ulong generation, NetworkObjectReference enemy,
            int stageIndex, int stepIndex, bool success, double startedAt)
        {
            if (!IsSpawned || generation == 0 ||
                thresholdEpoch == epoch && thresholdGeneration > generation) return;
            bool first = thresholdEpoch != epoch || thresholdGeneration != generation;
            if (!first && !thresholdReplicaActive) return;
            if (first)
            {
                ClearThresholdReplica();
                thresholdEpoch = epoch;
                thresholdGeneration = generation;
                thresholdReplicaActive = true;
                thresholdWasOwner = IsOwner;
                thresholdLifetime = StartCoroutine(ThresholdReplicaLifetime(epoch, generation));
                if (IsOwner) InputModeCoordinator.Enter(thresholdInputOwner, InputMode.ThresholdSequence);
            }
            // Authority already plays the exact same sequence through the shared controller.
            if (IsServer) return;
            if (!enemy.TryGet(out var target, NetworkManager)) { ClearThresholdReplica(); return; }
            var health = target.GetComponent<CharacterInfo>();
            var stages = health != null ? health.CharacterData?.combatHealthThresholdStages : null;
            if (stages == null || stageIndex < 0 || stageIndex >= stages.Count) { ClearThresholdReplica(); return; }
            var sequence = stages[stageIndex]?.sequence;
            if (sequence == null || stepIndex < 0 || stepIndex >= sequence.StepCount || animator == null || presentation == null || bridge == null)
            { ClearThresholdReplica(); return; }
            var step = sequence.steps[stepIndex];
            if (first)
            {
                presentation.ResolveReferences(animator, bridge);
                thresholdReplicaPresentation = presentation.BeginExternalPresentation(this);
                if (thresholdReplicaPresentation == 0 ||
                    !bridge.TryAcquireExternalLock(this, out thresholdReplicaLock, disableGameplayInput: false, stopActiveAbilities: true))
                { ClearThresholdReplica(); return; }
                thresholdReplicaOriginal = animator.runtimeAnimatorController;
                thresholdReplicaOverride = new AnimatorOverrideController(thresholdReplicaOriginal);
                animator.runtimeAnimatorController = thresholdReplicaOverride;
            }
            if (thresholdReplicaOverride == null) return;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            thresholdReplicaOverride.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                var key = overrides[i].Key;
                if (key.name == ThresholdSequence.DefaultQteAnimatorState)
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(key, step.qtePresentationClip);
                else if (key.name == "Threshold_Succes_Placeholder")
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(key, step.successPlayerAnimationClip);
            }
            thresholdReplicaOverride.ApplyOverrides(overrides);
            var state = success ? sequence.SuccessPlayerStateName : sequence.PlayerQteStateName;
            int hash = Animator.StringToHash(state);
            var clip = success ? step.successPlayerAnimationClip : step.qtePresentationClip;
            float normalized = clip != null && clip.length > 0
                ? Mathf.Clamp((float)(NetworkManager.ServerTime.Time - startedAt) / clip.length, 0, .999f) : 0;
            if (animator.HasState(0, hash)) animator.CrossFade(hash,
                success ? step.successEntryBlendSeconds : step.playerQteEntryBlendSeconds, 0, normalized);
        }

        [ClientRpc]
        private void ThresholdWindowClientRpc(ulong epoch, ulong generation, ulong window, byte button,
            double deadline, ClientRpcParams delivery = default)
        {
            if (!IsOwner || !IsSpawned || !thresholdReplicaActive || epoch != thresholdEpoch || generation != thresholdGeneration ||
                window <= thresholdWindow || button > (byte)CombatThresholdQteInput.X ||
                double.IsNaN(deadline) || double.IsInfinity(deadline)) return;
            CloseThresholdPrompt();
            thresholdWindow = window;
            thresholdDeadline = deadline;
            thresholdPromptOpen = true;
            thresholdFeedbackResolved = false;
            var map = LocalPlayerInput.FindSharedActionMap("CombatQTE");
            if (map == null) return; // No fabricated success: server still expires the authored window.
            thresholdExpectedAction = map.FindAction(((CombatThresholdQteInput)button).ToString(), false);
            foreach (CombatThresholdQteInput value in Enum.GetValues(typeof(CombatThresholdQteInput)))
            {
                var action = map.FindAction(value.ToString(), false);
                if (action == null) continue;
                byte captured = (byte)value;
                if (ThresholdButtonPhysicallyHeld(action)) thresholdHeldAtOpen.Add(captured);
                Action<InputAction.CallbackContext> pressed = context =>
                {
                    if (!thresholdHeldAtOpen.Contains(captured)) SendThresholdInput(epoch, generation, window, captured, true);
                };
                Action<InputAction.CallbackContext> released = context =>
                {
                    thresholdHeldAtOpen.Remove(captured);
                    SendThresholdInput(epoch, generation, window, captured, false);
                };
                action.performed += pressed;
                action.canceled += released;
                thresholdInputUnbind.Add(() => { action.performed -= pressed; action.canceled -= released; });
            }
            InputModeCoordinator.Enter(thresholdInputOwner, InputMode.CombatQTE);
            thresholdPanel = QTEPanelController.Instance;
            thresholdPanel?.Show((CombatThresholdQteInput)button);
            // Server starts release-gated; a held button must produce an actual release first.
            if (thresholdExpectedAction != null && !thresholdHeldAtOpen.Contains(button))
                SendThresholdInput(epoch, generation, window, button, false);
        }

        private static bool ThresholdButtonPhysicallyHeld(InputAction action)
        {
            if (action.IsPressed()) return true;
            foreach (var control in action.controls)
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.isPressed) return true;
            return false;
        }

        private void SendThresholdInput(ulong epoch, ulong generation, ulong window, byte button, bool pressed)
        {
            if (!IsOwner || !IsSpawned || !thresholdPromptOpen || epoch != thresholdEpoch ||
                generation != thresholdGeneration || window != thresholdWindow || thresholdInputSequence == ulong.MaxValue) return;
            ThresholdInputServerRpc(epoch, generation, window, ++thresholdInputSequence, button, pressed);
        }

        [ServerRpc]
        private void ThresholdInputServerRpc(ulong epoch, ulong generation, ulong window, ulong sequence,
            byte button, bool pressed, ServerRpcParams rpc = default)
        {
            if (!IsServer || !IsAssignedPlayer || rpc.Receive.SenderClientId != OwnerClientId ||
                serverThreshold == null || button > (byte)CombatNetworkQteButton.X) return;
            GetComponent<CombatHealthThresholdController>()?.SubmitNetworkThresholdInput(this, epoch, generation,
                window, sequence, (CombatNetworkQteButton)button, pressed, rpc.Receive.SenderClientId);
        }

        [ClientRpc]
        private void ThresholdResolutionClientRpc(ulong epoch, ulong generation, ulong window, bool success,
            ClientRpcParams delivery = default)
        {
            if (!IsOwner || !thresholdReplicaActive || thresholdEpoch != epoch || thresholdGeneration != generation || thresholdWindow != window) return;
            CloseThresholdPrompt();
            thresholdFeedbackResolved = true;
            if (success) thresholdPanel?.ResolveSuccess(); else thresholdPanel?.ResolveFailure();
            InputModeCoordinator.Enter(thresholdInputOwner, InputMode.ThresholdSequence);
        }

        [ClientRpc]
        private void ThresholdEndClientRpc(ulong epoch, ulong generation)
        {
            if (epoch == thresholdEpoch && generation == thresholdGeneration) ClearThresholdReplica(preserveFeedback: true);
        }

        private IEnumerator ThresholdReplicaLifetime(ulong epoch, ulong generation)
        {
            double duration = 0;
            double windowDuration = 0;
            ulong lastWindow = 0;
            while (thresholdReplicaActive && IsSpawned && isActiveAndEnabled && IsAlive() && NetworkManager != null && NetworkManager.IsListening &&
                epoch == thresholdEpoch && generation == thresholdGeneration && duration < 61 &&
                (!thresholdWasOwner || IsOwner))
            {
                var world = NavMeshWorldService.Instance;
                if (world != null && (world.State == NavMeshWorldState.Invalidating || world.State == NavMeshWorldState.Unloaded)) break;
                if (thresholdPromptOpen)
                {
                    double remaining = Math.Max(0, thresholdDeadline - NetworkManager.ServerTime.Time);
                    if (lastWindow != thresholdWindow) { lastWindow = thresholdWindow; windowDuration = remaining; }
                    thresholdPanel?.SetProgress(windowDuration > 0 ? 1f - (float)(remaining / windowDuration) : 1f);
                    if (remaining <= 0) { CloseThresholdPrompt(); thresholdPanel?.HideImmediate(); }
                }
                duration += Time.unscaledDeltaTime;
                yield return null;
            }
            if (thresholdReplicaActive && epoch == thresholdEpoch && generation == thresholdGeneration) ClearThresholdReplica();
        }

        private void CloseThresholdPrompt()
        {
            thresholdPromptOpen = false;
            foreach (var unbind in thresholdInputUnbind) unbind();
            thresholdInputUnbind.Clear();
            thresholdHeldAtOpen.Clear();
            thresholdExpectedAction = null;
        }

        private void ClearThresholdReplica(bool preserveFeedback = false)
        {
            thresholdReplicaActive = false;
            CloseThresholdPrompt();
            if (thresholdWasOwner)
            {
                if (!preserveFeedback || !thresholdFeedbackResolved) thresholdPanel?.HideImmediate();
                InputModeCoordinator.Exit(thresholdInputOwner);
            }
            thresholdPanel = null;
            thresholdWasOwner = false;
            thresholdFeedbackResolved = false;
            thresholdWindow = 0;
            if (thresholdReplicaOverride != null)
            {
                if (animator != null && animator.runtimeAnimatorController == thresholdReplicaOverride)
                    animator.runtimeAnimatorController = thresholdReplicaOriginal;
                Destroy(thresholdReplicaOverride);
            }
            thresholdReplicaOverride = null;
            thresholdReplicaOriginal = null;
            presentation?.EndExternalPresentation(thresholdReplicaPresentation);
            thresholdReplicaPresentation = 0;
            thresholdReplicaLock?.Dispose();
            thresholdReplicaLock = null;
        }

        // Call from the actor's existing despawn/disable/world-retirement hooks.
        internal void CleanupNetworkThresholdTransport()
        {
            if (IsServer)
            {
                var controller = GetComponent<CombatHealthThresholdController>();
                if (controller != null && controller.ThresholdParticipant == actor)
                    controller.AbortActiveSequence("participant reseau retire");
                serverThreshold?.Dispose();
                serverThreshold = null;
            }
            ClearThresholdReplica();
        }

        // Called after the existing actor references are resolved in OnNetworkSpawn.
        internal void InitializeNetworkThresholdTransport()
        {
            ClearThresholdReplica();
            thresholdEpoch = thresholdGeneration = 0;
            if (IsServer) thresholdReplicaState.Value = default;
            else StartCoroutine(ReplayThresholdForLateObserver());
        }

        private IEnumerator ReplayThresholdForLateObserver()
        {
            float wait = 0;
            while (IsSpawned && wait < 60)
            {
                var snapshot = thresholdReplicaState.Value;
                if (!snapshot.Active || thresholdReplicaActive) yield break;
                if (snapshot.Target.TryGet(out _, NetworkManager))
                {
                    ApplyThresholdPresentation(snapshot.Epoch, snapshot.Generation, snapshot.Target,
                        snapshot.Stage, snapshot.Step, snapshot.Success, snapshot.StartedAt);
                    if (snapshot.Exiting && animator != null)
                    {
                        int idle = Animator.StringToHash("CombatIdle");
                        if (animator.HasState(0, idle)) animator.CrossFade(idle, snapshot.ExitBlend, 0, 0);
                    }
                    yield break;
                }
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    internal struct CombatNetworkThresholdSnapshot : INetworkSerializable, IEquatable<CombatNetworkThresholdSnapshot>
    {
        public bool Active, Success, Exiting;
        public ulong Epoch, Generation;
        public NetworkObjectReference Target;
        public int Stage, Step;
        public double StartedAt;
        public float ExitBlend;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Active); serializer.SerializeValue(ref Success); serializer.SerializeValue(ref Exiting);
            serializer.SerializeValue(ref Epoch); serializer.SerializeValue(ref Generation); serializer.SerializeValue(ref Target);
            serializer.SerializeValue(ref Stage); serializer.SerializeValue(ref Step);
            serializer.SerializeValue(ref StartedAt); serializer.SerializeValue(ref ExitBlend);
        }
        public bool Equals(CombatNetworkThresholdSnapshot other) => Active == other.Active && Success == other.Success &&
            Exiting == other.Exiting && Epoch == other.Epoch && Generation == other.Generation && Target.Equals(other.Target) &&
            Stage == other.Stage && Step == other.Step && StartedAt.Equals(other.StartedAt) && ExitBlend.Equals(other.ExitBlend);
    }
}
