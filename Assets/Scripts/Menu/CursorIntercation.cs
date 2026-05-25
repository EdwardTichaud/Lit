using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Interaction ciblee par le pointeur du MainMenu.
// Le nom conserve la graphie demandee pour les objets de scene.
[DisallowMultipleComponent]
public class CursorIntercation : MonoBehaviour
{
    [Header("Outline")]
    [SerializeField] private bool createOutlineIfMissing = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onCursorEnter;
    [SerializeField] private UnityEvent onCursorExit;
    [SerializeField] private UnityEvent onCursorClick;

    private readonly List<RuntimeOutlineTarget> outlineTargets = new List<RuntimeOutlineTarget>();
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
        for (int i = 0; i < outlineTargets.Count; i++)
        {
            RuntimeOutlineTarget target = outlineTargets[i];
            if (target != null)
            {
                target.SetOutlined(visible);
            }
        }
    }

    private void ResolveOutline()
    {
        RuntimeOutlineUtility.CollectOutlineTargets(this, outlineTargets, createOutlineIfMissing && Application.isPlaying);
    }
}
