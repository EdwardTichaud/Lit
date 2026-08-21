using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GhostController))]
public sealed class GhostControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GhostController controller = (GhostController)target;
        GhostData data = controller.Data;
        if (data == null)
        {
            EditorGUILayout.HelpBox("Assignez un GhostData pour configurer la chaine d'enigmes.", MessageType.Warning);
            return;
        }

        List<string> issues = Validate(data, controller);
        if (issues.Count == 0)
            EditorGUILayout.HelpBox("Configuration des etapes validee.", MessageType.Info);
        else
            EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Warning);

        if (GUILayout.Button("Generer les identifiants d'etapes et de reponses"))
        {
            GenerateIds(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
    }

    private static List<string> Validate(GhostData data, GhostController controller)
    {
        List<string> issues = new List<string>();
        if (data.puzzleSteps == null || data.puzzleSteps.Count == 0)
        {
            issues.Add("Mode historique : une seule etape implicite nommee 'legacy'.");
            ValidateActions(controller, issues);
            return issues;
        }

        HashSet<string> stepIds = new HashSet<string>();
        for (int i = 0; i < data.puzzleSteps.Count; i++)
        {
            GhostPuzzleStep step = data.puzzleSteps[i];
            if (step == null) { issues.Add($"Etape {i + 1} vide."); continue; }
            if (string.IsNullOrWhiteSpace(step.stepId)) issues.Add($"Etape {i + 1} sans stepId.");
            else if (!stepIds.Add(step.stepId.Trim())) issues.Add($"stepId duplique : {step.stepId}.");
            if (string.IsNullOrWhiteSpace(step.question)) issues.Add($"Etape {i + 1} sans question.");
            if (step.reactions == null || step.reactions.Count == 0) issues.Add($"Etape {i + 1} sans reponse.");
        }
        ValidateActions(controller, issues);
        return issues;
    }

    private static void ValidateActions(GhostController controller, List<string> issues)
    {
        IReadOnlyList<GhostResolutionActionBinding> bindings = controller.ResolutionActionBindings;
        if (bindings == null) return;
        for (int i = 0; i < bindings.Count; i++)
        {
            GhostResolutionActionBinding binding = bindings[i];
            if (binding == null || binding.actions == null) continue;
            for (int j = 0; j < binding.actions.Count; j++)
            {
                GhostResolutionAction action = binding.actions[j];
                if (action == null) { issues.Add($"Action {i + 1}.{j + 1} vide."); continue; }
                string label = $"Action {i + 1}.{j + 1}";
                switch (action.actionType)
                {
                    case GhostResolutionActionType.PlayAnimationState:
                        if (!action.useLocalPlayerAnimator && action.animator == null) issues.Add(label + " : Animator manquant.");
                        if (string.IsNullOrWhiteSpace(action.animationState)) issues.Add(label + " : animationState manquant.");
                        break;
                    case GhostResolutionActionType.SetDoorOpen: if (action.door == null) issues.Add(label + " : Door manquante."); break;
                    case GhostResolutionActionType.SpawnPrefab: if (action.prefab == null) issues.Add(label + " : prefab manquant."); break;
                    case GhostResolutionActionType.PlayTimeline:
                        if (action.timelineDirector == null || action.timelineBindingProfile == null) issues.Add(label + " : PlayableDirector ou TimelineBindingProfile manquant.");
                        break;
                    case GhostResolutionActionType.PlayStorySequence: if (action.storySequenceRunner == null) issues.Add(label + " : StorySequenceRunner manquant."); break;
                }
            }
        }
    }

    private static void GenerateIds(GhostData data)
    {
        if (data.puzzleSteps == null) return;
        for (int i = 0; i < data.puzzleSteps.Count; i++)
        {
            GhostPuzzleStep step = data.puzzleSteps[i];
            if (step == null) continue;
            if (string.IsNullOrWhiteSpace(step.stepId)) step.stepId = "step_" + (i + 1);
            if (step.reactions == null) continue;
            for (int j = 0; j < step.reactions.Count; j++)
                if (step.reactions[j] != null && string.IsNullOrWhiteSpace(step.reactions[j].reactionId))
                    step.reactions[j].reactionId = "answer_" + (j + 1);
        }
    }
}
