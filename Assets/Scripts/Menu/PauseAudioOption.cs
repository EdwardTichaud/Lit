using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Ligne de menu pause pour ajuster un volume audio au clavier, a la manette ou a la souris.
[DisallowMultipleComponent]
public class PauseAudioOption : MonoBehaviour, IMenuCursorHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public enum VolumeChannel
    {
        Music = 0,
        Sfx = 1
    }

    [SerializeField] private PausePanelController controller;
    [SerializeField] private VolumeChannel channel;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private string optionLabel = "Musique";
    [SerializeField, Range(0.01f, 0.5f)] private float step = 0.1f;
    [SerializeField, Range(0.1f, 0.95f)] private float horizontalDeadzone = 0.5f;
    [SerializeField] private float initialRepeatDelay = 0.35f;
    [SerializeField] private float repeatInterval = 0.12f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool syncCursorOnHover = true;
    [SerializeField] private MenuCursorLink cursorLink;

    private RectTransform rectTransform;
    private bool isFocused;
    private bool holdActive;
    private int holdDirection;
    private float nextRepeatTime;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshLabel();
    }

    private void OnDisable()
    {
        isFocused = false;
        ResetHoldState();
    }

    private void Update()
    {
        if (!isFocused || !CanProcessHorizontalInput())
        {
            ResetHoldState();
            return;
        }

        HandleHorizontalInput();
    }

    public void Configure(PausePanelController pauseController, VolumeChannel volumeChannel, TMP_Text text, string displayLabel, float stepValue)
    {
        controller = pauseController;
        channel = volumeChannel;
        labelText = text;
        optionLabel = displayLabel;
        step = stepValue;
        RefreshLabel();
    }

    public void RefreshLabel()
    {
        if (labelText == null)
        {
            return;
        }

        int percentage = Mathf.RoundToInt(GetCurrentVolume() * 100f);
        string displayLabel = string.IsNullOrWhiteSpace(optionLabel)
            ? channel == VolumeChannel.Music ? "Musique" : "Sons"
            : optionLabel.Trim();
        labelText.text = $"{displayLabel}  < {percentage}% >";
    }

    public void OnCursorFocus()
    {
        isFocused = true;
        RefreshLabel();
        if (syncCursorOnHover)
        {
            SyncSharedCursor();
        }
    }

    public void OnCursorBlur()
    {
        isFocused = false;
        ResetHoldState();
    }

    public void OnCursorSubmit()
    {
        ApplyStep(1);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (syncCursorOnHover)
        {
            SyncSharedCursor();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (rectTransform != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            ApplyStep(localPoint.x >= 0f ? 1 : -1);
        }
        else
        {
            ApplyStep(1);
        }

        if (syncCursorOnHover)
        {
            SyncSharedCursor();
        }
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<PausePanelController>(true);
            if (controller == null)
            {
                controller = FindObjectOfType<PausePanelController>(true);
            }
        }

        if (cursorLink == null)
        {
            cursorLink = GetComponentInParent<MenuCursorLink>();
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private bool CanProcessHorizontalInput()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        return controller == null || controller.IsOpen;
    }

    private void HandleHorizontalInput()
    {
        Vector2 move = LocalInputRouter.MoveValue;
        float horizontal = move.x;
        float vertical = move.y;
        float deadzone = Mathf.Clamp(horizontalDeadzone, 0.1f, 0.95f);
        if (Mathf.Abs(horizontal) < deadzone || Mathf.Abs(horizontal) <= Mathf.Abs(vertical))
        {
            ResetHoldState();
            return;
        }

        int direction = horizontal > 0f ? 1 : -1;
        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (!holdActive || holdDirection != direction)
        {
            ApplyStep(direction);
            holdActive = true;
            holdDirection = direction;
            nextRepeatTime = now + Mathf.Max(0.01f, initialRepeatDelay);
            return;
        }

        if (now < nextRepeatTime)
        {
            return;
        }

        ApplyStep(direction);
        nextRepeatTime = now + Mathf.Max(0.01f, repeatInterval);
    }

    private void ApplyStep(int direction)
    {
        float stepValue = Mathf.Clamp(step, 0.01f, 1f);
        float target = Mathf.Clamp01(GetCurrentVolume() + (stepValue * Mathf.Sign(direction)));
        SetCurrentVolume(target);
        RefreshLabel();
    }

    private float GetCurrentVolume()
    {
        AudioManager manager = ResolveAudioManager();
        switch (channel)
        {
            case VolumeChannel.Sfx:
                return manager != null ? manager.SfxVolume : AudioManager.GetSavedSfxVolume();
            case VolumeChannel.Music:
            default:
                return manager != null ? manager.MusicVolume : AudioManager.GetSavedMusicVolume();
        }
    }

    private void SetCurrentVolume(float value)
    {
        AudioManager manager = ResolveAudioManager();
        switch (channel)
        {
            case VolumeChannel.Sfx:
                if (manager != null)
                {
                    manager.SetSfxVolume(value);
                }
                else
                {
                    AudioManager.SaveSfxVolumePreference(value);
                }
                break;
            case VolumeChannel.Music:
            default:
                if (manager != null)
                {
                    manager.SetMusicVolume(value);
                }
                else
                {
                    AudioManager.SaveMusicVolumePreference(value);
                }
                break;
        }
    }

    private AudioManager ResolveAudioManager()
    {
        if (AudioManager.Instance != null)
        {
            return AudioManager.Instance;
        }

        AudioManager manager = FindFirstObjectByType<AudioManager>();
        if (manager != null)
        {
            return manager;
        }

        return FindObjectOfType<AudioManager>(true);
    }

    private void ResetHoldState()
    {
        holdActive = false;
        holdDirection = 0;
        nextRepeatTime = 0f;
    }

    private void SyncSharedCursor()
    {
        CursorController sharedCursor = cursorLink != null ? cursorLink.Cursor : controller != null ? controller.cursorController : null;
        if (sharedCursor == null || rectTransform == null)
        {
            return;
        }

        MenuCursorSyncUtility.SyncCursorToItem(sharedCursor, rectTransform);
    }
}
