using UnityEngine;

/// <summary>
/// Makes a character's equipped torch participate in the lit-influence system.
/// The influence radius follows the attached Unity Light's effective range.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerTorchInfluence : MonoBehaviour
{
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
        CacheReferences();
        UpdateInfluence(true);
    }

    private void Update()
    {
        UpdateInfluence(false);
    }

    private void OnDisable()
    {
        litInfluence?.Clear(this, LitInfluenceSourceKind.Flame);
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
