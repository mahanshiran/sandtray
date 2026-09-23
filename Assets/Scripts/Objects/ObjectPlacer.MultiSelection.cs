using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;
using Sandplay.Data;

namespace Sandplay.Objects
{
    public partial class ObjectPlacer
    {
        private readonly List<PlacedObject> _selection = new List<PlacedObject>();
        public IReadOnlyList<PlacedObject> Selection => _selection;
        private bool _boxSelecting, _movingGroup;
        private bool _boxAdditive;
        private bool _gestureMoved;
        private Vector2 _boxStart;
        private RectTransform _selectionRectangle;
        private Vector3 _groupPointerStart;
        private List<PlacedObject> _copiedSelection = new List<PlacedObject>();
        private bool _batchMembership;
        public bool IsSelectionGestureActive => _boxSelecting || _movingGroup;
        private struct Pose
        {
            public PlacedObject obj;
            public Vector3 position, scale;
            public Quaternion rotation;
            public Pose(PlacedObject value) { obj = value; position = value.transform.position; scale = value.transform.localScale; rotation = value.transform.rotation; }
            public void Restore() { if (obj != null) obj.transform.SetPositionAndRotation(position, rotation); if (obj != null) obj.transform.localScale = scale; }
        }
        private List<Pose> _groupStart;
        private Vector3 _groupPivot;

        public void SetSelection(IEnumerable<PlacedObject> objects, bool changeTool = true)
        {
            var next = objects.Where(o => o != null).Distinct().ToList();
            foreach (var obj in _selection) if (obj != null && !next.Contains(obj)) obj.SetSelected(false);
            _selection.Clear(); _selection.AddRange(next);
            foreach (var obj in _selection) obj.SetSelected(true);
            _selectedPlaced = _selection.LastOrDefault();
            if (_selectedPlaced != null && changeTool && GameManager.Instance != null && GameManager.Instance.CurrentTool != ToolMode.WalkMode)
                GameManager.Instance.SetToolMode(ToolMode.ObjectSelect);
            EventBus.Publish(new ObjectSelectedEvent { PlacedObject = _selectedPlaced });
        }

        public static Bounds ObjectBounds(PlacedObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one * .1f);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        public Bounds SelectionBounds()
        {
            var items = _selection.Where(o => o != null).ToList();
            if (items.Count == 0) return new Bounds();
            var bounds = ObjectBounds(items[0]);
            foreach (var item in items) bounds.Encapsulate(ObjectBounds(item));
            return bounds;
        }

        public static bool IntersectsSelectionRect(UnityEngine.Camera camera, Rect area, Bounds bounds)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool visible = false;
            for (int i = 0; i < 8; i++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var screen = camera.WorldToScreenPoint(point);
                if (screen.z <= camera.nearClipPlane) continue;
                visible = true; min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            return visible && area.Overlaps(Rect.MinMaxRect(min.x, min.y, max.x, max.y), true);
        }

        private bool HandleMultiSelectionPointer()
        {
            if (Input.touchCount > 0 || !Input.GetMouseButtonDown(0)) return false;
            var mode = GameManager.Instance != null ? GameManager.Instance.CurrentTool : ToolMode.ObjectSelect;
            if (mode != ToolMode.ObjectSelect && mode != ToolMode.None) return false;
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) return false;
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            PlacedObject hitObject = null;
            if (Physics.Raycast(ray, out var hit, 100f)) hitObject = hit.collider.GetComponentInParent<PlacedObject>();
            bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (hitObject != null)
            {
                if (additive)
                {
                    var next = new List<PlacedObject>(_selection);
                    if (!next.Remove(hitObject)) next.Add(hitObject);
                    SetSelection(next);
                    return true;
                }
                if (_selection.Count > 1 && _selection.Contains(hitObject))
                {
                    _movingGroup = true;
                    _gestureMoved = false;
                    _boxStart = Input.mousePosition;
                    BeginGroupTransform();
                    var plane = new Plane(Vector3.up, _groupPivot);
                    if (plane.Raycast(ray, out float distance)) _groupPointerStart = ray.GetPoint(distance);
                    else { _movingGroup = false; _groupStart = null; }
                    return true;
                }
                return false;
            }
            _boxSelecting = true; _gestureMoved = false; _boxAdditive = additive; _boxStart = Input.mousePosition;
            return true;
        }

        private bool HandleActiveSelectionGesture()
        {
            if (!IsSelectionGestureActive) return false;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButton(1) || Input.GetMouseButton(2) ||
                (GameManager.Instance != null && GameManager.Instance.IsSpectator))
            { CancelSelectionGesture(); return true; }
            var point = (Vector2)Input.mousePosition;
            _gestureMoved |= Vector2.Distance(_boxStart, point) >= DragThreshold;
            bool moved = _gestureMoved;
            if (_boxSelecting && moved) DrawSelectionRect(point);
            if (_movingGroup && moved)
            {
                var ray = _cam.ScreenPointToRay(point);
                if (new Plane(Vector3.up, _groupPivot).Raycast(ray, out float distance))
                    TranslateGroup(ray.GetPoint(distance) - _groupPointerStart);
            }
            // Also complete if release was missed when cursor briefly left the window.
            if (!Input.GetMouseButton(0))
            {
                if (_boxSelecting)
                {
                    var area = Rect.MinMaxRect(Mathf.Min(_boxStart.x, point.x), Mathf.Min(_boxStart.y, point.y), Mathf.Max(_boxStart.x, point.x), Mathf.Max(_boxStart.y, point.y));
                    var next = _boxAdditive ? new List<PlacedObject>(_selection) : new List<PlacedObject>();
                    if (moved) next.AddRange(_placedObjects.Where(o => o != null && IntersectsSelectionRect(_cam, area, ObjectBounds(o))));
                    SetSelection(next);
                }
                else if (moved) EndGroupTransform();
                else _groupStart = null;
                _boxSelecting = _movingGroup = false;
                if (_selectionRectangle != null) _selectionRectangle.gameObject.SetActive(false);
            }
            return true;
        }

        private void DrawSelectionRect(Vector2 point)
        {
            if (_selectionRectangle == null)
            {
                var canvas = _actionPanel != null ? _actionPanel.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
                if (canvas == null) return;
                var go = new GameObject("SelectionRectangle", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LayoutElement));
                go.transform.SetParent(canvas.transform, false);
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                var image = go.GetComponent<Image>(); image.color = new Color(.45f,.78f,1f,.16f); image.raycastTarget = false;
                var outline = go.GetComponent<Outline>(); outline.effectColor = new Color(.65f,.86f,1f,.95f); outline.effectDistance = Vector2.one;
                _selectionRectangle = go.GetComponent<RectTransform>();
                _selectionRectangle.anchorMin = _selectionRectangle.anchorMax = new Vector2(.5f,.5f);
                _selectionRectangle.pivot = Vector2.zero;
            }
            var parent = (RectTransform)_selectionRectangle.parent;
            var owner = parent.GetComponent<Canvas>();
            var camera = owner.renderMode == RenderMode.ScreenSpaceOverlay ? null : owner.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _boxStart, camera, out var start);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, point, camera, out var end);
            _selectionRectangle.anchoredPosition = Vector2.Min(start, end);
            _selectionRectangle.sizeDelta = new Vector2(Mathf.Abs(start.x-end.x), Mathf.Abs(start.y-end.y));
            _selectionRectangle.SetAsLastSibling(); _selectionRectangle.gameObject.SetActive(true);
        }
        private void CancelSelectionGesture()
        {
            if (_movingGroup && _groupStart != null) foreach (var pose in _groupStart) pose.Restore();
            if (_movingGroup) _groupStart = null;
            _boxSelecting = _movingGroup = false;
            if (_selectionRectangle != null) _selectionRectangle.gameObject.SetActive(false);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelSelectionGesture(); }
        private void OnDestroy() { if (_selectionRectangle != null) Destroy(_selectionRectangle.gameObject); }

        public void CancelGroupTransform()
        {
            if (_groupStart != null) foreach (var pose in _groupStart) pose.Restore();
            _groupStart = null;
        }

        public void BeginGroupTransform()
        {
            _groupStart = _selection.Where(o => o != null).Select(o => new Pose(o)).ToList();
            _groupPivot = SelectionBounds().center;
        }
        private Vector3 ClampGroupDelta(IEnumerable<PlacedObject> objects, Vector3 delta)
        {
            foreach (var obj in objects.Where(o => o != null))
            {
                var allowed = ClampToBounds(obj.transform.position + delta, obj.gameObject) - obj.transform.position;
                delta.x = delta.x >= 0 ? Mathf.Min(delta.x, allowed.x) : Mathf.Max(delta.x, allowed.x);
                delta.z = delta.z >= 0 ? Mathf.Min(delta.z, allowed.z) : Mathf.Max(delta.z, allowed.z);
            }
            return delta;
        }
        public void TranslateGroup(Vector3 delta)
        {
            if (_groupStart == null) return;
            foreach (var pose in _groupStart) pose.Restore();
            delta = ClampGroupDelta(_groupStart.Select(p => p.obj), delta);
            foreach (var pose in _groupStart) if (pose.obj != null) pose.obj.transform.position = pose.position + delta;
        }
        public void RaiseGroup(float amount)
        {
            if (_groupStart == null || _sandMesh == null) return;
            foreach (var pose in _groupStart) pose.Restore();
            float min = float.NegativeInfinity, max = float.PositiveInfinity;
            float range = GameManager.Instance?.Config != null ? Mathf.Max(1f, GameManager.Instance.Config.SandMaxHeight) : 2f;
            foreach (var pose in _groupStart) if (pose.obj != null)
            {
                float bottom = GetObjectBottomOffset(pose.obj);
                min = Mathf.Max(min, bottom - pose.position.y);
                max = Mathf.Min(max, _sandMesh.SampleWorldHeight(pose.position) + bottom + range - pose.position.y);
            }
            if (min <= max) TranslateGroup(Vector3.up * Mathf.Clamp(amount, min, max));
        }
        public void RotateGroup(float degrees)
        {
            if (_groupStart == null) return;
            var rotation = Quaternion.Euler(0, degrees, 0);
            foreach (var pose in _groupStart) if (pose.obj != null)
            {
                pose.obj.transform.position = _groupPivot + rotation * (pose.position - _groupPivot);
                pose.obj.transform.rotation = rotation * pose.rotation;
            }
            KeepGroupInsideTray();
        }
        public void ResizeGroup(float factor)
        {
            if (_groupStart == null) return;
            float min = 0f, max = float.PositiveInfinity;
            foreach (var pose in _groupStart) if (pose.obj != null)
            {
                pose.Restore();
                float relative = pose.obj.Serialize().Scale;
                if (relative > 0) { min = Mathf.Max(min, .1f / relative); max = Mathf.Min(max, 5f / relative); }
            }
            if (min > max) return;
            factor = Mathf.Clamp(factor, min, max);
            foreach (var pose in _groupStart) if (pose.obj != null)
            {
                pose.obj.transform.position = _groupPivot + (pose.position - _groupPivot) * factor;
                pose.obj.transform.localScale = pose.scale * factor;
            }
            KeepGroupInsideTray();
        }
        private void KeepGroupInsideTray()
        {
            var config = GameManager.Instance?.Config;
            if (config == null || _groupStart == null) return;
            var bounds = SelectionBounds();
            if (bounds.size.x > config.SandboxWidth || bounds.size.z > config.SandboxDepth)
            { foreach (var pose in _groupStart) pose.Restore(); return; }
            float x = Mathf.Clamp(bounds.center.x, -config.SandboxWidth/2 + bounds.extents.x, config.SandboxWidth/2 - bounds.extents.x) - bounds.center.x;
            float z = Mathf.Clamp(bounds.center.z, -config.SandboxDepth/2 + bounds.extents.z, config.SandboxDepth/2 - bounds.extents.z) - bounds.center.z;
            foreach (var pose in _groupStart) if (pose.obj != null) pose.obj.transform.position += new Vector3(x,0,z);
        }
        public void EndGroupTransform(bool preserveBurial = false)
        {
            if (_groupStart == null) return;
            bool changed = _groupStart.Any(p => p.obj != null && (p.position != p.obj.transform.position || p.rotation != p.obj.transform.rotation || p.scale != p.obj.transform.localScale));
            if (changed && !AllowObjectsInAir && _sandMesh != null)
            {
                // Settle as a rigid group; don't collapse internal vertical spacing/stacking.
                float lift = float.NegativeInfinity;
                Physics.SyncTransforms();
                foreach (var pose in _groupStart) if (pose.obj != null)
                {
                    var pos = pose.obj.transform.position;
                    float bottom = GetObjectBottomOffset(pose.obj);
                    float landing = _sandMesh.SampleWorldHeight(pos) + bottom;
                    foreach (var hit in Physics.RaycastAll(pos + Vector3.up * .1f, Vector3.down, Mathf.Max(1, pos.y + 10f)))
                    {
                        var support = hit.collider.GetComponentInParent<PlacedObject>();
                        if (support != null && !_groupStart.Any(p => p.obj == support) && hit.point.y <= pos.y - bottom + .05f)
                            landing = Mathf.Max(landing, hit.point.y + bottom);
                    }
                    lift = Mathf.Max(lift, landing - pos.y);
                }
                if (preserveBurial) lift = Mathf.Min(0, lift);
                if (!float.IsInfinity(lift)) foreach (var pose in _groupStart) if (pose.obj != null) pose.obj.transform.position += Vector3.up * lift;
            }
            var commands = new List<ICommand>();
            foreach (var pose in _groupStart) if (pose.obj != null)
            {
                var tr = pose.obj.transform;
                if (tr.position != pose.position || tr.rotation != pose.rotation || tr.localScale != pose.scale)
                {
                    commands.Add(new MoveObjectCommand(pose.obj, pose.position, pose.rotation, pose.scale, tr.position, tr.rotation, tr.localScale));
                    EventBus.Publish(new ObjectTransformedEvent { PlacedObject = pose.obj });
                }
            }
            if (commands.Count > 0) UndoManager.Instance?.Record(new SelectionCommand(this, commands, false));
            _groupStart = null;
        }

        public void DeleteSelection()
        {
            var commands = _selection.Where(o => o != null).Select(o => (ICommand)new RemoveObjectCommand(this,o)).ToList();
            if (commands.Count > 0) ExecutePlacementCommand(new SelectionCommand(this, commands, true, true));
        }
        public void CopySelection() { _copiedSelection = _selection.Where(o => o != null).ToList(); }
        public void PasteSelection() { DuplicateSelection(_copiedSelection); }
        public void DuplicateSelection() { DuplicateSelection(_selection.ToList()); }
        private void DuplicateSelection(List<PlacedObject> sources)
        {
            sources = sources.Where(o => o != null && (o.ObjectData != null || o.NetworkItem != null)).ToList();
            if (sources.Count == 0) return;
            var bounds = ObjectBounds(sources[0]);
            foreach (var obj in sources) bounds.Encapsulate(ObjectBounds(obj));
            var right = _cam != null ? Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized : Vector3.right;
            if (right.sqrMagnitude < .001f) right = Vector3.right;
            float separation = Mathf.Abs(right.x)*bounds.size.x + Mathf.Abs(right.z)*bounds.size.z + .12f;
            var delta = ClampGroupDelta(sources, right*separation);
            var left = ClampGroupDelta(sources, -right*separation);
            if (left.sqrMagnitude > delta.sqrMagnitude) delta = left;
            var commands = new List<ICommand>();
            foreach (var obj in sources)
            {
                if (obj.ObjectData != null) commands.Add(new PlaceObjectCommand(this,obj.ObjectData,obj.transform.position+delta,obj.transform.rotation,obj.Serialize().Scale,true));
                else commands.Add(new PlaceNetworkObjectCommand(this,obj.NetworkItem,obj.transform.position+delta,obj.transform.rotation,obj.Serialize().Scale,true));
            }
            ExecutePlacementCommand(new SelectionCommand(this,commands,true));
            SetSelection(commands.Select(c => c is PlaceObjectCommand local ? local.PlacedObject : ((PlaceNetworkObjectCommand)c).PlacedObject));
        }

        private sealed class SelectionCommand : ICommand
        {
            private readonly ObjectPlacer placer;
            private readonly List<ICommand> commands;
            private readonly bool membership;
            private readonly bool settle;
            private List<Pose> before, after;
            public SelectionCommand(ObjectPlacer placer, List<ICommand> commands, bool membership, bool settle = false)
            { this.placer=placer; this.commands=commands; this.membership=membership; this.settle=settle; }
            public void Execute()
            {
                if (membership && before == null) before = placer._placedObjects.Select(o=>new Pose(o)).ToList();
                placer._batchMembership = membership;
                try { foreach(var command in commands) command.Execute(); }
                finally { placer._batchMembership = false; }
                if (membership)
                {
                    if (after == null) { if (settle && !AllowObjectsInAir) placer.SettleFloatingObjects(); after=placer._placedObjects.Select(o=>new Pose(o)).ToList(); }
                    else foreach(var pose in after) pose.Restore();
                }
            }
            public void Undo()
            {
                placer._batchMembership = membership;
                try { for(int i=commands.Count-1;i>=0;i--) commands[i].Undo(); }
                finally { placer._batchMembership = false; }
                if (before != null) foreach(var pose in before) pose.Restore();
            }
        }
    }
}
