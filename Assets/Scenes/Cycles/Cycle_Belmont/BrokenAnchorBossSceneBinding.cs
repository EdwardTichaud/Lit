using UnityEngine;

/// <summary>Scene-only Belmont wiring. The reusable boss remains on its prefab; this binds its local puzzle targets.</summary>
[DisallowMultipleComponent]
public sealed class BrokenAnchorBossSceneBinding : MonoBehaviour
{
    [SerializeField] private BrokenAnchorBoss boss;
    [SerializeField] private Transform emissionPoint;
    [SerializeField] private Flame[] torches;
    [SerializeField] private BrokenAnchorLightBall orbPrefab;
    [SerializeField] private float telegraphSeconds = .75f;
    [SerializeField] private float salvoIntervalSeconds = 3f;
    [SerializeField] private float orbSpeed = 12f;
    [SerializeField] private float orbLifetimeSeconds = 8f;
    [SerializeField] private int playerDamage = 15;
    [SerializeField] private float releaseConvergenceSeconds = 3.5f;
    private void Awake()
    {
        if (boss != null) boss.Configure(emissionPoint, torches, orbPrefab, telegraphSeconds, salvoIntervalSeconds, orbSpeed, orbLifetimeSeconds, playerDamage, releaseConvergenceSeconds);
    }
}
