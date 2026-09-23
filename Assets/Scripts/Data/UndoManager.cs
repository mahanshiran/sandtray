using System.Collections.Generic;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Data
{
    public interface ICommand
    {
        void Execute();
        void Undo();
    }

    public class UndoManager : MonoBehaviour
    {
        public static UndoManager Instance { get; private set; }

        private readonly Stack<ICommand> _undoStack = new();
        private readonly Stack<ICommand> _redoStack = new();
        private int _maxSteps = 50;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        private void Awake()
        {
            Instance = this;
            if (GameManager.Instance != null)
                _maxSteps = GameManager.Instance.Config.MaxUndoSteps;
        }

        /// <summary>
        /// Push a pre-executed command onto the undo stack (no Execute call).
        /// Use when the action has already been performed.
        /// </summary>
        public void Record(ICommand command)
        {
            _undoStack.Push(command);
            _redoStack.Clear();
        }

        public void Execute(ICommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();

            // Trim stack
            if (_undoStack.Count > _maxSteps)
            {
                // Stack doesn't support trimming from bottom, but for simplicity we allow it to grow slightly
                // A production version would use a deque
            }
        }

        public void UndoLast()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsSpectator) return;
            if (_undoStack.Count == 0) return;
            var cmd = _undoStack.Pop();
            cmd.Undo();
            _redoStack.Push(cmd);

            // Sync state to spectators after undo
            Core.NetworkBootstrapper.Instance?.SendUndoRedoSync();
        }

        public void RedoLast()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsSpectator) return;
            if (_redoStack.Count == 0) return;
            var cmd = _redoStack.Pop();
            cmd.Execute();
            _undoStack.Push(cmd);

            // Sync state to spectators after redo
            Core.NetworkBootstrapper.Instance?.SendUndoRedoSync();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        private void Update()
        {
            if (Core.InputHelper.IsInputBlocked || Core.InputHelper.IsTextInputFocused) return;
            if (Core.GameManager.Instance != null && Core.GameManager.Instance.IsSpectator) return;
            if (Sandplay.Camera.WalkModeController.Instance != null && Sandplay.Camera.WalkModeController.Instance.IsActive) return;
            var placer = FindAnyObjectByType<Sandplay.Objects.ObjectPlacer>();
            if (placer != null && placer.IsSelectionGestureActive) return;
            var panel = FindAnyObjectByType<Sandplay.UI.ObjectActionPanel>();
            if (panel != null && panel.IsActionActive) return;
            if (KeyboardShortcuts.Pressed(ShortcutAction.Undo)) UndoLast();
            else if (KeyboardShortcuts.Pressed(ShortcutAction.Redo)) RedoLast();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    // --- Concrete Commands ---

    public class PlaceObjectCommand : ICommand
    {
        private readonly Objects.ObjectPlacer _placer;
        private readonly Objects.SandplayObject _data;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private readonly float _scale;
        private readonly bool _skipOffset;
        private Objects.PlacedObject _placed;
        public Objects.PlacedObject PlacedObject => _placed;

        public PlaceObjectCommand(Objects.ObjectPlacer placer, Objects.SandplayObject data,
            Vector3 position, Quaternion rotation, float scale, bool skipOffset = false)
        {
            _placer = placer;
            _data = data;
            _position = position;
            _rotation = rotation;
            _scale = scale;
            _skipOffset = skipOffset;
        }

        public void Execute()
        {
            _placed = _placer.PlaceObject(_data, _position, _rotation, _scale, _skipOffset);
        }

        public void Undo()
        {
            if (_placed != null)
                _placer.RemoveObject(_placed);
        }
    }

    public class PlaceNetworkObjectCommand : ICommand
    {
        private readonly Objects.ObjectPlacer _placer;
        private readonly Objects.NetworkCatalogItem _item;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private readonly float _scale;
        private readonly bool _skipOffset;
        private Objects.PlacedObject _placed;
        public Objects.PlacedObject PlacedObject => _placed;

        public PlaceNetworkObjectCommand(Objects.ObjectPlacer placer, Objects.NetworkCatalogItem item,
            Vector3 position, Quaternion rotation, float scale, bool skipOffset = false)
        {
            _placer = placer;
            _item = item;
            _position = position;
            _rotation = rotation;
            _scale = scale;
            _skipOffset = skipOffset;
        }

        public void Execute()
        {
            _placed = _placer.PlaceNetworkObject(_item, _position, _rotation, _scale, _skipOffset);
        }

        public void Undo()
        {
            if (_placed != null)
                _placer.RemoveObject(_placed);
        }
    }

    public class RemoveObjectCommand : ICommand
    {
        private readonly Objects.ObjectPlacer _placer;
        private Objects.PlacedObject _placed;
        private Objects.PlacedObjectData _savedData;
        private Objects.SandplayObject _objectData;
        private Objects.NetworkCatalogItem _networkItem;

        public RemoveObjectCommand(Objects.ObjectPlacer placer, Objects.PlacedObject placed)
        {
            _placer = placer;
            _placed = placed;
            _objectData = placed.ObjectData;
            _networkItem = placed.NetworkItem;
            _savedData = placed.Serialize();
        }

        public void Execute()
        {
            _placer.RemoveObject(_placed);
        }

        public void Undo()
        {
            _placed = _objectData != null
                ? _placer.PlaceObject(_objectData, _savedData.Position, Quaternion.Euler(_savedData.Rotation), _savedData.Scale, skipOffset: true)
                : _placer.PlaceNetworkObject(_networkItem, _savedData.Position, Quaternion.Euler(_savedData.Rotation), _savedData.Scale, skipOffset: true);
            if (_placed != null) _placed.PlacementTime = _savedData.PlacementTime;
        }
    }

    public class TerrainModifyCommand : ICommand
    {
        private readonly Sand.SandMesh _sandMesh;
        private readonly float[] _beforeHeightmap;
        private float[] _afterHeightmap;

        public TerrainModifyCommand(Sand.SandMesh sandMesh)
        {
            _sandMesh = sandMesh;
            _beforeHeightmap = (float[])sandMesh.Heightmap.Clone();
        }

        public void CaptureAfter()
        {
            _afterHeightmap = (float[])_sandMesh.Heightmap.Clone();
        }

        public bool HasChanged()
        {
            if (_afterHeightmap == null) return false;
            for (int i = 0; i < _beforeHeightmap.Length; i++)
                if (_beforeHeightmap[i] != _afterHeightmap[i]) return true;
            return false;
        }

        public void Execute()
        {
            if (_afterHeightmap != null)
                _sandMesh.SetHeightmap(_afterHeightmap);
        }

        public void Undo()
        {
            _sandMesh.SetHeightmap(_beforeHeightmap);
        }
    }

    public class SplatmapPaintCommand : ICommand
    {
        private readonly Sand.SandMaterialController _ctrl;
        private readonly Color[] _before;
        private Color[] _after;

        public SplatmapPaintCommand(Sand.SandMaterialController ctrl)
        {
            _ctrl = ctrl;
            _before = (Color[])ctrl.SplatPixelsCopy;
        }

        public void CaptureAfter()
        {
            _after = (Color[])_ctrl.SplatPixelsCopy;
        }

        public bool HasChanged()
        {
            if (_after == null) return false;
            for (int i = 0; i < _before.Length; i++)
                if (_before[i] != _after[i]) return true;
            return false;
        }

        public void Execute()
        {
            if (_after != null) _ctrl.ApplySplatPixels(_after);
        }

        public void Undo()
        {
            _ctrl.ApplySplatPixels(_before);
        }
    }

    public class MoveObjectCommand : ICommand
    {
        private readonly Objects.PlacedObject _placed;
        private readonly Vector3 _beforePos;
        private readonly Quaternion _beforeRot;
        private readonly Vector3 _beforeScale;
        private readonly Vector3 _afterPos;
        private readonly Quaternion _afterRot;
        private readonly Vector3 _afterScale;

        public MoveObjectCommand(Objects.PlacedObject placed, Vector3 beforePos, Quaternion beforeRot,
            Vector3 afterPos, Quaternion afterRot)
        {
            _placed = placed;
            _beforePos = beforePos;
            _beforeRot = beforeRot;
            _beforeScale = placed != null ? placed.transform.localScale : Vector3.one;
            _afterPos = afterPos;
            _afterRot = afterRot;
            _afterScale = _beforeScale;
        }

        public MoveObjectCommand(Objects.PlacedObject placed, Vector3 beforePos, Quaternion beforeRot, Vector3 beforeScale,
            Vector3 afterPos, Quaternion afterRot, Vector3 afterScale)
        {
            _placed = placed;
            _beforePos = beforePos;
            _beforeRot = beforeRot;
            _beforeScale = beforeScale;
            _afterPos = afterPos;
            _afterRot = afterRot;
            _afterScale = afterScale;
        }

        public void Execute()
        {
            if (_placed != null)
            {
                _placed.transform.position = _afterPos;
                _placed.transform.rotation = _afterRot;
                _placed.transform.localScale = _afterScale;
            }
        }

        public void Undo()
        {
            if (_placed != null)
            {
                _placed.transform.position = _beforePos;
                _placed.transform.rotation = _beforeRot;
                _placed.transform.localScale = _beforeScale;
            }
        }
    }
}
