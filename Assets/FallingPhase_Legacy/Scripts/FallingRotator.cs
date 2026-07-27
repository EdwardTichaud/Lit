using UnityEngine;

[DisallowMultipleComponent]
public sealed class FallingRotator : MonoBehaviour
{
    [SerializeField] private Transform orbitingVisual;
    [SerializeField] private Vector3 orbitAxis = Vector3.up;
    [SerializeField, Min(0f)] private float orbitDegreesPerSecond = 70f;
    [SerializeField] private Vector3 selfSpinAxis = new Vector3(0.35f, 1f, 0.2f);
    [SerializeField, Min(0f)] private float selfSpinDegreesPerSecond = 180f;

    private void Update()
    {
        if (orbitingVisual == null)
        {
            return;
        }

        orbitingVisual.RotateAround(transform.position, orbitAxis.normalized, orbitDegreesPerSecond * Time.deltaTime);
        orbitingVisual.Rotate(selfSpinAxis.normalized, selfSpinDegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
