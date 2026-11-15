using UnityEngine;

namespace Lit.Traversal
{
    [DisallowMultipleComponent]
    internal sealed class ClingIkRelay : MonoBehaviour
    {
        internal PlayerAnimationController Owner { private get; set; }

        private void OnAnimatorIK(int layer)
        {
            if (Owner != null) Owner.ApplyClingIK(layer);
        }
    }
}
