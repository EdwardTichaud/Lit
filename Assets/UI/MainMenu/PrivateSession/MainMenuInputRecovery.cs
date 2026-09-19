using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Keeps a remembered input preference from trapping the player in the menu.
[DefaultExecutionOrder(-101)]
public sealed class MainMenuInputRecovery : MonoBehaviour
{
    private void Update()
    {
        PrivateSessionService session = PrivateSessionService.Instance;
        bool inMenu = SceneManager.GetActiveScene().name == MainMenuController.DefaultMenuSceneName ||
            (session != null && (session.IsBusy || session.Phase == PrivateSessionPhase.Lobby));
        if (!inMenu || MainMenuInputSettings.GetCurrentMode() == MainMenuInputSettings.InputMode.Automatic)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad gamepad = Gamepad.current;
        bool keyboardOrMouseUsed = (keyboard != null && keyboard.anyKey.wasPressedThisFrame) ||
            (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame));
        bool gamepadUsed = gamepad != null && (gamepad.leftStick.ReadValue().sqrMagnitude > .2f || gamepad.dpad.ReadValue().sqrMagnitude > .2f ||
            gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame ||
            gamepad.buttonWest.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);

        if ((!MainMenuInputSettings.AllowsKeyboardMouse() && keyboardOrMouseUsed) ||
            (!MainMenuInputSettings.AllowsGamepad() && gamepadUsed))
        {
            MainMenuInputSettings.SetMode(MainMenuInputSettings.InputMode.Automatic);
        }
    }
}
