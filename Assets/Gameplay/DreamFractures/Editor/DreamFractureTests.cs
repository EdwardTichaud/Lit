using System;
using System.IO;
using System.Linq;
using Lit.DreamFractures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DreamFractureTests
{
    private DreamFractureDefinitionSO definition;
    private string directory;
    [SetUp] public void Setup()
    {
        definition=ScriptableObject.CreateInstance<DreamFractureDefinitionSO>();
        definition.id="test";
        directory=Path.Combine(Path.GetTempPath(),"LitDreamFractureTests",Guid.NewGuid().ToString("N"));
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(definition);
        // Only this test's unique directory can be removed.
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    [Test] public void ScoreEventsCannotBeCountedTwiceOrOverflow()
    {
        var attempt=new DreamFractureAttempt();
        Assert.IsTrue(attempt.AddScore("one",int.MaxValue));
        Assert.IsFalse(attempt.AddScore("one",100));
        Assert.IsFalse(attempt.AddScore("two",1));
        Assert.IsFalse(attempt.AddScore("bad",-1));
        Assert.AreEqual(int.MaxValue,attempt.Score);
    }
    [TestCase(300,60,true,true)]
    [TestCase(100,40,false,true)]
    [TestCase(300,61,true,false)]
    [TestCase(100,61,false,false)]
    public void ObjectivesAreIndependentAndInclusive(int score,double time,bool scoreReached,bool timeReached)
    {
        var attempt=new DreamFractureAttempt();
        attempt.AddScore("objective",score);
        attempt.Tick(time);
        var result=attempt.Finish("run",definition,2,true,DreamFractureOutcome.Success);
        Assert.AreEqual(scoreReached,result.scoreReached);
        Assert.AreEqual(timeReached,result.timeReached);
        Assert.AreEqual(2,result.initialPlayers);
        Assert.IsTrue(result.hadDisconnect);
        Assert.AreSame(result,attempt.Finish("other",definition,1,false,DreamFractureOutcome.Failed));
        Assert.IsFalse(attempt.AddScore("late",100));
        attempt.Tick(100);
        Assert.AreEqual(time,attempt.Seconds);
    }
    [Test] public void InvalidTimeCannotPoisonResult()
    {
        var attempt=new DreamFractureAttempt();
        attempt.Tick(double.NaN);attempt.Tick(double.PositiveInfinity);attempt.Tick(-1);attempt.Tick(0);
        Assert.AreEqual(0,attempt.Seconds);
    }
    [Test] public void RecordsSurviveReloadAndKeepSeparateBestScoreAndTime()
    {
        string path=Path.Combine(directory,"records.json");
        var records=new DreamFractureRecords(path);
        records.Record(Result("one",300,50,1));
        records.Record(Result("two",100,30,1));
        records.Record(Result("three",900,1,1,DreamFractureOutcome.Failed));
        records.Record(Result("duo",500,20,2));
        var reloaded=new DreamFractureRecords(path);
        var solo=reloaded.Find("test",1,1);
        Assert.AreEqual(300,solo.bestScore);
        Assert.AreEqual(30,solo.bestSeconds);
        Assert.AreEqual("three",solo.lastResult.runId);
        Assert.AreEqual(500,reloaded.Find("test",1,2).bestScore);
        Assert.IsNull(reloaded.Find("test",2,1));
    }
    [Test] public void FailedAttemptDoesNotCreateAPerformanceRecordAndDeliveryIsIdempotent()
    {
        var records=new DreamFractureRecords(Path.Combine(directory,"records.json"));
        var result=Result("failed",300,2,1,DreamFractureOutcome.Abandoned);
        Assert.IsTrue(records.Record(result));
        Assert.IsTrue(records.Record(result));
        Assert.IsFalse(records.Find("test",1,1).hasSuccess);
    }
    [Test] public void DiskProjectionReturnsToMaisonAndPreservesCampaignArchives()
    {
        var catalog=Resources.Load<DreamFractureCatalog>("DreamFractures/Catalog");
        Assert.IsNotNull(catalog);
        var scene=catalog.fractures[0].zone.PrimarySceneName;
        var world=new WorldSnapshot { SceneName=scene };
        world.Players.Add(new PlayerSnapshot{ControlledObjectId="player",Position=Vector3.one*100});
        world.RuntimeObjects.Add(new PersistentObjectSnapshot{PersistentId="player",SceneName=scene});
        world.RuntimeObjects.Add(new PersistentObjectSnapshot{PersistentId="temporary-drop",SceneName=scene});
        world.SceneArchives.Add(new PersistentSceneArchive{SceneName=scene});
        world.SceneArchives.Add(new PersistentSceneArchive{SceneName="District_1"});
        DreamFractureSavePolicy.NormalizeWorldSave(world);
        Assert.AreEqual(catalog.returnSceneName,world.SceneName);
        Assert.AreEqual(catalog.returnPositions[0],world.Players[0].Position);
        Assert.AreEqual(1,world.RuntimeObjects.Count);
        Assert.AreEqual(catalog.returnSceneName,world.RuntimeObjects[0].SceneName);
        Assert.AreEqual("District_1",world.SceneArchives.Single().SceneName);
        DreamFractureSavePolicy.NormalizeWorldSave(world);
        Assert.AreEqual(1,world.RuntimeObjects.Count);
    }
    [Test] public void CatalogContainsUniqueValidDefinitionsAndBuiltScenes()
    {
        var catalog=Resources.Load<DreamFractureCatalog>("DreamFractures/Catalog");
        Assert.IsNotNull(catalog);
        Assert.AreEqual(4,catalog.returnPositions.Length);
        Assert.AreEqual(catalog.fractures.Length,catalog.fractures.Select(f=>f.id).Distinct().Count());
        foreach(var item in catalog.fractures)
        {
            Assert.IsTrue(item.IsValid,item.name);
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s=>s.enabled &&
                Path.GetFileNameWithoutExtension(s.path)==item.zone.PrimarySceneName));
        }
        var house=AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset");
        Assert.Contains("Maison_DreamFractures",house.LoadingSceneNames.ToArray());
    }
    private DreamFractureResult Result(string id,int score,double seconds,int players,
        DreamFractureOutcome outcome=DreamFractureOutcome.Success) => new DreamFractureResult
    {
        runId=id,fractureId="test",rulesVersion=1,score=score,seconds=seconds,
        initialPlayers=players,outcome=outcome
    };
}
