using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Camera;
using Sandplay.Core;
using Sandplay.Sand;

namespace Sandplay.Tests
{
    public class WalkRoomFloorTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _root, _bodyObject;
        private GameConfig _config;
        private SandMesh _sand;
        private WalkModeController _walk;
        private const float EyeHeight = .6f;
        private const float Step = 1f / 60;

        private void Build(bool circular, float sandHeight)
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.CircularTray = circular;
            _config.HeightmapResolution = 33;
            _root = new GameObject("Walk test");
            var tray = new GameObject("Tray", typeof(SandMesh));
            tray.transform.SetParent(_root.transform);
            _sand = tray.GetComponent<SandMesh>();
            typeof(SandMesh).GetMethod("Awake", Private).Invoke(_sand, null);
            _sand.Initialize(_config);
            _sand.SetAllHeights(sandHeight);
            var frame = tray.AddComponent<SandboxFrame>();
            frame.Initialize(_config); frame.Rebuild();
            frame.enabled = false;
            var room = new GameObject("TherapyRoom"); room.transform.SetParent(_root.transform);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor"; floor.transform.SetParent(room.transform);
            floor.transform.position = new Vector3(0, SandboxFrame.RoomFloorY - .075f, 0);
            floor.transform.localScale = new Vector3(80, .15f, 80);
            // The real room floor ignores tool rays, but still supports walking.
            floor.layer = LayerMask.NameToLayer("Ignore Raycast");
            _walk = _root.AddComponent<WalkModeController>();
            _bodyObject = new GameObject("Walk body");
            var body = _bodyObject.AddComponent<CharacterController>();
            body.height = .7f; body.radius = .12f; body.center = new Vector3(0, .35f, 0);
            body.skinWidth = .01f; body.stepOffset = .08f; body.slopeLimit = 50; body.minMoveDistance = 0;
            Set("_body", body); Set("_active", true); Set("_grounded", true);
            Physics.SyncTransforms();
        }

        private void PlaceFeet(Vector3 feet)
        {
            var body = _bodyObject.GetComponent<CharacterController>();
            body.enabled = false; _bodyObject.transform.position = feet; body.enabled = true;
            Set("_position", feet + Vector3.up * EyeHeight);
            Set("_verticalVelocity", 0f); Set("_grounded", true);
            Physics.SyncTransforms();
        }

        private void Walk(Vector2 input, int frames)
        {
            var step = typeof(WalkModeController).GetMethod("SimulateMovement", Private);
            for (int i = 0; i < frames; i++) step.Invoke(_walk, new object[] { input, Step });
        }
        private Vector3 Position => (Vector3)typeof(WalkModeController).GetField("_position", Private).GetValue(_walk);
        private void Set(string field, object value) => typeof(WalkModeController).GetField(field, Private).SetValue(_walk, value);
        private void AssertOnFloor()
        {
            Assert.That(Position.y, Is.EqualTo(SandboxFrame.RoomFloorY + EyeHeight).Within(.04f));
            Assert.IsTrue((bool)typeof(WalkModeController).GetField("_grounded", Private).GetValue(_walk));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JumpFromTallSandOverRimLandsAndWalksAndJumpsOnRoomFloor(bool circular)
        {
            Build(circular, 2f);
            PlaceFeet(new Vector3(4.65f, 2f, 0));
            _walk.Jump();
            Walk(Vector2.right, 240);
            Assert.Greater(Position.x, 6f, "Clearing the physical rim must allow leaving the tray");
            AssertOnFloor();
            float z = Position.z;
            Walk(Vector2.up, 60);
            Assert.Greater(Position.z, z + 1f);
            AssertOnFloor();
            _walk.Jump();
            Walk(Vector2.zero, 12);
            Assert.Greater(Position.y, SandboxFrame.RoomFloorY + EyeHeight + .2f);
            Walk(Vector2.zero, 90);
            AssertOnFloor();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OrdinaryWalkingStillCollidesWithTrayRim(bool circular)
        {
            Build(circular, .5f);
            PlaceFeet(new Vector3(4.65f, .5f, 0));
            Walk(Vector2.right, 180);
            Assert.Less(Position.x, 5f, "The rim must still block walkers below its top");
            Assert.Greater(Position.x, 4.65f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OutsideFootprintDoesNotExtendHillHeightOntoRoomFloor(bool circular)
        {
            Build(circular, 2f);
            PlaceFeet(circular ? new Vector3(4.4f, 3f, 4.4f) : new Vector3(5.5f, 3f, 0));
            Walk(Vector2.zero, 180);
            AssertOnFloor();
        }

        [Test]
        public void WalkingUnderTableStaysOnFloorAndRoomEdgeStopsMovement()
        {
            Build(false, 2f);
            PlaceFeet(new Vector3(-6, SandboxFrame.RoomFloorY, 0));
            Walk(Vector2.right, 240);
            Assert.That(Position.x, Is.EqualTo(0f).Within(.05f));
            AssertOnFloor();
            PlaceFeet(new Vector3(39.7f, SandboxFrame.RoomFloorY, 0));
            Walk(Vector2.right, 120);
            Assert.That(Position.x, Is.EqualTo(39.85f).Within(.02f));
            AssertOnFloor();
        }

        [Test]
        public void ModelsOutsideTrayStillBlockWalking()
        {
            Build(false, 2f);
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.SetParent(_root.transform);
            obstacle.transform.position = new Vector3(7, SandboxFrame.RoomFloorY + .5f, 0);
            PlaceFeet(new Vector3(6, SandboxFrame.RoomFloorY, 0));
            Walk(Vector2.right, 120);
            Assert.That(Position.x, Is.InRange(6f, 6.5f));
            AssertOnFloor();
        }

        [TearDown]
        public void Cleanup()
        {
            if (_bodyObject != null) Object.DestroyImmediate(_bodyObject);
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }
    }
}
