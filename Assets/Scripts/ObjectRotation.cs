
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ObjectRotation : MonoBehaviour
{
    [Header("GameObject")]
    public GameObject targetObject;

    [Header("Rotation Settings")]
    public Vector3 rotationSpeed = new Vector3(20f, 30f, 10f);
    public Space rotationSpace = Space.Self;

    [Header("Options")]
    public bool enableInEditMode = false;
    public bool randomInitialRotation = false;

    [Header("Baked Origin")]
    public bool hasBakedOrigin = false;
    public Quaternion bakedRotation = Quaternion.identity;

#if UNITY_EDITOR
    private double lastEditorTime;
#endif

    private void OnEnable()
    {
#if UNITY_EDITOR
        lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += EditorUpdate;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
    }

    private void Start()
    {
        if (Application.isPlaying &&
            randomInitialRotation &&
            targetObject != null)
        {
            targetObject.transform.rotation =
                Random.rotationUniform;
        }
    }

    public void BakeOrigin()
    {
        if (targetObject == null)
        {
            Debug.LogWarning("Target Object not assigned.");
            return;
        }

        bakedRotation = targetObject.transform.rotation;
        hasBakedOrigin = true;
    }

    public void ResetToOrigin()
    {
        if (targetObject == null || !hasBakedOrigin)
            return;

        targetObject.transform.rotation = bakedRotation;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        StepRotation(Time.deltaTime);
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;

        float dt = Mathf.Clamp(
            (float)(now - lastEditorTime),
            0f,
            0.05f
        );

        lastEditorTime = now;

        if (Application.isPlaying || !enableInEditMode)
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!gameObject.scene.IsValid() ||
            targetObject == null ||
            !targetObject.scene.IsValid())
            return;

        StepRotation(dt);
    }
#endif

    private void StepRotation(float dt)
    {
        if (targetObject == null)
            return;

        targetObject.transform.Rotate(
            rotationSpeed * dt,
            rotationSpace
        );
    }
}
