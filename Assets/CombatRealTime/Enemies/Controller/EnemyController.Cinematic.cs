using UnityEngine;

public sealed partial class EnemyController
{
    private bool cinematicSuspended;
    public bool IsSuspended => cinematicSuspended || IsFlameDormant;

    public void SetSuspended(bool value)
    {
        if (cinematicSuspended == value)
            return;
        cinematicSuspended = value;
        EnemyController motor = GetComponent<EnemyController>();
        if (value)
        {
            GetComponent<EnemyController>()?.Suspend();
            GetComponent<EnemyController>()?.StopNavigation();
            motor?.EnterCinematic();
        }
        else
            motor?.ExitCinematic();
    }

    public bool Place(Vector3 position, Quaternion rotation)
    {
        SetSuspended(true);
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
        }

        Physics.SyncTransforms();
        return true;
    }
}
