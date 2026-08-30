using System.Collections;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;
using UccCameraController = Opsive.UltimateCharacterController.Camera.CameraController;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(UccCameraController))]
public class LitUccCameraCharacterBinder : MonoBehaviour
{
    /// <summary>
    /// Raised only after UCC has a valid local character and can safely resolve
    /// its view types. Camera extensions use this instead of guessing a startup
    /// execution order.
    /// </summary>
    public event System.Action<UccCameraController, Transform> CharacterBound;

    [SerializeField] private UccCameraController cameraController;
    [SerializeField] private bool bindOnEnable = true;
    [SerializeField] private bool subscribeToLocalPlayerContext = true;
    [SerializeField] private bool subscribeToCameraRecenter = true;
    [SerializeField] private bool snapCameraOnBind = true;
    [SerializeField, Min(0f)] private float retryInterval = 0.1f;

    private Coroutine bindRoutine;
    private Transform boundCharacter;
    private bool waitingForInitialCharacter;
    private bool timelineControlHeld;

    /// <summary>Reserve la camera a une Timeline sans creer d'objet ni perdre le personnage actuellement lie.</summary>
    public void BeginTimelineControl()
    {
        timelineControlHeld = true;
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        ResolveCameraController();
        if (cameraController != null)
        {
            cameraController.enabled = false;
        }
    }

    /// <summary>Generic ownership handoff for Cinemachine, Timeline or another approved camera driver.</summary>
    public void BeginExternalCameraControl() => BeginTimelineControl();

    /// <summary>Rend le controle a UCC et revalide le binding vers le joueur local.</summary>
    public void EndTimelineControl(bool restoreController)
    {
        timelineControlHeld = false;
        ResolveCameraController();
        if (cameraController != null && restoreController)
        {
            cameraController.enabled = true;
        }

        QueueBind();
    }

    /// <summary>Returns camera ownership after an external camera sequence.</summary>
    public void EndExternalCameraControl(bool restoreController) => EndTimelineControl(restoreController);

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
        if (!SnapCameraToBoundCharacter(CameraSnapReason.ManualRecenter))
        {
            QueueBind();
        }
    }

    private void QueueBind(Transform preferredCharacter = null)
    {
        if (!isActiveAndEnabled || timelineControlHeld) return;

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
            return true;
        }

        SetInitCharacterOnAwake(false);
        SetCameraCharacter(characterObject, forceReinitialize: !IsCameraBoundAndInitialized(characterObject));

        if (waitingForInitialCharacter && !cameraController.enabled)
        {
            cameraController.enabled = true;
            waitingForInitialCharacter = false;
        }

        bool isCharacterSwitch = boundCharacter != null && boundCharacter != character;
        boundCharacter = character;
        SnapCameraToBoundCharacter(isCharacterSwitch ? CameraSnapReason.CharacterSwitch : CameraSnapReason.InitialBind);
        bool bound = IsCameraBoundAndInitialized(characterObject);
        if (bound)
        {
            CharacterBound?.Invoke(cameraController, boundCharacter);
        }

        return bound;
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

    private bool SnapCameraToBoundCharacter(CameraSnapReason reason)
    {
        if (!snapCameraOnBind
            || cameraController == null
            || cameraController.Character == null
            || cameraController.CharacterRigidbody == null
            || cameraController.ActiveViewType == null)
        {
            return false;
        }

        LitSmoothUccCameraViewAdapter smoothAdapter = GetComponent<LitSmoothUccCameraViewAdapter>();
        if (smoothAdapter != null)
        {
            return smoothAdapter.RequestImmediatePose(reason);
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
