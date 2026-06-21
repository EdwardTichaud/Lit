using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class MenuCursorButtonHandler : MonoBehaviour, IMenuCursorHandler
{
    [SerializeField] private Button button;
    [SerializeField] private bool selectButtonOnFocus = true;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    public void OnCursorFocus()
    {
        if (selectButtonOnFocus && button != null && button.interactable)
        {
            button.Select();
        }
    }

    public void OnCursorBlur()
    {
    }

    public void OnCursorSubmit()
    {
        if (button != null && button.interactable)
        {
            button.onClick.Invoke();
        }
    }
}
