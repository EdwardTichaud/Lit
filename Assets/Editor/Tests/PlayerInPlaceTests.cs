using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerInPlaceTests
{
    [Test]
    public void CompleteGameplayContract()
    {
        Assert.DoesNotThrow(PlayerInPlaceMigration.Validate);
    }

    [Test]
    public void CinematicAuthorityRequiresMatchingSessionAndReturnsToInPlace()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerInPlaceAudit.LucianPath);
        try
        {
            var bridge = root.GetComponent<LitOpsiveLocomotionBridge>();
            var actor = root.GetComponent<CombatActorAnimationRoot>();
            var motor = root.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>();
            motor.UseRootMotionPosition = true;
            motor.UseRootMotionRotation = true;
            actor.Animator.applyRootMotion = true;
            bridge.EnforceGameplayMotionAuthority();
            Assert.That(motor.UseRootMotionPosition || motor.UseRootMotionRotation || actor.Animator.applyRootMotion, Is.False);
            Assert.That(bridge.ApplyCinematicRootMotion(Vector3.one, Quaternion.identity), Is.False);
            actor.BeginCinematicMotion(7);
            Assert.That(actor.IsCinematicMotionActive && actor.Animator.applyRootMotion, Is.True);
            Assert.That(motor.UseRootMotionPosition || motor.UseRootMotionRotation, Is.False, "Cinematic relay must not also enable the UCC Animator consumer");
            actor.EndCinematicMotion(6);
            Assert.That(actor.IsCinematicMotionActive, Is.True);
            actor.EndCinematicMotion(7);
            Assert.That(actor.IsCinematicMotionActive || actor.Animator.applyRootMotion, Is.False);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void SamplingDoesNotDependOnPreviousClip()
    {
        var clips = PlayerInPlaceAudit.Collect(new List<string>()).Keys.ToArray();
        var target = clips.First(c => c.name.Contains("Landing_Hard"));
        var other = clips.First(c => c.name.Contains("BasicSkill_1"));
        using (var sampler = new PlayerInPlaceSampling())
        {
            var first = sampler.Sample(target);
            sampler.Sample(other);
            var second = sampler.Sample(target);
            Assert.That(second.Distance, Is.EqualTo(first.Distance).Within(.0001f));
            Assert.That(second.MaxDisplacement, Is.EqualTo(first.MaxDisplacement).Within(.0001f));
            Assert.That(second.MaxYaw, Is.EqualTo(first.MaxYaw).Within(.001f));
        }
    }

    [Test]
    public void MigratedTrajectoriesMatchSourceMeasurements()
    {
        var manifest = JsonUtility.FromJson<PlayerInPlaceMigration.Manifest>(File.ReadAllText(PlayerInPlaceMigration.ManifestPath));
        var library = AssetDatabase.LoadAssetAtPath<PlayerStateMotionLibrary>(PlayerInPlaceMigration.LibraryPath);
        using (var sampler = new PlayerInPlaceSampling())
        foreach (var profile in library.profiles)
        {
            var record = manifest.replacements.Single(r => r.consumers.Contains(profile.statePath));
            var source = AssetDatabase.LoadAllAssetsAtPath(record.sourcePath).OfType<AnimationClip>().First(c => {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string _, out long id); return id == record.sourceId;
            });
            var samples = sampler.Sample(source);
            float tolerance = Mathf.Max(.05f, samples.Distance * 1.04f * .05f);
            for (int i = 0; i < samples.positions.Length; i++)
            {
                var expected = Vector3.ProjectOnPlane(samples.positions[i], Vector3.up) * 1.04f;
                var actual = profile.Position((float)i / (samples.positions.Length - 1));
                Assert.That(Vector3.Distance(expected, actual), Is.LessThanOrEqualTo(tolerance), profile.statePath + " at sample " + i);
            }
        }
    }

    [TestCase(30)]
    [TestCase(50)]
    [TestCase(120)]
    public void CurveIntegrationDoesNotDependOnTickRate(int hz)
    {
        var library = AssetDatabase.LoadAssetAtPath<PlayerStateMotionLibrary>(PlayerInPlaceMigration.LibraryPath);
        foreach (var profile in library.profiles)
        {
            float t = 0;
            var total = Vector3.zero;
            while (t < 1)
            {
                float next = Mathf.Min(1, t + 1f / hz / profile.duration);
                total += profile.Position(next) - profile.Position(t);
                t = next;
            }
            Assert.That(Vector3.Distance(total, profile.Position(1) - profile.Position(0)), Is.LessThan(.0001f));
        }
    }

    [Test]
    public void ExistingDodgeAndJumpDoNotReceiveStateTrajectories()
    {
        var library = AssetDatabase.LoadAssetAtPath<PlayerStateMotionLibrary>(PlayerInPlaceMigration.LibraryPath);
        Assert.That(library.profiles.Any(p => p.statePath.Contains("Dodge") || p.statePath.Contains("Jump_")), Is.False);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerInPlaceAudit.LucianPath);
        Assert.That(prefab.GetComponent<PlayerScriptedJumpController>().TargetJumpHeight, Is.EqualTo(100f));
    }

    [Test]
    public void RepeatedMigrationWritesNoAssets()
    {
        var paths = AssetDatabase.GetAllAssetPaths().Where(p => File.Exists(p) &&
            (p.StartsWith(PlayerInPlaceMigration.Folder + "/") || p == PlayerInPlaceAudit.ControllerPath ||
             p.StartsWith("Assets/Characters/1_Squad/") && p.EndsWith(".prefab") ||
             p.StartsWith("Assets/CombatRealTime/Skills/") && p.EndsWith(".asset"))).ToArray();
        var hashes = paths.ToDictionary(p => p, PlayerInPlaceMigration.Hash);
        PlayerInPlaceMigration.Migrate();
        foreach (var path in paths) Assert.That(PlayerInPlaceMigration.Hash(path), Is.EqualTo(hashes[path]), path);
    }

    [Test]
    public void CombatReinstallationPreservesMigratedController()
    {
        string before = PlayerInPlaceMigration.Hash(PlayerInPlaceAudit.ControllerPath);
        RealTimeCombatAnimatorInstaller.Install();
        Assert.That(PlayerInPlaceMigration.Hash(PlayerInPlaceAudit.ControllerPath), Is.EqualTo(before));
        PlayerInPlaceMigration.Validate();
    }

    [Test]
    public void DedicatedCopiesPreserveMuscleAndCustomCurves()
    {
        var manifest = JsonUtility.FromJson<PlayerInPlaceMigration.Manifest>(File.ReadAllText(PlayerInPlaceMigration.ManifestPath));
        foreach (var record in manifest.replacements.Where(r => r.targetPath.StartsWith(PlayerInPlaceMigration.Folder + "/")))
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(record.sourcePath).OfType<AnimationClip>().Single(c => {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string _, out long id); return id == record.sourceId;
            });
            var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(record.targetPath);
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.path == "" && (binding.propertyName.StartsWith("Root") || binding.propertyName.StartsWith("Motion") || binding.type == typeof(Transform))) continue;
                var original = AnimationUtility.GetEditorCurve(source, binding);
                var copy = AnimationUtility.GetEditorCurve(target, binding);
                Assert.That(copy, Is.Not.Null, record.targetPath + ":" + binding.propertyName);
                Assert.That(copy.keys, Is.EqualTo(original.keys), record.targetPath + ":" + binding.propertyName);
                Assert.That(copy.preWrapMode, Is.EqualTo(original.preWrapMode));
                Assert.That(copy.postWrapMode, Is.EqualTo(original.postWrapMode));
            }
        }
    }
}
