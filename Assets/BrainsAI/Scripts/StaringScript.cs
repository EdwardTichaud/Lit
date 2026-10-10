using UnityEngine;

namespace Ultrabolt.BrainsAI
{
	/// <summary>
	/// Makes the character's head smoothly rotate to look at its current target,
	/// based on the FOV data from the attached <see cref="Brain"/>.
	/// - Uses the humanoid head bone for rotation.
	/// - Clamps looking to within the agent's view angle.
	/// - Smoothly interpolates the rotation to avoid snapping.
	/// </summary>
	[RequireComponent(typeof(Brain)), RequireComponent(typeof(Animator))]
	public class StaringScript : MonoBehaviour
	{
		[Header("Head Rotation Settings")]
		[Tooltip("Extra rotation offset applied after looking at the target (e.g. for slight tilts).")]
		public Vector3 headRotationOffset;

		[Tooltip("How quickly the head rotates to face the target.")]
		public float rotationSpeed = 5f;

		// ───── Internals ─────
		private Transform headObject;    // The humanoid head bone
		private Quaternion lastRotation; // Cached smooth rotation
		private Brain brain;             // Reference to the brain component

		// Current target to look at (from FOV)
		// Continuously fetch the current target from the Brain's FOV
		private Transform Target => brain.fov.CurrentTarget;

		#region Unity Methods
		void Start()
		{
			brain = GetComponent<Brain>();
			var animator = GetComponent<Animator>();

			// Find the head bone from the Animator (Humanoid rig)
			headObject = animator.GetBoneTransform(HumanBodyBones.Head);

			if (headObject == null)
			{
				Debug.LogWarning($"[{name}] No head bone found — StaringScript will be disabled.");
				enabled = false;
				return;
			}

			lastRotation = headObject.rotation;
		}

		void LateUpdate()
		{
			if (headObject == null || Target == null)
				return;

			// Direction from head to target
			Vector3 direction = (Target.position - headObject.position).normalized;

			// Clamp rotation to within view angle — if target is outside, just look forward
			float angle = Vector3.Angle(transform.forward, direction);
			if (angle > brain.fov.viewAngle)
			{
				direction = transform.forward;
			}

			LookAtTarget(direction);
		}
		#endregion

		#region Rotation Logic
		/// <summary>
		/// Smoothly rotates the head bone to look in the given direction,
		/// applying the configured offset.
		/// </summary>
		void LookAtTarget(Vector3 direction)
		{
			if (direction == Vector3.zero)
				return;

			// Desired head rotation
			Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
			targetRotation *= Quaternion.Euler(headRotationOffset);

			// Smooth interpolation
			lastRotation = Quaternion.Slerp(lastRotation, targetRotation, rotationSpeed * Time.deltaTime);
			headObject.rotation = lastRotation;
		}
		#endregion
	}
}
