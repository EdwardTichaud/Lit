using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public partial class PlayerInputs : IInputActionCollection2, IDisposable
{
    private const string AssetJson = "{\"version\":1,\"name\":\"PlayerInputs\",\"maps\":[{\"name\":\"Player\",\"id\":\"df70fa95-8a34-4494-b137-73ab6b9c7d37\",\"actions\":[{\"name\":\"Move\",\"type\":\"Value\",\"id\":\"351f2ccd-1f9f-44bf-9bec-d62ac5c5f408\",\"expectedControlType\":\"Vector2\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"LeftShoulder\",\"type\":\"Button\",\"id\":\"69058ce5-6c4d-4828-86fe-a53e3d61ecb2\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"Interact\",\"type\":\"Button\",\"id\":\"3659fe5c-0f05-4e04-888e-c6cde8c88a3d\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false},{\"name\":\"ToggleTorch\",\"type\":\"Button\",\"id\":\"fd1fcec9-25eb-4274-8289-07bb178bd0f1\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false},{\"name\":\"TakeAll\",\"type\":\"Button\",\"id\":\"6dd250b0-e1bc-4c93-858a-89992aad7031\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false},{\"name\":\"Return\",\"type\":\"Button\",\"id\":\"d29c84b2-f485-4658-ab76-d9db957d1421\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false},{\"name\":\"Inventory\",\"type\":\"Button\",\"id\":\"0dd61e90-cf79-4679-be53-f738e5386a6c\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false},{\"name\":\"Multi\",\"type\":\"Button\",\"id\":\"a1c88c79-ca89-4e10-936a-440568738263\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false},{\"name\":\"Start\",\"type\":\"Button\",\"id\":\"47188d76-9397-45dd-8cd4-27abc5407108\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false}],\"bindings\":[{\"name\":\"\",\"id\":\"29ec4b11-6c2a-41b0-98c7-c3cbc82e787f\",\"path\":\"<Gamepad>/leftShoulder\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"LeftShoulder\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"213cafd7-2050-47f0-9e4d-cf57ff086ef7\",\"path\":\"<Keyboard>/tab\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"LeftShoulder\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"9083101d-6214-4968-899f-901be7a30560\",\"path\":\"<Gamepad>/leftStick\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Move\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"WASD\",\"id\":\"1a1b5df6-33b2-4a61-9a1e-73d8cb153bbe\",\"path\":\"2DVector\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Move\",\"isComposite\":true,\"isPartOfComposite\":false},{\"name\":\"up\",\"id\":\"c6a46c81-bc06-43a9-bb12-dbe36c11d96a\",\"path\":\"<Keyboard>/w\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Move\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"down\",\"id\":\"087aaebd-624a-423f-af70-fe562d83665d\",\"path\":\"<Keyboard>/s\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Move\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"left\",\"id\":\"e6dbcc53-0465-4acc-8167-aed95f85978c\",\"path\":\"<Keyboard>/a\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Move\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"right\",\"id\":\"02019cfc-ff09-40dc-ba46-33c33d2c303a\",\"path\":\"<Keyboard>/d\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Move\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"\",\"id\":\"e3be23c9-de9e-4375-b93c-0a2c096deee3\",\"path\":\"<Gamepad>/buttonSouth\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Interact\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"9bcb1ab7-0cf2-475f-82aa-b8edff5d9d79\",\"path\":\"<Keyboard>/space\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Interact\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"1abd66c6-11de-4ed5-a3a8-522ebb5549a1\",\"path\":\"<Gamepad>/buttonWest\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"ToggleTorch\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"81bc4455-a6ec-4973-914a-5b06a452f491\",\"path\":\"<Gamepad>/buttonNorth\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"TakeAll\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"0acb7305-c5f0-4875-bfff-c45a1a37c1d2\",\"path\":\"<Gamepad>/buttonEast\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Return\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"67435425-930c-406d-92eb-118b0d93f754\",\"path\":\"<Gamepad>/buttonNorth\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Inventory\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"183a41b0-ee92-4e8f-964f-1201c43aa30f\",\"path\":\"<Gamepad>/select\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Multi\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"4f8202e6-e35e-406d-8aae-2414c8875e56\",\"path\":\"<Gamepad>/start\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Start\",\"isComposite\":false,\"isPartOfComposite\":false}]},{\"name\":\"Camera\",\"id\":\"bfabdbd0-9685-4bce-8334-4f658ab606ac\",\"actions\":[{\"name\":\"Pan\",\"type\":\"Value\",\"id\":\"8dd29057-a1c9-4077-968a-753f64c65b07\",\"expectedControlType\":\"Vector2\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"Orbit\",\"type\":\"Value\",\"id\":\"a41d2ef4-0f98-4f74-a045-52dac3fc0fba\",\"expectedControlType\":\"Vector2\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"Zoom\",\"type\":\"Value\",\"id\":\"03d189d8-485a-4560-9562-5e885482b283\",\"expectedControlType\":\"Axis\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"PointerScroll\",\"type\":\"Value\",\"id\":\"deac29f4-f6d5-4703-8f10-44d24face007\",\"expectedControlType\":\"Axis\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"PointerDelta\",\"type\":\"Value\",\"id\":\"ecc56b8f-8ff2-4e94-aa84-307b61d0cff9\",\"expectedControlType\":\"Vector2\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"PointerPosition\",\"type\":\"Value\",\"id\":\"27f3c815-ebe2-4a90-9736-76ee346030c1\",\"expectedControlType\":\"Vector2\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"OrbitModifier\",\"type\":\"Button\",\"id\":\"75bdc93b-5d28-42fe-a1be-beccef2640aa\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"PanModifier\",\"type\":\"Button\",\"id\":\"b29209d4-eae1-447a-b627-a02a9d0cba1c\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":true},{\"name\":\"Recenter\",\"type\":\"Button\",\"id\":\"5f26db55-7ad5-4025-8255-34f6cf138956\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false},{\"name\":\"ToggleFreeCamera\",\"type\":\"Button\",\"id\":\"8401479d-ed89-4177-a0be-396ea46b6223\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false}],\"bindings\":[{\"name\":\"Arrows\",\"id\":\"a3633657-8e11-4b64-aeb8-2ceeb7f39869\",\"path\":\"2DVector\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Pan\",\"isComposite\":true,\"isPartOfComposite\":false},{\"name\":\"up\",\"id\":\"82ea5c67-6796-4492-8c2c-2ae37738a0fe\",\"path\":\"<Keyboard>/upArrow\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Pan\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"down\",\"id\":\"ffc675d1-8a06-493b-83cd-abc060fb3108\",\"path\":\"<Keyboard>/downArrow\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Pan\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"left\",\"id\":\"765ff664-f025-4428-bb00-fc4ef2b06958\",\"path\":\"<Keyboard>/leftArrow\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Pan\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"right\",\"id\":\"9ac99851-ba7a-4a9c-a260-ab8ef7d5f5c9\",\"path\":\"<Keyboard>/rightArrow\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Pan\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"\",\"id\":\"25483807-7774-4c8d-aaf3-5a5e6d8defde\",\"path\":\"<Gamepad>/dpad\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Pan\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"4e9470a5-84f7-4871-85d5-aa067dd29618\",\"path\":\"<Gamepad>/rightStick\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Orbit\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"Triggers\",\"id\":\"29db8174-829b-4242-94cc-1a4f853f004b\",\"path\":\"1DAxis\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Zoom\",\"isComposite\":true,\"isPartOfComposite\":false},{\"name\":\"negative\",\"id\":\"02916bc5-4b79-4ac3-a370-c53336acf23e\",\"path\":\"<Gamepad>/leftTrigger\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Zoom\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"positive\",\"id\":\"ce7458b4-ab36-4cac-bf4a-14c75e2877f9\",\"path\":\"<Gamepad>/rightTrigger\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Zoom\",\"isComposite\":false,\"isPartOfComposite\":true},{\"name\":\"\",\"id\":\"a6ef8fc8-8db5-472f-9780-0907b698a3ab\",\"path\":\"<Mouse>/scroll/y\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"PointerScroll\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"e9e1ae7f-1f33-4e30-a692-133c057f3b94\",\"path\":\"<Pointer>/delta\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"PointerDelta\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"a1a729a2-2bb5-44f6-849c-ee15a38abc73\",\"path\":\"<Pointer>/position\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"PointerPosition\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"5e9528f1-b237-495c-9bcc-1131e23846f2\",\"path\":\"<Mouse>/rightButton\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"OrbitModifier\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"5c8ed7b6-9ea7-4438-a8db-4a5808be22cf\",\"path\":\"<Mouse>/middleButton\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"PanModifier\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"02e75207-a35b-4a35-9ccd-e66a9feafc8a\",\"path\":\"<Keyboard>/f\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Keyboard&Mouse\",\"action\":\"Recenter\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"190a5081-7346-4e93-91b8-5a3f7380e30f\",\"path\":\"<Gamepad>/rightStickPress\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"Recenter\",\"isComposite\":false,\"isPartOfComposite\":false},{\"name\":\"\",\"id\":\"a5a9c998-6fe3-418f-9256-83c354a77988\",\"path\":\"<Gamepad>/leftStickPress\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"Gamepad\",\"action\":\"ToggleFreeCamera\",\"isComposite\":false,\"isPartOfComposite\":false}]}],\"controlSchemes\":[{\"name\":\"Keyboard&Mouse\",\"bindingGroup\":\"Keyboard&Mouse\",\"devices\":[{\"devicePath\":\"<Keyboard>\",\"isOptional\":false,\"isOR\":false},{\"devicePath\":\"<Mouse>\",\"isOptional\":false,\"isOR\":false}]},{\"name\":\"Gamepad\",\"bindingGroup\":\"Gamepad\",\"devices\":[{\"devicePath\":\"<Gamepad>\",\"isOptional\":false,\"isOR\":false}]},{\"name\":\"Touch\",\"bindingGroup\":\"Touch\",\"devices\":[{\"devicePath\":\"<Touchscreen>\",\"isOptional\":false,\"isOR\":false}]},{\"name\":\"Joystick\",\"bindingGroup\":\"Joystick\",\"devices\":[{\"devicePath\":\"<Joystick>\",\"isOptional\":false,\"isOR\":false}]},{\"name\":\"XR\",\"bindingGroup\":\"XR\",\"devices\":[{\"devicePath\":\"<XRController>\",\"isOptional\":false,\"isOR\":false}]}]}";

    public InputActionAsset asset { get; }

    private readonly InputActionMap m_Player;
    private readonly InputActionMap m_Camera;

    private readonly InputAction m_Player_Move;
    private readonly InputAction m_Player_LeftShoulder;
    private readonly InputAction m_Player_Interact;
    private readonly InputAction m_Player_ToggleTorch;
    private readonly InputAction m_Player_TakeAll;
    private readonly InputAction m_Player_Return;
    private readonly InputAction m_Player_Inventory;
    private readonly InputAction m_Player_Multi;
    private readonly InputAction m_Player_Start;

    private readonly InputAction m_Camera_Pan;
    private readonly InputAction m_Camera_Orbit;
    private readonly InputAction m_Camera_Zoom;
    private readonly InputAction m_Camera_PointerScroll;
    private readonly InputAction m_Camera_PointerDelta;
    private readonly InputAction m_Camera_PointerPosition;
    private readonly InputAction m_Camera_OrbitModifier;
    private readonly InputAction m_Camera_PanModifier;
    private readonly InputAction m_Camera_Recenter;
    private readonly InputAction m_Camera_ToggleFreeCamera;

    private readonly List<IPlayerActions> m_PlayerCallbackInterfaces = new List<IPlayerActions>();
    private readonly List<ICameraActions> m_CameraCallbackInterfaces = new List<ICameraActions>();

    public PlayerInputs()
    {
        asset = InputActionAsset.FromJson(AssetJson);

        m_Player = asset.FindActionMap("Player", throwIfNotFound: true);
        m_Player_Move = m_Player.FindAction("Move", throwIfNotFound: true);
        m_Player_LeftShoulder = m_Player.FindAction("LeftShoulder", throwIfNotFound: true);
        m_Player_Interact = m_Player.FindAction("Interact", throwIfNotFound: true);
        m_Player_ToggleTorch = m_Player.FindAction("ToggleTorch", throwIfNotFound: true);
        m_Player_TakeAll = m_Player.FindAction("TakeAll", throwIfNotFound: true);
        m_Player_Return = m_Player.FindAction("Return", throwIfNotFound: true);
        m_Player_Inventory = m_Player.FindAction("Inventory", throwIfNotFound: true);
        m_Player_Multi = m_Player.FindAction("Multi", throwIfNotFound: true);
        m_Player_Start = m_Player.FindAction("Start", throwIfNotFound: true);

        m_Camera = asset.FindActionMap("Camera", throwIfNotFound: true);
        m_Camera_Pan = m_Camera.FindAction("Pan", throwIfNotFound: true);
        m_Camera_Orbit = m_Camera.FindAction("Orbit", throwIfNotFound: true);
        m_Camera_Zoom = m_Camera.FindAction("Zoom", throwIfNotFound: true);
        m_Camera_PointerScroll = m_Camera.FindAction("PointerScroll", throwIfNotFound: true);
        m_Camera_PointerDelta = m_Camera.FindAction("PointerDelta", throwIfNotFound: true);
        m_Camera_PointerPosition = m_Camera.FindAction("PointerPosition", throwIfNotFound: true);
        m_Camera_OrbitModifier = m_Camera.FindAction("OrbitModifier", throwIfNotFound: true);
        m_Camera_PanModifier = m_Camera.FindAction("PanModifier", throwIfNotFound: true);
        m_Camera_Recenter = m_Camera.FindAction("Recenter", throwIfNotFound: true);
        m_Camera_ToggleFreeCamera = m_Camera.FindAction("ToggleFreeCamera", throwIfNotFound: true);
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    public bool Contains(InputAction action) => asset.Contains(action);

    public IEnumerator<InputAction> GetEnumerator() => asset.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Enable() => asset.Enable();

    public void Disable() => asset.Disable();

    public IEnumerable<InputBinding> bindings => asset.bindings;

    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false) => asset.FindAction(actionNameOrId, throwIfNotFound);

    public int FindBinding(InputBinding bindingMask, out InputAction action) => asset.FindBinding(bindingMask, out action);

    public PlayerActions Player => new PlayerActions(this);

    public CameraActions Camera => new CameraActions(this);

    private void RegisterPlayerCallbacks(IPlayerActions instance)
    {
        m_Player_Move.started += instance.OnMove;
        m_Player_Move.performed += instance.OnMove;
        m_Player_Move.canceled += instance.OnMove;
        m_Player_LeftShoulder.started += instance.OnLeftShoulder;
        m_Player_LeftShoulder.performed += instance.OnLeftShoulder;
        m_Player_LeftShoulder.canceled += instance.OnLeftShoulder;
        m_Player_Interact.started += instance.OnInteract;
        m_Player_Interact.performed += instance.OnInteract;
        m_Player_Interact.canceled += instance.OnInteract;
        m_Player_ToggleTorch.started += instance.OnToggleTorch;
        m_Player_ToggleTorch.performed += instance.OnToggleTorch;
        m_Player_ToggleTorch.canceled += instance.OnToggleTorch;
        m_Player_TakeAll.started += instance.OnTakeAll;
        m_Player_TakeAll.performed += instance.OnTakeAll;
        m_Player_TakeAll.canceled += instance.OnTakeAll;
        m_Player_Return.started += instance.OnReturn;
        m_Player_Return.performed += instance.OnReturn;
        m_Player_Return.canceled += instance.OnReturn;
        m_Player_Inventory.started += instance.OnInventory;
        m_Player_Inventory.performed += instance.OnInventory;
        m_Player_Inventory.canceled += instance.OnInventory;
        m_Player_Multi.started += instance.OnMulti;
        m_Player_Multi.performed += instance.OnMulti;
        m_Player_Multi.canceled += instance.OnMulti;
        m_Player_Start.started += instance.OnStart;
        m_Player_Start.performed += instance.OnStart;
        m_Player_Start.canceled += instance.OnStart;
    }

    private void UnregisterPlayerCallbacks(IPlayerActions instance)
    {
        m_Player_Move.started -= instance.OnMove;
        m_Player_Move.performed -= instance.OnMove;
        m_Player_Move.canceled -= instance.OnMove;
        m_Player_LeftShoulder.started -= instance.OnLeftShoulder;
        m_Player_LeftShoulder.performed -= instance.OnLeftShoulder;
        m_Player_LeftShoulder.canceled -= instance.OnLeftShoulder;
        m_Player_Interact.started -= instance.OnInteract;
        m_Player_Interact.performed -= instance.OnInteract;
        m_Player_Interact.canceled -= instance.OnInteract;
        m_Player_ToggleTorch.started -= instance.OnToggleTorch;
        m_Player_ToggleTorch.performed -= instance.OnToggleTorch;
        m_Player_ToggleTorch.canceled -= instance.OnToggleTorch;
        m_Player_TakeAll.started -= instance.OnTakeAll;
        m_Player_TakeAll.performed -= instance.OnTakeAll;
        m_Player_TakeAll.canceled -= instance.OnTakeAll;
        m_Player_Return.started -= instance.OnReturn;
        m_Player_Return.performed -= instance.OnReturn;
        m_Player_Return.canceled -= instance.OnReturn;
        m_Player_Inventory.started -= instance.OnInventory;
        m_Player_Inventory.performed -= instance.OnInventory;
        m_Player_Inventory.canceled -= instance.OnInventory;
        m_Player_Multi.started -= instance.OnMulti;
        m_Player_Multi.performed -= instance.OnMulti;
        m_Player_Multi.canceled -= instance.OnMulti;
        m_Player_Start.started -= instance.OnStart;
        m_Player_Start.performed -= instance.OnStart;
        m_Player_Start.canceled -= instance.OnStart;
    }

    private void RegisterCameraCallbacks(ICameraActions instance)
    {
        m_Camera_Pan.started += instance.OnPan;
        m_Camera_Pan.performed += instance.OnPan;
        m_Camera_Pan.canceled += instance.OnPan;
        m_Camera_Orbit.started += instance.OnOrbit;
        m_Camera_Orbit.performed += instance.OnOrbit;
        m_Camera_Orbit.canceled += instance.OnOrbit;
        m_Camera_Zoom.started += instance.OnZoom;
        m_Camera_Zoom.performed += instance.OnZoom;
        m_Camera_Zoom.canceled += instance.OnZoom;
        m_Camera_PointerScroll.started += instance.OnPointerScroll;
        m_Camera_PointerScroll.performed += instance.OnPointerScroll;
        m_Camera_PointerScroll.canceled += instance.OnPointerScroll;
        m_Camera_PointerDelta.started += instance.OnPointerDelta;
        m_Camera_PointerDelta.performed += instance.OnPointerDelta;
        m_Camera_PointerDelta.canceled += instance.OnPointerDelta;
        m_Camera_PointerPosition.started += instance.OnPointerPosition;
        m_Camera_PointerPosition.performed += instance.OnPointerPosition;
        m_Camera_PointerPosition.canceled += instance.OnPointerPosition;
        m_Camera_OrbitModifier.started += instance.OnOrbitModifier;
        m_Camera_OrbitModifier.performed += instance.OnOrbitModifier;
        m_Camera_OrbitModifier.canceled += instance.OnOrbitModifier;
        m_Camera_PanModifier.started += instance.OnPanModifier;
        m_Camera_PanModifier.performed += instance.OnPanModifier;
        m_Camera_PanModifier.canceled += instance.OnPanModifier;
        m_Camera_Recenter.started += instance.OnRecenter;
        m_Camera_Recenter.performed += instance.OnRecenter;
        m_Camera_Recenter.canceled += instance.OnRecenter;
        m_Camera_ToggleFreeCamera.started += instance.OnToggleFreeCamera;
        m_Camera_ToggleFreeCamera.performed += instance.OnToggleFreeCamera;
        m_Camera_ToggleFreeCamera.canceled += instance.OnToggleFreeCamera;
    }

    private void UnregisterCameraCallbacks(ICameraActions instance)
    {
        m_Camera_Pan.started -= instance.OnPan;
        m_Camera_Pan.performed -= instance.OnPan;
        m_Camera_Pan.canceled -= instance.OnPan;
        m_Camera_Orbit.started -= instance.OnOrbit;
        m_Camera_Orbit.performed -= instance.OnOrbit;
        m_Camera_Orbit.canceled -= instance.OnOrbit;
        m_Camera_Zoom.started -= instance.OnZoom;
        m_Camera_Zoom.performed -= instance.OnZoom;
        m_Camera_Zoom.canceled -= instance.OnZoom;
        m_Camera_PointerScroll.started -= instance.OnPointerScroll;
        m_Camera_PointerScroll.performed -= instance.OnPointerScroll;
        m_Camera_PointerScroll.canceled -= instance.OnPointerScroll;
        m_Camera_PointerDelta.started -= instance.OnPointerDelta;
        m_Camera_PointerDelta.performed -= instance.OnPointerDelta;
        m_Camera_PointerDelta.canceled -= instance.OnPointerDelta;
        m_Camera_PointerPosition.started -= instance.OnPointerPosition;
        m_Camera_PointerPosition.performed -= instance.OnPointerPosition;
        m_Camera_PointerPosition.canceled -= instance.OnPointerPosition;
        m_Camera_OrbitModifier.started -= instance.OnOrbitModifier;
        m_Camera_OrbitModifier.performed -= instance.OnOrbitModifier;
        m_Camera_OrbitModifier.canceled -= instance.OnOrbitModifier;
        m_Camera_PanModifier.started -= instance.OnPanModifier;
        m_Camera_PanModifier.performed -= instance.OnPanModifier;
        m_Camera_PanModifier.canceled -= instance.OnPanModifier;
        m_Camera_Recenter.started -= instance.OnRecenter;
        m_Camera_Recenter.performed -= instance.OnRecenter;
        m_Camera_Recenter.canceled -= instance.OnRecenter;
        m_Camera_ToggleFreeCamera.started -= instance.OnToggleFreeCamera;
        m_Camera_ToggleFreeCamera.performed -= instance.OnToggleFreeCamera;
        m_Camera_ToggleFreeCamera.canceled -= instance.OnToggleFreeCamera;
    }

    public struct PlayerActions
    {
        private readonly PlayerInputs wrapper;

        public PlayerActions(PlayerInputs wrapper)
        {
            this.wrapper = wrapper;
        }

        public InputAction Move => wrapper.m_Player_Move;
        public InputAction LeftShoulder => wrapper.m_Player_LeftShoulder;
        public InputAction Interact => wrapper.m_Player_Interact;
        public InputAction ToggleTorch => wrapper.m_Player_ToggleTorch;
        public InputAction TakeAll => wrapper.m_Player_TakeAll;
        public InputAction Return => wrapper.m_Player_Return;
        public InputAction Inventory => wrapper.m_Player_Inventory;
        public InputAction Multi => wrapper.m_Player_Multi;
        public InputAction Start => wrapper.m_Player_Start;
        public InputActionMap Get() => wrapper.m_Player;
        public void Enable() => Get().Enable();
        public void Disable() => Get().Disable();
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(PlayerActions set) => set.Get();

        public void AddCallbacks(IPlayerActions instance)
        {
            if (instance == null || wrapper.m_PlayerCallbackInterfaces.Contains(instance))
            {
                return;
            }

            wrapper.m_PlayerCallbackInterfaces.Add(instance);
            wrapper.RegisterPlayerCallbacks(instance);
        }

        public void RemoveCallbacks(IPlayerActions instance)
        {
            if (wrapper.m_PlayerCallbackInterfaces.Remove(instance))
            {
                wrapper.UnregisterPlayerCallbacks(instance);
            }
        }

        public void SetCallbacks(IPlayerActions instance)
        {
            for (int i = 0; i < wrapper.m_PlayerCallbackInterfaces.Count; i++)
            {
                wrapper.UnregisterPlayerCallbacks(wrapper.m_PlayerCallbackInterfaces[i]);
            }

            wrapper.m_PlayerCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }

    public struct CameraActions
    {
        private readonly PlayerInputs wrapper;

        public CameraActions(PlayerInputs wrapper)
        {
            this.wrapper = wrapper;
        }

        public InputAction Pan => wrapper.m_Camera_Pan;
        public InputAction Orbit => wrapper.m_Camera_Orbit;
        public InputAction Zoom => wrapper.m_Camera_Zoom;
        public InputAction PointerScroll => wrapper.m_Camera_PointerScroll;
        public InputAction PointerDelta => wrapper.m_Camera_PointerDelta;
        public InputAction PointerPosition => wrapper.m_Camera_PointerPosition;
        public InputAction OrbitModifier => wrapper.m_Camera_OrbitModifier;
        public InputAction PanModifier => wrapper.m_Camera_PanModifier;
        public InputAction Recenter => wrapper.m_Camera_Recenter;
        public InputAction ToggleFreeCamera => wrapper.m_Camera_ToggleFreeCamera;
        public InputActionMap Get() => wrapper.m_Camera;
        public void Enable() => Get().Enable();
        public void Disable() => Get().Disable();
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(CameraActions set) => set.Get();

        public void AddCallbacks(ICameraActions instance)
        {
            if (instance == null || wrapper.m_CameraCallbackInterfaces.Contains(instance))
            {
                return;
            }

            wrapper.m_CameraCallbackInterfaces.Add(instance);
            wrapper.RegisterCameraCallbacks(instance);
        }

        public void RemoveCallbacks(ICameraActions instance)
        {
            if (wrapper.m_CameraCallbackInterfaces.Remove(instance))
            {
                wrapper.UnregisterCameraCallbacks(instance);
            }
        }

        public void SetCallbacks(ICameraActions instance)
        {
            for (int i = 0; i < wrapper.m_CameraCallbackInterfaces.Count; i++)
            {
                wrapper.UnregisterCameraCallbacks(wrapper.m_CameraCallbackInterfaces[i]);
            }

            wrapper.m_CameraCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }

    public interface IPlayerActions
    {
        void OnMove(InputAction.CallbackContext context);
        void OnLeftShoulder(InputAction.CallbackContext context);
        void OnInteract(InputAction.CallbackContext context);
        void OnToggleTorch(InputAction.CallbackContext context);
        void OnTakeAll(InputAction.CallbackContext context);
        void OnReturn(InputAction.CallbackContext context);
        void OnInventory(InputAction.CallbackContext context);
        void OnMulti(InputAction.CallbackContext context);
        void OnStart(InputAction.CallbackContext context);
    }

    public interface ICameraActions
    {
        void OnPan(InputAction.CallbackContext context);
        void OnOrbit(InputAction.CallbackContext context);
        void OnZoom(InputAction.CallbackContext context);
        void OnPointerScroll(InputAction.CallbackContext context);
        void OnPointerDelta(InputAction.CallbackContext context);
        void OnPointerPosition(InputAction.CallbackContext context);
        void OnOrbitModifier(InputAction.CallbackContext context);
        void OnPanModifier(InputAction.CallbackContext context);
        void OnRecenter(InputAction.CallbackContext context);
        void OnToggleFreeCamera(InputAction.CallbackContext context);
    }
}
