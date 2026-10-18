using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EnemyCounterTimingValidation
{
    public const string LabProfilePath = IluviliraeSetup.Folder + "/Clash/CombatLab_ClashEnemyProfile.asset";

    public static void Validate(LitBrainsCombatProfile profile)
    {
        if (profile == null) throw new InvalidOperationException("Profil de combat absent.");
        foreach (var attack in profile.attacks)
        {
            if (attack == null) throw new InvalidOperationException("Attaque nulle dans " + profile.name);
            if (!EnemyReactionTiming.ValidateImpact(attack.clip, attack.impactSeconds, out string reason))
                throw new InvalidOperationException(profile.name + "/" + attack.stateName + ": " + reason);
            var opening = AnimationUtility.GetAnimationEvents(attack.clip).Where(e => e.functionName == "OpenBrainsReactionOpportunity").ToArray();
            if (opening.Length != 1 || opening[0].time > Mathf.Max(0, attack.impactSeconds - .2f))
                throw new InvalidOperationException(attack.stateName + ": armer la reaction avant les 0.2 s precedant l'impact.");
        }
    }

    [MenuItem("Lit/Combat Lab/Validate Fair Counter Timings")]
    public static void ValidateLab()
    {
        Validate(AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(LabProfilePath));
        Debug.Log("[FairCounter] Timings du laboratoire valides : fenetres animees avant impact, clips inchanges.");
    }
}
