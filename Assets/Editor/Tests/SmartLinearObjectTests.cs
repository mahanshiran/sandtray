using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Objects;

namespace Sandplay.Tests
{
    public class SmartLinearObjectTests
    {
        GameObject placerRoot,managerRoot,networkRoot;
        GameConfig config;
        ObjectPlacer placer;
        NetworkBootstrapper network;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [SetUp] public void SetUp()
        {
            EventBus.Clear(); NetworkCatalogRegistry.Reset();
            managerRoot=new GameObject("Isolated smart test manager");var manager=managerRoot.AddComponent<GameManager>();
            typeof(GameManager).GetProperty("Instance").SetValue(null,manager);
            config=ScriptableObject.CreateInstance<GameConfig>();config.SandboxWidth=20;config.SandboxDepth=20;manager.Initialize(config);
            placerRoot=new GameObject("Isolated smart placer");placer=placerRoot.AddComponent<ObjectPlacer>();
            networkRoot=new GameObject("Isolated smart network");networkRoot.SetActive(false);network=networkRoot.AddComponent<NetworkBootstrapper>();
            NetworkCatalogRegistry.Register(SmartLinearCatalog.WithDefaults(null));
        }
        [TearDown] public void TearDown()
        {
            foreach(var p in placer.PlacedObjects.ToArray())if(p!=null)UnityEngine.Object.DestroyImmediate(p.gameObject);
            UnityEngine.Object.DestroyImmediate(networkRoot);UnityEngine.Object.DestroyImmediate(placerRoot);
            UnityEngine.Object.DestroyImmediate(managerRoot);UnityEngine.Object.DestroyImmediate(config);
            NetworkCatalogRegistry.Reset(); EventBus.Clear();
        }
        PlacedObject Place(string id)
        {
            Assert.IsTrue(NetworkCatalogRegistry.TryGet(id,out var item));
            Assert.IsTrue(SmartLinearCatalog.EnsureTemplate(item));
            return placer.PlaceNetworkObject(item,Vector3.zero,Quaternion.identity,1,true,false);
        }
        object Call(string method,params object[] args)=>typeof(NetworkBootstrapper).GetMethod(method,Private).Invoke(network,args);

        [Test] public void CatalogContainsThreeLocalEntriesAndRespectsHostLibrary()
        {
            var entries=SmartLinearCatalog.WithDefaults(BundledCatalog.Items);
            Assert.AreEqual(352,entries.Length);
            foreach(var id in SmartLinearCatalog.DefinitionIds)Assert.IsTrue(entries.Any(i=>i.id==id && NetworkCatalogLoader.IsGlbCached(i)));
            NetworkCatalogRegistry.Register(Array.Empty<NetworkCatalogItem>());
            Assert.AreEqual(0,NetworkCatalogRegistry.Snapshot().Length);
            Assert.IsTrue(NetworkCatalogRegistry.TryGet("smart.fence.v1:0:3",out _), "Existing board dependencies remain restorable.");
        }
        [TestCase("smart.wall.v1")][TestCase("smart.road.v1")][TestCase("smart.fence.v1")]
        public void GeometryExtendsAtExactLocalEndpointsAndKeepsOnePlacement(string id)
        {
            var placed=Place(id);var smart=placed.GetComponent<SmartLinearObject>();
            placed.transform.rotation=Quaternion.Euler(0,37,0);placed.transform.localScale=Vector3.one*2;
            var anchor=placed.transform.TransformPoint(Vector3.zero);
            Assert.IsTrue(smart.Change(1,2));
            Assert.AreEqual(-1,smart.State.FirstSegment);Assert.AreEqual(4,smart.State.SegmentCount);
            Assert.Less(Vector3.Distance(anchor,placed.transform.TransformPoint(Vector3.zero)),.0001f);
            Assert.AreEqual(1,placer.PlacedObjects.Count);
            var bounds=ObjectPlacer.ObjectBounds(placed);Assert.Greater(bounds.size.x,2);
            Assert.AreEqual(1,placed.GetComponentsInChildren<PlacedObject>().Length);
            Assert.LessOrEqual(placed.GetComponentsInChildren<Renderer>().Count(r=>r.enabled),2,"Sections should be combined into at most two material surfaces.");
            var template=SmartLinearCatalog.Item(id);SmartLinearCatalog.EnsureTemplate(template);
            Assert.IsTrue(template.LoadedPrefab.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh!=null && f.sharedMesh.vertexCount>0),"Changing an instance cannot dispose its shared default template mesh.");
            Assert.IsFalse(placed.GetComponent<BoxCollider>().enabled);
            Assert.IsTrue(placed.GetComponentsInChildren<BoxCollider>().Any(c=>c.enabled));
        }
        [Test] public void FenceHasSharedPostsAndOpenGapsAfterRebuild()
        {
            var placed=Place("smart.fence.v1:0:3");var smart=placed.GetComponent<SmartLinearObject>();
            Assert.AreEqual(4,placed.GetComponentsInChildren<Transform>().Count(t=>t.name=="Post"));
            placed.SetSelected(true);smart.Change(0,1);
            Assert.IsTrue(placed.IsSelected);Assert.AreEqual(5,placed.GetComponentsInChildren<Transform>().Count(t=>t.name=="Post"));
            Physics.SyncTransforms();
            Assert.IsFalse(Physics.Raycast(new Vector3(.12f,.37f,-1),Vector3.forward,2), "Fence gaps must remain open after extension.");
        }
        [Test] public void SaveJsonAndWireSnapshotsPreserveLayoutAndTransform()
        {
            var placed=Place("smart.road.v1:-2:7");placed.transform.SetPositionAndRotation(new Vector3(.2f,.3f,.4f),Quaternion.Euler(0,25,0));
            placed.transform.localScale=Vector3.one*1.4f;
            string json=JsonUtility.ToJson(placed.Serialize());
            StringAssert.DoesNotContain("\"LinearState\"",json);
            StringAssert.DoesNotContain("\"InstanceId\"",json);
            var saved=JsonUtility.FromJson<PlacedObjectData>(json);
            Assert.AreEqual("smart.road.v1:-2:7",saved.ObjectId);Assert.AreEqual(7,saved.LinearState.SegmentCount);Assert.AreEqual(1.4f,saved.Scale,.001f);
            var payload=NetSerializer.WriteFullState(20,20,2,new byte[16],new[]{new SpawnObjectData{NetId=7,ObjectId=saved.ObjectId,Position=saved.Position,Rotation=Quaternion.Euler(saved.Rotation),Scale=saved.Scale}},Array.Empty<ColorSyncData>());
            NetSerializer.ReadFullState(payload,out _,out _,out _,out _,out var objects,out _);
            Assert.AreEqual(saved.ObjectId,objects[0].ObjectId);
            var restored=Place(objects[0].ObjectId);Assert.AreEqual(7,restored.GetComponent<SmartLinearObject>().State.SegmentCount);
        }
        [Test] public void UndoRedoAndDeleteUndoKeepTheWholeStructure()
        {
            var placed=Place("smart.wall.v1:0:4");var smart=placed.GetComponent<SmartLinearObject>();
            var before=smart.State.Copy();var after=before.Copy();after.FirstSegment=-1;after.SegmentCount=5;
            var edit=new ChangeLinearObjectCommand(smart,before,after);
            edit.Execute();Assert.AreEqual(5,smart.State.SegmentCount);
            edit.Undo();Assert.AreEqual(4,smart.State.SegmentCount);
            edit.Execute();Assert.AreEqual(5,smart.State.SegmentCount);
            var remove=new RemoveObjectCommand(placer,placed);remove.Execute();remove.Undo();
            var restored = placer.PlacedObjects.Last();
            Assert.AreEqual(5,restored.GetComponent<SmartLinearObject>().State.SegmentCount);
            edit.Undo(); Assert.AreEqual(4,restored.GetComponent<SmartLinearObject>().State.SegmentCount);
            edit.Execute(); Assert.AreEqual(5,restored.GetComponent<SmartLinearObject>().State.SegmentCount);
        }
        [Test] public void DuplicateAndPlacementRedoPreserveConfiguration()
        {
            var placed=Place("smart.fence.v1:-2:5");
            var copy=placer.DuplicateObject(placed,Vector3.forward,false);
            Assert.AreEqual(placed.Serialize().ObjectId,copy.Serialize().ObjectId);
            Assert.AreNotSame(placed.GetComponent<SmartLinearObject>().State,copy.GetComponent<SmartLinearObject>().State);
            var command=new PlaceNetworkObjectCommand(placer,copy.NetworkItem,Vector3.zero,Quaternion.identity,1,true);
            command.Execute();command.Undo();command.Execute();Assert.AreEqual("smart.fence.v1:-2:5",command.PlacedObject.Serialize().ObjectId);
        }
        [Test] public void CompleteUndoRedoChainResolvesRecreatedObjects()
        {
            var item=SmartLinearCatalog.Item("smart.wall.v1");SmartLinearCatalog.EnsureTemplate(item);
            var placement=new PlaceNetworkObjectCommand(placer,item,Vector3.zero,Quaternion.identity,1,true);placement.Execute();
            var placed=placement.PlacedObject;var before=placed.GetComponent<SmartLinearObject>().State.Copy();var after=before.Copy();after.SegmentCount=3;
            var extend=new ChangeLinearObjectCommand(placed.GetComponent<SmartLinearObject>(),before,after);extend.Execute();
            var move=new MoveObjectCommand(placed,Vector3.zero,Quaternion.identity,Vector3.forward,Quaternion.identity);move.Execute();
            var remove=new RemoveObjectCommand(placer,placed);remove.Execute();remove.Undo();
            move.Undo();extend.Undo();Assert.AreEqual(1,placer.PlacedObjects.Last().GetComponent<SmartLinearObject>().State.SegmentCount);
            Assert.AreEqual(Vector3.zero,placer.PlacedObjects.Last().transform.position);
            placement.Undo();Assert.AreEqual(0,placer.PlacedObjects.Count);
            placement.Execute();extend.Execute();move.Execute();
            Assert.AreEqual(3,placer.PlacedObjects.Last().GetComponent<SmartLinearObject>().State.SegmentCount);
            Assert.AreEqual(Vector3.forward,placer.PlacedObjects.Last().transform.position);
        }
        [Test] public void JoinAndReplayGenerateLocallyAndApplyStateWithoutDuplicates()
        {
            network.HandleClientMessage(NetMsgType.SpawnObject,NetSerializer.WriteSpawnObject(31,"smart.fence.v1:0:3",Vector3.zero,Quaternion.identity,1));
            Assert.AreEqual(1,placer.PlacedObjects.Count);Assert.AreEqual(3,placer.PlacedObjects[0].GetComponent<SmartLinearObject>().State.SegmentCount);
            var packet=NetSerializer.WriteSpawnObject(31,"smart.fence.v1:-1:6",Vector3.zero,Quaternion.Euler(0,20,0),1.5f);
            network.HandleClientMessage(NetMsgType.SpawnObject,packet);network.HandleClientMessage(NetMsgType.SpawnObject,packet);
            Assert.AreEqual(1,placer.PlacedObjects.Count);Assert.AreEqual(6,placer.PlacedObjects[0].GetComponent<SmartLinearObject>().State.SegmentCount);
            Assert.AreEqual(1.5f,placer.PlacedObjects[0].transform.localScale.x,.001f);
            var snapshot=NetSerializer.WriteUndoRedoSync(Array.Empty<byte>(),new[]{new SpawnObjectData{NetId=31,ObjectId="smart.fence.v1:0:3",Position=Vector3.zero,Rotation=Quaternion.identity,Scale=1}});
            network.HandleClientMessage(NetMsgType.UndoRedoSync,snapshot);
            Assert.AreEqual(3,placer.PlacedObjects[0].GetComponent<SmartLinearObject>().State.SegmentCount);
        }
        [Test] public void HostRejectsInvalidLayoutOrDefinitionSwitchAndAcceptsValidEdit()
        {
            var placed=Place("smart.wall.v1");placed.NetworkId=51;network.RegisterNetworkObject(51,placed);
            foreach(string id in new[]{"smart.wall.v1:0:129","smart.fence.v1:0:3","smart.wall.v1:4096:2"})
                Call("ApplyAndRebroadcastSpawn",NetSerializer.WriteSpawnObject(51,id,Vector3.zero,Quaternion.identity,1));
            Assert.AreEqual("smart.wall.v1",placed.Serialize().ObjectId);
            Call("ApplyAndRebroadcastSpawn",NetSerializer.WriteSpawnObject(51,"smart.wall.v1:-1:4",Vector3.zero,Quaternion.identity,1));
            Assert.AreEqual(4,placed.GetComponent<SmartLinearObject>().State.SegmentCount);
        }
        [Test] public void RemovalImmediatelyUnregistersBeforeDeferredDestruction()
        {
            var placed=Place("smart.wall.v1");placed.NetworkId=91;network.RegisterNetworkObject(91,placed);
            typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null,network);
            typeof(NetworkBootstrapper).GetField("_isOnline",Private).SetValue(network,true);
            var syncRoot=new GameObject("Removal sync");var sync=syncRoot.AddComponent<ObjectSyncManager>();
            try
            {
                typeof(ObjectSyncManager).GetMethod("OnObjectRemoved",Private).Invoke(sync,new object[]{new ObjectRemovedEvent{PlacedObject=placed}});
                Assert.IsNull(network.GetNetworkObject(91));
            }
            finally
            {
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null,null);
                UnityEngine.Object.DestroyImmediate(syncRoot);
            }
        }
        [TestCase("smart.fence.v1")][TestCase("smart.road.v1")]
        public void DroppingPreservesDisabledSelectionAndDecorationColliders(string id)
        {
            var placed=Place(id);placed.transform.position=Vector3.up*2;
            var disabled=placed.GetComponentsInChildren<Collider>().Where(c=>!c.enabled).ToArray();
            config.HeightmapResolution=8;
            var sandRoot=new GameObject("Collider preservation sand");var sand=sandRoot.AddComponent<Sandplay.Sand.SandMesh>();
            typeof(Sandplay.Sand.SandMesh).GetMethod("Awake",Private).Invoke(sand,null);sand.Initialize(config);
            typeof(ObjectPlacer).GetField("_sandMesh",Private).SetValue(placer,sand);
            try
            {
                var routine=(System.Collections.IEnumerator)typeof(ObjectPlacer).GetMethod("DropObjectRoutine",Private).Invoke(placer,new object[]{placed,null});
                routine.MoveNext();(routine as IDisposable)?.Dispose();
                Assert.Greater(disabled.Length,0);Assert.IsTrue(disabled.All(c=>!c.enabled));
                Assert.IsTrue(placed.GetComponentsInChildren<Collider>().Any(c=>c.enabled));
            }
            finally { UnityEngine.Object.DestroyImmediate(sandRoot); }
        }
        [Test] public void PendingClientPlacementWaitsForHostBeforeHistoryOrMembershipEdits()
        {
            var placed=Place("smart.fence.v1");
            typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null,network);
            typeof(NetworkBootstrapper).GetField("_isOnline",Private).SetValue(network,true);
            var undoRoot=new GameObject("Pending placement history");var undo=undoRoot.AddComponent<UndoManager>();
            typeof(UndoManager).GetMethod("Awake",Private).Invoke(undo,null);
            try
            {
                var state=placed.GetComponent<SmartLinearObject>().State;
                undo.Record(new ChangeLinearObjectCommand(placed.GetComponent<SmartLinearObject>(),state,state));
                Assert.IsTrue(SmartLinearObject.AwaitingConfirmation(placed));Assert.IsFalse(undo.CanUndo);
                Assert.IsNull(placer.DuplicateObject(placed));
                placer.SetSelection(new[]{placed},false);placer.DeleteSelection();Assert.AreEqual(1,placer.PlacedObjects.Count);
                placed.NetworkId=99;Assert.IsFalse(SmartLinearObject.AwaitingConfirmation(placed));Assert.IsTrue(undo.CanUndo);
            }
            finally
            {
                typeof(NetworkBootstrapper).GetProperty("Instance").SetValue(null,null);
                UnityEngine.Object.DestroyImmediate(undoRoot);
            }
        }
        [Test] public void BoundsLimitsPermissionsAndNotificationsAreEnforced()
        {
            var placed=Place("smart.wall.v1");var smart=placed.GetComponent<SmartLinearObject>();
            config.SandboxWidth=1.3f;config.SandboxDepth=1.3f;
            Assert.IsFalse(smart.Change(0,3));Assert.AreEqual(1,smart.State.SegmentCount);
            config.SandboxWidth=20;config.SandboxDepth=20;
            GameManager.Instance.NetworkRole=PlayerRole.Observer;Assert.IsFalse(smart.Change(0,1));
            GameManager.Instance.NetworkRole=PlayerRole.Patient;
            int events=0;Action<ObjectTransformedEvent> handler=e=>events++;EventBus.Subscribe(handler);
            Assert.IsTrue(smart.Change(0,1));Assert.AreEqual(1,events);Assert.IsFalse(ObjectSyncManager.SuppressNetworkSync);
        }
        [TestCase("smart.wall.v2")][TestCase("smart.wall.v1:0:0")][TestCase("smart.road.v1:-5000:2")]
        [TestCase("smart.fence.v1:0:2147483647")][TestCase("smart.wall.v1:0:3:garbage")]
        public void InvalidOrUnknownLayoutsCannotGenerateGeometry(string id)=>Assert.IsFalse(LinearObjectState.TryParse(id,out _));
    }
}
