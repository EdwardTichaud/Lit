using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.DreamFractures
{
    /// <summary>Disk saves keep spent resources, but an ephemeral challenge never becomes a resume location.</summary>
    public static class DreamFractureSavePolicy
    {
        private static DreamFractureCatalog Catalog => Resources.Load<DreamFractureCatalog>("DreamFractures/Catalog");
        public static bool IsChallengeScene(string scene)
        {
            var catalog = Catalog;
            return catalog != null && catalog.fractures.Any(f => f != null && f.zone != null &&
                (f.zone.PrimarySceneName == scene || f.zone.LoadingSceneNames.Contains(scene) ||
                 f.zone.PostLoadingSceneNames.Contains(scene)));
        }
        public static string ResumeScene(string scene) => IsChallengeScene(scene) ? Catalog.returnSceneName : scene;
        private static Vector3 ReturnPosition(int index)
        {
            var points = Catalog.returnPositions;
            return points != null && points.Length > 0 ? points[index % points.Length] : Vector3.zero;
        }
        public static void NormalizeCharacterSave(CharacterSaveData data)
        {
            if (data == null || !IsChallengeScene(SceneManager.GetActiveScene().name)) return;
            int index = 0;
            foreach (var entry in data.characters)
                if (entry.inSquad) { entry.position = ReturnPosition(index++); entry.rotation = Quaternion.identity; }
        }
        public static void NormalizeWorldSave(WorldSnapshot snapshot)
        {
            if (snapshot == null || !IsChallengeScene(snapshot.SceneName)) return;
            var players = new HashSet<string>(snapshot.Players.Select(p => p.ControlledObjectId));
            for (int i = 0; i < snapshot.Players.Count; i++)
            {
                snapshot.Players[i].Position = ReturnPosition(i);
                snapshot.Players[i].Rotation = Quaternion.identity;
            }
            snapshot.SceneObjects.RemoveAll(o => IsChallengeScene(o.SceneName));
            snapshot.RuntimeObjects.RemoveAll(o => IsChallengeScene(o.SceneName) && !players.Contains(o.PersistentId));
            int slot = 0;
            foreach (var actor in snapshot.RuntimeObjects)
            {
                if (!players.Contains(actor.PersistentId)) continue;
                actor.SceneName = Catalog.returnSceneName;
                actor.Transform.Position = ReturnPosition(slot++);
                actor.Transform.Rotation = Quaternion.identity;
            }
            snapshot.SceneArchives.RemoveAll(a => IsChallengeScene(a.SceneName));
            snapshot.SceneName = Catalog.returnSceneName;
        }
    }
}
