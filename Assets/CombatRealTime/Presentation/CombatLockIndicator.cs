using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatLockIndicator : MonoBehaviour
{
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private AudioClipSO lockSfx;
    [SerializeField, Min(0f)] private float pulseAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float pulseFrequency = 2f;
    [SerializeField, Min(0f)] private float heightOffset = 0.35f;

    private Camera activeCamera;
    private Vector3 initialScale;
    private bool isLocked;
    private Canvas cursorCanvas;
    private Image cursorImage;
    private Sprite cursorSprite;
    private Texture2D cursorTexture;
    private Renderer[] actorRenderers;
    private GameObject worldCursorPrefab;
    private Vector3 worldCursorOffset = Vector3.up * 3f;
    private bool ownsWorldCursor;

    public void ConfigureWorldCursor(GameObject prefab, Vector3 worldOffset)
    {
        SetLocked(false, false);
        DestroyOwnedVisual();
        if (visualRoot != null) visualRoot.SetActive(false);
        visualRoot = null;
        worldCursorPrefab = prefab;
        worldCursorOffset = worldOffset;
    }

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

        if (ownsWorldCursor)
            visualRoot.transform.position = transform.position + worldCursorOffset;

        if (activeCamera != null)
        {
            if (cursorCanvas != null)
            {
                Vector3 position = transform.position;
                float top = position.y + 2f;
                bool foundBounds = false;
                foreach (Renderer actorRenderer in actorRenderers)
                {
                    if (actorRenderer == null || !actorRenderer.enabled || !(actorRenderer is SkinnedMeshRenderer)) continue;
                    top = foundBounds ? Mathf.Max(top, actorRenderer.bounds.max.y) : actorRenderer.bounds.max.y;
                    foundBounds = true;
                }
                position.y = top + heightOffset;
                Vector3 screen = activeCamera.WorldToScreenPoint(position);
                cursorImage.enabled = screen.z > 0 && activeCamera.pixelRect.Contains(new Vector2(screen.x, screen.y));
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)cursorCanvas.transform,
                    screen, null, out Vector2 local))
                    ((RectTransform)visualRoot.transform).anchoredPosition = local;
            }
            else
            {
                Vector3 directionToCamera = activeCamera.transform.position - visualRoot.transform.position;
                if (directionToCamera.sqrMagnitude > Mathf.Epsilon)
                    visualRoot.transform.rotation = Quaternion.LookRotation(directionToCamera, activeCamera.transform.up);
            }
        }
        else if (cursorImage != null) cursorImage.enabled = false;

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseFrequency) * pulseAmplitude;
        visualRoot.transform.localScale = initialScale * pulse;
    }

    private void OnDisable()
    {
        SetLocked(false, false);
    }

    public void SetLocked(bool locked, bool playSound)
    {
        if (locked && visualRoot == null) CreateCursor();
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

    private void CreateCursor()
    {
        if (worldCursorPrefab != null)
        {
            visualRoot = Instantiate(worldCursorPrefab, transform.position + worldCursorOffset, Quaternion.identity);
            visualRoot.name = worldCursorPrefab.name + " (Locked)";
            initialScale = visualRoot.transform.localScale;
            ownsWorldCursor = true;
            visualRoot.SetActive(false);
            return;
        }
        // Screen-space keeps the target readable regardless of distance or occlusion.
        var canvasObject = new GameObject("CombatLockCursor", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cursorCanvas = canvasObject.GetComponent<Canvas>();
        cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cursorCanvas.sortingOrder = 30;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        visualRoot = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        visualRoot.transform.SetParent(canvasObject.transform, false);
        ((RectTransform)visualRoot.transform).sizeDelta = new Vector2(28, 22);
        cursorImage = visualRoot.GetComponent<Image>();
        cursorImage.raycastTarget = false;
        cursorImage.enabled = false;
        cursorTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        cursorTexture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[32 * 32];
        for (int y = 2; y < 30; y++)
            for (int x = 0; x < 32; x++)
            {
                float halfWidth = (y - 2) * .5f;
                float distance = Mathf.Abs(x - 15.5f);
                if (distance <= halfWidth)
                    pixels[y * 32 + x] = distance > halfWidth - 2 || y > 27
                        ? new Color(.08f, .08f, .08f, 1) : new Color(1, .9f, .45f, 1);
            }
        cursorTexture.SetPixels(pixels);
        cursorTexture.Apply();
        cursorSprite = Sprite.Create(cursorTexture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f));
        cursorImage.sprite = cursorSprite;
        initialScale = Vector3.one;
        actorRenderers = GetComponentsInChildren<Renderer>(true);
        visualRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        DestroyOwnedVisual();
    }

    private void DestroyOwnedVisual()
    {
        if (ownsWorldCursor && visualRoot != null) Destroy(visualRoot);
        if (cursorCanvas != null) Destroy(cursorCanvas.gameObject);
        if (cursorSprite != null) Destroy(cursorSprite);
        if (cursorTexture != null) Destroy(cursorTexture);
        ownsWorldCursor = false;
        cursorCanvas = null;
        cursorImage = null;
        cursorSprite = null;
        cursorTexture = null;
    }

    public void PlayLockSound()
    {
        if (lockSfx != null)
        {
            AudioManager.PlayClipAtPoint(lockSfx, transform.position);
        }
    }
}
