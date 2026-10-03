using System;
using System.Collections.Generic;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UccCamera = Opsive.UltimateCharacterController.Camera.CameraController;
using UccView = Opsive.UltimateCharacterController.Camera.ViewTypes.ViewType;

public enum GameplayCameraMode { ThirdPerson, Tactical }

[DefaultExecutionOrder(500), DisallowMultipleComponent, RequireComponent(typeof(UccCamera))]
public sealed class LitGameplayCameraModeController : MonoBehaviour
{
    [SerializeField] private GameplayCameraMode initialMode = GameplayCameraMode.ThirdPerson;
    [SerializeField] private LitTacticalCameraProfile profile;
    [SerializeField] private GameplayCameraMode effectiveMode;
    private GameplayCameraMode requestedMode;
    private UccCamera ucc;
    private LitUccCameraCharacterBinder binder;
    private LitTacticalUccViewType tactical;
    private GameObject boundCharacter;
    private bool inspection, external, installationFailed, reportedFailure, initializedMode;
    private float blendStarted = -100;
    private Vector3 blendStartPosition, previousPosition;
    private Vector3 lastVisibleAnchor;
    private Quaternion blendStartRotation;
    private float savedThirdPersonPitch;
    private bool cursorOwned, hasPreviousPosition, hasVisibleAnchor;
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private LitTacticalVisibilityMask visibilityMask;
    private TacticalObstacleMode lastObstacleMode;
    private LitTacticalCameraProfile runtimeProfile;
    public GameplayCameraMode RequestedMode => requestedMode;
    public GameplayCameraMode EffectiveMode => effectiveMode;
    public bool InspectionActive => inspection;
    public bool TacticalRequested => requestedMode == GameplayCameraMode.Tactical && !installationFailed;
    public bool ExternalControl => external;
    public TacticalObstacleMode RequestedObstacleMode => profile != null ? profile.obstacleMode : TacticalObstacleMode.Sliding;
    public TacticalObstacleMode EffectiveObstacleMode => LitTacticalCameraProfile.ResolveObstacleMode(RequestedObstacleMode,
        inspection || tactical == null || !tactical.Following, external || effectiveMode != GameplayCameraMode.Tactical || !isActiveAndEnabled);
    public bool MaskActive => EffectiveObstacleMode == TacticalObstacleMode.VisibilityMask && ucc != null && ucc.enabled && ucc.Character != null;
    public bool IsBlending => !external && profile != null && Time.time - blendStarted < profile.transitionTime;
    public void ResetPoseTracking() { hasPreviousPosition = hasVisibleAnchor = false; blendStarted = -100; RestoreOcclusion(); }
    public struct TacticalInput { public Vector2 pan, orbit; public float zoom; }
    public TacticalInput FrameInput { get; private set; }
    public static bool KeepsTacticalView(UccCamera camera)
    {
        var controller = camera != null ? camera.GetComponent<LitGameplayCameraModeController>() : null;
        return controller != null && controller.isActiveAndEnabled && controller.TacticalRequested;
    }
    public static Type ResolveView(GameplayCameraMode mode, bool locked, bool cinematic = false) => cinematic ? null : mode == GameplayCameraMode.Tactical
        ? typeof(LitTacticalUccViewType) : locked ? typeof(CombatLockAdventureViewType) : typeof(LitSmoothAdventureViewType);
    private void Awake()
    {
        ucc = GetComponent<UccCamera>();
        binder = GetComponent<LitUccCameraCharacterBinder>();
        requestedMode = initialMode;
        if (profile == null) { runtimeProfile = ScriptableObject.CreateInstance<LitTacticalCameraProfile>(); profile = runtimeProfile; }
        visibilityMask = new LitTacticalVisibilityMask(this, GetComponent<Camera>());
    }
    private void OnEnable()
    {
        if (binder != null) binder.CharacterBound += OnBound;
        InputModeCoordinator.ModeChanged += OnInputModeChanged;
        LocalInputRouter.CameraRecenter += Recenter;
        LocalInputRouter.TacticalInspection += ToggleInspection;
    }
    private void OnDisable()
    {
        if (binder != null) binder.CharacterBound -= OnBound;
        InputModeCoordinator.ModeChanged -= OnInputModeChanged;
        LocalInputRouter.CameraRecenter -= Recenter;
        LocalInputRouter.TacticalInspection -= ToggleInspection;
        ExitInspection(); RestoreOcclusion(); ReleaseCursor(); FrameInput = default;
        if (ucc != null && ucc.enabled && ucc.Character != null && ucc.ActiveViewType is LitTacticalUccViewType)
        { requestedMode = GameplayCameraMode.ThirdPerson; ApplyMode(false); }
    }
    private void OnDestroy() { visibilityMask?.Dispose(); if (runtimeProfile != null) Destroy(runtimeProfile); }
    private void OnBound(UccCamera camera, Transform character)
    { boundCharacter = null; initializedMode = false; ExitInspection(); RestoreOcclusion(); }
    public void SetMode(GameplayCameraMode mode)
    {
        if (!Application.isPlaying) { initialMode = mode; return; }
        if (requestedMode == mode && initializedMode) return;
        requestedMode = mode;
        ExitInspection(); RestoreOcclusion(); FrameInput = default;
        if (!external) ApplyMode(true);
    }
    [ContextMenu("Play: Third Person")] private void SelectThirdPerson() => SetMode(GameplayCameraMode.ThirdPerson);
    [ContextMenu("Play: Tactical")] private void SelectTactical() => SetMode(GameplayCameraMode.Tactical);
    public void BeginExternalControl()
    { external = true; ExitInspection(); RestoreOcclusion(); ReleaseCursor(); FrameInput = default; blendStarted = -100; }
    public void EndExternalControl()
    { external = false; if (!isActiveAndEnabled) return; ApplyMode(false); tactical?.Recenter(true); }
    public bool HandleSnap(CameraSnapReason reason)
    {
        if (!TacticalRequested) return false;
        if (external) return true;
        ApplyMode(false);
        if (reason == CameraSnapReason.ManualRecenter) Recenter();
        else
        {
            ExitInspection(); tactical?.Recenter(true); blendStarted = -100;
            if (ucc.enabled && ucc.Character != null) ucc.PositionImmediately(true);
        }
        return true;
    }
    public void Recenter()
    { if (!TacticalRequested || external) return; ExitInspection(); tactical?.Recenter(false); }
    private bool Install()
    {
        if (tactical != null) return true;
        if (installationFailed || ucc == null || ucc.Character == null || ucc.ActiveViewType == null) return false;
        try
        {
            ThirdPerson source = ucc.GetViewType<LitSmoothAdventureViewType>() ?? ucc.GetViewType<Adventure>();
            if (source == null) throw new InvalidOperationException("Adventure UCC absente");
            tactical = ucc.GetViewType<LitTacticalUccViewType>();
            if (tactical == null)
            {
                tactical = new LitTacticalUccViewType();
                var views = new List<UccView>(ucc.ViewTypes) { tactical };
                ucc.ViewTypes = views.ToArray(); ucc.InitializeViewTypes(); tactical.Awake(); tactical.AttachCharacter(ucc.Character);
            }
            tactical.Configure(this, profile, source);
            return true;
        }
        catch (Exception exception)
        {
            installationFailed = true; requestedMode = GameplayCameraMode.ThirdPerson;
            if (!reportedFailure) { Debug.LogError("[TacticalCamera] Installation impossible, retour third-person. " + exception, this); reportedFailure = true; }
            return false;
        }
    }
    private void ApplyMode(bool blend)
    {
        if (external || ucc == null || !ucc.enabled || ucc.Character == null) return;
        if (TacticalRequested && !Install() && !installationFailed) return;
        bool locked = RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.LockedEnemy != null;
        Type type = ResolveView(requestedMode, locked);
        if (type == typeof(CombatLockAdventureViewType) && ucc.GetViewType<CombatLockAdventureViewType>() == null) type = typeof(LitSmoothAdventureViewType);
        if (ucc.GetViewType<LitSmoothAdventureViewType>() == null && type == typeof(LitSmoothAdventureViewType)) return;
        if (ucc.ActiveViewType != null && ucc.ActiveViewType.GetType() == type)
        { effectiveMode = requestedMode; initializedMode = true; return; }
        Vector3 pose = transform.position;
        Quaternion rotation = transform.rotation;
        if (type == typeof(LitTacticalUccViewType)) savedThirdPersonPitch = Mathf.DeltaAngle(0, rotation.eulerAngles.x);
        if (type == typeof(LitTacticalUccViewType)) tactical.Enter(rotation.eulerAngles.y);
        blendStartPosition = previousPosition = pose; blendStartRotation = rotation;
        hasPreviousPosition = true;
        blendStarted = blend ? Time.time : -100;
        effectiveMode = requestedMode; initializedMode = true;
        // SetViewType evaluates Rotate/Move synchronously, so the blend must already be armed.
        ucc.SetViewType(type, true);
        if (type != typeof(LitTacticalUccViewType) && ucc.ActiveViewType is ThirdPerson third)
            third.ChangeViewType(true, savedThirdPersonPitch,
                Mathf.DeltaAngle(0, rotation.eulerAngles.y - ucc.Character.transform.eulerAngles.y), ucc.Character.transform.rotation);
    }
    private float BlendFraction => Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - blendStarted) / Mathf.Max(.001f, profile.transitionTime)));
    public Quaternion BlendRotation(Quaternion target) => Quaternion.Slerp(blendStartRotation, target, BlendFraction);
    public Vector3 BlendPosition(Vector3 target, Vector3 anchor, float radius)
    {
        Vector3 position = Vector3.Lerp(blendStartPosition, target, BlendFraction);
        Vector3 playerAnchor = ucc.Anchor != null ? ucc.Anchor.position + ucc.AnchorOffset : anchor;
        if (inspection) position = LitTacticalCameraMath.ClampCameraDistance(position, playerAnchor, profile.maximumFreeCameraDistance);
        position = ConstrainPose(anchor, position, radius);
        // Continuous sweep is required in tactical mode; third-person keeps native UCC collision outside transitions.
        if (hasPreviousPosition && (effectiveMode == GameplayCameraMode.Tactical || BlendFraction < 1))
        {
            Vector3 requestedPosition = position;
            position = Slide(previousPosition, requestedPosition, radius);
            // If a corner prevents further progress, retract along a verified path
            // towards the pivot rather than pinning the historical pose forever.
            if (effectiveMode == GameplayCameraMode.Tactical && !IsBlending &&
                (position - requestedPosition).sqrMagnitude > .0025f &&
                (position - previousPosition).sqrMagnitude < .0001f)
            {
                Vector3 retreat = Vector3.MoveTowards(previousPosition, hasVisibleAnchor ? lastVisibleAnchor : anchor,
                    profile.collisionRecoverySpeed * Mathf.Min(Time.deltaTime, .1f));
                position = Slide(previousPosition, retreat, radius);
            }
        }
        if (inspection)
        {
            Vector3 limited = LitTacticalCameraMath.ClampCameraDistance(position, playerAnchor, profile.maximumFreeCameraDistance);
            position = hasPreviousPosition ? Sweep(previousPosition, limited, radius) : limited;
        }
        previousPosition = position;
        hasPreviousPosition = true;
        if ((Sweep(position, anchor, radius) - anchor).sqrMagnitude < .0001f)
        { lastVisibleAnchor = anchor; hasVisibleAnchor = true; }
        return position;
    }
    public Vector3 ConstrainPose(Vector3 anchor, Vector3 desired, float radius) => Sweep(anchor, desired, radius);
    public Vector3 ConstrainPan(Vector3 start, Vector3 desired, float radius) => Slide(start, desired, radius);
    private Vector3 Slide(Vector3 start, Vector3 target, float radius)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Vector3 resolved = Sweep(start, target, radius, out Vector3 normal);
            if ((resolved - target).sqrMagnitude < .000001f || normal.sqrMagnitude < .5f) return resolved;
            Vector3 remaining = LitTacticalCameraMath.SlideMotion(target - resolved, normal);
            start = resolved;
            target = resolved + remaining;
            if (remaining.sqrMagnitude < .000001f) return resolved;
        }
        return start;
    }
    private Vector3 Sweep(Vector3 start, Vector3 end, float radius)
        => Sweep(start, end, radius, out _);

    private Vector3 Sweep(Vector3 start, Vector3 end, float radius, out Vector3 normal)
    {
        normal = Vector3.zero;
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length < .0001f || ucc.Character == null) return end;
        var layers = ucc.Character.GetComponent<CharacterLayerManager>();
        int mask = layers != null ? layers.IgnoreInvisibleCharacterWaterLayers : Physics.DefaultRaycastLayers;
        int count = Physics.SphereCastNonAlloc(start, Mathf.Max(.01f, radius), delta / length, hits, length, mask, QueryTriggerInteraction.Ignore);
        float distance = length;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(ucc.Character.transform)) continue;
            if (MaskActive)
            {
                var group = hits[i].collider.GetComponentInParent<LitCameraOcclusionGroup>();
                // Only bypass groups covered by the current detection. Unknown
                // hits (including a saturated non-alloc query) stay conservative.
                if (group != null && group.CanBypassCameraCollision() && visibilityMask != null && visibilityMask.Covers(group)) continue;
            }
            // A touching surface must not stop motion away from it or tangent to it.
            if (hits[i].distance <= .001f && Vector3.Dot(delta / length, hits[i].normal) >= -.001f) continue;
            float candidate = Mathf.Max(0, hits[i].distance - .02f);
            if (candidate < distance) { distance = candidate; normal = hits[i].normal; }
        }
        if (count == hits.Length) distance = 0;
        return start + delta / length * distance;
    }
    private void Update()
    {
        if (external || ucc == null || !ucc.enabled || ucc.Character == null) return;
        if (boundCharacter != ucc.Character)
        { boundCharacter = ucc.Character; tactical?.Recenter(true); initializedMode = false; }
        ApplyMode(initializedMode);
        FrameInput = default;
        bool allowed = effectiveMode == GameplayCameraMode.Tactical && InputModeCoordinator.IsCameraAllowed &&
            !InputFocusStack.HasAnyFocusBlockingCamera() && !JoinSyncSystem.IsGameplayBlocked && Application.isFocused;
        if (!allowed) ExitInspection();
        if (!allowed) { ReleaseCursor(); return; }
        AcquireCursor(); ReadInput();
    }
    private void ReadInput()
    {
        float dt = Mathf.Min(Time.deltaTime, .1f);
        TacticalInput input = default;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool textActive = selected != null &&
            (selected.GetComponent<TMPro.TMP_InputField>() != null || selected.GetComponent<UnityEngine.UI.InputField>() != null);
        bool mouseBlocked = textActive || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
        Vector2 delta = LocalInputRouter.ConsumeCameraPointerDelta();
        float scroll = LocalInputRouter.ConsumeCameraPointerScrollValue();
        if (textActive) return;
        if (MainMenuInputSettings.AllowsKeyboardMouse() && !mouseBlocked)
        {
            var k = Keyboard.current;
            Vector2 arrows = k == null ? Vector2.zero : new Vector2(
                (k.rightArrowKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed ? 1 : 0),
                (k.upArrowKey.isPressed ? 1 : 0) - (k.downArrowKey.isPressed ? 1 : 0));
            if (profile.edgeScrolling && Mouse.current != null)
            {
                Vector2 p = LocalInputRouter.CameraPointerPosition;
                if (p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height)
                    arrows += new Vector2(p.x < profile.edgePixels ? -1 : p.x > Screen.width - profile.edgePixels ? 1 : 0,
                        p.y < profile.edgePixels ? -1 : p.y > Screen.height - profile.edgePixels ? 1 : 0);
            }
            if (inspection)
            {
                input.pan = Vector2.ClampMagnitude(arrows, 1) * profile.panSpeed * dt;
                if (LocalInputRouter.CameraPanModifierPressed) input.pan -= delta * profile.dragSensitivity;
            }
            if (LocalInputRouter.CameraOrbitModifierPressed) input.orbit = delta * profile.mouseOrbitSensitivity;
            input.zoom = scroll * profile.wheelSensitivity;
        }
        if (MainMenuInputSettings.AllowsGamepad())
        {
            input.orbit += LocalInputRouter.CameraOrbitValue * profile.gamepadOrbitSpeed * dt;
            if (inspection && Gamepad.current != null)
            {
                input.pan += Gamepad.current.leftStick.ReadValue() * profile.panSpeed * dt;
                input.zoom += (Gamepad.current.rightTrigger.ReadValue() - Gamepad.current.leftTrigger.ReadValue()) * profile.gamepadZoomSpeed * dt;
            }
        }
        FrameInput = input;
    }
    private void ToggleInspection()
    {
        if (inspection) { ExitInspection(); return; }
        if (!TacticalRequested || external || InputFocusStack.HasAnyFocus() ||
            (InputModeCoordinator.CurrentMode != InputMode.Exploration && InputModeCoordinator.CurrentMode != InputMode.Combat)) return;
        inspection = true;
        RestoreOcclusion();
        tactical?.SetFreeCamera(true);
        GamepadInputContextStack.Push(this, GamepadInputContext.UserInterface);
        InputModeCoordinator.Enter(this, InputMode.TacticalInspection);
    }
    private void ExitInspection()
    {
        if (!inspection) return;
        inspection = false;
        tactical?.SetFreeCamera(false);
        GamepadInputContextStack.Pop(this);
        InputModeCoordinator.Exit(this);
        FrameInput = default;
    }
    private void OnInputModeChanged(InputMode mode)
    {
        FrameInput = default;
        if (inspection && mode != InputMode.TacticalInspection) ExitInspection();
        if (!InputModeCoordinator.IsCameraAllowed) ReleaseCursor();
    }
    private void OnApplicationFocus(bool focused)
    { if (!focused) { ExitInspection(); FrameInput = default; ReleaseCursor(); } }
    private void AcquireCursor()
    {
        if (cursorOwned) return;
        cursorOwned = true;
        LitSystemCursorLease.Acquire(this, true, CursorLockMode.None);
    }
    private void ReleaseCursor()
    { if (!cursorOwned) return; cursorOwned = false; LitSystemCursorLease.Release(this); }
    private void LateUpdate() { if (!MaskActive) RestoreOcclusion(); }
    public void PrepareVisibilityMask(Vector3 desired, Vector3 playerAnchor, float radius)
    {
        // Live profile changes restore the previous effect without changing orbit or binding.
        if (lastObstacleMode != EffectiveObstacleMode) { RestoreOcclusion(); lastObstacleMode = EffectiveObstacleMode; }
        var layers = ucc.Character != null ? ucc.Character.GetComponent<CharacterLayerManager>() : null;
        var enemy = RealTimeCombatManager.Instance != null ? RealTimeCombatManager.Instance.LockedEnemy : null;
        visibilityMask?.Prepare(desired, playerAnchor, enemy != null ? enemy.transform : null, radius,
            layers != null ? layers.IgnoreInvisibleCharacterWaterLayers : Physics.DefaultRaycastLayers, profile);
    }
    public void RestoreOcclusion() => visibilityMask?.Clear();
    private void OnGUI()
    { if (inspection) GUI.Label(new Rect(20, Screen.height - 60, 520, 30), "Caméra libre — clic L3 : suivre le joueur ; maintien R3 : recentrer"); }
    private void OnDrawGizmosSelected()
    {
        if (tactical == null || profile == null || !profile.showDiagnostics) return;
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(tactical.Pivot, .2f); Gizmos.DrawLine(transform.position, tactical.Pivot);
    }
}
