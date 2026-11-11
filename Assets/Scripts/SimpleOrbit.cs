
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SimpleOrbit : MonoBehaviour
{
    public GameObject targetObject;
    public GameObject orbitCenter;

    public Vector3 orbitAxis = Vector3.up;
    public float speed = 30f;
    public float radius = 5f;

    public bool enableInEditMode = false;

    private Vector3 offset;

#if UNITY_EDITOR
    private double lastTime;
#endif

    private void OnEnable()
    {
        if (targetObject != null && orbitCenter != null)
            offset = targetObject.transform.position - orbitCenter.transform.position;

#if UNITY_EDITOR
        lastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += EditorUpdate;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
    }

    private void Update()
    {
        if (Application.isPlaying)
            Rotate(Time.deltaTime);
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - lastTime), 0.05f);
        lastTime = now;

        if (!Application.isPlaying && enableInEditMode)
            Rotate(dt);
    }
#endif

    private void Rotate(float dt)
    {
        if (targetObject == null || orbitCenter == null)
            return;

        if (orbitAxis.sqrMagnitude < 0.000001f)
            return;

        if (offset.sqrMagnitude < 0.000001f)
            offset = Vector3.forward;

        offset = Quaternion.AngleAxis(
            speed * dt,
            orbitAxis.normalized
        ) * offset;

        targetObject.transform.position =
            orbitCenter.transform.position + offset.normalized * Mathf.Max(0f, radius);
    }
}
