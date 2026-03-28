using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Pivot : MonoBehaviour
{
    [Header("Pivot")]
    [SerializeField, Tooltip("Axe de rotation local utilise pour le pivot.")]
    private Vector3 localAxis = Vector3.up;
    [SerializeField, Tooltip("Angle applique a chaque declenchement.")]
    private float angleDegrees = 90f;
    [SerializeField, Tooltip("Duree du pivot en secondes.")]
    private float duration = 1f;
    [SerializeField, Tooltip("Active des logs de debug.")]
    private bool logDebug;

    private Coroutine pivotRoutine;

    public void TriggerPivot()
    {
        Quaternion startRotation = transform.localRotation;
        Vector3 normalizedAxis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
        Quaternion targetRotation = startRotation * Quaternion.AngleAxis(angleDegrees, normalizedAxis);

        if (pivotRoutine != null)
        {
            StopCoroutine(pivotRoutine);
        }

        pivotRoutine = StartCoroutine(RotateToTarget(startRotation, targetRotation));
    }

    private IEnumerator RotateToTarget(Quaternion startRotation, Quaternion targetRotation)
    {
        float elapsed = 0f;
        float pivotDuration = Mathf.Max(0.01f, duration);
        while (elapsed < pivotDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pivotDuration);
            transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.localRotation = targetRotation;
        pivotRoutine = null;

        if (logDebug)
        {
            Debug.Log($"[Pivot] event='completed' target='{name}' angle={angleDegrees} duration={pivotDuration}", this);
        }
    }
}
