using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoonriseInteractionPresentationAuthoring
{
    public static void ApplyAndPlayBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/District_1/District_1_Enigme_Levers.unity", OpenSceneMode.Single);
            var levers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Lever>(true)).ToArray();
            if (levers.Length != 2) throw new InvalidOperationException("Expected exactly the two Moonrise puzzle levers.");
            foreach (var lever in levers)
            {
                var serialized = new SerializedObject(lever);
                serialized.FindProperty("useSharedInteractionSelection").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(lever);
                SetPrompt(lever, "Actionner", Vector3.up * 1.7f);
                if (lever.GetComponentsInChildren<RuntimeOutlineTarget>(true).Length == 0) lever.gameObject.AddComponent<RuntimeOutlineTarget>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Moonrise lever presentation.");

            scene = EditorSceneManager.OpenScene("Assets/Scenes/District_1/District_1_SecretNotes.unity", OpenSceneMode.Single);
            var portal = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PortalController>(true))
                .Single(p => p.name == "Acces_libre_aux_Cours_interieures");
            SetPrompt(portal, "Rejoindre Stillhaven", Vector3.up * 3.25f);
            var header = portal.transform.Find("Arche_du_passage");
            if (header == null) throw new InvalidOperationException("Missing existing entrance presentation.");
            header.localPosition = Vector3.up * 2.85f;
            header.localScale = new Vector3(2.3f, .25f, .35f);
            foreach (var side in new[] { (name: "Montant_gauche", x: -1.05f), (name: "Montant_droit", x: 1.05f) })
            {
                var pillar = portal.transform.Find(side.name);
                if (pillar == null)
                {
                    pillar = UnityEngine.Object.Instantiate(header.gameObject, portal.transform).transform;
                    pillar.name = side.name;
                }
                pillar.localPosition = new Vector3(side.x, 1.35f, 0);
                pillar.localRotation = Quaternion.identity;
                pillar.localScale = new Vector3(.25f, 2.7f, .35f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the existing onward entrance presentation.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        BootstrapDistrictPlayProbe.StartMoonriseCombatBatch();
    }

    private static void SetPrompt(MonoBehaviour target, string text, Vector3 offset)
    {
        var prompt = target.GetComponent<WorldInteractionPrompt>() ?? target.gameObject.AddComponent<WorldInteractionPrompt>();
        prompt.target = target;
        prompt.label = text;
        prompt.worldOffset = offset;
        EditorUtility.SetDirty(prompt);
    }
}
