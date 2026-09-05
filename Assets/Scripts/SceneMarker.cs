using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Point d'auteur unique pour les personnages, items et fantomes. Tous les
/// objets sont materialises par le bake d'editeur. Au runtime le
/// marker ne cree jamais de prefab : il ne fait que referencer l'objet baked
/// deja present dans la scene.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Lit/Scene Marker")]
public sealed class SceneMarker : MonoBehaviour
{
    public enum MarkerAssetType
    {
        Character = 0,
        Item = 1,
        Ghost = 2
    }

    [SerializeField] private MarkerAssetType assetType = MarkerAssetType.Character;
    [SerializeField] private CharacterData characterData;
    [SerializeField] private Item item;
    [SerializeField] private GhostData ghost;
    [SerializeField, HideInInspector] private string markerId;
    [SerializeField, HideInInspector] private GameObject bakedCharacterInstance;

    private static readonly Dictionary<string, SceneMarker> markersById = new Dictionary<string, SceneMarker>();

    private GameObject runtimeInstance;
    private Coroutine navigationValidationRoutine;
    public CharacterData CharacterData => characterData;
    public Item Item => item;
    public GhostData Ghost => ghost;
    public MarkerAssetType AssetType => assetType;
    public bool UsesCharacter => assetType == MarkerAssetType.Character;
    public bool UsesItem => assetType == MarkerAssetType.Item;
    public bool UsesGhost => assetType == MarkerAssetType.Ghost;
    public string MarkerId => markerId;
    public GameObject RuntimeInstance => runtimeInstance;
    public GameObject BakedCharacterInstance => bakedCharacterInstance;

    private void Awake()
    {
        if (!UsesCharacter)
        {
            return;
        }

        RegisterMarker(this);
        EnsurePersistentState();
        runtimeInstance = ResolveBakedCharacterInstance();
        if (runtimeInstance != bakedCharacterInstance && bakedCharacterInstance != null)
        {
            Debug.LogWarning("[SceneMarker] Reference bakedCharacterInstance corrigee pour '" + name +
                             "' : la reference serialisee ne correspondait pas a son CharacterData.", this);
        }
        ScheduleBakedEnemyNavigationValidation();
    }

    private void OnDestroy()
    {
        if (!UsesCharacter)
        {
            return;
        }

        UnregisterMarker(this);
    }

    private void OnDisable()
    {
        if (navigationValidationRoutine != null)
        {
            StopCoroutine(navigationValidationRoutine);
            navigationValidationRoutine = null;
        }
    }

    public void SetCharacterData(CharacterData data)
    {
        assetType = MarkerAssetType.Character;
        characterData = data;
    }

    public void SetBakedCharacterInstance(GameObject instance)
    {
        bakedCharacterInstance = instance;
    }

    public void SetItem(Item value)
    {
        assetType = MarkerAssetType.Item;
        item = value;
    }

    public void SetGhost(GhostData value)
    {
        assetType = MarkerAssetType.Ghost;
        ghost = value;
    }

    public GameObject ResolvePreviewPrefab()
    {
        if (UsesItem)
        {
            return item != null ? item.ResolveWorldPrefab() : null;
        }

        if (UsesGhost)
        {
            return ghost != null ? ghost.ResolveWorldPrefab() : null;
        }

        return characterData != null ? characterData.ResolveWorldPrefab() : null;
    }

    private GameObject ResolveBakedCharacterInstance()
    {
        if (IsValidBakedCharacterInstance(bakedCharacterInstance))
        {
            return bakedCharacterInstance;
        }

        if (characterData == null)
        {
            return null;
        }

        // Scene files can retain a stale hidden reference after two markers
        // have been rebaked. Resolve the actual direct child by its authored
        // CharacterData instead of trusting that reference blindly.
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            CharacterInfo info = child.GetComponent<CharacterInfo>();
            if (info != null && info.CharacterData == characterData)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private bool IsValidBakedCharacterInstance(GameObject instance)
    {
        if (instance == null || instance.transform.parent != transform)
        {
            return false;
        }

        CharacterInfo info = instance.GetComponent<CharacterInfo>();
        return info != null && info.CharacterData == characterData;
    }

    public static bool TryGetRegisteredMarker(string id, out SceneMarker marker)
    {
        marker = null;
        return !string.IsNullOrWhiteSpace(id) && markersById.TryGetValue(id, out marker) && marker != null;
    }

    /// <summary>Configures the root generated by a SceneMarker before it is spawned.</summary>
    public static void ConfigureSpawnedCharacter(GameObject instance, CharacterData data, string persistentId, GameObject sourcePrefab = null)
    {
        if (instance == null || data == null)
        {
            return;
        }

        CharacterInfo characterInfo = instance.GetComponent<CharacterInfo>();
        if (characterInfo == null)
        {
            characterInfo = instance.AddComponent<CharacterInfo>();
        }
        characterInfo.SetCharacterData(data);

        if (data.isEnemy)
        {
            CombatHealth health = instance.GetComponent<CombatHealth>();
            if (health == null)
            {
                health = instance.AddComponent<CombatHealth>();
            }
            int maxHp = data.ResolveMaxHp();
            health.SetHealth(maxHp, maxHp);

            CombatEnemyRuntimeContract contract = instance.GetComponent<CombatEnemyRuntimeContract>();
            // A baked scene instance may predate the validator component while
            // still containing the complete combat setup. The contract itself
            // is only a validation/coordination component: adding it here does
            // not alter the authored physics, skills or Animator.
            if (contract == null && CombatEnemyRuntimeContract.HasRequiredComponents(instance))
            {
                contract = instance.AddComponent<CombatEnemyRuntimeContract>();
            }
            string contractReport;
            bool cloneValid;
            if (contract == null)
            {
                cloneValid = false;
                contractReport = "CombatEnemyRuntimeContract absent";
            }
            else
            {
                cloneValid = contract.ValidateContract(out contractReport);
            }
            bool sourceValid = sourcePrefab == null || CombatEnemyRuntimeContract.HasRequiredComponents(sourcePrefab);
            string sourceReport = CombatEnemyRuntimeContract.DescribeRequiredComponents(sourcePrefab);
            // A freshly spawned NavMeshAgent can be disabled until the dynamic
            // surface is built. Its enabled state is not a prefab-structure
            // mismatch and must not turn off the enemy's whole combat stack.
            if (!cloneValid || !sourceValid)
            {
                Debug.LogError("[SceneMarker] Ennemi runtime invalide | CharacterData='" +
                               data.name + "' | prefab='" + (data.worldPrefab != null ? data.worldPrefab.name : "<none>") +
                               "' | source={" + sourceReport +
                               "} | clone={" + contractReport + "}.", instance);
                if (contract != null)
                {
                    contract.DisableCombatSystems();
                }
                else
                {
                    DisableIncompleteEnemyCombat(instance);
                }
            }

        }

        if (TryGetRegisteredMarker(persistentId, out SceneMarker marker))
        {
            marker.SetRuntimeInstance(instance);
        }
    }

    private static void DisableIncompleteEnemyCombat(GameObject instance)
    {
        EnemySkills skills = instance.GetComponent<EnemySkills>();
        if (skills != null) skills.enabled = false;
        CombatEnemyPhysicsMotor physics = instance.GetComponent<CombatEnemyPhysicsMotor>();
        if (physics != null) physics.enabled = false;
        RealTimeCombatEnemyBehaviour behaviour = instance.GetComponent<RealTimeCombatEnemyBehaviour>();
        if (behaviour != null) behaviour.enabled = false;
        CombatEnemyLocomotionController locomotion = instance.GetComponent<CombatEnemyLocomotionController>();
        if (locomotion != null) locomotion.enabled = false;
    }

    public void ApplyPersistedState(Vector3 position, Quaternion rotation, Vector3 scale, int currentHp, int maxHp, bool active)
    {
        if (runtimeInstance == null)
        {
            return;
        }

        bool resetEnemyPose = characterData != null && characterData.isEnemy;
        if (resetEnemyPose)
        {
            // La sauvegarde conserve la vie et l'etat actif, mais jamais la
            // pose d'un ennemi : chaque session repart de son marker auteur.
            runtimeInstance.transform.SetPositionAndRotation(transform.position, transform.rotation);
            AuditEnemyRuntimePose("pose reinitialisee pour nouvelle session");
        }
        else
        {
            runtimeInstance.transform.SetPositionAndRotation(position, rotation);
            runtimeInstance.transform.localScale = scale;
            AuditEnemyRuntimePose("etat persistant applique");
        }
        CombatHealth health = runtimeInstance.GetComponent<CombatHealth>();
        if (health != null && maxHp > 0)
        {
            health.SetHealth(currentHp, maxHp);
        }

        runtimeInstance.SetActive(active);
    }

    private void SetRuntimeInstance(GameObject instance)
    {
        runtimeInstance = instance;
        AuditEnemyRuntimePose("clone configure");
        ValidateSpawnedEnemyNavigation(runtimeInstance);
        ScheduleBakedEnemyNavigationValidation();
    }

    private void ScheduleBakedEnemyNavigationValidation()
    {
        if (navigationValidationRoutine != null)
        {
            StopCoroutine(navigationValidationRoutine);
        }

        if (runtimeInstance != null && characterData != null && characterData.isEnemy && isActiveAndEnabled)
        {
            navigationValidationRoutine = StartCoroutine(ValidateEnemyNavigationAfterWorldBake());
        }
    }

    private IEnumerator ValidateEnemyNavigationAfterWorldBake()
    {
        NavMeshWorldService world = FindAnyObjectByType<NavMeshWorldService>();
        SquadAIManager navigation = SquadAIManager.Instance;
        while (world != null && !world.IsReady && world.State != NavMeshWorldState.Failed)
        {
            yield return null;
        }

        navigationValidationRoutine = null;
        if (world != null)
        {
            if (!world.IsReady)
            {
                Debug.LogWarning("[SceneMarker] Validation NavMesh en attente pour l'ennemi '" + name +
                                 "' : NavMeshWorldService a refuse le monde courant. Aucun repositionnement automatique.", this);
                yield break;
            }

            ValidateSpawnedEnemyNavigation(runtimeInstance);
            yield break;
        }

        if (navigation == null || !navigation.IsNavMeshReady)
        {
            Debug.LogWarning("[SceneMarker] Validation NavMesh en attente pour l'ennemi '" + name +
                             "' : aucun service NavMesh pret. Aucun repositionnement automatique.", this);
            yield break;
        }

        ValidateSpawnedEnemyNavigation(runtimeInstance);
    }

    private void AuditEnemyRuntimePose(string phase)
    {
        if (runtimeInstance == null || characterData == null || !characterData.isEnemy)
        {
            return;
        }

        float positionOffset = Vector3.Distance(runtimeInstance.transform.position, transform.position);
        float rotationOffset = Quaternion.Angle(runtimeInstance.transform.rotation, transform.rotation);
        Debug.Log("[SceneMarker] Pose ennemi | marker='" + name + "' | phase=" + phase +
                  " | markerWorld=" + transform.position + " | actorWorld=" + runtimeInstance.transform.position +
                  " | actorLocal=" + runtimeInstance.transform.localPosition +
                  " | parent=" + (runtimeInstance.transform.parent != null ? runtimeInstance.transform.parent.name : "<none>") +
                  " | offset=" + positionOffset.ToString("F4") + "m | rotation=" + rotationOffset.ToString("F2") + "deg.", this);
    }

    private void ValidateSpawnedEnemyNavigation(GameObject instance)
    {
        if (instance == null || characterData == null || !characterData.isEnemy)
        {
            return;
        }

        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogWarning("[SceneMarker] Ennemi '" + name + "' sans NavMeshAgent: il restera statique.", this);
            return;
        }

        Vector3 offset = instance.transform.position - transform.position;
        float horizontalOffset = new Vector2(offset.x, offset.z).magnitude;
        // The physics motor can make a small vertical grounding adjustment as
        // soon as the actor is created. A marker mismatch is only meaningful
        // when the actor moved sideways or was displaced by a full body height.
        if (horizontalOffset > 0.15f || Mathf.Abs(offset.y) > 0.75f)
        {
            Debug.LogWarning("[SceneMarker] Pose de spawn incoherente pour '" + name +
                             "' | marker=" + transform.position + " | instance=" + instance.transform.position + ".", this);
        }

        NavMeshWorldService world = FindAnyObjectByType<NavMeshWorldService>();
        int areaMask = agent.areaMask == 0 ? NavMesh.AllAreas : agent.areaMask;
        if (world != null)
        {
            if (!world.TryValidatePosition(instance.transform.position, areaMask, out _))
            {
                Debug.LogError("[SceneMarker] NavMeshWorldService refuse la projection locale de '" + name +
                                "' | actor=" + instance.transform.position + ". Aucun repositionnement automatique.", this);
            }
            return;
        }

        if (!NavMesh.SamplePosition(instance.transform.position, out NavMeshHit hit, 1.5f, areaMask))
        {
            Debug.LogError("[SceneMarker] Ennemi '" + name + "' hors NavMesh a son spawn: " +
                           instance.transform.position + ". Aucun repositionnement automatique ne sera applique.", this);
            return;
        }

        if (Vector3.Distance(hit.position, instance.transform.position) > 0.15f)
        {
            Debug.LogError("[SceneMarker] NavMesh trop eloigne pour '" + name + "' | actor=" +
                           instance.transform.position + " | nav=" + hit.position + ".", this);
        }
    }

    private void EnsurePersistentState()
    {
        if (string.IsNullOrWhiteSpace(markerId))
        {
            return;
        }

        if (GetComponent<PersistentSceneMarkerCharacterState>() == null)
        {
            gameObject.AddComponent<PersistentSceneMarkerCharacterState>();
        }

        PersistentNetworkObject persistentObject = NetcodeRuntimeUtilities.GetOrAdd<PersistentNetworkObject>(gameObject);
        persistentObject.AssignSceneIdentity(markerId);
    }

    private static void RegisterMarker(SceneMarker marker)
    {
        if (marker != null && !string.IsNullOrWhiteSpace(marker.markerId))
        {
            markersById[marker.markerId] = marker;
        }
    }

    private static void UnregisterMarker(SceneMarker marker)
    {
        if (marker != null && !string.IsNullOrWhiteSpace(marker.markerId) &&
            markersById.TryGetValue(marker.markerId, out SceneMarker registered) && registered == marker)
        {
            markersById.Remove(marker.markerId);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (!UsesCharacter)
        {
            return;
        }

        string generatedId = PersistentIdUtility.GenerateSceneObjectId(gameObject);
        if (!string.IsNullOrWhiteSpace(generatedId) && !string.Equals(markerId, generatedId, StringComparison.Ordinal))
        {
            markerId = generatedId;
            EditorUtility.SetDirty(this);
        }
    }

    private void OnDrawGizmos()
    {
        DrawWorldPrefabGizmo(Selection.Contains(gameObject));
    }

    private void DrawWorldPrefabGizmo(bool selected)
    {
        GameObject worldPrefab = ResolvePreviewPrefab();
        if (worldPrefab == null)
        {
            return;
        }

        Renderer[] renderers = worldPrefab.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds localBounds = renderer.localBounds;
            Matrix4x4 relativeMatrix = worldPrefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Vector3 center = relativeMatrix.MultiplyPoint3x4(localBounds.center);
            if (!hasBounds)
            {
                bounds = new Bounds(center, localBounds.size);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(center + localBounds.extents);
                bounds.Encapsulate(center - localBounds.extents);
            }
        }

        if (!hasBounds)
        {
            return;
        }

        Color previous = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = selected ? new Color(1f, 0.65f, 0.15f, 1f) : new Color(0.15f, 0.85f, 1f, 0.95f);
        Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.TRS(Vector3.zero, worldPrefab.transform.localRotation, worldPrefab.transform.localScale);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        Gizmos.color = previous;
        Gizmos.matrix = previousMatrix;
    }
#endif
}
