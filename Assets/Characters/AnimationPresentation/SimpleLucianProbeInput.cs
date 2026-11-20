#if UNITY_EDITOR
using UnityEngine;

/// <summary>Temporary Play Mode validation driver. Never attached to production assets or included in builds.</summary>
[DefaultExecutionOrder(-32000), AddComponentMenu("")]
public sealed class SimpleLucianProbeInput : MonoBehaviour
{
    public LitOpsiveLocomotionBridge bridge;
    public Vector2 direction;
    private void FixedUpdate() { if(bridge!=null) bridge.SetMoveInput(direction,true); }
}
#endif
