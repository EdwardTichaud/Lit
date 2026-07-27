using System.Collections;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;
using UnityEngine.SceneManagement;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(UccCameraController))]
public class LitUccCameraCharacterBinder : MonoBehaviour
{
    [SerializeField] private UccCameraController cameraController;
    [SerializeField] private bool bindOnEnable = true;
    [SerializeField] private bool subscribeToLocalPlayerContext = true;
    [SerializeField] private bool subscribeToCameraRecenter = true;
    [SerializeField] private bool snapCameraOnBind = true;
    [SerializeField, Min(0f)] private float retryInterval = 0.1f;

    private static bool sceneHookRegistered;

    private Coroutine bindRoutine;
    private Transform boundCharacter;
    private bool waitingForInitialCharacter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        sceneHookRegistered = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSceneCameraBinders()
    {
        RegisterSceneHook();
        InstallBindersInLoadedScenes();
    }

    private static void RegisterSceneHook()
    {
        if (sceneHookRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHookRegistered = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallBindersInScene(scene);
    }

    private static void InstallBindersInLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            InstallBindersInScene(SceneManager.GetSceneAt(i));
        }
    }

    private static void InstallBindersInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            UccCameraController[] controllers = roots[i].GetComponentsInChildren<UccCameraController>(true);
            for (int j = 0; j < controllers.Length; j++)
            {
                UccCameraController controller = controllers[j];
                if (controller == null || controller.GetComponent<LitUccCameraCharacterBinder>() != null)
                {
                    continue;
                }

                controller.gameObject.AddComponent<LitUccCameraCharacterBinder>();
            }
        }
    }

    private void Reset() => ResolveCameraController();

    private void Awake()
    {
        ResolveCameraController();
        SetInitCharacterOnAwake(false);

        // UCC registers the camera with its simulation before Start. Do not let
        // that simulation rotate an unbound camera: CameraController.Rotate
        // requires a valid CharacterLocomotion.
        if (cameraController != null && cameraController.Character == null)
        {
            waitingForInitialCharacter = true;
            cameraController.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (subscribeToLocalPlayerContext)
        {
            LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        }

        if (subscribeToCameraRecenter)
        {
            LocalInputRouter.CameraRecenter += OnCameraRecenter;
        }

        if (bindOnEnable)
        {
            QueueBind();
        }
    }

    private void Start() => QueueBind();

    private void OnDisable()
    {
        if (subscribeToLocalPlayerContext)
        {
            LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;
        }

        if (subscribeToCameraRecenter)
        {
            LocalInputRouter.CameraRecenter -= OnCameraRecenter;
        }

        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }
    }

    private void OnLocalCharacterChanged(Transform characterRoot)
    {
        QueueBind(characterRoot);
    }

    private void OnCameraRecenter()
    {
        if (!SnapCameraToBoundCharacter())
        {
            QueueBind();
        }
    }

    private void QueueBind(Transform preferredCharacter = null)
    {
        if (!isActiveAndEnabled) return;

        if (bindRoutine != null)
            StopCoroutine(bindRoutine);

        bindRoutine = StartCoroutine(BindWhenCharacterAvailable(preferredCharacter));
    }

    private IEnumerator BindWhenCharacterAvailable(Transform preferredCharacter)
    {
        while (isActiveAndEnabled)
        {
            Transform character = ResolveCharacter(preferredCharacter);

            if (TryBind(character))
            {
                bindRoutine = null;
                yield break;
            }

            preferredCharacter = null;
            yield return retryInterval <= 0f ? null : new WaitForSecondsRealtime(retryInterval);
        }

        bindRoutine = null;
    }

    private Transform ResolveCharacter(Transform preferredCharacter)
    {
        if (IsValidCharacter(preferredCharacter))
            return preferredCharacter;

        if (IsValidCharacter(LocalPlayerContext.LocalCharacterRoot))
            return LocalPlayerContext.LocalCharacterRoot;

        if (SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null)
        {
            Transform current = SquadManager.Instance.currentCharacter.transform;
            if (IsValidCharacter(current))
                return current;
        }

        GameObject taggedPlayer = FindTaggedPlayer();
        if (taggedPlayer != null && IsValidCharacter(taggedPlayer.transform))
            return taggedPlayer.transform;

        return null;
    }

    private bool TryBind(Transform character)
    {
        if (!IsValidCharacter(character))
            return false;

        ResolveCameraController();
        if (cameraController == null)
            return false;

        GameObject characterObject = character.gameObject;

        if (boundCharacter == character && IsCameraBoundAndInitialized(characterObject))
        {
            SnapCameraToBoundCharacter();
            return true;
        }

        SetInitCharacterOnAwake(false);
        SetCameraCharacter(characterObject, forceReinitialize: !IsCameraBoundAndInitialized(characterObject));

        if (waitingForInitialCharacter && !cameraController.enabled)
        {
            cameraController.enabled = true;
            waitingForInitialCharacter = false;
        }

        boundCharacter = character;
        SnapCameraToBoundCharacter();
        return IsCameraBoundAndInitialized(characterObject);
    }

    private void SetCameraCharacter(GameObject character, bool forceReinitialize)
    {
        if (forceReinitialize && cameraController.Character == character)
        {
            cameraController.Character = null;
        }

        cameraController.Character = character;
    }

    private void SetInitCharacterOnAwake(bool value)
    {
        if (cameraController == null)
            return;

        cameraController.InitCharacterOnAwake = value;
    }

    private bool IsCameraBoundAndInitialized(GameObject character)
    {
        return cameraController != null
            && cameraController.Character == character
            && cameraController.CharacterTransform != null
            && cameraController.CharacterLocomotion != null
            && cameraController.CharacterRigidbody != null
            && cameraController.enabled;
    }

    private bool SnapCameraToBoundCharacter()
    {
        if (!snapCameraOnBind
            || cameraController == null
            || cameraController.Character == null
            || cameraController.CharacterRigidbody == null
            || cameraController.ActiveViewType == null)
        {
            return false;
        }

        cameraController.PositionImmediately(true);
        return true;
    }

    private static bool IsValidCharacter(Transform character)
    {
        return character != null && character.GetComponent<UltimateCharacterLocomotion>() != null;
    }

    private void ResolveCameraController()
    {
        if (cameraController == null)
            cameraController = GetComponent<UccCameraController>();
    }

    private static GameObject FindTaggedPlayer()
    {
        try { return GameObject.FindGameObjectWithTag("Player"); }
        catch (UnityException) { return null; }
    }
}
