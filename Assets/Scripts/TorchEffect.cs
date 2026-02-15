using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TorchEffect", menuName = "Scriptable Objects/Effects/Torch")]
// Effet passif: permet de recharger la torche en rentrant a la maison.
public class TorchEffect : Effect, ISquadEffect
{
    [Header("Torch Max")]
    [Tooltip("Secondes max de torche au niveau 1.")]
    [SerializeField] private int maxSeconds = 300;
    [Tooltip("Bonus de secondes max par niveau.")]
    [SerializeField] private int maxSecondsPerLevel = 0;
    [Tooltip("Override par niveau (index 0 = niveau 1).")]
    [SerializeField] private List<int> maxSecondsByLevel = new List<int>();

    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return true;
    }

    public bool ApplyToSquad(int levelDelta)
    {
        return levelDelta > 0;
    }

    public int GetMaxSecondsForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        if (maxSecondsByLevel != null && maxSecondsByLevel.Count >= safeLevel)
        {
            return Mathf.Max(0, maxSecondsByLevel[safeLevel - 1]);
        }

        int value = maxSeconds + maxSecondsPerLevel * Mathf.Max(0, safeLevel - 1);
        return Mathf.Max(0, value);
    }

    public override string GetDescriptionForLevel(int level)
    {
        int value = GetMaxSecondsForLevel(level);
        return $"Torche max: {value}s (reset maison)";
    }

    public override string GetBonusDescriptionForLevel(int level)
    {
        int value = GetMaxSecondsForLevel(level);
        return $"Torche max: {value}s";
    }
}
