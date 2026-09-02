using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sole runtime authority for the shared PlayerInputs ActionMaps. Context owners
/// acquire a mode and the most recent owner exclusively determines active maps.
/// </summary>
public enum InputMode
{
    Exploration,
    Dialogue,
    UserInterface,
    Placement,
    Combat,
    ThresholdSequence,
    CombatQTE,
    CombatWheel,
    Cinematic,
    Disabled
}

public enum InputModeAction { Submit, Cancel, Navigate, Pause }

public interface IInputModeHandler
{
    bool HandleInputModeAction(InputModeAction action, InputAction.CallbackContext context);
}

public sealed class InputModeCoordinator : MonoBehaviour
{
    private readonly struct Entry
    {
        public readonly object Owner;
        public readonly InputMode Mode;
        public Entry(object owner, InputMode mode) { Owner = owner; Mode = mode; }
    }

    private static InputModeCoordinator instance;
    private static readonly object BaseOwner = new object();
    private readonly List<Entry> stack = new List<Entry>();
    private InputActionAsset actions;
    private InputMode baseMode = InputMode.Exploration;
    private string lastTransition = "Initialisation";

    public static InputMode CurrentMode => instance != null ? instance.ResolveMode() : InputMode.Exploration;
    public static string Diagnostics => instance != null ? instance.BuildDiagnostics() : "InputModeCoordinator non initialise.";
    public static event Action<InputMode> ModeChanged;

    public static void Configure(InputActionAsset asset)
    {
        if (!Application.isPlaying || asset == null) return;
        if (instance == null)
        {
            GameObject host = new GameObject("InputModeCoordinator");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<InputModeCoordinator>();
        }

        instance.actions = asset;
        instance.BindModeActions();
        instance.Apply("Configure");
    }

    public static void SetBaseMode(InputMode mode)
    {
        if (instance == null) return;
        instance.baseMode = mode;
        instance.Apply("Base=" + mode);
    }

    public static void Enter(object owner, InputMode mode)
    {
        if (instance == null || owner == null) return;
        instance.Remove(owner);
        instance.stack.Add(new Entry(owner, mode));
        instance.Apply("Enter " + mode + " (" + OwnerName(owner) + ")");
    }

    public static void Exit(object owner)
    {
        if (instance == null || owner == null) return;
        if (instance.Remove(owner)) instance.Apply("Exit (" + OwnerName(owner) + ")");
    }

    public static void Clear()
    {
        if (instance == null) return;
        instance.stack.Clear();
        instance.baseMode = InputMode.Exploration;
        instance.Apply("Clear");
    }

    public static bool IsGameplayBlocked => CurrentMode != InputMode.Exploration;
    public static bool IsCameraAllowed => CurrentMode == InputMode.Exploration || CurrentMode == InputMode.Dialogue ||
                                          CurrentMode == InputMode.Placement || CurrentMode == InputMode.Combat ||
                                          CurrentMode == InputMode.ThresholdSequence || CurrentMode == InputMode.CombatQTE;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        // Les objets Unity detruits ne declenchent pas toujours leur chemin de
        // sortie applicatif. Ce garde-fou restitue le profil precedent.
        if (PurgeDestroyedOwners())
        {
            Apply("Purge owner detruit");
        }
    }

    private void BindModeActions()
    {
        Bind("Dialogue", "Advance", InputModeAction.Submit);
        Bind("Dialogue", "Cancel", InputModeAction.Cancel);
        Bind("Dialogue", "Navigate", InputModeAction.Navigate);
        Bind("UI", "Submit", InputModeAction.Submit);
        Bind("UI", "Cancel", InputModeAction.Cancel);
        Bind("UI", "Navigate", InputModeAction.Navigate);
        Bind("Placement", "Confirm", InputModeAction.Submit);
        Bind("Placement", "Cancel", InputModeAction.Cancel);
        Bind("Placement", "MovePreview", InputModeAction.Navigate);
        Bind("System", "Pause", InputModeAction.Pause);
    }

    private void Bind(string mapName, string actionName, InputModeAction modeAction)
    {
        InputAction action = actions != null ? actions.FindAction(mapName + "/" + actionName, false) : null;
        if (action == null) return;
        action.performed -= OnModeAction;
        action.performed += OnModeAction;
    }

    private void OnModeAction(InputAction.CallbackContext context)
    {
        InputModeAction action = context.action.name switch
        {
            "Advance" => InputModeAction.Submit,
            "Submit" => InputModeAction.Submit,
            "Confirm" => InputModeAction.Submit,
            "Cancel" => InputModeAction.Cancel,
            "Navigate" => InputModeAction.Navigate,
            "MovePreview" => InputModeAction.Navigate,
            "Pause" => InputModeAction.Pause,
            _ => InputModeAction.Submit
        };

        object owner = TopOwner;
        if (owner is IInputModeHandler handler && handler.HandleInputModeAction(action, context)) return;

        switch (action)
        {
            case InputModeAction.Submit: LocalInputRouter.RaiseInteract(context); break;
            case InputModeAction.Cancel: LocalInputRouter.RaiseReturn(context); break;
            case InputModeAction.Pause: LocalInputRouter.RaiseStart(context); break;
        }
    }

    private void Apply(string transition)
    {
        if (actions == null) return;
        PurgeDestroyedOwners();
        InputMode mode = ResolveMode();
        actions.Disable();
        foreach (string mapName in GetMaps(mode))
        {
            actions.FindActionMap(mapName, false)?.Enable();
        }

        LocalInputRouter.ResetMove();
        LocalInputRouter.ResetCamera();
        lastTransition = transition;
        ModeChanged?.Invoke(mode);

        // A disabled ActionMap does not emit a new callback for a stick or
        // shoulder button that stays held when it comes back. Re-read those
        // controls on the next frame so a cinematic/UI handoff cannot leave
        // locomotion permanently at zero.
        if (mode == InputMode.Exploration || mode == InputMode.Combat)
        {
            LocalPlayerInput.RequestHeldLocomotionReconciliation("InputMode " + mode);
        }
    }

    private static IEnumerable<string> GetMaps(InputMode mode)
    {
        switch (mode)
        {
            case InputMode.Exploration: yield return "Player"; yield return "Camera"; yield break;
            case InputMode.Dialogue: yield return "Dialogue"; yield return "Camera"; yield break;
            case InputMode.UserInterface: yield return "UI"; yield break;
            case InputMode.Placement: yield return "Placement"; yield return "Camera"; yield break;
            // Le combat conserve le mouvement, la camera et le verrouillage
            // manuel. Les actions monde sont filtrees par le contexte Combat.
            case InputMode.Combat:
                yield return "Player";
                yield return "Camera";
                yield return "RealTimeCombat";
                yield break;
            // Les paliers n'ont pas de camera cinematographique. La vue reste
            // libre pendant que locomotion et actions demeurent bloquees.
            case InputMode.ThresholdSequence:
                yield return "Camera";
                yield break;
            case InputMode.CombatWheel: yield return "CombatWheel"; yield break;
            case InputMode.CombatQTE:
                yield return "Camera";
                yield return "CombatQTE";
                yield break;
            case InputMode.Cinematic:
            case InputMode.Disabled: yield break;
        }
    }

    private InputMode ResolveMode() => stack.Count == 0 ? baseMode : stack[stack.Count - 1].Mode;
    private object TopOwner => stack.Count == 0 ? BaseOwner : stack[stack.Count - 1].Owner;

    private bool Remove(object owner)
    {
        bool removed = false;
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(stack[i].Owner, owner)) { stack.RemoveAt(i); removed = true; }
        }
        return removed;
    }

    private bool PurgeDestroyedOwners()
    {
        bool removed = false;
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (stack[i].Owner is UnityEngine.Object unityOwner && unityOwner == null)
            {
                stack.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    private string BuildDiagnostics()
    {
        return "mode=" + ResolveMode() + " | transition=" + lastTransition + " | stack=" + stack.Count;
    }

    private static string OwnerName(object owner) => owner is UnityEngine.Object unityOwner ? unityOwner.name : owner.GetType().Name;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        instance = null;
        ModeChanged = null;
    }
}
