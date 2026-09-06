using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NinaCycleController))]
public sealed class NinaCycleInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var cycle = (NinaCycleController)target;
        if (cycle.definition == null) { Missing("Définition du cycle"); return; }
        if (cycle.definition.existence == null || cycle.definition.dilemma == null) Missing("Savoirs du cycle");
        if (cycle.scientistMarker == null || cycle.scientistMarker.BakedCharacterInstance == null) Missing("Scientifique fou : assigner son prefab CharacterData puis Bake in Scene sur son marker");
        if (cycle.ninaAnimator == null || cycle.ninaAnimator.runtimeAnimatorController == null) Missing("Modèle Nina et Animator avec clips Idle et Dead");
        if (cycle.ninaBlood == null || cycle.ninaBlood.GetComponentInChildren<Renderer>(true) == null) Missing("Prefab Nina's blood : placer sous l'objet réservé");
        if (cycle.director == null || cycle.director.playableAsset == null || cycle.bindingProfile == null) Missing("Timeline et TimelineBindingProfile de la cinématique");
        if (cycle.definition.cicatrice == null) Missing("SkillSO Cicatrice : assigner la compétence finalisée");
        if (cycle.scar == null || cycle.scar.GetComponentInChildren<Renderer>(true) == null) Missing("Modèle de Scar");
        EditorGUILayout.HelpBox("Les objets sont des emplacements d'auteur. Placer les markers dans District_1 et baker le parchemin après assignation de son WorldPrefab. Aucun substitut artistique n'est utilisé.", MessageType.Info);
    }
    private static void Missing(string message) => EditorGUILayout.HelpBox("À configurer : " + message, MessageType.Warning);
}
