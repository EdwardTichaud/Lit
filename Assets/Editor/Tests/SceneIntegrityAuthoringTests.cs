#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class SceneIntegrityAuthoringTests
{
    [Test]
    public void AnimationLabKeepsCurrentSignalReceiversWithoutDeletedTrackBindings()
    {
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Workshop/AnimationLab.unity");
        try
        {
            var director = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayableDirector>(true))
                .Single(d => d.name == "_Authoring");
            Assert.IsNotNull(director.playableAsset);
            var signalOutputs = director.playableAsset.outputs.Where(o => o.outputTargetType == typeof(SignalReceiver)).ToArray();
            Assert.IsNotEmpty(signalOutputs);
            foreach (var output in signalOutputs)
                Assert.IsNotNull(director.GetGenericBinding(output.sourceObject), "The current signal track must retain its authored receiver.");
            var bindings = new SerializedObject(director).FindProperty("m_SceneBindings");
            for (int index = 0; index < bindings.arraySize; index++)
            {
                var key = bindings.GetArrayElementAtIndex(index).FindPropertyRelative("key");
                Assert.IsTrue(key.objectReferenceValue != null || key.objectReferenceEntityIdValue == default,
                    "A deleted track must not survive as an orphan binding.");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
#endif
