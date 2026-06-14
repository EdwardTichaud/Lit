using System.Collections;
using System.Reflection;
using Opsive.UltimateCharacterController.Camera;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CameraController))]
public class LitUccCameraCharacterBinder : MonoBehaviour
{
    [SerializeField] private CameraController cameraController;
    [SerializeField] private bool bindOnEnable = true;
    [SerializeField] private bool subscribeToLocalPlayerContext = true;
    [SerializeField, Min(0f)] private float retryInterval = 0.1f;

    private Coroutine bindRoutine;
    private Transform boundCharacter;

    private void Reset() => ResolveCameraController();

    private void Awake()
    {
        ResolveCameraController();
        SetInitCharacterOnAwake(false);
    }

    private void OnEnable()
    {
        if (subscribeToLocalPlayerContext)
            LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;

        if (bindOnEnable)
            QueueBind();
    }

    private void Start() => QueueBind();

    private void OnDisable()
    {
        if (subscribeToLocalPlayerContext)
            LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;

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

        if (boundCharacter == character && GetCameraCharacter() == characterObject)
            return true;

        SetInitCharacterOnAwake(false);
        SetCameraCharacter(characterObject);

        boundCharacter = character;
        return GetCameraCharacter() == characterObject;
    }

    private GameObject GetCameraCharacter()
    {
        var type = cameraController.GetType();

        var property = type.GetProperty("Character", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
            return property.GetValue(cameraController) as GameObject;

        var field = type.GetField("m_Character", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            return field.GetValue(cameraController) as GameObject;

        return null;
    }

    private void SetCameraCharacter(GameObject character)
    {
        var type = cameraController.GetType();

        var property = type.GetProperty("Character", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            property.SetValue(cameraController, character);
            return;
        }

        var field = type.GetField("m_Character", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(cameraController, character);
    }

    private void SetInitCharacterOnAwake(bool value)
    {
        if (cameraController == null)
            return;

        var type = cameraController.GetType();

        var property = type.GetProperty("InitCharacterOnAwake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            property.SetValue(cameraController, value);
            return;
        }

        var field = type.GetField("m_InitCharacterOnAwake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(cameraController, value);
    }

    private static bool IsValidCharacter(Transform character)
    {
        return character != null && character.GetComponent<UltimateCharacterLocomotion>() != null;
    }

    private void ResolveCameraController()
    {
        if (cameraController == null)
            cameraController = GetComponent<CameraController>();
    }

    private static GameObject FindTaggedPlayer()
    {
        try { return GameObject.FindGameObjectWithTag("Player"); }
        catch (UnityException) { return null; }
    }
}