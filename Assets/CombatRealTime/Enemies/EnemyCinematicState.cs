using UnityEngine;

/// <summary>Placement/suspension service shared by the old adapter and the pattern brain.</summary>
[DisallowMultipleComponent]
public sealed class EnemyCinematicState : MonoBehaviour
{
    public bool IsSuspended { get; private set; }

    public void SetSuspended(bool value)
    {
        if (IsSuspended == value) return;
        IsSuspended = value;
        var motor = GetComponent<CombatEnemyPhysicsMotor>();
        if (value)
        {
            GetComponent<EnemyCombatBrain>()?.Suspend();
            GetComponent<CombatEnemyLocomotionController>()?.StopNavigation();
            motor?.EnterCinematic();
        }
        else motor?.ExitCinematic();
    }

    public bool Place(Vector3 position, Quaternion rotation)
    {
        SetSuspended(true);
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        var body = GetComponent<Rigidbody>();
        if (body != null) { body.position = position; body.rotation = rotation; }
        Physics.SyncTransforms();
        return true;
    }
}
