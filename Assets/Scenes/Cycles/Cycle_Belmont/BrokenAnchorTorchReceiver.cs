using UnityEngine;

[DisallowMultipleComponent]
public sealed class BrokenAnchorTorchReceiverStandalone : MonoBehaviour
{
    [SerializeField] private BrokenAnchorTorch torch;
    public BrokenAnchorTorch Torch => torch != null ? torch : GetComponentInParent<BrokenAnchorTorch>();
    public bool TryLight()
    {
        BrokenAnchorTorch target = Torch;
        return target != null && target.TryLight();
    }
}
