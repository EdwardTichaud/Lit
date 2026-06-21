using UnityEngine;

// Reference vers le curseur partage du menu.
public class MenuCursorLink : MonoBehaviour
{
    [SerializeField] private CursorController cursor;

    public CursorController Cursor => cursor;
}
