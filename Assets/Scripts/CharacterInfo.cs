using System;
using UnityEngine;

/// <summary>Instance data and health; runtime changes never modify the authored asset.</summary>
[DisallowMultipleComponent]
public class CharacterInfo : MonoBehaviour
{
    [SerializeField, Tooltip("Fiche source partagee. Les donnees modifiables sont copiees pour cette instance.")]
    private CharacterData characterData;
    [SerializeField, HideInInspector] private int maxHp = 8;
    [SerializeField, HideInInspector] private int currentHp;
    [SerializeField, HideInInspector] private bool initializeEmptyHealthToMax = true;
    private CharacterData runtimeData;
    private EnemyCombatProfileSO runtimeProfile;
    private bool healthInitialized;
    private bool healthRestored;

    public CharacterData SourceData => characterData;
    public CharacterData CharacterData { get { EnsureData(); return runtimeData != null ? runtimeData : characterData; } }
    public CharacterData Enemy => CharacterData;
    public int MaxHp { get { EnsureHealth(); return maxHp; } }
    public int CurrentHp { get { EnsureHealth(); return currentHp; } }
    public bool IsDead => CurrentHp <= 0;
    public event Action<CharacterInfo> HealthChanged;
    public event Action<CharacterInfo> DataChanged;

    private void Awake() { EnsureData(); EnsureHealth(); }

    private void EnsureData()
    {
        if (runtimeData != null || characterData == null || !Application.isPlaying) return;
        runtimeData = Instantiate(characterData);
        runtimeData.name = characterData.name + " (runtime)";
        runtimeData.hideFlags = HideFlags.HideAndDontSave;
        if (characterData.enemyCombatProfile != null)
        {
            runtimeProfile = Instantiate(characterData.enemyCombatProfile);
            runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            runtimeData.enemyCombatProfile = runtimeProfile;
        }
    }

    private void EnsureHealth()
    {
        if (healthInitialized) return;
        healthInitialized = true;
        EnsureData();
        if (!healthRestored && CharacterData != null) maxHp = CharacterData.ResolveMaxHp();
        maxHp = Mathf.Max(1, maxHp);
        if (!healthRestored && initializeEmptyHealthToMax && currentHp <= 0) currentHp = maxHp;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
    }

    public void SetCharacterData(CharacterData data)
    {
        if (characterData == data) { EnsureData(); EnsureHealth(); return; }
        ReleaseData();
        characterData = data;
        EnsureData();
        if (!healthRestored)
        {
            healthInitialized = false;
            currentHp = 0;
            EnsureHealth();
        }
        DataChanged?.Invoke(this);
    }

    public void SetEnemy(CharacterData data) => SetCharacterData(data);
    public bool InitializeMaxHealthFromCharacterData()
    {
        EnsureData();
        EnsureHealth();
        return CharacterData != null;
    }

    /// <summary>Explicit restored state, including zero HP.</summary>
    public void SetHealth(int current, int max)
    {
        healthRestored = healthInitialized = true;
        maxHp = Mathf.Max(1, max);
        currentHp = Mathf.Clamp(current, 0, maxHp);
        HealthChanged?.Invoke(this);
    }

    public int ApplyDamage(int amount)
    {
        EnsureHealth();
        if (amount <= 0 || currentHp <= 0) return 0;
        int applied = Mathf.Min(amount, currentHp);
        currentHp -= applied;
        HealthChanged?.Invoke(this);
        return applied;
    }

    public void ForceDefeat()
    {
        EnsureHealth();
        if (currentHp <= 0) return;
        currentHp = 0;
        HealthChanged?.Invoke(this);
    }

    public void RestoreToMax()
    {
        EnsureHealth();
        currentHp = maxHp;
        HealthChanged?.Invoke(this);
    }

    private void OnDestroy() => ReleaseData();
    private void ReleaseData()
    {
        if (runtimeProfile != null) Destroy(runtimeProfile);
        if (runtimeData != null) Destroy(runtimeData);
        runtimeProfile = null;
        runtimeData = null;
    }
}
