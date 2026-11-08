using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class SecretPassagesAuthoring
{
    [MenuItem("Tools/Lit/Zone 2/Apply approved District access and second passage")]
    public static void CompleteDistrictConnection()
    {
        var setup = Setup(); newItems.Clear();
        stone = Load<Material>("Assets/Scenes/Reve/Completion/Data/LimbesExpansionStone.mat");
        try
        {
            var catalog = Load<SecretPassageCatalog>(CatalogPath);
            if (catalog.entries[1].definition != null) throw new InvalidOperationException("District passage already authored; edit it in place.");
            var district = EditorSceneManager.OpenScene(DistrictNotesScene, OpenSceneMode.Single);
            var core = EditorSceneManager.OpenScene("Assets/Scenes/District_1/District_1_Core.unity", OpenSceneMode.Additive);
            foreach (var source in Load<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset").loadingScenes)
            {
                string path = AssetDatabase.GetAssetPath(source);
                if (!SceneManager.GetSceneByPath(path).isLoaded) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            Physics.SyncTransforms();
            var root = district.GetRootGameObjects().Single().transform;
            var spawn = core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ZoneSpawnPoint>(true)).OrderBy(p => p.PartySlot).First();
            var a = ClearLanding(spawn.transform.position + new Vector3(-2, 0, 3));
            var b = ClearLanding(new Vector3(-45, -93.73f, 33));
            var portalPoint = ClearLanding(new Vector3(-47, -93.73f, 36));
            var definition = Define(catalog, 2, "district1.entry_to_west", "District 1 — l'entrée et le couloir ouest",
                "District 1 : près de l'entrée du quartier, une ouverture étroite rejoint le couloir ouest. Elle évite le détour par les salles.",
                district.name, "Reve_Limbes_Secrets");
            Passage(root, definition, a, b, "Cette ouverture relie l'entrée du quartier au couloir ouest.");
            var portal = Child(root, "Acces_libre_aux_Cours_interieures", portalPoint);
            var arch = Block(portal.transform, "Arche_du_passage", portalPoint + Vector3.up * 1.5f, new Vector3(2, 3, .35f), false);
            var trigger = portal.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.center = Vector3.up; trigger.radius = 1.5f;
            var travel = portal.AddComponent<PortalController>(); Set(travel, "destinationZone", Load<ZoneManifest>(CourtsAuthoring.Folder + "/ZoneManifest_District_2_Courts.asset"));
            var props = new SerializedObject(travel); props.FindProperty("destinationMode").enumValueIndex = 1;
            props.FindProperty("interactionCollider").objectReferenceValue = trigger;
            var targets = props.FindProperty("destinationPoints"); targets.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var point = Child(portal.transform, "Arrivee_Cours_" + i, CourtsAuthoring.Origin + new Vector3((i % 2) * 2 - 1, .1f, (i / 2) * 2)).transform;
                targets.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            props.ApplyModifiedPropertiesWithoutUndo(); portal.AddComponent<RuntimeOutlineTarget>();
            // The portal is permanently available; no quest condition, global-note count or skill gate.
            EditorSceneManager.SaveScene(district);
            var limbes = EditorSceneManager.OpenScene(LimbesScene, OpenSceneMode.Additive);
            PlaceNote(limbes.GetRootGameObjects().Single().transform, definition.note, new Vector3(597.5f, 120.5f, -1));
            EditorSceneManager.SaveScene(limbes);
            RegisterItems(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log($"[DistrictConnection] Second passage A={a} B={b}; free portal at {portalPoint}; note 02 in Limbes. Ten current passages and ten future reservations.");
        }
        finally { newItems.Clear(); EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Verify();
    }

    private static Vector3 ClearLanding(Vector3 desired)
    {
        // Use the existing district's actual colliders, without adding replacement floors or altering its missing prefabs.
        foreach (float radius in new[] { 0f, 1f, 2f, 3f })
            for (int i = 0; i < (radius == 0 ? 1 : 8); i++)
            {
                var point = desired + new Vector3(Mathf.Cos(i * Mathf.PI / 4), 0, Mathf.Sin(i * Mathf.PI / 4)) * radius;
                foreach (var hit in Physics.RaycastAll(point + Vector3.up * 3, Vector3.down, 6, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                {
                    if (hit.normal.y < .9f || Mathf.Abs(hit.point.y - desired.y) > 1.2f) continue;
                    if (Physics.CheckCapsule(hit.point + Vector3.up * .4f, hit.point + Vector3.up * 1.6f, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return hit.point + Vector3.up * .06f;
                }
            }
        var nearby = Physics.RaycastAll(desired + Vector3.up * 10, Vector3.down, 30, ~0, QueryTriggerInteraction.Ignore);
        System.IO.Directory.CreateDirectory("Library/Zone2Secrets");
        System.IO.File.WriteAllLines("Library/Zone2Secrets/DistrictFloorDiagnostic.txt", new[] { "Desired " + desired, "Colliders " + UnityEngine.Object.FindObjectsByType<Collider>().Length }
            .Concat(nearby.Select(h => h.collider.name + " " + h.point + " normal=" + h.normal)));
        throw new InvalidOperationException("No clear existing floor near " + desired + "; no fallback floor will be invented.");
    }
}
