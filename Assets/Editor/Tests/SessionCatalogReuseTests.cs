using System;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.Tests
{
    public class SessionCatalogReuseTests
    {
        GameObject template;
        GameObject hostRoot;
        NetworkBootstrapper host;
        NetworkCatalogItem local;

        [SetUp]
        public void SetUp()
        {
            NetworkCatalogRegistry.Reset();
            template = new GameObject("Previously imported local model");
            hostRoot = new GameObject("Inactive session loader");
            hostRoot.SetActive(false); // Never connect to a real session.
            host = hostRoot.AddComponent<NetworkBootstrapper>();
            local = new NetworkCatalogItem
            {
                id = "join-model", display_name = "Local model",
                model_url = "https://old.example/model.glb", model_hash = new string('a', 64),
                LoadedPrefab = template,
            };
            NetworkCatalogRegistry.Register(new[] { local });
        }

        [TearDown]
        public void TearDown()
        {
            NetworkCatalogRegistry.Reset();
            UnityEngine.Object.DestroyImmediate(hostRoot);
            UnityEngine.Object.DestroyImmediate(template);
        }

        NetworkCatalogItem Receive(string hash)
        {
            var outgoing = new NetworkCatalogItem
            {
                id = local.id, display_name = local.display_name,
                model_url = "https://host.example/model.glb?new-token", model_hash = hash,
            };
            return NetSerializer.ReadCatalogManifest(NetSerializer.WriteCatalogManifest(new[] { outgoing }))[0];
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ReceivedManifestReusesLoadedModelAcrossUrlChanges(bool merge, bool restoreOnly)
        {
            if (restoreOnly) NetworkCatalogRegistry.Register(Array.Empty<NetworkCatalogItem>());
            var incoming = Receive(local.model_hash);
            if (merge) NetworkCatalogRegistry.Merge(new[] { incoming });
            else NetworkCatalogRegistry.Register(new[] { incoming });
            Assert.IsTrue(NetworkCatalogRegistry.TryGet(incoming.id, out var resolved));
            Assert.AreSame(template, resolved.LoadedPrefab);
            Assert.AreEqual(incoming.model_url, resolved.model_url, "Keep the host's current metadata.");

            int done = 0;
            var load = NetworkCatalogLoader.PreloadGlb(host, resolved, () => done++);
            Assert.IsFalse(load.MoveNext(), "An imported model needs no file read, download or import wait.");
            Assert.AreEqual(1, done);
        }

        [Test]
        public void ChangedHashDoesNotReuseStaleModel()
        {
            var incoming = Receive(new string('b', 64));
            NetworkCatalogRegistry.Register(new[] { incoming });
            Assert.IsNull(incoming.LoadedPrefab);
        }

        [Test]
        public void ManifestDoesNotImportUnplacedBundledModels()
        {
            var incoming = NetSerializer.ReadCatalogManifest(
                NetSerializer.WriteCatalogManifest(BundledCatalog.Items));
            NetworkCatalogRegistry.Register(incoming);
            Assert.Greater(incoming.Length, 0);
            foreach (var item in NetworkCatalogRegistry.Snapshot())
            {
                Assert.IsTrue(NetworkCatalogLoader.IsGlbCached(item));
                Assert.IsNull(item.LoadedPrefab, "Receiving the library must not import every model.");
            }
        }

        [Test]
        public void LegacyModelWithoutHashNeedsMatchingUrl()
        {
            local.model_hash = "";
            var incoming = Receive("");
            NetworkCatalogRegistry.Register(new[] { incoming });
            Assert.IsNull(incoming.LoadedPrefab);
        }
    }
}
