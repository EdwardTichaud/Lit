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

        if (GUILayout.Button("Validate All Threshold Sequences"))
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
        CombatHealthThresholdStageSettings stageSettings = data.combatHealthThresholdStageSettings;
        if (stageSettings == null)
        {
            issues.Add("Les reglages de pose des paliers sont absents.");
        }
        else
        {
            if (stageSettings.stageDistance < 0.1f) issues.Add("Distance de pose inferieure a 0,1 m.");
            if (stageSettings.stageRetrySeconds < 0.01f) issues.Add("Retry de pose inferieur a 0,01 s.");
            if (stageSettings.stageClearance < 0f) issues.Add("Clearance de pose negative.");
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

        if (!sequence.TryGetStepValidationIssue(out string sequenceIssue))
        {
            issues.Add(prefix + sequenceIssue + ".");
            return;
        }

        for (int stepIndex = 0; stepIndex < sequence.StepCount; stepIndex++)
        {
            ThresholdSequenceStep step = sequence.steps[stepIndex];
            if (step.failureResult != ThresholdSequenceFailureResult.EnemySkill) continue;
            EnemySkills enemySkills = data.worldPrefab != null
                ? data.worldPrefab.GetComponent<EnemySkills>() ?? data.worldPrefab.GetComponentInChildren<EnemySkills>(true)
                : null;
            if (enemySkills == null || !enemySkills.Skills.Contains(step.failureRetaliationSkill))
            {
                issues.Add(prefix + "step " + (stepIndex + 1) + " : la riposte doit appartenir a EnemySkills du WorldPrefab ennemi.");
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
