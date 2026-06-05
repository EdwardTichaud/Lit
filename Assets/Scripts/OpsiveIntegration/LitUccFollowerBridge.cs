using UnityEngine;

// Optional follower adapter for UCC-driven squad members.
[DisallowMultipleComponent]
public class LitUccFollowerBridge : MonoBehaviour
{
    [SerializeField] private LitOpsiveLocomotionBridge locomotionBridge;

    public bool TryTeleport(Vector3 destination)
    {
        ResolveReferences();
        if (locomotionBridge == null)
        {
            return false;
        }

        locomotionBridge.StopBridgeInput();
        return locomotionBridge.SetExternalPositionAndRotation(destination, transform.rotation, stopActiveAbilities: true);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (locomotionBridge == null)
        {
            locomotionBridge = GetComponent<LitOpsiveLocomotionBridge>();
        }
    }
}
