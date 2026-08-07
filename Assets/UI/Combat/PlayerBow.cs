using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBow : MonoBehaviour
{
    [SerializeField] private GameObject appearVfx;
    [SerializeField] private GameObject disappearVfx;

    public bool IsManifested => gameObject.activeSelf;
    public event System.Action<PlayerBow, bool> ManifestationChanged;

    public void Show()
    {
        if (gameObject.activeSelf)
        {
            return;
        }

        gameObject.SetActive(true);
        SpawnVfx(appearVfx);
        ManifestationChanged?.Invoke(this, true);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        SpawnVfx(disappearVfx);
        gameObject.SetActive(false);
        ManifestationChanged?.Invoke(this, false);
    }

    private void SpawnVfx(GameObject vfxPrefab)
    {
        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, transform.position, transform.rotation);
        }
    }
}
