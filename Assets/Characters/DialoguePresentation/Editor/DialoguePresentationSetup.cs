using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DialoguePresentationSetup
{
    public const string Folder = "Assets/Animations/Autres/Dialogue/Lucian";
    public const string ProfilePath = Folder + "/Lucian_DialoguePresentation.asset";
    private const string Report = "Library/DialoguePresentation";
    private const string PrefabPath = "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab";
    private const string DataPath = "Assets/Characters/1_Squad/Lucian/Lucian.asset";

    [Serializable] private sealed class ClipAudit
    {
        public string id, source, result;
        public long sourceId;
        public float seconds, beforeDistance, beforeYaw, afterDistance, afterYaw, footSpan;
        public bool fullBody;
    }
    [Serializable] private sealed class Audit { public List<ClipAudit> clips = new List<ClipAudit>(); }
    private sealed class Candidate
    {
        public string id, sourceName;
        public DialogueRole role;
        public DialogueIntent intent;
        public bool seated;
        public AnimationClip source;
    }
    static DialoguePresentationSetup() => EditorApplication.update += RunRequested;
    private static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            AssetDatabase.IsAssetImportWorkerProcess() || !File.Exists(Report + "/install-v4.request")) return;
        File.Delete(Report + "/install-v4.request");
        File.WriteAllText(Report + "/install.error", "");
        try { Install(); File.WriteAllText(Report + "/install.result", "PASS"); }
        catch (Exception e) { File.WriteAllText(Report + "/install.error", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Lit/Animation/Install Lucian Dialogue Presentation")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "/progress.txt", "Resolving source clips and Lucian's avatar.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var sourceAnimator = prefab.GetComponent<CharacterAnimationController>().Animator;
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath);
        EnsureFolder(Folder);
        string motionSource = AnimationLibraryAssets.Resolve("Assets/Characters/1_Squad/Lucian/Lucian_CC5_Embed/Lucian_CC5_Character_Model_Motion.Fbx");
        const string motionCopy = Folder + "/Lucian_Dialogue_Motion_Humanoid.Fbx";
        if (!File.Exists(motionCopy))
        {
            if (!AssetDatabase.CopyAsset(motionSource, motionCopy)) throw new InvalidOperationException("Could not prepare Lucian dialogue source copy.");
            var importer = (ModelImporter)AssetImporter.GetAtPath(motionCopy);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAnimator.avatar;
            var importedNames = new HashSet<string> { "HD Talk_M", "_Talk_M", "Talk Serious", "Sit Talk", "Await", "Focus" };
            importer.clipAnimations = importer.defaultClipAnimations.Where(c => importedNames.Contains(c.name)).ToArray();
            if (importer.clipAnimations.Length != importedNames.Count)
                throw new InvalidOperationException("Lucian dialogue takes are ambiguous.");
            importer.SaveAndReimport();
        }
        var candidates = new[]
        {
            Make("Speak_Explain_A", "HD Talk_M", DialogueRole.Speaking, DialogueIntent.Explain),
            Make("Speak_Explain_B", "_Talk_M", DialogueRole.Speaking, DialogueIntent.Explain),
            Make("Speak_Firm", "Talk Serious", DialogueRole.Speaking, DialogueIntent.Firm),
            Make("Speak_Seated", "Sit Talk", DialogueRole.Speaking, DialogueIntent.Explain, true),
            Make("Listen_Attentive", "BreathingIdle", DialogueRole.Listening, DialogueIntent.Neutral),
            Make("Wait_Attentive", "BreathingIdle", DialogueRole.Waiting, DialogueIntent.Neutral),
            Make("Speak_Neutral", "A_IP_Standing_Idle_Talking_F", DialogueRole.Speaking, DialogueIntent.Neutral),
            Make("Speak_Question", "A_IP_Standing_Idle_Talking_L", DialogueRole.Speaking, DialogueIntent.Question),
            Make("Speak_Explain_C", "A_IP_Standing_Idle_Talking_R", DialogueRole.Speaking, DialogueIntent.Explain)
        };
        // Resolve every subasset before creating any presentation resource.
        foreach (var c in candidates)
        {
            if (c.sourceName == "BreathingIdle")
                c.source = AnimationLibraryAssets.LoadAssetAtPath<AnimationClip>("Assets/Characters/1_Squad/Lucian/Animation/Mixamo_Idle_Breathing.anim");
            else if (c.sourceName.StartsWith("A_IP_"))
            {
                string path = AssetDatabase.FindAssets(c.sourceName + " t:Model", new[] { "Assets/Animations" })
                    .Select(AssetDatabase.GUIDToAssetPath).Single(p => Path.GetFileNameWithoutExtension(p) == c.sourceName);
                c.source = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(a => !a.name.StartsWith("__preview__"));
            }
            else
            {
                string path = motionCopy;
                c.source = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(a => a.name == c.sourceName);
            }
            if (!c.source.isHumanMotion || AnimationUtility.GetAnimationEvents(c.source).Length != 0)
                throw new InvalidOperationException("Requires a reviewed humanoid clip without gameplay events: " + c.sourceName +
                    " humanoid=" + c.source.isHumanMotion + " events=" + AnimationUtility.GetAnimationEvents(c.source).Length);
        }

        EnsureFolder(Folder);
        var profile = AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<DialoguePresentationProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        string profileBefore = EditorJsonUtility.ToJson(profile);
        var audit = new Audit();
        using (var sampler = new PlayerInPlaceSampling(sourceAnimator))
        using (var poses = new ActorPoseSampler(sourceAnimator))
        foreach (var c in candidates)
        {
            File.WriteAllText(Report + "/progress.txt", "Sampling and preparing " + c.id);
            var prior = profile.gestures.Find(g => g.id == c.id);
            bool repair = prior != null && prior.source != c.source && c.sourceName == "BreathingIdle" &&
                prior.source != null && (prior.source.name == "Await" || prior.source.name == "Focus");
            var measured = sampler.Sample(c.source);
            string bodyPath = Folder + "/" + c.id + ".anim";
            var body = AssetDatabase.LoadAssetAtPath<AnimationClip>(bodyPath);
            if (body == null || repair)
            {
                body = Object.Instantiate(c.source);
                body.name = c.id;
                PlayerInPlaceSampling.Neutralize(body, measured);
                foreach (var binding in AnimationUtility.GetCurveBindings(body))
                    if (binding.type == typeof(Transform) || binding.type == typeof(SkinnedMeshRenderer) ||
                        IsFace(binding.propertyName))
                        AnimationUtility.SetEditorCurve(body, binding, null);
                body = StoreClip(body, bodyPath);
            }
            string upperPath = Folder + "/" + c.id + "_UpperBody.anim";
            var upper = AssetDatabase.LoadAssetAtPath<AnimationClip>(upperPath);
            if (upper == null || repair)
            {
                upper = Object.Instantiate(body);
                upper.name = c.id + "_UpperBody";
                foreach (var binding in AnimationUtility.GetCurveBindings(upper))
                    if (IsLowerBody(binding.propertyName))
                        AnimationUtility.SetEditorCurve(upper, binding, null);
                upper = StoreClip(upper, upperPath);
            }
            var after = sampler.Sample(body);
            var frames = poses.Sample(body);
            float span = Mathf.Max(FootSpan(frames.Select(f => f.leftFoot)), FootSpan(frames.Select(f => f.rightFoot)));
            bool fullBody = !c.seated && span <= .03f && after.MaxDisplacement <= .01f && after.MaxYaw <= .5f;
            var entry = profile.gestures.Find(g => g.id == c.id);
            if (entry == null || repair)
            {
                entry = new DialoguePresentationProfile.Gesture
                {
                    id = c.id, source = c.source, body = fullBody ? body : null, upperBody = upper,
                    role = c.role, intent = c.intent, seated = c.seated || c.role != DialogueRole.Speaking,
                    standing = !c.seated, validated = true,
                    validation = fullBody ? "Measured Inplace, foot span <= 3 cm. Visual review pending." :
                        "Upper-body use approved. Full-body requires pose/appui review. Visual review pending."
                };
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c.source, out string _, out entry.sourceId);
                if (repair) profile.gestures[profile.gestures.IndexOf(prior)] = entry;
                else profile.gestures.Add(entry);
            }
            if (c.id == "Speak_Explain_A" || c.id == "Speak_Explain_B")
            {
                if (entry.validated)
                {
                    entry.validated = false;
                    entry.validation = "Facial/head-only source. Excluded from body gesture selection.";
                }
            }
            else if (entry.startNormalizedTime == 0 && c.role == DialogueRole.Speaking)
                entry.startNormalizedTime = FindGestureStart(upper);

            audit.clips.Add(new ClipAudit
            {
                id = c.id, source = AssetDatabase.GetAssetPath(c.source), sourceId = entry.sourceId, seconds = c.source.length,
                beforeDistance = measured.MaxDisplacement, beforeYaw = measured.MaxYaw,
                afterDistance = after.MaxDisplacement, afterYaw = after.MaxYaw, footSpan = span,
                fullBody = entry.body != null, result = entry.validation
            });
        }
        PrepareExpressions(data, profile);
        if (EditorJsonUtility.ToJson(profile) != profileBefore) EditorUtility.SetDirty(profile);
        if (data.playerSettings.dialoguePresentation != profile)
        {
            data.playerSettings.dialoguePresentation = profile;
            EditorUtility.SetDirty(data);
        }
        var masks = new[] { PrepareMask(0), PrepareMask(1), PrepareMask(2) };
        foreach (string controllerPath in new[]
        {
            "Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.controller",
            "Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01/LucianCadence.controller"
        }) PrepareController(AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath), profile, masks);
        foreach (string path in new[]
        {
            PrefabPath, "Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.prefab",
            "Assets/Characters/AnimationPresentation/LucianCoherencePreview/Cadence01/LucianCadence.prefab"
        })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (Bind(root, profile)) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        File.WriteAllText(Report + "/progress.txt", "Binding Lucian's prefabs and menu scene.");
        BindMenu(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        AssetDatabase.SaveAssetIfDirty(data);
        File.WriteAllText(Report + "/clip-audit.json", JsonUtility.ToJson(audit, true));
        DialoguePresentationValidator.Validate();
        Debug.Log("LUCian_DIALOGUE_INSTALL_PASS");
    }

    private static float FindGestureStart(AnimationClip clip)
    {
        var curves = AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.propertyName.Contains("Arm") || b.propertyName.Contains("Shoulder"))
            .Select(b => AnimationUtility.GetEditorCurve(clip, b)).ToArray();
        float window = Mathf.Min(3f, clip.length);
        float best = 0, start = 0;
        for (float t = 0; t + window <= clip.length; t += .25f)
        {
            float score = 0;
            foreach (var curve in curves)
            for (float k = t; k < t + window; k += .1f)
                score += Mathf.Abs(curve.Evaluate(Mathf.Min(k + .1f, clip.length)) - curve.Evaluate(k));
            if (score > best) { best = score; start = t; }
        }
        // Retain room for the blend and the following recovery.
        return Mathf.Clamp(start, 0, Mathf.Max(0, clip.length - 5f)) / Mathf.Max(.001f, clip.length);
    }

    private static AnimationClip StoreClip(AnimationClip clip, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
        EditorUtility.CopySerialized(clip, existing);
        Object.DestroyImmediate(clip);
        AssetDatabase.SaveAssetIfDirty(existing);
        return existing;
    }

    private static Candidate Make(string id, string source, DialogueRole role, DialogueIntent intent, bool seated = false) =>
        new Candidate { id = id, sourceName = source, role = role, intent = intent, seated = seated };

    public static bool IsFace(string property) =>
        property.IndexOf("Jaw", StringComparison.OrdinalIgnoreCase) >= 0 ||
        property.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0 ||
        property.StartsWith("blendShape.", StringComparison.Ordinal);

    public static bool IsLowerBody(string property) =>
        property.StartsWith("Root", StringComparison.Ordinal) || property.StartsWith("Motion", StringComparison.Ordinal) ||
        property.StartsWith("Left Leg", StringComparison.Ordinal) || property.StartsWith("Right Leg", StringComparison.Ordinal) ||
        property.StartsWith("Left Upper Leg", StringComparison.Ordinal) || property.StartsWith("Right Upper Leg", StringComparison.Ordinal) ||
        property.StartsWith("Left Lower Leg", StringComparison.Ordinal) || property.StartsWith("Right Lower Leg", StringComparison.Ordinal) ||
        property.StartsWith("Left Foot", StringComparison.Ordinal) || property.StartsWith("Right Foot", StringComparison.Ordinal) ||
        property.StartsWith("Left Toes", StringComparison.Ordinal) || property.StartsWith("Right Toes", StringComparison.Ordinal) ||
        property.StartsWith("LeftFoot", StringComparison.Ordinal) || property.StartsWith("RightFoot", StringComparison.Ordinal);

    private static float FootSpan(IEnumerable<Vector3> feet)
    {
        var points = feet.Select(v => new Vector3(v.x, 0, v.z)).ToArray();
        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (var p in points) bounds.Encapsulate(p);
        return bounds.size.magnitude;
    }

    private static AvatarMask PrepareMask(int mode)
    {
        string path = Folder + "/" + CharacterDialoguePresentationController.Layers[mode].Replace(" ", "") + ".mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask != null) return mask;
        mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        if (mode == 0)
        {
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
        }
        if (mode != 2)
        {
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        }
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        AssetDatabase.CreateAsset(mask, path);
        return mask;
    }

    private static void PrepareController(AnimatorController controller, DialoguePresentationProfile profile, AvatarMask[] masks)
    {
        string before = EditorJsonUtility.ToJson(controller);
        for (int i = 0; i < CharacterDialoguePresentationController.Layers.Length; i++)
        {
            string name = CharacterDialoguePresentationController.Layers[i];
            var layers = controller.layers;
            int index = Array.FindIndex(layers, l => l.name == name);
            if (index < 0) { controller.AddLayer(name); layers = controller.layers; index = layers.Length - 1; }
            var layer = layers[index];
            layer.avatarMask = masks[i];
            layer.defaultWeight = 0;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.iKPass = false;
            foreach (var entry in profile.gestures)
            {
                var state = layer.stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == entry.id);
                if (state == null)
                {
                    state = layer.stateMachine.AddState(entry.id);
                    state.writeDefaultValues = false;
                    state.iKOnFeet = false;
                }
                var clip = i == 0 ? entry.body : entry.upperBody;
                if (state.motion != clip) { state.motion = clip; EditorUtility.SetDirty(state); }
            }
            layers[index] = layer;
            controller.layers = layers;
        }
        if (EditorJsonUtility.ToJson(controller) != before) EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
    }

    private static void PrepareExpressions(CharacterData data, DialoguePresentationProfile profile)
    {
        foreach (DialogueIntent intent in Enum.GetValues(typeof(DialogueIntent)))
        {
            if (intent == DialogueIntent.Automatic || profile.expressions.Any(e => e.intent == intent)) continue;
            FacialEmotion emotion = intent == DialogueIntent.Firm ? FacialEmotion.Anger :
                intent == DialogueIntent.Concerned ? FacialEmotion.Fear :
                intent == DialogueIntent.Question ? FacialEmotion.Suspicious :
                intent == DialogueIntent.Amused ? FacialEmotion.HalfSmile :
                intent == DialogueIntent.Surprised ? FacialEmotion.Surprise : FacialEmotion.Idle;
            var source = data.playerSettings.face.presets.FirstOrDefault(p => p != null && p.emotion == emotion);
            if (source == null) continue;
            string path = Folder + "/Expression_" + intent + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<FacialExpressionPreset>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<FacialExpressionPreset>();
                preset.emotion = emotion;
                foreach (var e in source.blendShapes.Where(e => e != null && FacialExpressionController.IsDialogueSafeShape(e.blendShapeName)))
                    preset.blendShapes.Add(new FacialBlendShapeWeight { blendShapeName = e.blendShapeName, weight = Mathf.Min(20, e.weight * .4f) });
                AssetDatabase.CreateAsset(preset, path);
            }
            profile.expressions.Add(new DialoguePresentationProfile.IntentExpression { intent = intent, preset = preset });
        }
    }

    private static bool Bind(GameObject root, DialoguePresentationProfile profile)
    {
        var actor = root.GetComponentInChildren<SquadCharacterController>(true);
        if (actor == null) return false;
        var presenter = actor.GetComponent<CharacterDialoguePresentationController>();
        bool added = presenter == null;
        if (added) presenter = actor.gameObject.AddComponent<CharacterDialoguePresentationController>();
        var so = new SerializedObject(presenter);
        var p = so.FindProperty("fallbackProfile");
        if (!added && p.objectReferenceValue == profile) return false;
        p.objectReferenceValue = profile;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static void BindMenu(DialoguePresentationProfile profile)
    {
        const string path = "Assets/Scenes/MainMenu/MainMenu.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Save MainMenu before installing its dialogue presentation.");
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            bool dirty = false;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var actor in root.GetComponentsInChildren<SquadCharacterController>(true))
                if (actor.CharacterData != null && actor.CharacterData.characterId == "Lucian" || actor.name == "Player_Model_Lucian")
                    dirty |= Bind(actor.gameObject, profile);
            if (dirty) EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        EnsureFolder(Path.GetDirectoryName(folder).Replace(Path.DirectorySeparatorChar, '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace(Path.DirectorySeparatorChar, '/'), Path.GetFileName(folder));
    }
}
