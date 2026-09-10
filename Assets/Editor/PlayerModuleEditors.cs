using UnityEditor;
using UnityEngine;

public class PlayerModuleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var module = (Component)target;
        var data = PlayerSettingsAuthoring.ResolveData(module);
        EditorGUILayout.HelpBox("Les reglages propres au personnage sont dans CharacterData > Modules du joueur. Les references ci-dessous appartiennent a cette instance.", MessageType.Info);
        if (data != null && GUILayout.Button("Ouvrir la fiche du personnage")) Selection.activeObject = data;
        var property = serializedObject.GetIterator();
        bool enter = true;
        while (property.NextVisible(enter))
        {
            enter = false;
            if (property.name == "m_Script") continue;
            EditorGUILayout.PropertyField(property, new GUIContent(property.displayName,
                string.IsNullOrEmpty(property.tooltip) ? "Reference locale : " + property.displayName + "." : property.tooltip), true);
        }
        serializedObject.ApplyModifiedProperties();
        if (!Application.isPlaying) return;
        using (new EditorGUI.DisabledScope(true))
        {
            if (module is PlayerScriptedJumpController jump) EditorGUILayout.Toggle("Saut en cours", jump.IsActive);
            if (module is PlayerScriptedDodgeController dodge) EditorGUILayout.Toggle("Esquive en cours", dodge.IsActive);
            if (module is PlayerStateMotionController motion) EditorGUILayout.Toggle("Trajectoire en cours", motion.IsActive);
            if (module is LitOpsiveLocomotionBridge bridge) EditorGUILayout.Toggle("Pilotage UCC actif", bridge.IsDriving);
            if (module is PlayerActionPresentationController presentation) EditorGUILayout.Toggle("Action en cours", presentation.IsActionActive);
        }
    }
}

[CustomEditor(typeof(PlayerScriptedJumpController))]
public sealed class PlayerScriptedJumpControllerModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(PlayerScriptedDodgeController))]
public sealed class PlayerScriptedDodgeControllerModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(PlayerStateMotionController))]
public sealed class PlayerStateMotionControllerModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(LitOpsiveLocomotionBridge))]
public sealed class LitOpsiveLocomotionBridgeModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(LitUccInteractionBridge))]
public sealed class LitUccInteractionBridgeModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(LitUccFollowerBridge))]
public sealed class LitUccFollowerBridgeModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(LitUccDamageBridge))]
public sealed class LitUccDamageBridgeModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(PlayerRootMotionRelay))]
public sealed class PlayerRootMotionRelayModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(PlayerAnimationController))]
public sealed class PlayerAnimationControllerModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(PlayerActionPresentationController))]
public sealed class PlayerActionPresentationControllerModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(RealTimeCombatLoadout))]
public sealed class RealTimeCombatLoadoutModuleEditor : PlayerModuleEditor { }

[CustomEditor(typeof(LocomotionAnimationEvent))]
public sealed class LocomotionAnimationEventModuleEditor : PlayerModuleEditor { }
