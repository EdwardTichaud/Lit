using UnityEngine;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Controls the camera to automatically follow any active <see cref="BrainController"/> in the scene.  
    /// If no controller is currently assigned, a left-click on any object with a <see cref="Brain"/> 
    /// component will give it a <see cref="BrainController"/> to enable player control.
    /// 
    /// • Automatically follows the active controlled agent.  
    /// • Left Click on a Brain = Take control of it.  
    /// • 'A' Key = Call nearby agents to the controlled agent's position.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        public Vector3 offset = new Vector3(0f, 10f, -10f);
        public float smoothness = 5f;

        private BrainController currentController;
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (currentController != null)
            {
                FollowTarget();

                // Press 'A' to call nearby AI agents to the controlled agent's position
                if (Input.GetKeyDown(KeyCode.A))
                {
                    if (currentController.TryGetComponent<Brain>(out var brain))
                        brain.CallAgents(currentController.transform.position);
                }
            }
            else
            {
                // Continuously search for an active controller in the scene
                currentController = FindObjectOfType<BrainController>();

                // Left Click on a Brain to give it a controller
                if (Input.GetMouseButtonDown(0))
                    TryAssignControllerToBrain();
            }
        }

        /// <summary>
        /// Smoothly follows and looks at the controlled agent.
        /// </summary>
        private void FollowTarget()
        {
            Transform target = currentController.transform;
            transform.position = Vector3.Lerp(
                transform.position,
                target.position + offset,
                Time.deltaTime * smoothness
            );

            transform.LookAt(target);
        }

        /// <summary>
        /// Checks if the clicked object has a <see cref="Brain"/> component and gives it a controller.
        /// </summary>
        private void TryAssignControllerToBrain()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.TryGetComponent(out Brain brain))
                {
                    brain.gameObject.AddComponent<BrainController>();
                }
            }
        }
    }
}
