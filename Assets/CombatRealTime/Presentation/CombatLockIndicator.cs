using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatLockIndicator : MonoBehaviour
{
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private AudioClipSO lockSfx;
    [SerializeField, Min(0f)] private float pulseAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float pulseFrequency = 2f;

    private Camera activeCamera;
    private Vector3 initialScale;
    private bool isLocked;

    private void Awake()
    {
        if (visualRoot != null)
        {
            initialScale = visualRoot.transform.localScale;
            visualRoot.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!isLocked || visualRoot == null)
        {
            return;
        }

        if (activeCamera == null)
        {
            activeCamera = Camera.main;
        }

        if (activeCamera != null)
        {
            Vector3 directionToCamera = activeCamera.transform.position - visualRoot.transform.position;
            if (directionToCamera.sqrMagnitude > Mathf.Epsilon)
            {
                visualRoot.transform.rotation = Quaternion.LookRotation(directionToCamera, activeCamera.transform.up);
            }
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseFrequency) * pulseAmplitude;
        visualRoot.transform.localScale = initialScale * pulse;
    }

    private void OnDisable()
    {
        SetLocked(false, false);
    }

    public void SetLocked(bool locked, bool playSound)
    {
        if (isLocked == locked)
        {
            return;
        }

        isLocked = locked;
        if (visualRoot != null)
        {
            visualRoot.SetActive(locked);
            if (!locked)
            {
                visualRoot.transform.localScale = initialScale;
            }
        }

        if (locked && playSound && lockSfx != null)
        {
            PlayLockSound();
        }
    }

    public void PlayLockSound()
    {
        if (lockSfx != null)
        {
            AudioManager.PlayClipAtPoint(lockSfx, transform.position);
        }
    }
}
