using NUnit.Framework;
using UnityEngine;
using Sandplay.Data;
using Sandplay.Objects;

namespace Sandplay.Tests
{
    [TestFixture]
    public class ObjectDuplicationTests
    {
        private GameObject _placerObject;
        private GameObject _undoObject;
        private GameObject _prefab;
        private ObjectPlacer _placer;

        [SetUp]
        public void SetUp()
        {
            _undoObject = new GameObject("UndoManager_Test");
            var undo = _undoObject.AddComponent<UndoManager>();
            // EditMode tests do not consistently invoke private runtime lifecycle methods.
            typeof(UndoManager).GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(undo, null);
            _placerObject = new GameObject("ObjectPlacer_Test");
            _placer = _placerObject.AddComponent<ObjectPlacer>();
            _prefab = new GameObject();
            _prefab.name = "DuplicatePrefab_Test";
            _prefab.transform.localScale = new Vector3(0.8f, 1.2f, 0.6f);
            _prefab.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_placerObject != null) Object.DestroyImmediate(_placerObject);
            if (_undoObject != null) Object.DestroyImmediate(_undoObject);
            if (_prefab != null) Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void DuplicateObject_LocalPrefab_PreservesTransformAndSupportsUndo()
        {
            var data = ScriptableObject.CreateInstance<SandplayObject>();
            data.DisplayName = "Test figure";
            data.ObjectId = "test-figure";
            data.Prefab = _prefab;

            var source = _placer.PlaceObject(data, new Vector3(0f, 0.7f, 0f),
                Quaternion.Euler(0f, 37f, 0f), 1.4f, skipOffset: true);
            var duplicate = _placer.DuplicateObject(source);

            Assert.NotNull(duplicate);
            Assert.AreSame(data, duplicate.ObjectData);
            Assert.AreEqual(source.transform.rotation, duplicate.transform.rotation);
            Assert.AreEqual(source.transform.localScale, duplicate.transform.localScale);
            Assert.AreEqual(source.transform.position.y, duplicate.transform.position.y, 0.0001f);
            Assert.Greater(Vector2.Distance(
                new Vector2(source.transform.position.x, source.transform.position.z),
                new Vector2(duplicate.transform.position.x, duplicate.transform.position.z)), 0.1f);
            Assert.AreEqual(2, _placer.PlacedObjects.Count);

            UndoManager.Instance.UndoLast();
            Assert.AreEqual(1, _placer.PlacedObjects.Count);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void DuplicateObject_DownloadedCatalogObject_KeepsCatalogIdentity()
        {
            var item = new NetworkCatalogItem
            {
                id = "catalog-object-id",
                display_name = "Uploaded figure",
                LoadedPrefab = _prefab,
            };
            var source = _placer.PlaceNetworkObject(item, new Vector3(0f, 1.1f, 0f),
                Quaternion.Euler(0f, 83f, 0f), 0.75f, skipOffset: true);
            var duplicate = _placer.DuplicateObject(source);

            Assert.NotNull(duplicate);
            Assert.AreSame(item, duplicate.NetworkItem);
            Assert.AreEqual(source.transform.rotation, duplicate.transform.rotation);
            Assert.AreEqual(source.transform.localScale, duplicate.transform.localScale);
            Assert.AreEqual(source.transform.position.y, duplicate.transform.position.y, 0.0001f);
            Assert.AreEqual(2, _placer.PlacedObjects.Count);

            UndoManager.Instance.UndoLast();
            Assert.AreEqual(1, _placer.PlacedObjects.Count);
        }
    }
}
