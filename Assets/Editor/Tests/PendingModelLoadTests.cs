using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Objects;
using System.Collections.Generic;

namespace Sandplay.Tests
{
    public class PendingModelLoadTests
    {
        [Test]
        public void ModelDownloadTimeoutAllowsSlowMobileTransfers()
        {
            var field = typeof(NetworkCatalogLoader).GetField("ModelDownloadTimeoutSeconds",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field);
            Assert.GreaterOrEqual((int)field.GetRawConstantValue(), 60);
        }

        [Test]
        public void ReconnectModelQueueReusesFinishedModelAndDropsCancelledWork()
        {
            var owner = typeof(NetworkCatalogLoader).GetField("_modelLoadOwner", BindingFlags.NonPublic | BindingFlags.Static);
            var previous = owner.GetValue(null);
            var go = new GameObject("Import queue test");
            var host = go.AddComponent<NetworkBootstrapper>();
            var prefab = new GameObject("Completed import");
            try
            {
                owner.SetValue(null, host);
                var item = new NetworkCatalogItem { id = "queued" };
                bool current = true, completed = false;
                var cancelled = NetworkCatalogLoader.PreloadGlb(host, item, shouldContinue: () => current);
                Assert.IsTrue(cancelled.MoveNext());
                current = false;
                Assert.IsFalse(cancelled.MoveNext(), "Superseded snapshot must not import queued models.");
                var waiting = NetworkCatalogLoader.PreloadGlb(host, item, () => completed = true);
                Assert.IsTrue(waiting.MoveNext());
                item.LoadedPrefab = prefab;
                Assert.IsFalse(waiting.MoveNext(), "Reuse the first import instead of decoding again.");
                Assert.IsTrue(completed);
                Assert.AreSame(host, owner.GetValue(null), "A waiting caller cannot release another import's slot.");
            }
            finally
            {
                owner.SetValue(null, previous);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RepeatedHandoffManifestsRetainAssetsAndInFlightTarget()
        {
            var previous = NetworkCatalogRegistry.Snapshot();
            bool ready = NetworkCatalogRegistry.IsLoaded;
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            var prefab = new GameObject("Cached model");
            try
            {
                var original = new NetworkCatalogItem { id = "shared", model_url = "model",
                    model_hash = "v1", thumbnail_url = "thumbnail" };
                NetworkCatalogRegistry.Register(new[] { original });
                for (int i = 0; i < 20; i++)
                {
                    // Use the actual wire format (null metadata becomes empty strings).
                    var incoming = NetSerializer.ReadCatalogManifest(
                        NetSerializer.WriteCatalogManifest(new[] { original }));
                    NetworkCatalogRegistry.Merge(incoming);
                    Assert.AreSame(original, incoming[0], "Keep the target of in-flight loaders.");
                    original.ThumbnailSprite = sprite;
                    original.LoadedPrefab = prefab;
                    Assert.AreSame(sprite, incoming[0].ThumbnailSprite);
                    var load = NetworkCatalogLoader.PreloadThumbnail(incoming[0]);
                    Assert.IsFalse(load.MoveNext(), "Cached thumbnail must not decode/download again.");
                }
                var renamed = new NetworkCatalogItem { id = "shared", display_name = "New name",
                    model_url = "model", model_hash = "v1", thumbnail_url = "thumbnail" };
                NetworkCatalogRegistry.Merge(new[] { renamed });
                Assert.AreSame(sprite, renamed.ThumbnailSprite);
                Assert.AreSame(prefab, renamed.LoadedPrefab);
                var changed = new NetworkCatalogItem { id = "shared", model_url = "model",
                    model_hash = "v2", thumbnail_url = "new-thumbnail" };
                NetworkCatalogRegistry.Merge(new[] { changed });
                Assert.IsNull(changed.LoadedPrefab, "Changed model hash invalidates cached model.");
                Assert.IsNull(changed.ThumbnailSprite, "Changed thumbnail URL invalidates image.");
            }
            finally
            {
                NetworkCatalogRegistry.Register(previous);
                typeof(NetworkCatalogRegistry).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static)
                    .SetValue(null, ready);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void CancelledCatalogWaitExitsOnNextResumeEvenWhilePaused()
        {
            var loaded = typeof(NetworkCatalogRegistry).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);
            bool previousLoaded = NetworkCatalogRegistry.IsLoaded;
            float previousTimeScale = Time.timeScale;
            var go = new GameObject("Catalog wait cancellation test");
            try
            {
                loaded.SetValue(null, false); Time.timeScale = 0;
                var net = go.AddComponent<NetworkBootstrapper>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var begin = typeof(NetworkBootstrapper).GetMethod("BeginModelLoad", flags);
                var wait = typeof(NetworkBootstrapper).GetMethod("SpawnObjectWhenRegistryReady", flags);
                var old = begin.Invoke(net, new object[] { 42u });
                var routine = (System.Collections.IEnumerator)wait.Invoke(net,
                    new object[] { "waiting", 42u, Vector3.zero, Quaternion.identity, 1f, old, false });
                Assert.IsTrue(routine.MoveNext());
                net.HandleClientMessage(NetMsgType.RemoveObject, NetSerializer.WriteRemoveObject(42));
                var replacement = begin.Invoke(net, new object[] { 42u });
                Assert.IsFalse(routine.MoveNext(), "Cancelled wait must not linger until catalog readiness or timeout.");
                Assert.IsTrue((bool)typeof(NetworkBootstrapper).GetMethod("IsCurrentModelLoad", flags)
                    .Invoke(net, new object[] { 42u, replacement }), "Old wait must not remove the replacement ticket.");
            }
            finally
            {
                Time.timeScale = previousTimeScale; loaded.SetValue(null, previousLoaded);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CatalogRegistrationToleratesNullEntriesAndResetsReadiness()
        {
            var previous = NetworkCatalogRegistry.Snapshot();
            bool previousLoaded = NetworkCatalogRegistry.IsLoaded;
            try
            {
                NetworkCatalogRegistry.Register(new[] { null, new NetworkCatalogItem { id = "valid" },
                    new NetworkCatalogItem { id = "" } });
                Assert.IsTrue(NetworkCatalogRegistry.IsLoaded);
                Assert.AreEqual(1, NetworkCatalogRegistry.Snapshot().Length);
                Assert.IsTrue(NetworkCatalogRegistry.TryGet("valid", out _));
                NetworkCatalogRegistry.Register(null);
                Assert.IsFalse(NetworkCatalogRegistry.IsLoaded);
                Assert.AreEqual(0, NetworkCatalogRegistry.Snapshot().Length);
                NetworkCatalogRegistry.Register(new NetworkCatalogItem[0]);
                Assert.IsTrue(NetworkCatalogRegistry.IsLoaded, "A successful empty catalog is ready.");
            }
            finally
            {
                NetworkCatalogRegistry.Register(previous);
                typeof(NetworkCatalogRegistry).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static)
                    .SetValue(null, previousLoaded);
            }
        }

        [Test]
        public void IncompleteSnapshotsNeverBecomeApplied()
        {
            var go = new GameObject("Incomplete snapshot test");
            try
            {
                var net = go.AddComponent<NetworkBootstrapper>();
                var applied = typeof(NetworkBootstrapper).GetField("_snapshotApplied", BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (string objectId in new[] { "", "missing-placer" })
                {
                    applied.SetValue(net, true);
                    net.HandleClientMessage(NetMsgType.FullState, NetSerializer.WriteFullState(10, 10, 2, null,
                        new[] { new SpawnObjectData { NetId = 42, ObjectId = objectId,
                            Rotation = Quaternion.identity, Scale = 1 } }, null));
                    Assert.IsFalse((bool)applied.GetValue(net), objectId);
                }
                applied.SetValue(net, true);
                net.HandleClientMessage(NetMsgType.FullState,
                    NetSerializer.WriteFullState(-1, 10, 2, null, null, null));
                Assert.IsFalse((bool)applied.GetValue(net), "Invalid replacement must not reuse previous success.");
                applied.SetValue(net, true);
                Assert.Throws<System.IO.InvalidDataException>(() =>
                    net.HandleClientMessage(NetMsgType.FullState, new byte[0]));
                Assert.IsFalse((bool)applied.GetValue(net), "Truncated data must clear previous success.");
                net.HandleClientMessage(NetMsgType.FullState,
                    NetSerializer.WriteFullState(10, 10, 2, null, null, null));
                Assert.IsTrue((bool)applied.GetValue(net), "A valid empty board remains supported.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FullStateReportsObjectProgressOnlyAfterPlacementCompletes()
        {
            var go = new GameObject("Snapshot progress test");
            var template = new GameObject("Template");
            var catalog = ScriptableObject.CreateInstance<ObjectCatalog>();
            var entry = ScriptableObject.CreateInstance<SandplayObject>();
            ObjectPlacer placer = null;
            try
            {
                var net = go.AddComponent<NetworkBootstrapper>();
                placer = go.AddComponent<ObjectPlacer>();
                entry.ObjectId = "progress-object";
                entry.Prefab = template;
                catalog.Objects.Add(entry);
                typeof(ObjectPlacer).GetField("_catalog", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(placer, catalog);
                int loaded = -1, total = -1;
                net.OnSnapshotLoadProgress += (resolved, expected) =>
                {
                    loaded = resolved;
                    total = expected;
                };

                net.HandleClientMessage(NetMsgType.FullState, NetSerializer.WriteFullState(10, 10, 2, null,
                    new[] { new SpawnObjectData { NetId = 7, ObjectId = entry.ObjectId,
                        Rotation = Quaternion.identity, Scale = 1 } }, null));

                Assert.AreEqual(1, total);
                Assert.AreEqual(1, loaded);
                Assert.IsNotNull(net.GetNetworkObject(7));
            }
            finally
            {
                if (placer != null)
                    foreach (var placed in placer.PlacedObjects)
                        if (placed != null) Object.DestroyImmediate(placed.gameObject);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(entry);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void LocalCatalogPlacementSkipsNullEntriesAndPreservesSuppression()
        {
            var go = new GameObject("Catalog placement test");
            var template = new GameObject("Template");
            var catalog = ScriptableObject.CreateInstance<ObjectCatalog>();
            var entry = ScriptableObject.CreateInstance<SandplayObject>();
            bool previous = ObjectSyncManager.SuppressNetworkSync;
            ObjectPlacer placer = null;
            try
            {
                var net = go.AddComponent<NetworkBootstrapper>();
                placer = go.AddComponent<ObjectPlacer>();
                entry.ObjectId = "local-test"; entry.Prefab = template;
                catalog.Objects.Add(null); catalog.Objects.Add(entry);
                typeof(ObjectPlacer).GetField("_catalog", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(placer, catalog);
                uint id = 1;
                foreach (bool suppressed in new[] { false, true })
                {
                    ObjectSyncManager.SuppressNetworkSync = suppressed;
                    net.HandleClientMessage(NetMsgType.SpawnObject,
                        NetSerializer.WriteSpawnObject(id, "local-test", Vector3.zero, Quaternion.identity, 1));
                    Assert.IsNotNull(net.GetNetworkObject(id++));
                    Assert.AreEqual(suppressed, ObjectSyncManager.SuppressNetworkSync);
                }
            }
            finally
            {
                ObjectSyncManager.SuppressNetworkSync = previous;
                if (placer != null)
                    foreach (var placed in placer.PlacedObjects)
                        if (placed != null) Object.DestroyImmediate(placed.gameObject);
                Object.DestroyImmediate(go); Object.DestroyImmediate(template);
                Object.DestroyImmediate(entry); Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void NewSnapshotCancelsOldLoadsButStaleMarkersPreserveCurrentLoads()
        {
            var go = new GameObject("Snapshot load test");
            try
            {
                var net = go.AddComponent<NetworkBootstrapper>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(NetworkBootstrapper).GetField("_relayMode", flags).SetValue(net, true);
                var begin = typeof(NetworkBootstrapper).GetMethod("BeginModelLoad", flags);
                var valid = typeof(NetworkBootstrapper).GetMethod("IsCurrentModelLoad", flags);
                var moves = (Dictionary<uint, byte[]>)typeof(NetworkBootstrapper).GetField("_loadingMoves", flags).GetValue(net);
                var old = begin.Invoke(net, new object[] { 42u });
                net.HandleClientMessage(NetMsgType.MoveObject,
                    NetSerializer.WriteMoveObject(42, Vector3.one, Quaternion.identity, 2));
                Assert.AreEqual(1, moves.Count);
                net.HandleClientMessage(NetMsgType.SnapshotBegin, System.BitConverter.GetBytes(10L));
                Assert.IsFalse((bool)valid.Invoke(net, new object[] { 42u, old }));
                Assert.AreEqual(0, moves.Count);
                var current = begin.Invoke(net, new object[] { 42u });
                net.HandleClientMessage(NetMsgType.SnapshotBegin, System.BitConverter.GetBytes(10L));
                net.HandleClientMessage(NetMsgType.SnapshotBegin, System.BitConverter.GetBytes(9L));
                net.HandleClientMessage(NetMsgType.SnapshotBegin, new byte[9]);
                Assert.IsTrue((bool)valid.Invoke(net, new object[] { 42u, current }));
                net.HandleClientMessage(NetMsgType.SnapshotBegin, System.BitConverter.GetBytes(11L));
                Assert.IsFalse((bool)valid.Invoke(net, new object[] { 42u, current }));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void UndoRedoPreservesDownloadedTemplateScaleAndSanitizesInvalidScale()
        {
            var go = new GameObject("Undo transform test");
            var model = new GameObject("Downloaded object");
            var template = new GameObject("Downloaded template");
            try
            {
                var net = go.AddComponent<NetworkBootstrapper>();
                var placed = model.AddComponent<PlacedObject>();
                template.transform.localScale = new Vector3(2, 3, 4);
                placed.NetworkItem = new NetworkCatalogItem { id = "test", LoadedPrefab = template };
                net.RegisterNetworkObject(42, placed);
                foreach (float scale in new[] { 2f, -1f, float.NaN, float.PositiveInfinity })
                {
                    var position = new Vector3(4, 5, 6);
                    var rotation = Quaternion.Euler(0, 45, 0);
                    net.HandleClientMessage(NetMsgType.UndoRedoSync, NetSerializer.WriteUndoRedoSync(null,
                        new[] { new SpawnObjectData { NetId = 42, ObjectId = "test", Position = position,
                            Rotation = rotation, Scale = scale } }));
                    Assert.AreEqual(template.transform.localScale * (scale == 2f ? 2f : 1f), model.transform.localScale);
                    Assert.AreEqual(position, model.transform.position);
                    Assert.Less(Quaternion.Angle(rotation, model.transform.rotation), .01f);
                }
                placed.NetworkItem = null;
                net.HandleClientMessage(NetMsgType.UndoRedoSync, NetSerializer.WriteUndoRedoSync(null,
                    new[] { new SpawnObjectData { NetId = 42, Rotation = Quaternion.identity, Scale = 3 } }));
                Assert.AreEqual(Vector3.one * 3, model.transform.localScale);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(model); Object.DestroyImmediate(template); }
        }

        [Test]
        public void DeletedThenRestoredIdRejectsItsOldLoad()
        {
            var go = new GameObject("PendingLoadTest");
            try
            {
                var net = go.AddComponent<NetworkBootstrapper>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var begin = typeof(NetworkBootstrapper).GetMethod("BeginModelLoad", flags);
                var valid = typeof(NetworkBootstrapper).GetMethod("IsCurrentModelLoad", flags);
                var old = begin.Invoke(net, new object[] { 42u });
                net.HandleClientMessage(NetMsgType.RemoveObject, NetSerializer.WriteRemoveObject(42));
                var replacement = begin.Invoke(net, new object[] { 42u });
                Assert.IsFalse((bool)valid.Invoke(net, new object[] { 42u, old }));
                Assert.IsTrue((bool)valid.Invoke(net, new object[] { 42u, replacement }));
                typeof(NetworkBootstrapper).GetMethod("ResetPendingModels", flags).Invoke(net, null);
                Assert.IsFalse((bool)valid.Invoke(net, new object[] { 42u, replacement }));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
