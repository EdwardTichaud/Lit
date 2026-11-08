using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Lit.Fractures
{
    public sealed partial class FractureGraybox
    {
        [Tooltip("Embedded district content: never creates or drives a character/camera.")]
        public bool districtIntegration;
        public const string DistrictStateKey = "world.district1.fracture.archives.v1";
        private WorldRulesStateManager districtRules;
        private int lastDistrictState = -1;
        private bool requestedDistrictState;
        private bool districtStarted;

        public static FractureGraybox District => Object.FindObjectsByType<FractureGraybox>().FirstOrDefault(lab => lab.districtIntegration);
        public bool ContainsDistrictPoint(Vector3 point)
        {
            var p = transform.InverseTransformPoint(point);
            return p.x >= -36 && p.x <= 36 && p.z >= -29.5f && p.z <= 80 && Mathf.Abs(p.y) < 6;
        }
        private void InitializeDistrict()
        {
            helpStations = GetComponentsInChildren<FractureStation>(true);
            initialized = true;
            RefreshGates();
            UpdateDistrict();
        }
        private void UpdateDistrict()
        {
            if (!initialized) return;
            var root = LocalPlayerContext.LocalCharacterRoot;
            Player = root != null ? root.GetComponent<SquadCharacterController>() : null;
            bridge = Player != null ? Player.GetComponent<LitOpsiveLocomotionBridge>() : null;
            if (districtRules == null) districtRules = Object.FindAnyObjectByType<WorldRulesStateManager>();
            if (districtRules != null && districtRules.TryGetInt(DistrictStateKey, out int saved) && saved != lastDistrictState)
            {
                ApplyDistrictState(saved, null, null);
                var service = WorldInteractionService.Instance;
                if (service != null && service.IsSpawned && service.IsServer)
                    service.FractureStateClientRpc(saved, string.Empty, string.Empty);
            }
            var network = NetworkManager.Singleton;
            if (network != null && network.IsListening && !network.IsServer && !requestedDistrictState &&
                !JoinSyncSystem.IsGameplayBlocked && districtRules != null && Player != null &&
                WorldInteractionService.Instance != null && WorldInteractionService.Instance.IsSpawned)
            {
                requestedDistrictState = true;
                WorldInteractionService.Instance.RequestFractureStateServerRpc();
            }
        }
        private bool TryDistrictInteraction(FractureStation station, SquadCharacterController actor)
        {
            if (!AcceptsInput || actor != Player || station == null || station.labyrinth != this) return false;
            var network = NetworkManager.Singleton;
            if (network != null && network.IsListening && !network.IsServer)
            {
                WorldInteractionService.Instance?.RequestFractureInteractionServerRpc(NetcodeSceneIdUtility.GetStableId(station.transform));
                return true;
            }
            return ServerDistrictInteraction(station, actor);
        }
        public bool ServerDistrictInteraction(FractureStation station, SquadCharacterController actor)
        {
            if (!districtIntegration || !initialized || station == null || actor == null || station.labyrinth != this ||
                !station.isActiveAndEnabled || !station.CanBeDetectedBy(actor) || !ContainsDistrictPoint(actor.transform.position) ||
                !CharacterInteractionDetection.IsInActiveFlameInfluence(station) ||
                !CharacterInteractionDetection.IsInteractionTargetVisibleFromCharacter(station, station.GetInteractionDetectionCollider(),
                    station.transform, actor.GetInteractionOriginWorldPosition(), actor.transform)) return false;
            var locomotion = actor.GetComponent<LitOpsiveLocomotionBridge>();
            if (locomotion == null || !locomotion.Grounded || locomotion.IsInputSuppressedByUcc) return false;
            float previousAnnouncement = announcementUntil;
            ExecuteInteraction(station, actor);
            if (station.kind == FractureStation.Kind.Entry && Attempt.IsRunning) districtStarted = true;
            RefreshGates();
            int state = CaptureDistrictState();
            lastDistrictState = state;
            districtRules?.SetInt(DistrictStateKey, state);
            var service = WorldInteractionService.Instance;
            if (service != null && service.IsSpawned && service.IsServer)
                service.FractureStateClientRpc(state, Feedback, announcementUntil != previousAnnouncement ? announcement : string.Empty);
            return true;
        }
        public int CaptureDistrictState()
        {
            int mask = 0;
            for (int i = 0; i < 3; i++) if (ArchivePuzzle.HasClue(i)) mask |= 1 << i;
            int code = ArchivePuzzle.GetInput(0) + ArchivePuzzle.GetInput(1) * 3 + ArchivePuzzle.GetInput(2) * 9;
            int state = (districtStarted ? 1 : 0) | mask << 1 | code << 4 |
                (ArchivePuzzle.DoorOpen ? 512 : 0) | (ExplorationCompleted ? 1024 : 0);
            int index = 0;
            foreach (var shelf in GetComponentsInChildren<FractureStation>().Where(s => s.kind == FractureStation.Kind.Bookshelf).OrderBy(s => s.name))
            { if (shelf.searched) state |= 1 << (11 + index); index++; }
            return state;
        }
        public void ApplyDistrictState(int state, string feedback, string reveal)
        {
            if (state < 0 || state >= 1 << 17) return;
            lastDistrictState = state;
            districtStarted = (state & 1) != 0;
            districtRules?.SetInt(DistrictStateKey, state);
            ArchivePuzzle.Restore((state >> 1) & 7, (state >> 4) & 31, (state & 512) != 0);
            if (Attempt.IsRunning) Attempt.End(FracturePhase.Abandoned);
            if (Attempt.CanReform) Attempt.Reform();
            if ((state & 1) != 0) Attempt.Start(1, true);
            ExplorationCompleted = (state & 1024) != 0 && ArchivePuzzle.DoorOpen;
            if (ExplorationCompleted) Attempt.End(FracturePhase.Abandoned);
            int index = 0;
            foreach (var shelf in GetComponentsInChildren<FractureStation>().Where(s => s.kind == FractureStation.Kind.Bookshelf).OrderBy(s => s.name))
            { shelf.searched = (state & (1 << (11 + index))) != 0; index++; }
            if (!string.IsNullOrEmpty(feedback)) Feedback = feedback;
            if (!string.IsNullOrEmpty(reveal)) { announcement = reveal; announcementUntil = Time.unscaledTime + 5; }
            RefreshGates();
        }
    }
}
