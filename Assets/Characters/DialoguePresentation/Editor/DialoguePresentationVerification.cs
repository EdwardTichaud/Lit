using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public sealed class DialoguePresentationVerification : ICallbacks
{
    private const string Report = "Library/DialoguePresentation";
    private static TestRunnerApi runner;
    static DialoguePresentationVerification() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Report + "/verify-v3.request") || File.Exists(Report + "/install-v4.request") ||
            EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            AssetDatabase.IsAssetImportWorkerProcess()) return;
        // The Test Runner may ask to save scenes. Never open that modal from an
        // automatic request while the user has unsaved work in the Editor.
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) return;
        File.Delete(Report + "/verify-v3.request");
        try
        {
            Capture();
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new DialoguePresentationVerification());
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                groupNames = new[] { "^DialoguePresentationTests", "^DialoguePresentationNativeRigTests", "^StorySequenceAuthoringTests", "^CycleAutoDialogueTests" }
            }) { runSynchronously = true });
        }
        catch (Exception e) { File.WriteAllText(Report + "/verification.error", e.ToString()); Debug.LogException(e); }
    }
    public void RunStarted(ITestAdaptor tests) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, Report + "/Tests.xml");
        File.WriteAllText(Report + "/tests.result", result.TestStatus + " passed=" + result.PassCount + " failed=" + result.FailCount);
        runner.UnregisterCallbacks(this);
        Object.DestroyImmediate(runner);
    }

    private static GameObject BakeForSnapshot(GameObject actor, Scene scene)
    {
        var root = new GameObject("Snapshot skin");
        SceneManager.MoveGameObjectToScene(root, scene);
        foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.enabled || skin.sharedMesh == null) continue;
            var original = skin.sharedMesh;
            var vertices = original.vertices;
            var normals = original.normals;
            var tangents = original.tangents;
            var bindposes = original.bindposes;
            for (int shape = 0; shape < original.blendShapeCount; shape++)
            {
                float value = skin.GetBlendShapeWeight(shape);
                if (Mathf.Abs(value) < .001f) continue;
                int frame = original.GetBlendShapeFrameCount(shape) - 1;
                var delta = new Vector3[vertices.Length];
                original.GetBlendShapeFrameVertices(shape, frame, delta, null, null);
                float weight = value / Mathf.Max(.001f, original.GetBlendShapeFrameWeight(shape, frame));
                for (int v = 0; v < vertices.Length; v++) vertices[v] += delta[v] * weight;
            }
            var matrices = skin.bones.Select((bone, i) => bone != null ? bone.localToWorldMatrix * bindposes[i] : Matrix4x4.identity).ToArray();
            using (var counts = original.GetBonesPerVertex())
            using (var weights = original.GetAllBoneWeights())
            {
                int index = 0;
                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 point = Vector3.zero, normal = Vector3.zero, tangent = Vector3.zero;
                    for (int j = 0; j < counts[v]; j++, index++)
                    {
                        var weight = weights[index];
                        point += matrices[weight.boneIndex].MultiplyPoint3x4(vertices[v]) * weight.weight;
                        if (normals.Length == vertices.Length)
                            normal += matrices[weight.boneIndex].MultiplyVector(normals[v]) * weight.weight;
                        if (tangents.Length == vertices.Length)
                            tangent += matrices[weight.boneIndex].MultiplyVector((Vector3)tangents[v]) * weight.weight;
                    }
                    vertices[v] = point;
                    if (normals.Length == vertices.Length) normals[v] = normal.normalized;
                    if (tangents.Length == vertices.Length)
                    {
                        tangent.Normalize();
                        tangents[v] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[v].w);
                    }
                }
            }
            var mesh = Object.Instantiate(original);
            mesh.ClearBlendShapes();
            mesh.vertices = vertices;
            if (normals.Length == vertices.Length) mesh.normals = normals;
            if (tangents.Length == vertices.Length) mesh.tangents = tangents;
            mesh.RecalculateBounds();
            var display = new GameObject(skin.name);
            display.transform.SetParent(root.transform, false);
            display.AddComponent<MeshFilter>().sharedMesh = mesh;
            display.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            skin.enabled = false;
        }
        return root;
    }

    [MenuItem("Lit/Animation/Capture Lucian Dialogue Poses")]
    public static void Capture()
    {
        Directory.CreateDirectory(Report + "/captures");
        const string labPath = "Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test.unity";
        var lab = SceneManager.GetSceneByPath(labPath);
        bool opened = !lab.IsValid() || !lab.isLoaded;
        Scene active = SceneManager.GetActiveScene();
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            if (opened) lab = EditorSceneManager.OpenScene(labPath, OpenSceneMode.Additive);
            var root = new GameObject("Dialogue preview");
            SceneManager.MoveGameObjectToScene(root, preview);
            foreach (var source in lab.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)))
                Object.Instantiate(source.gameObject, root.transform);
            foreach (var source in lab.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Volume>(true)))
            {
                var copy = Object.Instantiate(source.gameObject, root.transform);
                copy.layer = 31;
            }
            var floor = lab.GetRootGameObjects().FirstOrDefault(r => r.name == "TestFloor");
            if (floor != null) Object.Instantiate(floor, root.transform);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab");
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var component in actor.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = false;
            foreach (var cloth in actor.GetComponentsInChildren<Cloth>(true)) cloth.enabled = false;
            var animator = actor.GetComponent<CharacterAnimationController>().Animator;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            var camera = new GameObject("Dialogue camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, preview);
            camera.scene = preview;
            camera.fieldOfView = 36;
            camera.nearClipPlane = .05f;
            camera.transform.position = actor.transform.position + actor.transform.forward * 3.8f + actor.transform.right * 2.3f + Vector3.up * 1.5f;
            camera.transform.LookAt(actor.transform.position + Vector3.up * .92f);
            ActorAnimationLabValidation.ConfigureInspectionCamera(camera);
            camera.GetComponent<HDAdditionalCameraData>().volumeLayerMask = 1 << 31;
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            var profile = AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(DialoguePresentationSetup.ProfilePath);
            var shots = new[]
            {
                (name:"Idle", gesture:"", mode:0, seated:false),
                (name:"Speaking", gesture:"Speak_Explain_C", mode:1, seated:false),
                (name:"Speaking_Question", gesture:"Speak_Question", mode:1, seated:false),
                (name:"Seated", gesture:"Speak_Seated", mode:1, seated:true),
                (name:"Torch", gesture:"Speak_Explain_C", mode:2, seated:false),
                (name:"Waiting", gesture:"Wait_Attentive", mode:0, seated:false)
            };
            foreach (var shot in shots)
            {
                animator.Rebind();
                animator.SetBool("IsSitting", shot.seated);
                animator.SetBool("Flame", shot.mode == 2);
                animator.SetFloat("LitSpeed", 0);
                animator.SetBool("Moving", false);
                animator.Play(shot.seated ? "Base Layer.Sitting_Idle" : "Base Layer.Locomotion", 0, .2f);
                animator.Update(.05f);
                foreach (string name in CharacterDialoguePresentationController.Layers)
                    animator.SetLayerWeight(animator.GetLayerIndex(name), 0);
                int flameLayer = animator.GetLayerIndex("Upper Body Flame");
                if (flameLayer >= 0)
                {
                    animator.SetLayerWeight(flameLayer, shot.mode == 2 ? 1 : 0);
                    if (shot.mode == 2) animator.Play("Upper Body Flame.Flame_Locomotion", flameLayer, .25f);
                }
                if (!string.IsNullOrEmpty(shot.gesture))
                {
                    var entry = profile.gestures.Single(g => g.id == shot.gesture);
                    int mode = shot.mode == 0 && entry.body == null ? 1 : shot.mode;
                    string layerName = CharacterDialoguePresentationController.Layers[mode];
                    int layer = animator.GetLayerIndex(layerName);
                    animator.SetLayerWeight(layer, 1);
                    animator.Play(layerName + "." + entry.id, layer, Mathf.Clamp01(entry.startNormalizedTime + .03f));
                }
                animator.Update(0);
                var face = actor.GetComponentInChildren<FacialExpressionController>(true);
                typeof(FacialExpressionController).GetMethod("ApplyNeutralJawPose", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(face, null);
                var baked = BakeForSnapshot(actor, preview);
                try { ActorAnimationLabValidation.RenderSnapshot(camera, Report + "/captures/" + shot.name + ".png"); }
                finally
                {
                    foreach (var mesh in baked.GetComponentsInChildren<MeshFilter>()) Object.DestroyImmediate(mesh.sharedMesh);
                    Object.DestroyImmediate(baked);
                    foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.enabled = true;
                }
            }
            File.WriteAllText(Report + "/captures.result",
                "Actual Lucian prefab, avatar and materials; Iluvilirae_Test lighting. Cloth simulation disabled only for static captures.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (opened && lab.IsValid() && lab.isLoaded) EditorSceneManager.CloseScene(lab, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }
}
