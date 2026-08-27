using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public sealed class LightSkillPostTimelineState
{
    [Tooltip("State Animator a jouer apres une fin naturelle de Timeline. Vide = conserver la pose finale.")]
    [SerializeField] private string animatorStateName;
    [SerializeField, Min(0f)] private float transitionSeconds = 0.08f;
    [SerializeField, Range(0f, 1f)] private float normalizedStartTime;

    public string AnimatorStateName => animatorStateName;
    public float TransitionSeconds => transitionSeconds;
    public float NormalizedStartTime => normalizedStartTime;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(animatorStateName);
}

[CreateAssetMenu(fileName = "LightSkillSO", menuName = "Scriptable Objects/Combat/Light Skill SO")]
public sealed class LightSkillSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName = "Light Skill";
    [SerializeField] private Sprite icon;

    [Header("Clarte")]
    [SerializeField] private LightSkillClarityTier requiredRank = LightSkillClarityTier.E;

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
    [SerializeField, Min(0f), Tooltip("Distance horizontale minimale entre Lucian et la cible au lancement de la cinematic.")]
    private float minimumCinematicStartDistance;
    [SerializeField, Min(0.1f), Tooltip("Distance horizontale maximale entre Lucian et la cible au lancement de la cinematic.")]
    private float maximumCinematicStartDistance = 18f;
    [Header("Cinematic Audio")]
    [SerializeField] private AudioClipSO startSfx;
    [SerializeField] private AudioClipSO impulseSfx;
    [SerializeField] private AudioClipSO impactSfx;
    [SerializeField, Min(0)] private int damage = 50;
    [Tooltip("Active l'impact a l'arret de la Timeline si aucun Signal n'a appele ResolveLightSkillImpact.")]
    [SerializeField] private bool resolveDamageWhenTimelineStops = true;

    [Header("Post Timeline States")]
    [Tooltip("Optionnel. Ne s'applique qu'a la fin naturelle de la Timeline.")]
    [SerializeField] private LightSkillPostTimelineState postTimelinePlayerState = new LightSkillPostTimelineState();
    [Tooltip("Optionnel. Ne s'applique qu'a la fin naturelle de la Timeline.")]
    [SerializeField] private LightSkillPostTimelineState postTimelineEnemyState = new LightSkillPostTimelineState();

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
    public LightSkillClarityTier RequiredRank => requiredRank;
    public PlayableAsset Timeline => timeline;
    public CombatCinematicRig CombatCinematicRigPrefab => combatCinematicRigPrefab;
    public string PlayerAnimatorTrackName => playerAnimatorTrackName;
    public string EnemyAnimatorTrackName => enemyAnimatorTrackName;
    public string CinemachineTrackName => cinemachineTrackName;
    public float MinimumCinematicStartDistance => Mathf.Min(minimumCinematicStartDistance, maximumCinematicStartDistance);
    public float MaximumCinematicStartDistance => Mathf.Max(minimumCinematicStartDistance, maximumCinematicStartDistance);
    public AudioClipSO StartSfx => startSfx;
    public AudioClipSO ImpulseSfx => impulseSfx;
    public AudioClipSO ImpactSfx => impactSfx;
    public int Damage => damage;
    public bool ResolveDamageWhenTimelineStops => resolveDamageWhenTimelineStops;
    public LightSkillPostTimelineState PostTimelinePlayerState => postTimelinePlayerState;
    public LightSkillPostTimelineState PostTimelineEnemyState => postTimelineEnemyState;
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

    public bool IsWithinCinematicStartRange(float horizontalDistance)
    {
        return horizontalDistance >= MinimumCinematicStartDistance &&
               horizontalDistance <= MaximumCinematicStartDistance;
    }

    private void OnValidate()
    {
        minimumCinematicStartDistance = Mathf.Max(0f, minimumCinematicStartDistance);
        maximumCinematicStartDistance = Mathf.Max(minimumCinematicStartDistance, maximumCinematicStartDistance);
    }
}
