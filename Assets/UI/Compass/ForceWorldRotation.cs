using UnityEngine;

public class ForceWorldRotation : MonoBehaviour
{
    [SerializeField]
    private Vector3 worldEulerAngles;

    [SerializeField]
    private bool applyOnStart = true;

    private Quaternion _targetRotation;

    private void Awake()
    {
        _targetRotation = Quaternion.Euler(worldEulerAngles);
    }

    private void Start()
    {
        if (applyOnStart)
        {
            transform.rotation = _targetRotation;
        }
    }

    private void LateUpdate()
    {
        transform.rotation = _targetRotation;
    }

    public void SetWorldRotation(Vector3 euler)
    {
        worldEulerAngles = euler;
        _targetRotation = Quaternion.Euler(euler);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _targetRotation = Quaternion.Euler(worldEulerAngles);

        if (!Application.isPlaying)
        {
            transform.rotation = _targetRotation;
        }
    }
#endif
}