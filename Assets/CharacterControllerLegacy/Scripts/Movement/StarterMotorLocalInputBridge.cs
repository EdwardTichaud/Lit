using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(StarterInspiredThirdPersonMotor))]
public sealed class StarterMotorLocalInputBridge : MonoBehaviour
{
    [SerializeField] private StarterInspiredThirdPersonMotor motor;
    [SerializeField] private bool readKeyboard = true;
    [SerializeField] private bool readGamepad = true;
    [SerializeField] private bool readJump = true;
    [SerializeField] private bool readFlightControls = true;

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
        if (readFlightControls)
        {
            bool boostOrSprintInput = ReadBoostInput();
            motor.SetBoostInput(boostOrSprintInput);
            motor.SetSprintInput(boostOrSprintInput);
            motor.SetFlightVerticalInput(ReadFlightVerticalInput());
        }

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
            motor.SetBoostInput(false);
            motor.SetSprintInput(false);
            motor.SetFlightVerticalInput(0f);
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

    private bool ReadBoostInput()
    {
        if (readKeyboard && Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
        {
            return true;
        }

        return readGamepad &&
               Gamepad.current != null &&
               Gamepad.current.rightShoulder.isPressed;
    }

    private float ReadFlightVerticalInput()
    {
        float verticalInput = 0f;

        if (readKeyboard && Keyboard.current != null)
        {
            if (Keyboard.current.eKey.isPressed)
            {
                verticalInput += 1f;
            }

            if (Keyboard.current.qKey.isPressed)
            {
                verticalInput -= 1f;
            }
        }

        if (readGamepad && Gamepad.current != null)
        {
            Gamepad gamepad = Gamepad.current;
            verticalInput += gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
        }

        return Mathf.Clamp(verticalInput, -1f, 1f);
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
