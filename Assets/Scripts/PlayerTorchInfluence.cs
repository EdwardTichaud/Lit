using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a character's equipped torch participate in the lit-influence system.
/// The influence radius follows the attached Unity Light's effective range.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerTorchInfluence : MonoBehaviour
{
    private static readonly HashSet<PlayerTorchInfluence> activeTorches = new();
    private static bool registryInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        activeTorches.Clear(); registryInitialized = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeRegistry()
    {
        if (registryInitialized) return;
        foreach (var source in FindObjectsByType<PlayerTorchInfluence>(FindObjectsInactive.Exclude))
            if (source.isActiveAndEnabled) activeTorches.Add(source);
        registryInitialized = true;
    }

    public static void CopyActiveInfluenceTorches(List<PlayerTorchInfluence> destination)
    {
        if (Application.isPlaying) InitializeRegistry();
        destination.Clear();
        foreach (var source in activeTorches)
            if (source != null && source.isActiveAndEnabled) destination.Add(source);
    }

    [SerializeField, Tooltip("Lumiere de la torche. Si vide, cherche une Light sur cet objet.")]
    private Light torchLight;
    [SerializeField, Tooltip("Controleur du personnage portant la torche. Si vide, cherche dans les parents.")]
    private SquadCharacterController owner;
    [SerializeField, Tooltip("Zone d'influence active quand la torche du personnage est equipee.")]
    private LitInfluenceSource litInfluence = new LitInfluenceSource();

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        activeTorches.Add(this);
        CacheReferences();
        UpdateInfluence(true);
    }

    private void Update()
    {
        UpdateInfluence(false);
    }

    private void OnDisable()
    {
        activeTorches.Remove(this);
        litInfluence?.Clear(this, LitInfluenceSourceKind.Flame);
    }

    public bool ProvidesLitInfluenceTo(Collider targetCollider, Vector3 fallbackPoint)
    {
        CacheReferences();
        if (!isActiveAndEnabled || owner == null || !owner.IsFlameEquipped ||
            torchLight == null || !torchLight.isActiveAndEnabled) return false;
        litInfluence.SetRadius(torchLight.range);
        return litInfluence.TouchesCollider(transform, targetCollider, fallbackPoint);
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (torchLight == null)
        {
            torchLight = GetComponent<Light>();
        }

        if (owner == null)
        {
            owner = GetComponentInParent<SquadCharacterController>(true);
        }

        if (litInfluence == null)
        {
            litInfluence = new LitInfluenceSource();
        }
    }

    private void UpdateInfluence(bool force)
    {
        CacheReferences();
        if (torchLight != null)
        {
            litInfluence.SetRadius(torchLight.range);
        }

        bool isLit = owner != null
            && owner.IsFlameEquipped
            && torchLight != null
            && torchLight.isActiveAndEnabled;
        litInfluence.Tick(this, LitInfluenceSourceKind.Flame, isLit, force);
    }
}
