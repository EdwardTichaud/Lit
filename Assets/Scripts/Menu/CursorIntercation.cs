using UnityEngine;
using UnityEngine.Events;

// Interaction ciblee par le pointeur du MainMenu.
// Le nom conserve la graphie demandee pour les objets de scene.
[DisallowMultipleComponent]
public class CursorIntercation : MonoBehaviour
{
    [Header("Outline")]
    [SerializeField] private Outline outline;
    [SerializeField] private bool createOutlineIfMissing = true;
    [SerializeField] private Color outlineColor = new Color(1f, 0.82f, 0.35f, 1f);
    [SerializeField, Range(0f, 10f)] private float outlineWidth = 4f;
    [SerializeField] private Outline.Mode outlineMode = Outline.Mode.OutlineAll;

    [Header("Events")]
    [SerializeField] private UnityEvent onCursorEnter;
    [SerializeField] private UnityEvent onCursorExit;
    [SerializeField] private UnityEvent onCursorClick;

    private bool hovered;

    private void Awake()
    {
        ConfigureOutline(false);
    }

    private void OnDisable()
    {
        SetCursorHovered(false);
    }

    public void SetCursorHovered(bool value)
    {
        if (hovered == value)
        {
            return;
        }

        hovered = value;
        ConfigureOutline(hovered);

        if (hovered)
        {
            onCursorEnter?.Invoke();
        }
        else
        {
            onCursorExit?.Invoke();
        }
    }

    public void NotifyCursorClick()
    {
        onCursorClick?.Invoke();
    }

    private void ConfigureOutline(bool visible)
    {
        ResolveOutline();
        if (outline == null)
        {
            return;
        }

        outline.OutlineMode = outlineMode;
        outline.OutlineColor = outlineColor;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = visible;
    }

    private void ResolveOutline()
    {
        if (outline != null || !createOutlineIfMissing)
        {
            return;
        }

        outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = GetComponentInChildren<Outline>(true);
        }

        if (outline == null && Application.isPlaying)
        {
            outline = gameObject.AddComponent<Outline>();
        }
    }
}
