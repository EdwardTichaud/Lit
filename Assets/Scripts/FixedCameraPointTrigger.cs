using System.Collections.Generic;
using UnityEngine;

public class FixedCameraPointTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private Transform cameraPoint;

    [Header("Auto Resolve")]
    [SerializeField] private bool autoResolveChildren = true;
    [SerializeField] private string triggerChildName = "";
    [SerializeField] private string cameraPointChildName = "";

    [Header("Camera Settings")]
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private int priority;
    [SerializeField, Min(0f)] private float transitionSharpness = 8f;
    [SerializeField] private bool controlledCharacterOnly = true;

    private readonly List<GameObject> charactersInRange = new();
    private readonly Dictionary<GameObject, HashSet<Collider>> characterColliders = new();

    private GameObject currentCharacter;

    private void Awake()
    {
        SetupTrigger();
    }

    private void OnEnable()
    {
        SetupTrigger();
        RefreshCurrentCharacter();
    }

    private void OnDisable()
    {
        ReleaseCurrentCamera();
        ResetTrackedCharacters();
    }

    private void Update()
    {
        RefreshCurrentCharacter();
    }

    private void OnValidate()
    {
        transitionSharpness = Mathf.Max(0f, transitionSharpness);
        ResolveSceneReferences();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void SetupTrigger()
    {
        ResolveSceneReferences();

        if (triggerCollider == null)
        {
            Debug.LogWarning($"{name}: aucun Trigger Collider assigne.", this);
            return;
        }

        triggerCollider.isTrigger = true;

        TriggerForwarder forwarder = triggerCollider.GetComponent<TriggerForwarder>();

        if (forwarder == null)
            forwarder = triggerCollider.gameObject.AddComponent<TriggerForwarder>();

        forwarder.Initialize(this);
    }

    private void ResolveSceneReferences()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (autoResolveChildren)
        {
            if (triggerCollider == null)
                triggerCollider = ResolveChildCollider(triggerChildName);

            if (cameraPoint == null)
                cameraPoint = ResolveChildTransform(cameraPointChildName, "Point", "CameraPoint");
        }

        if (cameraPoint == null && triggerCollider != null && triggerCollider.transform == transform && transform.parent != null)
            cameraPoint = transform.parent;

        if (cameraPoint == null)
            cameraPoint = transform;
    }

    private Collider ResolveChildCollider(string exactName)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        Collider fallbackTrigger = null;
        Collider fallbackAny = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate == null || candidate.transform == transform)
                continue;

            if (!string.IsNullOrWhiteSpace(exactName) && candidate.name == exactName)
                return candidate;

            if (fallbackTrigger == null && candidate.isTrigger)
                fallbackTrigger = candidate;

            if (fallbackAny == null)
                fallbackAny = candidate;

            if (NameLooksLike(candidate.name, "Trigger"))
                return candidate;
        }

        return fallbackTrigger != null ? fallbackTrigger : fallbackAny;
    }

    private Transform ResolveChildTransform(string exactName, params string[] nameMarkers)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate == transform)
                continue;

            if (!string.IsNullOrWhiteSpace(exactName) && candidate.name == exactName)
                return candidate;

            for (int markerIndex = 0; markerIndex < nameMarkers.Length; markerIndex++)
            {
                if (NameLooksLike(candidate.name, nameMarkers[markerIndex]))
                    return candidate;
            }
        }

        return null;
    }

    private static bool NameLooksLike(string candidateName, string marker)
    {
        if (string.IsNullOrEmpty(candidateName) || string.IsNullOrEmpty(marker))
            return false;

        return candidateName == marker ||
               candidateName.EndsWith("_" + marker) ||
               candidateName.Contains(marker);
    }

    public void HandleTriggerEnter(Collider other)
    {
        TrackCollider(other);
    }

    public void HandleTriggerStay(Collider other)
    {
        TrackCollider(other);
    }

    public void HandleTriggerExit(Collider other)
    {
        if (other == null || other.isTrigger)
            return;

        GameObject character = ResolveSquadCharacter(other);

        if (character == null)
            return;

        if (!characterColliders.TryGetValue(character, out HashSet<Collider> colliders))
            return;

        colliders.Remove(other);

        if (colliders.Count <= 0)
        {
            characterColliders.Remove(character);
            charactersInRange.Remove(character);
        }

        if (character == currentCharacter)
            currentCharacter = null;

        RefreshCurrentCharacter();
    }

    private void TrackCollider(Collider other)
    {
        if (other == null || other.isTrigger)
            return;

        GameObject character = ResolveSquadCharacter(other);

        if (character == null)
            return;

        if (!characterColliders.TryGetValue(character, out HashSet<Collider> colliders))
        {
            colliders = new HashSet<Collider>();
            characterColliders[character] = colliders;
            charactersInRange.Add(character);
        }

        colliders.Add(other);
        RefreshCurrentCharacter();
    }

    private void RefreshCurrentCharacter()
    {
        PruneInvalidTrackedCharacters();

        GameObject desiredCharacter = ResolveDesiredCharacter();

        if (desiredCharacter == null)
        {
            ReleaseCurrentCamera();
            return;
        }

        CameraController controller = ResolveCameraController();

        if (controller == null || cameraPoint == null)
        {
            ReleaseCurrentCamera();
            return;
        }

        currentCharacter = desiredCharacter;

        controller.TrySetFixedCamera(
            this,
            cameraPoint,
            currentCharacter.transform,
            lookAtOffset,
            priority,
            transitionSharpness
        );
    }

    private GameObject ResolveDesiredCharacter()
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        GameObject trackedControlled = FindTrackedCharacter(controlled);

        if (trackedControlled != null)
            return trackedControlled;

        if (controlledCharacterOnly)
            return null;

        if (currentCharacter != null && charactersInRange.Contains(currentCharacter))
            return currentCharacter;

        return charactersInRange.Count > 0 ? charactersInRange[0] : null;
    }

    private GameObject FindTrackedCharacter(GameObject controlled)
    {
        if (controlled == null)
            return null;

        foreach (GameObject candidate in charactersInRange)
        {
            if (IsSameOrRelatedTransform(candidate != null ? candidate.transform : null, controlled.transform))
                return candidate;
        }

        return null;
    }

    private void PruneInvalidTrackedCharacters()
    {
        for (int i = charactersInRange.Count - 1; i >= 0; i--)
        {
            GameObject character = charactersInRange[i];

            if (character == null || !character.activeInHierarchy)
            {
                RemoveTrackedCharacterAt(i, character);
                continue;
            }

            if (!characterColliders.TryGetValue(character, out HashSet<Collider> colliders))
            {
                RemoveTrackedCharacterAt(i, character);
                continue;
            }

            colliders.RemoveWhere(collider =>
                collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy
            );

            if (colliders.Count <= 0)
                RemoveTrackedCharacterAt(i, character);
        }
    }

    private void RemoveTrackedCharacterAt(int index, GameObject character)
    {
        if (index >= 0 && index < charactersInRange.Count)
            charactersInRange.RemoveAt(index);

        if (character != null)
            characterColliders.Remove(character);

        if (character == currentCharacter)
            currentCharacter = null;
    }

    private CameraController ResolveCameraController()
    {
        if (cameraController != null)
            return cameraController;

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
            cameraController = mainCamera.GetComponentInParent<CameraController>();

        if (cameraController == null)
            cameraController = FindAnyObjectByType<CameraController>();

        return cameraController;
    }

    private void ReleaseCurrentCamera()
    {
        CameraController controller = ResolveCameraController();

        if (controller != null)
            controller.ReleaseFixedCamera(this);

        currentCharacter = null;
    }

    private void ResetTrackedCharacters()
    {
        charactersInRange.Clear();
        characterColliders.Clear();
    }

    private static GameObject ResolveSquadCharacter(Collider other)
    {
        if (other == null)
            return null;

        SquadManager manager = SquadManager.Instance;

        if (manager != null && manager.squadCharacters != null)
        {
            Transform current = other.transform;

            while (current != null)
            {
                if (manager.squadCharacters.Contains(current.gameObject))
                    return current.gameObject;

                current = current.parent;
            }
        }

        SquadCharacterController controller = other.GetComponentInParent<SquadCharacterController>();

        if (controller != null)
            return controller.gameObject;

        Transform taggedRoot = FindTaggedPlayerRoot(other.transform);
        return taggedRoot != null ? taggedRoot.gameObject : null;
    }

    private static Transform FindTaggedPlayerRoot(Transform start)
    {
        Transform current = start;

        while (current != null)
        {
            if (current.CompareTag("Player"))
                return current;

            current = current.parent;
        }

        return null;
    }

    private static bool IsSameOrRelatedTransform(Transform a, Transform b)
    {
        if (a == null || b == null)
            return false;

        return a == b || a.IsChildOf(b) || b.IsChildOf(a);
    }

    private class TriggerForwarder : MonoBehaviour
    {
        private FixedCameraPointTrigger owner;

        public void Initialize(FixedCameraPointTrigger newOwner)
        {
            owner = newOwner;
        }

        private void OnTriggerEnter(Collider other)
        {
            owner?.HandleTriggerEnter(other);
        }

        private void OnTriggerStay(Collider other)
        {
            owner?.HandleTriggerStay(other);
        }

        private void OnTriggerExit(Collider other)
        {
            owner?.HandleTriggerExit(other);
        }
    }
}
