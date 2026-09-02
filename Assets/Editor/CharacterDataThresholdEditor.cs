using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[CustomEditor(typeof(CharacterData))]
public sealed class CharacterDataThresholdEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CharacterData data = (CharacterData)target;
        if (!data.enableCombatHealthThresholds) return;

        List<string> issues = Validate(data);
        EditorGUILayout.HelpBox(
            issues.Count == 0
                ? "Paliers de vie valides. Chaque ThresholdSequence est autonome et ne depend pas d'une Timeline."
                : string.Join("\n", issues),
            issues.Count == 0 ? MessageType.Info : MessageType.Error);

        if (GUILayout.Button("Migrate Health Threshold Sequences"))
        {
            ThresholdSequenceMigrationUtility.MigrateAllCharacterData();
        }

        if (GUILayout.Button("Validate Combat Health Thresholds"))
        {
            if (issues.Count == 0) Debug.Log("[CombatThreshold] Validation auteur OK : " + data.name, data);
            else Debug.LogError("[CombatThreshold] Validation auteur echouee :\n" + string.Join("\n", issues), data);
        }
    }

    private static List<string> Validate(CharacterData data)
    {
        List<string> issues = new List<string>();
        if (!data.isEnemy) issues.Add("Les paliers de vie ne sont disponibles que pour un ennemi.");
        if (data.combatHealthThresholdStages == null || data.combatHealthThresholdStages.Count == 0)
        {
            issues.Add("Ajoutez au moins un palier ou desactivez cette option.");
            return issues;
        }

        HashSet<int> percents = new HashSet<int>();
        int previous = 100;
        for (int i = 0; i < data.combatHealthThresholdStages.Count; i++)
        {
            CombatHealthThresholdStage stage = data.combatHealthThresholdStages[i];
            string prefix = "Palier " + (i + 1) + " : ";
            if (stage == null)
            {
                issues.Add(prefix + "reference vide.");
                continue;
            }

            if (stage.healthPercent < 1 || stage.healthPercent > 99) issues.Add(prefix + "pourcentage requis entre 1 et 99.");
            if (!percents.Add(stage.healthPercent)) issues.Add(prefix + "pourcentage duplique.");
            if (stage.healthPercent >= previous) issues.Add(prefix + "la liste doit etre strictement decroissante.");
            previous = stage.healthPercent;
            ValidateSequence(data, stage.sequence, prefix, issues);
        }
        return issues;
    }

    private static void ValidateSequence(CharacterData data, ThresholdSequence sequence, string prefix, List<string> issues)
    {
        if (sequence == null)
        {
            issues.Add(prefix + "ThresholdSequence requise. Utilisez Migrate Health Threshold Sequences pour convertir les donnees existantes.");
            return;
        }

        if (sequence.successPlayerAnimationClip == null) issues.Add(prefix + "clip de reussite de Lucian requis.");
        if (sequence.successResolutionDelaySeconds < 0f) issues.Add(prefix + "delai de reussite negatif.");
        if (sequence.failureResult == ThresholdSequenceFailureResult.EnemySkill && sequence.failureRetaliationSkill == null)
        {
            issues.Add(prefix + "SkillSO de riposte requis lorsque Failure Result vaut Enemy Skill.");
        }
        else if (sequence.failureResult == ThresholdSequenceFailureResult.EnemySkill)
        {
            EnemySkills enemySkills = data.worldPrefab != null
                ? data.worldPrefab.GetComponent<EnemySkills>() ?? data.worldPrefab.GetComponentInChildren<EnemySkills>(true)
                : null;
            if (enemySkills == null || !enemySkills.Skills.Contains(sequence.failureRetaliationSkill))
            {
                issues.Add(prefix + "la riposte doit appartenir a EnemySkills du WorldPrefab ennemi.");
            }
        }

        AnimatorController controller = FindPlayerController();
        if (controller == null || string.IsNullOrWhiteSpace(sequence.PlayerQteStateName)) return;

        AnimatorState state = FindState(controller.layers, sequence.PlayerQteStateName);
        if (state == null)
        {
            issues.Add(prefix + "etat QTE introuvable dans Player_Model.controller : '" + sequence.PlayerQteStateName + "'.");
            return;
        }

        AnimatorState successState = !string.IsNullOrWhiteSpace(sequence.SuccessPlayerStateName)
            ? FindState(controller.layers, sequence.SuccessPlayerStateName)
            : null;
        if (!string.IsNullOrWhiteSpace(sequence.SuccessPlayerStateName) && successState == null)
        {
            issues.Add(prefix + "etat de reussite introuvable dans Player_Model.controller : '" + sequence.SuccessPlayerStateName + "'.");
        }

        if (!(state.motion is AnimationClip))
        {
            issues.Add(prefix + "l'etat QTE generique doit contenir un AnimationClip placeholder pour permettre le binding runtime.");
        }
        if (successState != null && !(successState.motion is AnimationClip))
        {
            issues.Add(prefix + "l'etat de reussite generique doit contenir un AnimationClip placeholder pour permettre le binding runtime.");
        }

        AnimationClip clip = sequence.playerQteAnimationClip != null
            ? sequence.playerQteAnimationClip
            : state.motion as AnimationClip;
        if (clip == null)
        {
            issues.Add(prefix + "le clip QTE doit etre assigne ou l'etat QTE doit utiliser un AnimationClip direct.");
            return;
        }

        List<float> qteTimes = new List<float>();
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        for (int i = 0; i < events.Length; i++)
        {
            if (events[i].functionName != "QTE") continue;
            string value = (events[i].stringParameter ?? string.Empty).Trim().ToUpperInvariant();
            if (value != "A" && value != "B" && value != "X" && value != "Y")
            {
                issues.Add(prefix + "QTE invalide '" + events[i].stringParameter + "' : utilisez A, B, X ou Y.");
            }
            qteTimes.Add(events[i].time);
        }
        if (qteTimes.Count == 0)
        {
            issues.Add(prefix + "l'etat QTE doit contenir au moins un Animation Event QTE(input).");
            return;
        }

        qteTimes.Sort();
        for (int i = 1; i < qteTimes.Count; i++)
        {
            if (qteTimes[i] - qteTimes[i - 1] <= 0.001f)
            {
                issues.Add(prefix + "deux Animation Events QTE(input) ont le meme instant. Espacez les pour conserver une chaine lisible.");
                break;
            }
        }
    }

    private static AnimatorController FindPlayerController()
    {
        string[] guids = AssetDatabase.FindAssets("Player_Model t:AnimatorController");
        return guids.Length == 0
            ? null
            : AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static AnimatorState FindState(AnimatorControllerLayer[] layers, string stateName)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            AnimatorState found = FindState(layers[i].stateMachine, stateName);
            if (found != null) return found;
        }
        return null;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == stateName) return states[i].state;
        }

        ChildAnimatorStateMachine[] children = machine.stateMachines;
        for (int i = 0; i < children.Length; i++)
        {
            AnimatorState found = FindState(children[i].stateMachine, stateName);
            if (found != null) return found;
        }
        return null;
    }
}
