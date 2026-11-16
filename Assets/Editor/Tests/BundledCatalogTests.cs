using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Objects;

namespace Sandplay.Tests
{
    public class BundledCatalogTests
    {
        [Test]
        public void EveryBundledModelDecompressesToItsManifestHash()
        {
            var items = BundledCatalog.Items;
            Assert.Greater(items.Length, 0);
            foreach (var item in items)
            {
                Assert.IsTrue(BundledCatalog.IsPlacementAllowed(item), item.display_name);
                string path = Path.Combine(Application.streamingAssetsPath,
                    "BundledCatalog", item.model_hash + ".glb.gz");
                using (var input = File.OpenRead(path))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(gzip)).Replace("-", "").ToLowerInvariant();
                    Assert.AreEqual(item.model_hash, hash, item.display_name);
                }
            }
        }

        [Test]
        public void ChangedModelAndFutureObjectUseCloudPath()
        {
            var original = BundledCatalog.Items[0];
            Assert.IsTrue(BundledCatalog.Contains(original));
            var revision = new NetworkCatalogItem { id = original.id, model_hash = new string('0', 64) };
            var future = new NetworkCatalogItem { id = Guid.NewGuid().ToString(), model_hash = original.model_hash };
            Assert.IsFalse(BundledCatalog.Contains(revision));
            Assert.IsFalse(BundledCatalog.Contains(future));
            Assert.IsTrue(BundledCatalog.IsPlacementAllowed(future));
        }

        [Test]
        public void OnlyOfficialFencesAreExcludedFromNewPlacement()
        {
            var fence = new NetworkCatalogItem { id = "c05af8c8-b81f-4755-8661-f178650b3e9f" };
            var picket = new NetworkCatalogItem { id = "9db81f0f-a902-4231-a1e9-3bbaafa91972" };
            var custom = new NetworkCatalogItem { id = Guid.NewGuid().ToString(), display_name = "Fence" };
            var selected = BundledCatalog.FilterForPlacement(new[] { fence, picket, custom });
            CollectionAssert.AreEqual(new[] { custom }, selected);
        }
    }
}
