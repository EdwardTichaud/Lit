using UnityEngine;

/// <summary>Leg-only presentation for free grounded movement; UCC still owns all motion.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class PlayerMovingLegsOverlay : MonoBehaviour
{
    public const string LayerName = "Moving Legs";
    private LitOpsiveLocomotionBridge bridge;
    private Animator animator;
    private RuntimeAnimatorController controller;
    private int layer = -1;
    public float Weight => animator != null && layer >= 0 ? animator.GetLayerWeight(layer) : 0;

    private void Update() => Refresh(Time.deltaTime);

    public void Refresh(float deltaTime)
    {
        if (bridge == null) bridge = GetComponent<LitOpsiveLocomotionBridge>();
        if (animator == null) animator = GetComponent<CharacterAnimationController>()?.Animator ?? GetComponentInChildren<Animator>();
        if (animator == null || !animator.isActiveAndEnabled || bridge == null) return;
        if (controller != animator.runtimeAnimatorController)
        {
            controller = animator.runtimeAnimatorController;
            layer = animator.GetLayerIndex(LayerName);
        }
        if (layer < 0) return; // Historical controllers are not implicitly migrated.
        var health = GetComponent<SquadCharacterController>();
        bool blocked = !bridge.isActiveAndEnabled || !bridge.Grounded || bridge.IsInputSuppressedByUcc ||
            bridge.IsCinematicMotionSessionActive || GetComponent<PlayerActionPresentationController>()?.IsDeathAnimationLocked == true ||
            health != null && health.CurrentHp <= 0;
        if (blocked) { animator.SetLayerWeight(layer, 0); return; }

        Vector3 velocity = bridge.PlanarVelocity;
        float speed = velocity.magnitude;
        Vector3 local = transform.InverseTransformDirection(velocity.normalized);
        animator.SetFloat("MovingLegsX", local.x);
        animator.SetFloat("MovingLegsZ", local.z);
        animator.SetFloat("MovingLegsSpeed", speed);
        animator.SetFloat("MovingLegsMagnitude", Mathf.Clamp01(speed / .2f));
        float tier = animator.GetFloat("LocomotionTier");
        animator.SetFloat("MovingLegsTier", tier > .1f ? tier : 1.1f);
        float desired = speed > .08f ? 1 : 0;
        animator.SetLayerWeight(layer, Mathf.MoveTowards(Weight, desired, Mathf.Max(0, deltaTime) / .08f));
    }

    private void OnDisable()
    {
        if (animator != null && layer >= 0 && layer < animator.layerCount) animator.SetLayerWeight(layer, 0);
        controller = null; layer = -1;
    }
}
