#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using Lit.Story;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public sealed class StoryDialoguePlacementTests
{
    [TestCase(0,90,45)] [TestCase(0,-90,-45)] [TestCase(90,100,10)] [TestCase(170,-170,20)]
    public void HeadLimitIsRelativeToBodyEvenAcrossAngleWrap(float body,float world,float expected)
        =>Assert.That(CharacterDialoguePresentationController.ClampSequenceHeadYaw(body,Quaternion.Euler(0,world,0)*Vector3.forward),Is.EqualTo(expected).Within(.001f));

    [Test] public void GazeIdentitySurvivesLinesAndOldSessionsCannotReleaseNewLook()
    {
        var actor=new GameObject("Dialogue gaze fixture");var owner=new GameObject("Sequence owner");var speaker=new GameObject("Other speaker");var secondSpeaker=new GameObject("Next speaker");
        try
        {
            var controller=actor.AddComponent<CharacterDialoguePresentationController>();
            var so=new SerializedObject(controller);
            so.FindProperty("fallbackProfile").objectReferenceValue=AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(DialoguePresentationSetup.ProfilePath);
            so.ApplyModifiedPropertiesWithoutUndo();
            DialogueSequenceLookState State()=>(DialogueSequenceLookState)typeof(CharacterDialoguePresentationController).GetField("lookState",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
            int first=controller.BeginSequenceLook(owner);
            Assert.That(first,Is.GreaterThan(0));
            speaker.transform.position=Vector3.right*3;controller.SetSequenceSpeaker(first,speaker.transform);
            Assert.That(State().headYaw,Is.EqualTo(45).Within(.001f));int revision=State().revision;
            speaker.transform.position=Vector3.left*3;controller.SetSequenceSpeaker(first,speaker.transform);
            Assert.That(State().revision,Is.EqualTo(revision));
            controller.SetSequenceSpeaker(first,actor.transform);
            Assert.That(State().headYaw,Is.EqualTo(45).Within(.001f));
            int line=controller.Begin(owner,speaker.transform,true,null,DialogueRole.Listening);controller.End(line);
            Assert.That(State().active,Is.True);
            secondSpeaker.transform.position=Vector3.left*3;controller.SetSequenceSpeaker(first,secondSpeaker.transform);
            Assert.That(State().headYaw,Is.EqualTo(-45).Within(.001f));
            int next=controller.BeginSequenceLook(owner);controller.EndSequenceLook(first);
            Assert.That(State().active,Is.True);controller.EndSequenceLook(next);Assert.That(State().active,Is.False);
        }
        finally{Object.DestroyImmediate(actor);Object.DestroyImmediate(owner);Object.DestroyImmediate(speaker);Object.DestroyImmediate(secondSpeaker);}
    }

    [Test] public void SequenceLookReplicatesItsSnapshotAndRevision()
    {
        var value=new DialogueSequenceLookState {active=true,generation=7,revision=3,bodyYaw=120,headYaw=-45};
        using(var writer=new FastBufferWriter(128,Allocator.Temp))
        {
            writer.WriteNetworkSerializable(value);
            using(var reader=new FastBufferReader(writer,Allocator.Temp))
            {reader.ReadNetworkSerializable(out DialogueSequenceLookState received);Assert.That(received.Equals(value),Is.True);}
        }
    }

    [Test] public void PlacementFieldUsesOneTransformAndKeepsExistingScenesOptional()
    {
        var go=new GameObject("Sequence placement settings");go.SetActive(false);
        try
        {
            var runner=go.AddComponent<StorySequenceRunner>();var so=new SerializedObject(runner);
            Assert.That(so.FindProperty("playerTransform").objectReferenceValue,Is.Null);
            Assert.That(so.FindProperty("playerApproachTimeout").floatValue,Is.EqualTo(30));
            Assert.That(runner.IsPreparingPlayer,Is.False);
            Assert.That(typeof(StorySequenceRunner).GetField("playerRotation",BindingFlags.Instance|BindingFlags.NonPublic),Is.Null);
        }
        finally{Object.DestroyImmediate(go);}
    }

    [Test] public void PlacementUsesACompletePathAroundAnObstacleAndRejectsUnreachableGoals()
    {
        var settings=NavMesh.GetSettingsByIndex(0);Assert.That(settings.agentTypeID,Is.GreaterThanOrEqualTo(0));
        Vector3 origin=new Vector3(2000,0,2000);
        var sources=new List<NavMeshBuildSource> {
            new NavMeshBuildSource {shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(origin,new Quaternion(0,0,0,1),Vector3.one),size=new Vector3(12,.2f,12),area=0},
            new NavMeshBuildSource {shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(origin+Vector3.up,Quaternion.identity,Vector3.one),size=new Vector3(2,2,3),area=1}
        };
        var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(origin,new Vector3(14,6,14)),Vector3.zero,Quaternion.identity);
        Assert.That(data,Is.Not.Null);var instance=NavMesh.AddNavMeshData(data);
        try
        {
            var method=typeof(StorySequenceRunner).GetMethod("TryPlacementPath",BindingFlags.Static|BindingFlags.NonPublic);
            var filter=new NavMeshQueryFilter {agentTypeID=settings.agentTypeID,areaMask=NavMesh.AllAreas};var path=new NavMeshPath();
            bool Find(Vector3 end)=>(bool)method.Invoke(null,new object[]{origin+Vector3.left*4,end,filter,path});
            Assert.That(Find(origin+Vector3.right*4),Is.True);
            Assert.That(path.corners.Length,Is.GreaterThan(2),"Route must go around the obstacle.");
            Assert.That(Find(origin+Vector3.right*40),Is.False);
        }
        finally{instance.Remove();Object.DestroyImmediate(data);}
    }
}
#endif
