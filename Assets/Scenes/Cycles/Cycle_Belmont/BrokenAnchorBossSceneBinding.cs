using UnityEngine;

/// <summary>Scene-only Belmont wiring. The reusable boss remains on its prefab; this binds its local puzzle targets.</summary>
[DisallowMultipleComponent]
public sealed class BrokenAnchorBossSceneBinding : MonoBehaviour
{
    [SerializeField, Tooltip("Racine de l'instance présente dans la scène. Elle prévaut sur le prefab de secours.")]
    private Transform bossRoot;
    [SerializeField] private BrokenAnchorBoss boss;
    [SerializeField] private Transform emissionPoint;
    [SerializeField] private BrokenAnchorTorch[] torches;
    [SerializeField] private BrokenAnchorLightBall orbPrefab;
    [SerializeField] private float telegraphSeconds = .75f;
    [SerializeField] private float salvoIntervalSeconds = 3f;
    [SerializeField] private float orbSpeed = 12f;
    [SerializeField] private float orbLifetimeSeconds = 8f;
    [SerializeField] private int playerDamage = 15;
    [SerializeField] private float releaseConvergenceSeconds = 3.5f;

    private void Awake()
    {
        ConfigureSceneBoss();
    }

    private void Start() => ConfigureSceneBoss();

    private void ConfigureSceneBoss()
    {
        // A scene reference can point at the prefab source instead of the baked instance.
        // Resolve below the authored root so the active boss receives the three scene torches.
        BrokenAnchorBoss instance = bossRoot != null ? bossRoot.GetComponentInChildren<BrokenAnchorBoss>(true) : null;
        if (instance != null) boss = instance;
        if (boss == null)
        {
            Debug.LogError("[BrokenAnchorSceneBinding] Boss introuvable : torches non configurées.", this);
            return;
        }

        boss.Configure(emissionPoint, torches, orbPrefab, telegraphSeconds, salvoIntervalSeconds, orbSpeed, orbLifetimeSeconds, playerDamage, releaseConvergenceSeconds);
        Debug.Log("[BrokenAnchorSceneBinding] Ancre configurée avec " + (torches != null ? torches.Length : 0) + " torches.", this);
    }
}
