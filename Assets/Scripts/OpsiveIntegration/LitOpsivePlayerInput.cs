using System.Collections.Generic;
using UnityEngine;
using InputAction = UnityEngine.InputSystem.InputAction;
using OpsivePlayerInput = Opsive.Shared.Input.PlayerInput;

// Bridges the project's LocalInputRouter into Opsive's input abstraction.
public class LitOpsivePlayerInput : OpsivePlayerInput
{
    private static readonly string[] JumpNames = { "jump" };
    private static readonly string[] SprintNames = { "changespeeds", "changespeed", "sprint", "run", "rightshoulder" };
    private static readonly string[] CrouchNames = { "crouch", "heightchange", "locomotionmode" };

    [SerializeField, Tooltip("Use LocalInputRouter when no explicit network/facade override is provided.")]
    private bool fallbackToLocalInputRouter = true;

    private readonly Dictionary<string, int> buttonDownFrames = new Dictionary<string, int>();
    private readonly Dictionary<string, int> buttonUpFrames = new Dictionary<string, int>();
    private readonly HashSet<string> heldButtons = new HashSet<string>();

    private bool hasMovementOverride;
    private Vector2 movementOverride;
    private bool hasSprintOverride;
    private bool sprintOverride;

    protected override bool CanCheckForController => false;

    public void SetMovementOverride(Vector2 value, bool active)
    {
        hasMovementOverride = active;
        movementOverride = active ? Vector2.ClampMagnitude(value, 1f) : Vector2.zero;
    }

    public void SetSprintOverride(bool pressed, bool active)
    {
        hasSprintOverride = active;
        sprintOverride = active && pressed;
        SetHeld("changespeeds", sprintOverride);
        SetHeld("sprint", sprintOverride);
        SetHeld("run", sprintOverride);
        SetHeld("rightshoulder", sprintOverride);
    }

    public void PulseButton(string buttonName)
    {
        string normalized = Normalize(buttonName);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        buttonDownFrames[normalized] = Time.frameCount;
        buttonUpFrames[normalized] = Time.frameCount + 1;
    }

    private void OnEnable()
    {
        LocalInputRouter.Jump += OnJump;
        LocalInputRouter.LocomotionMode += OnLocomotionMode;
    }

    private void OnDisable()
    {
        LocalInputRouter.Jump -= OnJump;
        LocalInputRouter.LocomotionMode -= OnLocomotionMode;
        heldButtons.Clear();
        buttonDownFrames.Clear();
        buttonUpFrames.Clear();
        hasMovementOverride = false;
        hasSprintOverride = false;
    }

    private void LateUpdate()
    {
        ClearOldButtonFrames(buttonDownFrames);
        ClearOldButtonFrames(buttonUpFrames);
    }

    protected override float GetAxisInternal(string buttonName)
    {
        return GetAxisRawInternal(buttonName);
    }

    protected override float GetAxisRawInternal(string buttonName)
    {
        Vector2 movement = ResolveMovement();
        switch (Normalize(buttonName))
        {
            case "horizontal":
            case "x":
            case "movehorizontal":
                return movement.x;
            case "vertical":
            case "y":
            case "forward":
            case "moveforward":
                return movement.y;
            default:
                return 0f;
        }
    }

    protected override bool GetButtonInternal(string buttonName)
    {
        string normalized = Normalize(buttonName);
        if (heldButtons.Contains(normalized))
        {
            return true;
        }

        if (Matches(normalized, SprintNames))
        {
            return hasSprintOverride ? sprintOverride : fallbackToLocalInputRouter && LocalInputRouter.RightShoulderPressed;
        }

        return IsCurrentFrame(buttonDownFrames, normalized);
    }

    protected override bool GetButtonDownInternal(string buttonName)
    {
        string normalized = Normalize(buttonName);
        if (IsCurrentFrame(buttonDownFrames, normalized))
        {
            return true;
        }

        if (Matches(normalized, SprintNames))
        {
            return IsCurrentFrame(buttonDownFrames, "changespeeds");
        }

        if (Matches(normalized, JumpNames))
        {
            return IsCurrentFrame(buttonDownFrames, "jump");
        }

        if (Matches(normalized, CrouchNames))
        {
            return IsCurrentFrame(buttonDownFrames, "crouch");
        }

        return false;
    }

    protected override bool GetButtonUpInternal(string buttonName)
    {
        string normalized = Normalize(buttonName);
        if (IsCurrentFrame(buttonUpFrames, normalized))
        {
            return true;
        }

        if (Matches(normalized, SprintNames))
        {
            return IsCurrentFrame(buttonUpFrames, "changespeeds");
        }

        return false;
    }

    public override Vector2 GetLookVector(bool smoothed)
    {
        // The current project camera remains authoritative for now.
        return Vector2.zero;
    }

    private Vector2 ResolveMovement()
    {
        if (hasMovementOverride)
        {
            return movementOverride;
        }

        return fallbackToLocalInputRouter ? Vector2.ClampMagnitude(LocalInputRouter.MoveValue, 1f) : Vector2.zero;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        PulseButton("jump");
    }

    public void OnJump(UnityEngine.InputSystem.InputValue value)
    {
        if (value != null && !value.isPressed)
        {
            return;
        }

        PulseButton("jump");
    }

    public void OnLocomotionMode(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        if (ShouldLetFacadeHandleLocomotionMode())
        {
            return;
        }

        PulseButton("crouch");
    }

    public void OnLocomotionMode(UnityEngine.InputSystem.InputValue value)
    {
        if (value != null && !value.isPressed)
        {
            return;
        }

        if (ShouldLetFacadeHandleLocomotionMode())
        {
            return;
        }

        PulseButton("crouch");
    }

    private bool ShouldLetFacadeHandleLocomotionMode()
    {
        LitOpsiveLocomotionBridge bridge = GetComponent<LitOpsiveLocomotionBridge>();
        return bridge != null && bridge.IsDriving;
    }

    private void SetHeld(string buttonName, bool pressed)
    {
        string normalized = Normalize(buttonName);
        bool wasPressed = heldButtons.Contains(normalized);
        if (pressed)
        {
            heldButtons.Add(normalized);
            if (!wasPressed)
            {
                buttonDownFrames[normalized] = Time.frameCount;
            }
        }
        else if (heldButtons.Remove(normalized))
        {
            buttonUpFrames[normalized] = Time.frameCount;
        }
    }

    private static bool Matches(string normalized, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (normalized == names[i])
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCurrentFrame(Dictionary<string, int> frames, string buttonName)
    {
        return frames.TryGetValue(buttonName, out int frame) && frame == Time.frameCount;
    }

    private static void ClearOldButtonFrames(Dictionary<string, int> frames)
    {
        if (frames.Count == 0)
        {
            return;
        }

        s_ExpiredButtons.Clear();
        foreach (KeyValuePair<string, int> pair in frames)
        {
            if (pair.Value < Time.frameCount)
            {
                s_ExpiredButtons.Add(pair.Key);
            }
        }

        for (int i = 0; i < s_ExpiredButtons.Count; i++)
        {
            frames.Remove(s_ExpiredButtons[i]);
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private static readonly List<string> s_ExpiredButtons = new List<string>();
}
