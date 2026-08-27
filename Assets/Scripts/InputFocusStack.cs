// Façade de compatibilité : UIManager possède désormais l'état du focus UI.
public interface ICameraInputPassthrough
{
    bool AllowCameraInput { get; }
}

public static class InputFocusStack
{
    public static bool HasAnyFocus() => UIManager.HasAnyFocus();
    public static bool HasAnyFocusBlockingCamera() => UIManager.HasAnyFocusBlockingCamera();
    public static bool HasFocus(object owner) => UIManager.HasFocus(owner);
    public static void Push(object owner) => UIManager.PushFocus(owner, InputMode.UserInterface);
    public static void PushExclusive(object owner) => UIManager.PushFocus(owner, InputMode.UserInterface, true);
    public static void PushDialogue(object owner) => UIManager.PushFocus(owner, InputMode.Dialogue);
    public static void PushPlacement(object owner) => UIManager.PushFocus(owner, InputMode.Placement);
    public static void Pop(object owner) => UIManager.PopFocus(owner);
    public static void Clear() => UIManager.ClearFocus();
}
