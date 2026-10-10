using UnityEngine;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Handles animation setup and synchronization between the NavMeshAgent's movement
    /// and the Animator parameters (Speed, MoveX/Y, Grounded, Attack triggers, etc).
    /// </summary>
    public partial class Brain : MonoBehaviour
    {
        #region === Animator Updates ===

        /// <summary>
        /// Updates movement and grounded animation parameters every frame,
        /// and controls agent movement depending on the current animation state.
        /// </summary>
        protected virtual void UpdateAnimator()
        {
            // Convert world velocity into local space (X = strafe, Y = forward)
            Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
            Vector2 moveInput = new(localVelocity.x, localVelocity.z);
            moveInput = moveInput.normalized;

            // Map currentSpeed to an animator blend value
            float targetAnimSpeed = 0f;
            if (currentSpeed == sneakSpeed) targetAnimSpeed = 0f;
            if (currentSpeed == walkSpeed) targetAnimSpeed = 0.5f;
            if (currentSpeed == runSpeed) targetAnimSpeed = 1f;

            // Smoothly blend towards the target speed value
            currentAnimatorSpeed = Mathf.MoveTowards(currentAnimatorSpeed, targetAnimSpeed, Time.deltaTime * 5f);

            // Apply parameters to Animator
            anim.SetFloat("Speed", currentAnimatorSpeed);
            anim.SetFloat("MoveX", moveInput.x, 0.1f, Time.deltaTime);
            anim.SetFloat("MoveY", moveInput.y, 0.1f, Time.deltaTime);
            anim.SetBool("Grounded", IsGrounded);

            // Check current animator state
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0); // Layer 0

            // Prevent agent from moving during non-moveable animations (e.g. attack, stunned)
            if (!stateInfo.IsTag("Moveable") && agent.speed != 0)
                agent.speed = 0f;

            // Restore movement speed when returning to moveable states
            if (stateInfo.IsTag("Moveable") && agent.speed == 0)
                agent.speed = currentSpeed;
        }

        #endregion
    }
}
