using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[CreateAssetMenu(fileName = "LightSkillSO", menuName = "Scriptable Objects/Combat/Light Skill SO")]
public sealed class LightSkillSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName = "Light Skill";
    [SerializeField] private Sprite icon;

    [Header("Charge")]
    [SerializeField, Min(1f)] private float requiredCharge = 100f;

    [Header("Cinematic Resolution")]
    [SerializeField] private PlayableAsset timeline;
    [Tooltip("Rig runtime poolé contenant Director, receiver et caméras de cette compétence.")]
    [SerializeField] private CombatCinematicRig combatCinematicRigPrefab;
    [Header("Timeline Bindings")]
    [Tooltip("Nom de l'Animation Track ciblee par l'Animator de Lucian.")]
    [SerializeField] private string playerAnimatorTrackName = "Player.Animator";
    [Tooltip("Nom de l'Animation Track ciblee par l'Animator de l'ennemi verrouille.")]
    [SerializeField] private string enemyAnimatorTrackName = "Enemy.Animator";
    [Tooltip("Nom de la piste Cinemachine de la Timeline.")]
    [SerializeField] private string cinemachineTrackName = "Cinemachine";
    [SerializeField, Min(0.1f), Tooltip("Portee maximale entre Lucian et la cible au lancement de la cinematic.")]
    private float maximumCinematicStartDistance = 18f;
    [Header("Cinematic Audio")]
    [SerializeField] private AudioClipSO startSfx;
    [SerializeField] private AudioClipSO impulseSfx;
    [SerializeField] private AudioClipSO impactSfx;
    [SerializeField, Min(0)] private int damage = 50;
    [SerializeField, Min(0f)] private float clarityGain = 15f;
    [Tooltip("Active l'impact a l'arret de la Timeline si aucun Signal n'a appele ResolveLightSkillImpact.")]
    [SerializeField] private bool resolveDamageWhenTimelineStops = true;

    [Header("Timeline VFX")]
    [Tooltip("Prefab instancie au premier signal, en enfant du point d'emission du caster.")]
    [SerializeField] private GameObject projectileVfxPrefab;
    [Tooltip("Chemin relatif depuis l'Animator du caster. Vide = racine du joueur.")]
    [SerializeField] private string projectileSpawnTransformPath;
    [SerializeField] private Vector3 projectileSpawnLocalOffset;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 18f;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private Vector3 impactVfxOffset;

    [Header("Timeline Signals")]
    [SerializeField] private SignalAsset spawnProjectileSignal;
    [SerializeField] private SignalAsset launchProjectileSignal;
    [SerializeField] private SignalAsset spawnImpactVfxSignal;
    [SerializeField] private SignalAsset resolveDamageSignal;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public float RequiredCharge => requiredCharge;
    public PlayableAsset Timeline => timeline;
    public CombatCinematicRig CombatCinematicRigPrefab => combatCinematicRigPrefab;
    public string PlayerAnimatorTrackName => playerAnimatorTrackName;
    public string EnemyAnimatorTrackName => enemyAnimatorTrackName;
    public string CinemachineTrackName => cinemachineTrackName;
    public float MaximumCinematicStartDistance => maximumCinematicStartDistance;
    public AudioClipSO StartSfx => startSfx;
    public AudioClipSO ImpulseSfx => impulseSfx;
    public AudioClipSO ImpactSfx => impactSfx;
    public int Damage => damage;
    public float ClarityGain => clarityGain;
    public bool ResolveDamageWhenTimelineStops => resolveDamageWhenTimelineStops;
    public GameObject ProjectileVfxPrefab => projectileVfxPrefab;
    public string ProjectileSpawnTransformPath => projectileSpawnTransformPath;
    public Vector3 ProjectileSpawnLocalOffset => projectileSpawnLocalOffset;
    public float ProjectileSpeed => projectileSpeed;
    public GameObject ImpactVfxPrefab => impactVfxPrefab;
    public Vector3 ImpactVfxOffset => impactVfxOffset;
    public SignalAsset SpawnProjectileSignal => spawnProjectileSignal;
    public SignalAsset LaunchProjectileSignal => launchProjectileSignal;
    public SignalAsset SpawnImpactVfxSignal => spawnImpactVfxSignal;
    public SignalAsset ResolveDamageSignal => resolveDamageSignal;
}
