using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class JuggernautEssentialEvents
{
    private const string Folder = "Assets/Characters/3_Enemy/Juggernaut/";
    private static readonly string[] Names = { "Strike", "Followup", "Sweep", "Assomoir" };
    private static readonly float[] QteTimes = { .55f, .55f, .625f, .837f };
    private static readonly HashSet<string> Removed = new HashSet<string> {
        "CombatWarningOn", "CombatWarningOff", "BeginReactionTelegraph", "ShowReactionPrompt",
        "InstantiateEnemySkillVFX", "InstantiateEnemySkillVFXAtIndex", "ShowInput", "HideInput", "EndEnemyRush"
    };

    static JuggernautEssentialEvents() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        const string request = "Library/JuggernautEssentialEvents.request";
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request);
        try { Configure(); File.WriteAllText("Library/JuggernautEssentialEvents.result", "PASS"); }
        catch (Exception e) { File.WriteAllText("Library/JuggernautEssentialEvents.result", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Lit/Combat/Clean Juggernaut Animation Events")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Quitter Play Mode.");
        var skills = Names.Select(n => AssetDatabase.LoadAssetAtPath<SkillSO>(Folder + "Skill_Juggernaut_" + n + ".asset")).ToArray();
        if (skills.Any(s => s == null || s.AnimationClip == null)) throw new InvalidOperationException("Skill ou clip Juggernaut absent.");
        for (int i = 0; i < skills.Length; i++) ConfigureSkill(skills[i], QteTimes[i]);
        AssetDatabase.SaveAssets();
        Debug.Log("[JuggernautEvents] Quatre clips nettoyes et valides : reaction invisible, mouvement, EnemyAttack instantane, fin.");
    }

    public static void ConfigureSkill(SkillSO skill, float defaultQteTime)
    {
        var clip = skill.AnimationClip;
        var events = new List<AnimationEvent>();
        bool hasQte = AnimationUtility.GetAnimationEvents(clip).Any(e => e.functionName == "OpenEnemyReactionOpportunity" || e.functionName == "QTE");
        foreach (var entry in AnimationUtility.GetAnimationEvents(clip))
        {
            if (Removed.Contains(entry.functionName)) continue;
            if (entry.functionName == "QTE")
            {
                events.Add(Qte(entry.time));
                continue;
            }
            if (entry.functionName == "OpenReactionWindow")
            {
                if (!hasQte) { events.Add(Qte(entry.time)); hasQte = true; }
                continue;
            }
            if (entry.functionName == "ResolveEnemyAttackImpact" || entry.functionName == "OpenEnemyAttackHitbox" || entry.functionName == "HitPlayer" || entry.functionName == "HitPlayerIf" || entry.functionName == "ResolveThresholdFailureImpact")
            {
                events.Add(new AnimationEvent { functionName = "EnemyAttack", objectReferenceParameter = skill, time = entry.time });
                continue;
            }
            if (entry.functionName == "CloseEnemyAttackHitbox") continue;
            if (entry.functionName == "EnemyAttack") entry.objectReferenceParameter = skill;
            events.Add(entry);
        }
        if (!hasQte) events.Add(Qte(defaultQteTime));
        // Preserve author order for events sharing the same timestamp.
        AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());
        var data = new SerializedObject(skill);
        // EnemyAttack now owns these cues; never erase authored skill effects.
        data.FindProperty("acceptedEnemyReactions").ClearArray();
        data.FindProperty("requireAllEnemyReactions").boolValue = false;
        foreach (string profile in new[] { "combatWarning", "reactionTelegraph" })
        {
            var property = data.FindProperty(profile);
            property.FindPropertyRelative("enabled").boolValue = false;
            var iterator = property.Copy();
            var end = property.GetEndProperty();
            while (iterator.Next(true) && !SerializedProperty.EqualContents(iterator, end))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference) iterator.objectReferenceValue = null;
        }
        data.FindProperty("combatWarning.useSlowMotion").boolValue = false;
        data.FindProperty("reactionTelegraph.usePerfectWindowSlowMotion").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        EditorUtility.SetDirty(clip);
        Validate(skill);
    }

    private static AnimationEvent Qte(float time) => new AnimationEvent { functionName = "OpenEnemyReactionOpportunity", time = time };

    public static void Validate(SkillSO skill)
    {
        var events = AnimationUtility.GetAnimationEvents(skill.AnimationClip);
        var allowed = new HashSet<string> { "OpenEnemyReactionOpportunity", "LockEnemyAttackDirection", "BeginEnemyAdvance", "EndEnemyAdvance",
            "BeginEnemyAirborne", "RequestEnemyLanding", "BeginEnemyRush", "EndEnemyRush",
            "EnemyAttack", "EndEnemyAttack" };
        if (events.Any(e => !allowed.Contains(e.functionName)))
            throw new InvalidOperationException(skill.name + ": evenement hors contrat minimal.");
        var qtes = events.Where(e => e.functionName == "OpenEnemyReactionOpportunity").ToArray();
        if (qtes.Length != 1)
            throw new InvalidOperationException(skill.name + ": exactement une opportunite de reaction requise.");
        if (events.Any(e => Removed.Contains(e.functionName) || e.functionName == "OpenReactionWindow" || e.functionName == "ResolveEnemyAttackImpact"))
            throw new InvalidOperationException(skill.name + ": evenement legacy present.");
        if (events.Count(e => e.functionName == "EndEnemyAttack") != 1 || events.Last().functionName != "EndEnemyAttack")
            throw new InvalidOperationException(skill.name + ": fin d'attaque absente ou mal ordonnee.");
        var impacts = events.Where(e => e.functionName == "EnemyAttack").ToArray();
        if (impacts.Length != 1 || impacts[0].objectReferenceParameter != skill)
            throw new InvalidOperationException(skill.name + ": exactement un EnemyAttack reference vers ce SkillSO requis.");
        var locks = events.Where(e => e.functionName == "LockEnemyAttackDirection").ToArray();
        float impactTime = impacts[0].time;
        if (locks.Length != 1 || locks[0].time > impactTime || qtes[0].time >= impactTime)
            throw new InvalidOperationException(skill.name + ": QTE et verrouillage doivent preceder l'impact.");
        RequirePair(events, "BeginEnemyAdvance", "EndEnemyAdvance", skill.EnemyActionMotion.enableAdvance);
        RequirePair(events, "BeginEnemyAirborne", "RequestEnemyLanding", skill.EnemyActionMotion.movementMode == EnemyActionMovementMode.Airborne);
        if (events.Count(e => e.functionName == "BeginEnemyRush") != (skill.EnemyActionMotion.enableHomingRush ? 1 : 0))
            throw new InvalidOperationException(skill.name + ": un declenchement d'impulsion requis si activee.");
    }

    private static void RequirePair(AnimationEvent[] events, string begin, string end, bool required)
    {
        var starts = events.Where(e => e.functionName == begin).ToArray();
        var stops = events.Where(e => e.functionName == end).ToArray();
        if (starts.Length != stops.Length || starts.Length != (required ? 1 : 0) ||
            starts.Length == 1 && starts[0].time > stops[0].time)
            throw new InvalidOperationException("Evenements physiques incoherents : " + begin + " / " + end);
    }
}
