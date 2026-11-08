using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoonriseLucPlacementAuthoring
{
    public static void ApplyAndPlayBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/District_1/District_1_Rooms_Environment.unity", OpenSceneMode.Single);
            var scene = EditorSceneManager.OpenScene(LucCycleSetup.ScenePath, OpenSceneMode.Additive);
            Physics.SyncTransforms();
            var collar = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InteractableItem>(true))
                .Single(i => i.representedItem != null && i.representedItem.itemId == "jon_collar");
            var renderer = collar.GetComponentInChildren<MeshRenderer>();
            if (renderer == null || renderer.GetComponent<MeshFilter>()?.sharedMesh == null)
                throw new InvalidOperationException("The existing collar mesh must be valid before correcting its authored placement.");
            Vector3 before = collar.transform.position;
            float bottomOffset = renderer.bounds.min.y - before.y;
            // This is the physical floor location reached by actual UCC in the
            // failed approach, beside the table and close to Jon's grave.
            Vector3 walkablePoint = new Vector3(-6.74f, -93.97f, 54.54f);
            var floor = Physics.RaycastAll(walkablePoint + Vector3.up * 2, Vector3.down, 4, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.transform.IsChildOf(collar.transform) && h.normal.y > .8f)
                .OrderBy(h => h.distance).FirstOrDefault();
            if (floor.collider == null || floor.collider.name == "Table" || Mathf.Abs(floor.point.y - walkablePoint.y) > .4f)
                throw new InvalidOperationException("The recorded standing point must still have its clear physical floor.");
            var blockers = Physics.OverlapCapsule(floor.point + Vector3.up * .25f, floor.point + Vector3.up * 1.55f, .2f, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !c.transform.IsChildOf(collar.transform)).ToArray();
            Vector3 candidate = floor.point + Vector3.up * (.03f - bottomOffset);
            string diagnostic = "before=" + before + " modelBottomOffset=" + bottomOffset + " floor=" + floor.collider.name +
                " surface=" + floor.point + " after=" + candidate + " blockers=" + string.Join(",", blockers.Select(c => c.name));
            Directory.CreateDirectory("Library/MoonriseRoute");
            File.WriteAllText("Library/MoonriseRoute/luc-collar-placement.txt", diagnostic);
            if (blockers.Length != 0) throw new InvalidOperationException("The authored floor clue must not occupy a wall or furniture volume; no scene saved.");
            collar.transform.position = candidate;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the corrected collar placement.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        BootstrapDistrictPlayProbe.StartMoonriseCombatBatch();
    }
}
