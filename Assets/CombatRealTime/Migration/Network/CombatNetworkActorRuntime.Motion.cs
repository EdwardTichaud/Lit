using System.Collections;
using UnityEngine;

namespace Lit.Combat.Migration.Network
{
    public sealed partial class CombatNetworkActorRuntime
    {
        private int airborneHandle;
        private Coroutine planarRoutine;
        private bool ownsPlanarMotion;
        private int serverDodgeSession;

        private void StopOwnedTransientMotion()
        {
            input?.ClearActorCombatGuard();
            if (RealTimeCombatManager.Instance?.PlayerRoot == transform) CounterSkillCombatController.Instance?.EndGuard();
            presentation?.ReleaseChargedFloatForWorldExit();
            int session = serverDodgeSession; serverDodgeSession = 0;
            if (session != 0 && presentation != null && presentation.IsCurrentSession(session)) presentation.CancelAction();
        }

        internal double CaptureNetworkInputTime(double inputTimestamp)
        {
            double age = double.IsNaN(inputTimestamp) ? 0 : UnityEngine.InputSystem.LowLevel.InputState.currentTime - inputTimestamp;
            return NetworkManager.ServerTime.Time - System.Math.Clamp(age, 0, .25);
        }

        public bool RequestDodge(Vector3 worldDirection, double inputTimestamp = double.NaN)
        {
            if (!IsSpawned || !IsOwner || !IsAssigned(OwnerClientId) || !CombatCommandPolicy.Finite(worldDirection)) return false;
            DodgeServerRpc(worldDirection, CaptureNetworkInputTime(inputTimestamp));
            return true;
        }

        [Unity.Netcode.ServerRpc]
        private void DodgeServerRpc(Vector3 direction, double pressedAt, Unity.Netcode.ServerRpcParams parameters = default)
        {
            if (!IsServer || !admitted || !IsAssigned(parameters.Receive.SenderClientId) || !IsAlive() ||
                !CombatCommandPolicy.Finite(direction) || authority == null || authority.IsCinematicParticipant(NetworkObject)) return;
            if (TryServerReactionDodge(direction, out bool reactionAccepted, pressedAt))
            {
                if (reactionAccepted)
                {
                    int reactionSession = presentation.ActionGeneration;
                    serverDodgeSession = reactionSession;
                    PublishDodgePresentation(animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).fullPathHash : animator.GetCurrentAnimatorStateInfo(0).fullPathHash, reactionSession);
                    presentation.RegisterActionCleanup(reactionSession, () => { if (serverDodgeSession == reactionSession) serverDodgeSession = 0; });
                }
                return;
            }
            var mobility = combatWorld != null ? combatWorld.GetComponent<CombatMobilityController>() : null;
            if (mobility != null && mobility.TryDodgeActor(actor, direction))
            {
                StopCharge(); chargeRequiresRelease = true;
                int session = presentation.ActionGeneration;
                serverDodgeSession = session;
                PublishDodgePresentation(animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).fullPathHash : animator.GetCurrentAnimatorStateInfo(0).fullPathHash, session);
                presentation.RegisterActionCleanup(session, () => { if (serverDodgeSession == session) serverDodgeSession = 0; });
            }
        }

        private bool StartPlanarMove(SkillSO skill, int session)
        {
            float distance = presentation.GetSafePlanarTravel(active.Direction, skill.control.distance);
            if (!CombatCommandPolicy.Finite(distance) || distance <= .02f || !bridge.BeginScriptedPlanarMotion(this)) return false;
            ownsPlanarMotion = true;
            bridge.BeginDodgeDirectionFacing(active.Direction, true);
            planarRoutine = StartCoroutine(MovePlanar(session, active.Direction, distance, skill.control.durationSeconds));
            return true;
        }

        private IEnumerator MovePlanar(int session, Vector3 direction, float distance, float duration)
        {
            yield return new WaitForFixedUpdate();
            var start = transform.position;
            var clock = GetComponent<CombatTimeDomain>();
            float elapsed = 0;
            while (hasAction && presentation.IsCurrentSession(session) && authority.IsCurrent(active.Ticket) && elapsed < duration)
            {
                float dt = Time.fixedDeltaTime;
                float actionDt = clock != null ? clock.FixedDeltaTime : dt;
                if (!CombatCommandPolicy.Finite(dt) || !CombatCommandPolicy.Finite(actionDt) || !CombatCommandPolicy.Finite(transform.position))
                { planarRoutine = null; authority.Cancel(active.Ticket); yield break; }
                if (actionDt <= 0) { bridge.DriveScriptedPlanarMotion(this, Vector3.zero); yield return new WaitForFixedUpdate(); continue; }
                float remaining = distance - Vector3.Dot(transform.position - start, direction);
                if (!CombatCommandPolicy.Finite(remaining)) { planarRoutine = null; authority.Cancel(active.Ticket); yield break; }
                if (remaining <= .025f) break;
                float step = presentation.GetSafePlanarTravel(direction, Mathf.Min(remaining, distance * Mathf.Min(actionDt, duration - elapsed) / duration));
                if (!CombatCommandPolicy.Finite(step)) { planarRoutine = null; authority.Cancel(active.Ticket); yield break; }
                if (remaining <= .025f || step <= .001f || dt <= 0 || !bridge.DriveScriptedPlanarMotion(this, direction * (step / dt))) break;
                elapsed += actionDt;
                yield return new WaitForFixedUpdate();
            }
            planarRoutine = null;
            if (hasAction && presentation.IsCurrentSession(session)) authority.Complete(active.Ticket);
        }

        private void ReleaseExecutionMotion()
        {
            if (planarRoutine != null) StopCoroutine(planarRoutine);
            planarRoutine = null;
            if (bridge != null)
            {
                bridge.ReleaseCombatAirborneHold(airborneHandle);
                if (ownsPlanarMotion)
                {
                    bridge.DriveScriptedPlanarMotion(this, Vector3.zero);
                    bridge.EndScriptedPlanarMotion(this);
                    bridge.EndDodgeDirectionFacing();
                }
            }
            airborneHandle = 0;
            ownsPlanarMotion = false;
        }
    }
}
