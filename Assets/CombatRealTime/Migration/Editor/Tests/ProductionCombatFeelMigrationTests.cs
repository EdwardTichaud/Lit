#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ProductionCombatFeelMigrationTests
{
    [Test]
    public void BootstrapRegistersDistrictOneForNormalTravelAndSaveReload()
    {
        const string path = "Assets/Scenes/Bootstrap.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
            GameFlowService flow = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<GameFlowService>(true))
                { Assert.That(flow, Is.Null, "Only one flow owns Bootstrap."); flow = candidate; }
            Assert.That(flow, Is.Not.Null);
            var zones = new SerializedObject(flow).FindProperty("zoneManifests");
            int matches = 0;
            for (int i = 0; i < zones.arraySize; i++)
                if (zones.GetArrayElementAtIndex(i).objectReferenceValue == manifest) matches++;
            Assert.That(matches, Is.EqualTo(1), "An Editor Start Manifest is not a runtime zone registration.");
            var resolved = typeof(GameFlowService).GetMethod("ResolveGameplayManifest",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(flow, new object[] { manifest.PrimarySceneName });
            Assert.That(resolved, Is.SameAs(manifest), "Normal starts and save reloads must resolve the complete zone.");
        }
        finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void ChargedSkillKeepsCanonicalIdentityAndValidatedPreparation()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Lucian/Lucian.asset");
        var canonical = AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SourceSkillPath);
        var validated = AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SkillPath);
        Assert.That(data.chargedSkill, Is.SameAs(canonical));
        Assert.That(canonical.preparationMode, Is.EqualTo(ChargedPreparationMode.BasicTail));
        Assert.That(canonical.launchTrajectory, Is.EqualTo(ChargedLaunchTrajectory.Impulse));
        Assert.That(canonical.chargeSeconds, Is.EqualTo(validated.chargeSeconds));
        Assert.That(canonical.chargeTailPlaybackRate, Is.EqualTo(validated.chargeTailPlaybackRate));
        Assert.That(canonical.ascentSeconds, Is.EqualTo(validated.ascentSeconds));
        Assert.That(canonical.airborneHoldSeconds, Is.EqualTo(validated.airborneHoldSeconds));
        Assert.That(canonical.AnimationClip, Is.SameAs(validated.AnimationClip));
        Assert.That(canonical.playerFloatClip, Is.SameAs(validated.playerFloatClip));
    }

    [Test]
    public void ProductionCameraUsesActionFramingWithoutReplacingWideExploration()
    {
        var session = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCombatMigration.SessionPath);
        var mode = session.GetComponentInChildren<LitGameplayCameraModeController>(true);
        var profile = new SerializedObject(mode).FindProperty("profile").objectReferenceValue as LitTacticalCameraProfile;
        var wide = AssetDatabase.LoadAssetAtPath<LitTacticalCameraProfile>("Assets/CombatRealTime/Camera/TacticalDefault.asset");
        var validated = AssetDatabase.LoadAssetAtPath<LitTacticalCameraProfile>(CombatFeelLabSetup.CameraPath);
        Assert.That(AssetDatabase.GetAssetPath(profile), Is.EqualTo(ProductionCombatMigration.CameraProfilePath));
        Assert.That(profile, Is.Not.SameAs(wide));
        Assert.That(profile.initialDistance, Is.EqualTo(wide.initialDistance));
        Assert.That(profile.initialPitch, Is.EqualTo(wide.initialPitch));
        Assert.That(profile.fieldOfView, Is.EqualTo(wide.fieldOfView));
        Assert.That(wide.actionFraming.enabled, Is.False);
        Assert.That(JsonUtility.ToJson(profile.actionFraming), Is.EqualTo(JsonUtility.ToJson(validated.actionFraming)));
    }

    [Test]
    public void ProductionJuggernautHasSingleContactPoseWithoutReplacingItsIdentity()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset");
        var prefab = data.ResolveWorldPrefab();
        Assert.That(prefab.GetComponents<CombatContactPose>(), Has.Length.EqualTo(1));
        Assert.That(prefab.GetComponent<LitBrainsEnemy>().characterData, Is.SameAs(data));
        Assert.That(prefab, Is.Not.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(ChargedSkillSetup.EnemyPath)));
    }

    [TestCase("Lucian")]
    [TestCase("Aurell")]
    [TestCase("Vesta")]
    [TestCase("Soren")]
    public void PlayerHasSingleAuthoredContactAudioWithValidatedSettings(string character)
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/" + character + "/" + character + ".asset");
        var audio = data.ResolveWorldPrefab().GetComponents<CombatContactAudio>();
        var validated = AssetDatabase.LoadAssetAtPath<GameObject>(GroundImpactSkillSetup.PrefabPath).GetComponent<CombatContactAudio>();
        Assert.That(audio, Has.Length.EqualTo(1));
        Assert.That(audio[0].lightPitch, Is.EqualTo(validated.lightPitch));
        Assert.That(audio[0].heavyPitch, Is.EqualTo(validated.heavyPitch));
        Assert.That(audio[0].lightDamage, Is.EqualTo(validated.lightDamage));
        Assert.That(audio[0].heavyDamage, Is.EqualTo(validated.heavyDamage));
    }
}
#endif
