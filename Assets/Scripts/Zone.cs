using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(Collider))]
// Zone generique: musique, torche, et gestion Maison (waiting points + IA).
public class Zone : MonoBehaviour
{
    [Header("Torch")]
    [Tooltip("Si false, la torche ne consomme pas dans cette zone.")]
    public bool torchConsumes = true;

    [Header("Audio")]
    [Tooltip("Joue la musique de zone a l'entree/sortie.")]
    public bool playZoneMusic = true;
    [Tooltip("Clip de musique associe a cette zone.")]
    public AudioClipSO zoneMusic;

    [Header("Maison")]
    [Tooltip("Active le comportement Maison (waiting points, IA des personnages non controles).")]
    public bool isMaison = false;
    [Tooltip("Distance d'arrivee pour considerer un waiting point atteint.")]
    public float maisonArrivalDistance = 0.1f;
    [Tooltip("Utilise la direction NavMesh pour orienter les personnages.")]
    public bool maisonUseNavMeshDirection = true;
    [Tooltip("Ajoute automatiquement un SquadFollowerAgent aux personnages dans la maison.")]
    public bool maisonAutoAddNavMeshFollower = true;
    [Tooltip("Valeur max d'input injectee pour simuler le controle.")]
    public float maisonMaxInput = 1f;

    [Header("Maison - Detection")]
    [Tooltip("Re-scan periodique des personnages dans la maison.")]
    public bool maisonPollCharacters = true;
    [Tooltip("Intervalle de polling en secondes.")]
    public float maisonPollInterval = 0.2f;
    [Tooltip("Inclut les colliders Trigger lors du polling.")]
    public bool maisonIncludeTriggerColliders = true;

    private static readonly Dictionary<GameObject, int> noConsumeCounts = new Dictionary<GameObject, int>();
    private static readonly Dictionary<GameObject, int> maisonCounts = new Dictionary<GameObject, int>();
    private readonly HashSet<GameObject> trackedCharacters = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private readonly HashSet<int> maisonMissingPointWarnings = new HashSet<int>();
    private bool isTriggerZone;
    private Collider zoneCollider;
    private float nextMaisonPollTime;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                {
                    zoneCollider = colliders[i];
                    break;
                }
            }

            if (zoneCollider == null && colliders.Length > 0)
            {
                zoneCollider = colliders[0];
            }
        }

        isTriggerZone = zoneCollider != null && zoneCollider.isTrigger;
        if (zoneCollider != null && !zoneCollider.isTrigger)
        {
            Debug.LogWarning("Zone: le collider n'est pas en mode Trigger.");
        }

        if (zoneCollider == null)
        {
            Debug.LogWarning("Zone: aucun collider trouve sur cette zone.");
        }
    }

    private void LateUpdate()
    {
        // Gestion des personnages maison (waiting points + follow auto).
        if (!isMaison)
        {
            return;
        }

        if (!ShouldSimulateMaisonCharacters())
        {
            return;
        }

        if (ShouldPollMaison())
        {
            PollMaisonCharactersIfNeeded();
        }

        UpdateMaisonWaitingCharacters();
    }

    private void OnDisable()
    {
        if (trackedCharacters.Count > 0)
        {
            NotifyZoneExit();
        }

        ClearTrackedCharacters();
        characterColliderCounts.Clear();
        maisonMissingPointWarnings.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (isMaison)
        {
            if (ShouldPollMaison())
            {
                return;
            }

            if (other.isTrigger && !maisonIncludeTriggerColliders)
            {
                return;
            }
        }
        else if (other.isTrigger)
        {
            return;
        }

        if (!isTriggerZone)
        {
            return;
        }

        GameObject character = isMaison ? GetMaisonCharacter(other) : GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        if (RegisterCharacterCollider(character))
        {
            AddCharacter(character);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (isMaison)
        {
            if (ShouldPollMaison())
            {
                return;
            }

            if (other.isTrigger && !maisonIncludeTriggerColliders)
            {
                return;
            }
        }
        else if (other.isTrigger)
        {
            return;
        }

        if (!isTriggerZone)
        {
            return;
        }

        GameObject character = isMaison ? GetMaisonCharacter(other) : GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        if (UnregisterCharacterCollider(character))
        {
            RemoveCharacter(character);
        }
    }

    private void AddCharacter(GameObject character)
    {
        if (character == null || trackedCharacters.Contains(character))
        {
            return;
        }

        bool wasEmpty = trackedCharacters.Count == 0;
        trackedCharacters.Add(character);
        if (wasEmpty)
        {
            NotifyZoneEnter();
        }

        if (!torchConsumes)
        {
            if (!noConsumeCounts.TryGetValue(character, out int count))
            {
                count = 0;
            }

            noConsumeCounts[character] = count + 1;
        }

        if (isMaison)
        {
            bool enteredMaison = AddMaisonCharacter(character);
            if (enteredMaison && ShouldSimulateMaisonCharacters())
            {
                TryResetTorchOnMaisonEntry(character);
            }
        }
    }

    private void RemoveCharacter(GameObject character)
    {
        if (character == null || !trackedCharacters.Remove(character))
        {
            return;
        }

        if (trackedCharacters.Count == 0)
        {
            NotifyZoneExit();
        }

        if (!torchConsumes && noConsumeCounts.TryGetValue(character, out int count))
        {
            count -= 1;
            if (count <= 0)
            {
                noConsumeCounts.Remove(character);
            }
            else
            {
                noConsumeCounts[character] = count;
            }
        }

        if (isMaison)
        {
            RemoveMaisonCharacter(character, 1);
            if (ShouldSimulateMaisonCharacters())
            {
                SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
                if (controller != null)
                {
                    if (NetcodePlayerUtils.ShouldUsePlayerControl(character, out _))
                    {
                        NetcodePlayerUtils.LogControlDecision(
                            "maison_exit",
                            character,
                            followerAiEnabled: false,
                            waitingPointEnabled: false,
                            movementMode: "player_owned_skip",
                            reason: "waitingPoint return skipped because character is player-owned");
                        return;
                    }

                    controller.Stop();
                }
            }
        }
    }

    private void TryResetTorchOnMaisonEntry(GameObject character)
    {
        if (!isMaison || character == null)
        {
            return;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            return;
        }

        int maxSeconds = GetTorchResetSeconds();
        if (maxSeconds <= 0)
        {
            return;
        }

        controller.ResetTorchToMax(maxSeconds, true);
    }

    private int GetTorchResetSeconds()
    {
        int maxSeconds = 0;
        BuilderController builder = GetBuilderControllerForTorch();
        if (builder != null)
        {
            builder.EnsureBuiltBuildings();
            if (builder.builtBuildings != null)
            {
                for (int i = 0; i < builder.builtBuildings.Count; i++)
                {
                    BuilderController.BuiltBuildingEntry entry = builder.builtBuildings[i];
                    if (entry == null || entry.building == null)
                    {
                        continue;
                    }

                    TorchEffect effect = FindTorchEffect(entry.building, entry.level);
                    if (effect == null)
                    {
                        continue;
                    }

                    int level = Mathf.Max(1, entry.level);
                    maxSeconds = Mathf.Max(maxSeconds, effect.GetMaxSecondsForLevel(level));
                }
            }
        }
        else
        {
#if UNITY_2023_1_OR_NEWER
            BuildingInfoInteractable[] infos = FindObjectsByType<BuildingInfoInteractable>(FindObjectsSortMode.None);
#else
            BuildingInfoInteractable[] infos = FindObjectsOfType<BuildingInfoInteractable>();
#endif
            if (infos != null)
            {
                for (int i = 0; i < infos.Length; i++)
                {
                    BuildingInfoInteractable info = infos[i];
                    if (info == null || info.BuildingItem == null)
                    {
                        continue;
                    }

                    TorchEffect effect = FindTorchEffect(info.BuildingItem, info.Level);
                    if (effect == null)
                    {
                        continue;
                    }

                    int level = Mathf.Max(1, info.Level);
                    maxSeconds = Mathf.Max(maxSeconds, effect.GetMaxSecondsForLevel(level));
                }
            }
        }

        return maxSeconds;
    }

    private BuilderController GetBuilderControllerForTorch()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<BuilderController>();
#else
        return FindObjectOfType<BuilderController>();
#endif
    }

    private TorchEffect FindTorchEffect(Item building, int level)
    {
        if (building == null || !building.isBuilding)
        {
            return null;
        }

        IReadOnlyList<Effect> effects = building.GetBuildingEffectsForLevel(level);
        if (effects == null || effects.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            TorchEffect effect = effects[i] as TorchEffect;
            if (effect != null)
            {
                return effect;
            }
        }

        return null;
    }

    private void ClearTrackedCharacters()
    {
        if (trackedCharacters.Count == 0)
        {
            return;
        }

        if (!torchConsumes)
        {
            foreach (GameObject character in trackedCharacters)
            {
                if (character == null)
                {
                    continue;
                }

                if (noConsumeCounts.TryGetValue(character, out int count))
                {
                    count -= 1;
                    if (count <= 0)
                    {
                        noConsumeCounts.Remove(character);
                    }
                    else
                    {
                        noConsumeCounts[character] = count;
                    }
                }
            }
        }

        if (isMaison)
        {
            foreach (GameObject character in trackedCharacters)
            {
                if (character == null)
                {
                    continue;
                }

                RemoveMaisonCharacter(character, 1);
            }
        }

        trackedCharacters.Clear();
    }

    public static bool ShouldConsumeTorch(GameObject character)
    {
        if (character == null)
        {
            return true;
        }

        return !noConsumeCounts.TryGetValue(character, out int count) || count <= 0;
    }

    public static bool IsCharacterInMaison(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        return maisonCounts.TryGetValue(character, out int count) && count > 0;
    }

    private bool ShouldPollMaison()
    {
        return isMaison && maisonPollCharacters;
    }

    private void PollMaisonCharactersIfNeeded()
    {
        if (zoneCollider == null)
        {
            return;
        }

        float interval = Mathf.Max(0.02f, maisonPollInterval);
        if (Time.time < nextMaisonPollTime)
        {
            return;
        }

        nextMaisonPollTime = Time.time + interval;
        RebuildMaisonCharacters();
    }

    private void RebuildMaisonCharacters()
    {
        HashSet<GameObject> previous = new HashSet<GameObject>(trackedCharacters);
        HashSet<GameObject> current = new HashSet<GameObject>();

        Bounds bounds = zoneCollider.bounds;
        QueryTriggerInteraction triggerInteraction = maisonIncludeTriggerColliders
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, ~0, triggerInteraction);
        if (hits != null)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                GameObject character = GetMaisonCharacter(hit);
                if (character != null)
                {
                    current.Add(character);
                }
            }
        }

        foreach (GameObject character in previous)
        {
            if (!current.Contains(character))
            {
                RemoveCharacter(character);
            }
        }

        foreach (GameObject character in current)
        {
            if (!previous.Contains(character))
            {
                AddCharacter(character);
            }
        }

        trackedCharacters.Clear();
        trackedCharacters.UnionWith(current);
        characterColliderCounts.Clear();
        foreach (GameObject character in current)
        {
            characterColliderCounts[character] = 1;
        }
    }

    private void UpdateMaisonWaitingCharacters()
    {
        if (trackedCharacters.Count == 0)
        {
            return;
        }

        float arriveDistance = Mathf.Max(0f, maisonArrivalDistance);

        foreach (GameObject character in trackedCharacters)
        {
            if (character == null)
            {
                continue;
            }

            if (NetcodePlayerUtils.ShouldUsePlayerControl(character, out _))
            {
                NetcodePlayerUtils.LogControlDecision(
                    "maison_waiting",
                    character,
                    followerAiEnabled: false,
                    waitingPointEnabled: false,
                    movementMode: "player_owned_skip",
                    reason: "waitingPoint return skipped because character is player-owned");
                continue;
            }

            SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
            if (controller == null)
            {
                continue;
            }

            CharacterData data = controller.CharacterData;
            if (data == null)
            {
                controller.Stop();
                continue;
            }

            if (!MaisonWaitingPoint.TryGetPoint(data.maisonWaitingPoint, out Transform waitingPoint))
            {
                if (maisonMissingPointWarnings.Add(data.maisonWaitingPoint))
                {
                    Debug.LogWarning($"Zone: aucun MaisonWaitingPoint trouve pour l'indice {data.maisonWaitingPoint}.", this);
                }

                controller.Stop();
                continue;
            }

            Vector3 characterPosition = character.transform.position;
            Vector3 targetPosition = waitingPoint.position;
            targetPosition.y = characterPosition.y;
            Vector3 toTarget = targetPosition - characterPosition;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance <= arriveDistance)
            {
                controller.Stop();
                ApplyWaitingRotation(character, waitingPoint.rotation);
                continue;
            }

            Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
            SquadFollowerAgent agent = null;
            bool followerAiEnabled = false;
            if (maisonUseNavMeshDirection)
            {
                agent = GetFollowerAgent(character);
                if (agent != null && agent.TryGetDesiredDirection(targetPosition, out Vector3 navDirection))
                {
                    direction = navDirection;
                }

                followerAiEnabled = agent != null;
            }

            NetcodePlayerUtils.LogControlDecision(
                "maison_waiting",
                character,
                followerAiEnabled,
                waitingPointEnabled: true,
                movementMode: distance <= arriveDistance ? "waiting_point_idle" : "waiting_point_return",
                reason: "this character is follower/waiting");
            float inputScale = Mathf.Max(1f, maisonMaxInput);
            controller.MoveWorld(new Vector2(direction.x, direction.z) * inputScale);
        }
    }

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        SquadManager manager = SquadManager.Instance;
        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject taggedRoot = null;
        GameObject squadRoot = null;

        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
                taggedRoot = current.gameObject;
            }

            if (manager != null && manager.squadCharacters != null && manager.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null && manager != null && manager.squadCharacters != null)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                    taggedRoot = root.gameObject;
                }

                for (int i = 0; i < manager.squadCharacters.Count; i++)
                {
                    GameObject candidate = manager.squadCharacters[i];
                    if (candidate != null && candidate.transform.IsChildOf(root))
                    {
                        squadRoot = candidate;
                        break;
                    }
                }
            }
        }

        if (squadRoot != null)
        {
            return squadRoot;
        }

        if (hasPlayerTag)
        {
            return taggedRoot;
        }

        return null;
    }

    private GameObject GetMaisonCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (current.TryGetComponent(out SquadCharacterController controller))
            {
                return controller.gameObject;
            }

            current = current.parent;
        }

        Transform root = other.transform.root;
        if (root != null)
        {
            SquadCharacterController controller = root.GetComponentInChildren<SquadCharacterController>(true);
            if (controller != null)
            {
                return controller.gameObject;
            }
        }

        return null;
    }

    private void NotifyZoneEnter()
    {
        if (!playZoneMusic || zoneMusic == null)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.RegisterZoneEnter(this);
        }
    }

    private void NotifyZoneExit()
    {
        if (!playZoneMusic || zoneMusic == null)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.RegisterZoneExit(this);
        }
    }

    private void ApplyWaitingRotation(GameObject character, Quaternion rotation)
    {
        if (character == null)
        {
            return;
        }

        Rigidbody rb = character.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.MoveRotation(rotation);
            return;
        }

        character.transform.rotation = rotation;
    }

    private SquadFollowerAgent GetFollowerAgent(GameObject character)
    {
        if (character == null || !maisonAutoAddNavMeshFollower)
        {
            return character != null ? character.GetComponent<SquadFollowerAgent>() : null;
        }

        SquadFollowerAgent agent = character.GetComponent<SquadFollowerAgent>();
        if (agent == null)
        {
            agent = character.AddComponent<SquadFollowerAgent>();
        }

        return agent;
    }

    private bool RegisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            characterColliderCounts[character] = 1;
            return true;
        }

        characterColliderCounts[character] = count + 1;
        return false;
    }

    private bool UnregisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            return false;
        }

        count -= 1;
        if (count > 0)
        {
            characterColliderCounts[character] = count;
            return false;
        }

        characterColliderCounts.Remove(character);
        return true;
    }

    private static bool AddMaisonCharacter(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!maisonCounts.TryGetValue(character, out int count))
        {
            count = 0;
        }

        maisonCounts[character] = count + 1;
        return count == 0;
    }

    private static void RemoveMaisonCharacter(GameObject character, int amount)
    {
        if (character == null)
        {
            return;
        }

        if (!maisonCounts.TryGetValue(character, out int count))
        {
            return;
        }

        count -= Mathf.Max(1, amount);
        if (count > 0)
        {
            maisonCounts[character] = count;
        }
        else
        {
            maisonCounts.Remove(character);
        }
    }

    private static bool ShouldSimulateMaisonCharacters()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }
}
