using UnityEngine;

public class ForceLocalRotation : MonoBehaviour
{
    [SerializeField]
    private Vector3 localEulerAngles;

    [SerializeField]
    private bool applyOnStart = true;

    private Quaternion _targetRotation;

    private void Awake()
    {
        _targetRotation = Quaternion.Euler(localEulerAngles);
    }

    private void Start()
    {
        if (applyOnStart)
        {
            transform.localRotation = _targetRotation;
        }
    }

    private void LateUpdate()
    {
        transform.localRotation = _targetRotation;
    }

    public void SetLocalRotation(Vector3 euler)
    {
        localEulerAngles = euler;
        _targetRotation = Quaternion.Euler(euler);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _targetRotation = Quaternion.Euler(localEulerAngles);

        if (!Application.isPlaying)
        {
            transform.localRotation = _targetRotation;
        }
    }
#endif
}