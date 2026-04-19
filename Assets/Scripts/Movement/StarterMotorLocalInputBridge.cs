using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(StarterInspiredThirdPersonMotor))]
public sealed class StarterMotorLocalInputBridge : MonoBehaviour
{
    [SerializeField] private StarterInspiredThirdPersonMotor motor;
    [SerializeField] private bool readKeyboard = true;
    [SerializeField] private bool readGamepad = true;
    [SerializeField] private bool readJump = true;

    private void Reset()
    {
        motor = GetComponent<StarterInspiredThirdPersonMotor>();
    }

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<StarterInspiredThirdPersonMotor>();
        }
    }

    private void Update()
    {
        if (motor == null)
        {
            return;
        }

        motor.SetMoveInput(ReadMoveInput());

        if (readJump && ReadJumpPressedThisFrame())
        {
            motor.RequestJump();
        }
    }

    private void OnDisable()
    {
        if (motor != null)
        {
            motor.Stop();
        }
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

        if (readKeyboard && Keyboard.current != null)
        {
            input += ReadKeyboardInput(Keyboard.current);
        }

        if (readGamepad && Gamepad.current != null)
        {
            Vector2 gamepadInput = Gamepad.current.leftStick.ReadValue();
            if (gamepadInput.sqrMagnitude > input.sqrMagnitude)
            {
                input = gamepadInput;
            }
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    private bool ReadJumpPressedThisFrame()
    {
        if (readKeyboard && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        return readGamepad &&
               Gamepad.current != null &&
               Gamepad.current.buttonSouth.wasPressedThisFrame;
    }

    private static Vector2 ReadKeyboardInput(Keyboard keyboard)
    {
        Vector2 input = Vector2.zero;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            input.x -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            input.x += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            input.y -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            input.y += 1f;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }
}
