using UnityEngine;

[DisallowMultipleComponent]
public sealed class BrokenAnchorTorchReceiverStandalone : MonoBehaviour
{
    [SerializeField] private Flame flame;
    public Flame Flame => flame != null ? flame : GetComponentInParent<Flame>();
    public bool TryLight()
    {
        Flame target = Flame;
        if (target == null || target.IsEffectivelyLit) return false;
        target.SetLit(true);
        return true;
    }
}
