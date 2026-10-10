using UnityEngine;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Overrides the control of a <see cref="Brain"/> AI and allows the player 
    /// to manually control it using a point-and-click (Click to Move) system.
    /// 
    /// • Right Click = Move to position  
    /// • Right Click + Left Shift = Sneak Move  
    /// • Right Click + Left Control = Run  
    /// • Left Click = Attack  
    /// • Escape = Stop controlling the brain (removes this component)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Brain))]
    public class BrainController : MonoBehaviour
    {
        private Brain brain;
        private Camera mainCamera;
        private float attackCooldown;

        private void OnEnable()
        {
            brain = GetComponent<Brain>();
            mainCamera = Camera.main;

            // Prevent AI logic from overriding player control
            if (brain != null)
                brain.StopControlConditions["Controller"] = true;
        }

        private void OnDisable()
        {
            // Restore AI control when player controller is disabled
            if (brain != null && brain.StopControlConditions.ContainsKey("Controller"))
                brain.StopControlConditions.Remove("Controller");
        }

        private void Update()
        {
            if (brain == null) return;

            // --- Handle Movement ---
            bool rightClick = Input.GetMouseButtonDown(1);
            bool sneak = Input.GetKey(KeyCode.LeftShift) && rightClick;
            bool run = Input.GetKey(KeyCode.LeftControl) && rightClick;

            if (rightClick)
            {
                float moveSpeed = run ? brain.runSpeed :
                                   sneak ? brain.sneakSpeed :
                                   brain.walkSpeed;

                MoveToMousePosition(moveSpeed);
            }

            // --- Handle Attacks ---
            attackCooldown = Mathf.MoveTowards(attackCooldown, 0f, Time.deltaTime);

            if (Input.GetMouseButtonDown(0) && brain.canAttack && attackCooldown <= 0f)
            {
                attackCooldown = Random.Range(brain.attackTime.x, brain.attackTime.y);
                brain.Attack();
            }

            // --- Stop Control ---
            if (Input.GetKeyDown(KeyCode.Escape))
                Destroy(this);
        }

        /// <summary>
        /// Sends the brain to move toward the clicked point using NavMesh.
        /// </summary>
        private void MoveToMousePosition(float speed)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                brain.MoveTo(hit.point, speed);
            }
        }
    }
}
