using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Sand;

namespace Sandplay.Objects
{
    public class ObjectPlacer : MonoBehaviour
    {
        private const string AllowObjectsInAirPrefKey = "sandplay_allow_objects_in_air";

        public static bool AllowObjectsInAir
        {
            get => PlayerPrefs.GetInt(AllowObjectsInAirPrefKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(AllowObjectsInAirPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [SerializeField] private ObjectCatalog _catalog;
        [SerializeField] private SandMesh _sandMesh;
        [SerializeField] private UnityEngine.Camera _cam;

        private SandplayObject _selectedCatalogObject;
        private GameObject _ghostPreview;
        private PlacedObject _selectedPlaced;

        private readonly List<PlacedObject> _placedObjects = new();
        public IReadOnlyList<PlacedObject> PlacedObjects => _placedObjects;
        public ObjectCatalog Catalog => _catalog;

        private bool _isDraggingObject;
        private bool _dragStarted; // true once pointer exceeds drag threshold
        private Vector2 _pointerDownPos;
        private const float DragThreshold = 10f; // pixels
        private Vector3 _dragOffset;

        private Sandplay.UI.ObjectActionPanel _actionPanel;

        // Undo tracking for move/rotate/vertical
        private Vector3 _moveStartPos;
        private Quaternion _moveStartRot;

        // Copy/paste clipboard
        private PlacedObject _copiedObject;

        public void Initialize(ObjectCatalog catalog)
        {
            _catalog = catalog;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CatalogObjectSelectedEvent>(OnCatalogObjectSelected);
            EventBus.Subscribe<ToolModeChangedEvent>(OnToolModeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CatalogObjectSelectedEvent>(OnCatalogObjectSelected);
            EventBus.Unsubscribe<ToolModeChangedEvent>(OnToolModeChanged);
        }

        private void Start()
        {
            if (_sandMesh == null) _sandMesh = SandMesh.Instance;
            if (_cam == null) _cam = UnityEngine.Camera.main;
        }

        private void Update()
        {
            if (_cam == null || _sandMesh == null) return;
            if (InputHelper.IsInputBlocked) return;

            // Handle keyboard shortcuts first (works globally when keyboard is available)
            HandleKeyboardShortcuts();

            // Psychologists can only select objects, not modify them
            bool isPsychologist = GameManager.Instance != null && GameManager.Instance.IsPsychologist;
            // Observers cannot interact at all
            bool isObserver = GameManager.Instance != null && GameManager.Instance.IsObserver;

            if (isObserver) return;

            // Skip world input while the floating toolbar handles a transform drag.
            if (_actionPanel == null)
                _actionPanel = FindAnyObjectByType<Sandplay.UI.ObjectActionPanel>();
            if (_actionPanel != null && _actionPanel.IsActionActive) return;

            if (InputHelper.IsPointerOverUI()) return;
            if (Sandplay.UI.CatalogDragHandler.IsDragging) return;
            var sandTool = FindAnyObjectByType<Sandplay.Sand.SandToolController>();
            if (sandTool != null && sandTool.IsDrawing) return;
            if (Sandplay.Camera.WalkModeController.Instance != null && Sandplay.Camera.WalkModeController.Instance.IsActive) return;

            // Psychologists cannot drag or modify objects
            if (isPsychologist)
            {
                // Only allow selection
                if (InputHelper.GetPointerDown())
                {
                    Ray ray = _cam.ScreenPointToRay(InputHelper.GetPointerPosition());
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        var placed = hit.collider.GetComponentInParent<PlacedObject>();
                        if (placed != null)
                        {
                            SelectObject(placed);
                            NetworkBootstrapper.Instance?.SendObjectSelection(_selectedPlaced != null ? _selectedPlaced.NetworkId : 0);
                        }
                        else if (_selectedPlaced != null)
                        {
                            SelectObject(null);
                            NetworkBootstrapper.Instance?.SendObjectSelection(0);
                        }
                    }
                    else if (_selectedPlaced != null)
                    {
                        SelectObject(null);
                        NetworkBootstrapper.Instance?.SendObjectSelection(0);
                    }
                }
                return;
            }

            // Handle horizontal drag in progress regardless of mode
            if (_isDraggingObject && _selectedPlaced != null)
            {
                if (InputHelper.GetPointerHeld())
                {
                    // Only begin moving once pointer exceeds drag threshold
                    if (!_dragStarted)
                    {
                        float dist = Vector2.Distance(InputHelper.GetPointerPosition(), _pointerDownPos);
                        if (dist < DragThreshold) return;
                        _dragStarted = true;
                    }

                    Ray dragRay = _cam.ScreenPointToRay(InputHelper.GetPointerPosition());

                    // Temporarily disable dragged object's colliders
                    var selfColliders = _selectedPlaced.GetComponentsInChildren<Collider>();
                    foreach (var c in selfColliders) c.enabled = false;

                    // Use RaycastAll to find both sand and object hits
                    var hits = Physics.RaycastAll(dragRay, 100f);
                    PlacedObject hitPlaced = null;
                    RaycastHit sandHit = default;
                    bool foundSand = false;

                    foreach (var h in hits)
                    {
                        if (h.collider.gameObject == _sandMesh.gameObject)
                        {
                            sandHit = h;
                            foundSand = true;
                        }
                        else
                        {
                            var p = h.collider.GetComponentInParent<PlacedObject>();
                            if (p != null && p != _selectedPlaced)
                                hitPlaced = p;
                        }
                    }

                    if (hitPlaced != null)
                    {
                        // Stack on top of the other object
                        float topY = GetObjectTopY(hitPlaced);
                        float bottomOffset = GetObjectBottomOffset(_selectedPlaced);
                        Vector3 newPos = new Vector3(
                            hitPlaced.transform.position.x,
                            topY + bottomOffset,
                            hitPlaced.transform.position.z);
                        newPos = ClampToBounds(newPos, _selectedPlaced.gameObject);
                        _selectedPlaced.transform.position = newPos;
                    }
                    else if (foundSand)
                    {
                        Vector3 newPos = sandHit.point + new Vector3(_dragOffset.x, 0, _dragOffset.z);
                        float sandY = _sandMesh.SampleWorldHeight(newPos);
                        float bottomOffset = GetObjectBottomOffset(_selectedPlaced);
                        newPos.y = sandY + bottomOffset;
                        newPos = ClampToBounds(newPos, _selectedPlaced.gameObject);
                        _selectedPlaced.transform.position = newPos;
                    }

                    // Re-enable colliders
                    foreach (var c in selfColliders) c.enabled = true;

                    return;
                }
                if (InputHelper.GetPointerUp())
                {
                    _isDraggingObject = false;
                    if (_selectedPlaced != null && _selectedPlaced.transform.position != _moveStartPos)
                    {
                        var cmd = new MoveObjectCommand(_selectedPlaced, _moveStartPos, _moveStartRot,
                            _selectedPlaced.transform.position, _selectedPlaced.transform.rotation);
                        UndoManager.Instance?.Record(cmd);
                    }
                    EventBus.Publish(new ObjectTransformedEvent { PlacedObject = _selectedPlaced });
                    return;
                }
            }

            // Tap on any object to select it (works in any tool mode)
            if (InputHelper.GetPointerDown())
            {
                // On mobile, skip object interaction when 2+ fingers are down (pinch/zoom)
                bool isMobile = Application.platform == RuntimePlatform.IPhonePlayer ||
                                Application.platform == RuntimePlatform.Android;
                if (isMobile && Input.touchCount >= 2) { /* let camera handle it */ }
                else
                {
                    Ray ray = _cam.ScreenPointToRay(InputHelper.GetPointerPosition());
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        var placed = hit.collider.GetComponentInParent<PlacedObject>();
                        if (placed != null)
                        {
                            // On mobile: first tap selects, drag only starts if object was already selected
                            bool wasAlreadySelected = (placed == _selectedPlaced);
                            SelectObject(placed);
                            // Don't start dragging in walk mode — no free cursor
                            bool inWalkMode = GameManager.Instance != null &&
                                             GameManager.Instance.CurrentTool == ToolMode.WalkMode;
                            if (!inWalkMode && (!isMobile || wasAlreadySelected))
                            {
                                _isDraggingObject = true;
                                _dragStarted = false;
                                _pointerDownPos = InputHelper.GetPointerPosition();
                                _dragOffset = placed.transform.position - hit.point;
                                _moveStartPos = placed.transform.position;
                                _moveStartRot = placed.transform.rotation;
                            }
                            return; // Don't run sand tools on this click
                        }
                        else
                        {
                            // Clicked anything that isn't an object — deselect
                            if (_selectedPlaced != null)
                                SelectObject(null);
                        }
                    }
                    else
                    {
                        // Clicked empty space — deselect
                        if (_selectedPlaced != null)
                            SelectObject(null);
                    }
                } // end mobile multi-touch check
            }

            var mode = GameManager.Instance.CurrentTool;

            switch (mode)
            {
                case ToolMode.ObjectPlace:
                    HandlePlacementMode();
                    break;
                case ToolMode.ObjectRotate:
                    HandleRotateMode();
                    break;
            }
        }

        private void HandlePlacementMode()
        {
            if (_selectedCatalogObject == null || _selectedCatalogObject.Prefab == null) return;

            Ray ray = _cam.ScreenPointToRay(InputHelper.GetPointerPosition());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f) && hit.collider.gameObject == _sandMesh.gameObject)
            {
                // Update ghost preview position
                if (_ghostPreview == null)
                    CreateGhostPreview();

                Vector3 pos = hit.point;
                pos.y = _sandMesh.SampleWorldHeight(pos);
                _ghostPreview.transform.position = pos;

                if (InputHelper.GetPointerDown())
                {
                    var cmd = new PlaceObjectCommand(this, _selectedCatalogObject, pos, Quaternion.identity, 1f);
                    UndoManager.Instance?.Execute(cmd);
                }
            }
        }

        private void HandleRotateMode()
        {
            if (_selectedPlaced == null) return;

            if (InputHelper.GetPointerDown())
            {
                _moveStartPos = _selectedPlaced.transform.position;
                _moveStartRot = _selectedPlaced.transform.rotation;
            }

            if (InputHelper.GetPointerHeld())
            {
                Vector2 delta = InputHelper.GetPointerDelta();
                var config = GameManager.Instance.Config;
                _selectedPlaced.transform.Rotate(Vector3.up, delta.x * config.ObjectRotationSpeed * Time.deltaTime, Space.World);
            }

            if (InputHelper.GetPointerUp())
            {
                if (_selectedPlaced != null && _selectedPlaced.transform.rotation != _moveStartRot)
                {
                    var cmd = new MoveObjectCommand(_selectedPlaced, _moveStartPos, _moveStartRot,
                        _selectedPlaced.transform.position, _selectedPlaced.transform.rotation);
                    UndoManager.Instance?.Record(cmd);
                }
                EventBus.Publish(new ObjectTransformedEvent { PlacedObject = _selectedPlaced });
            }
        }

        /// <summary>
        /// Handle keyboard shortcuts for copy/paste/delete operations.
        /// Works when keyboard is available (desktop/iPad with keyboard).
        /// </summary>
        private void HandleKeyboardShortcuts()
        {
            // Skip if UI element has focus (e.g., text input)
            if (InputHelper.IsPointerOverUI()) return;

            // Check if user is psychologist or observer (no edit permissions)
            bool isPsychologist = GameManager.Instance != null && GameManager.Instance.IsPsychologist;
            bool isObserver = GameManager.Instance != null && GameManager.Instance.IsObserver;
            if (isPsychologist || isObserver) return;

            // Detect Ctrl (Windows/Linux) or Cmd (Mac)
            bool modifier = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                           Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

            // Copy: Ctrl/Cmd+C
            if (modifier && Input.GetKeyDown(KeyCode.C))
            {
                if (_selectedPlaced != null && _selectedPlaced.ObjectData != null)
                {
                    _copiedObject = _selectedPlaced;
                    Debug.Log($"[ObjectPlacer] Copied object: {_copiedObject.ObjectData.DisplayName}");
                }
            }

            // Paste: Ctrl/Cmd+V
            if (modifier && Input.GetKeyDown(KeyCode.V))
            {
                if (_copiedObject != null && _copiedObject.ObjectData != null && _copiedObject.gameObject != null)
                {
                    // Place copy slightly offset from the original
                    Vector3 pastePos = _copiedObject.transform.position + new Vector3(0.5f, 0, 0.5f);

                    // Use raycast to place at correct sand height
                    if (_sandMesh != null)
                    {
                        float sandY = _sandMesh.SampleWorldHeight(pastePos);
                        float bottomOffset = GetObjectBottomOffset(_copiedObject);
                        pastePos.y = sandY + bottomOffset;
                    }

                    var cmd = new PlaceObjectCommand(
                        this,
                        _copiedObject.ObjectData,
                        pastePos,
                        _copiedObject.transform.rotation,
                        _copiedObject.transform.localScale.x / _copiedObject.ObjectData.Prefab.transform.localScale.x
                    );
                    UndoManager.Instance?.Execute(cmd);

                    Debug.Log($"[ObjectPlacer] Pasted object: {_copiedObject.ObjectData.DisplayName}");
                }
            }

            // Delete: Delete key or Backspace
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (_selectedPlaced != null && _selectedPlaced.ObjectData != null)
                {
                    string objectName = _selectedPlaced.ObjectData.DisplayName;
                    var cmd = new RemoveObjectCommand(this, _selectedPlaced);
                    UndoManager.Instance?.Execute(cmd);
                    Debug.Log($"[ObjectPlacer] Deleted object: {objectName}");
                }
            }
        }

        public PlacedObject PlaceObject(SandplayObject data, Vector3 position, Quaternion rotation, float scale, bool skipOffset = false)
        {
            if (data == null || data.Prefab == null) return null;

            GameObject go = Instantiate(data.Prefab, position, rotation);
            go.SetActive(true);
            // Preserve the template's pre-baked scale; multiply by the placement scale factor
            go.transform.localScale = data.Prefab.transform.localScale * scale;

            if (!skipOffset)
            {
                // Offset upward so the object sits on top of the sand, not inside it
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var r in renderers)
                        bounds.Encapsulate(r.bounds);
                    float bottomOffset = go.transform.position.y - bounds.min.y;
                    Vector3 pos = go.transform.position;
                    pos.y += bottomOffset;
                    go.transform.position = pos;
                }

                // Clamp so object edges stay inside sandbox walls
                go.transform.position = ClampToBounds(go.transform.position, go);
            }

            var placed = go.GetComponent<PlacedObject>();
            if (placed == null)
                placed = go.AddComponent<PlacedObject>();
            placed.ObjectData = data;

            // Ensure all renderers cast and receive shadows
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            // Ensure a collider on the ROOT for selection raycasting.
            // GLB imports may have child colliders that don't cover the full model,
            // so always add a fitted BoxCollider on the root.
            var rootCol = go.GetComponent<BoxCollider>();
            if (rootCol == null)
                rootCol = go.AddComponent<BoxCollider>();
            // Auto-fit to combined renderer bounds
            var collRenderers = go.GetComponentsInChildren<Renderer>();
            if (collRenderers.Length > 0)
            {
                Bounds bounds = collRenderers[0].bounds;
                foreach (var r in collRenderers)
                    bounds.Encapsulate(r.bounds);
                rootCol.center = go.transform.InverseTransformPoint(bounds.center);
                // Transform world-space size into local space
                rootCol.size = new Vector3(
                    bounds.size.x / go.transform.lossyScale.x,
                    bounds.size.y / go.transform.lossyScale.y,
                    bounds.size.z / go.transform.lossyScale.z);
            }

            _placedObjects.Add(placed);
            EventBus.Publish(new ObjectPlacedEvent { PlacedObject = placed });

            return placed;
        }

        /// <summary>
        /// Place a network-loaded GameObject (no SandplayObject ScriptableObject).
        /// The <paramref name="item"/> reference is stored on the resulting PlacedObject
        /// so the session serializer can record the API UUID.
        /// </summary>
        public PlacedObject PlaceNetworkObject(NetworkCatalogItem item, Vector3 position, Quaternion rotation, float scale, bool skipOffset = false)
        {
            if (item?.LoadedPrefab == null) return null;

            GameObject go = Instantiate(item.LoadedPrefab, position, rotation);
            go.SetActive(true);
            go.transform.localScale = item.LoadedPrefab.transform.localScale * scale;

            if (!skipOffset)
            {
                // Sit on top of the sand surface
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    float bottomOffset = go.transform.position.y - bounds.min.y;
                    Vector3 pos = go.transform.position;
                    pos.y += bottomOffset;
                    go.transform.position = pos;
                }
                go.transform.position = ClampToBounds(go.transform.position, go);
            }

            var placed = go.GetComponent<PlacedObject>();
            if (placed == null) placed = go.AddComponent<PlacedObject>();
            placed.NetworkItem = item;

            // Root collider for selection raycasting.
            // Use Unity-aware null check (not ??) to handle destroyed/stale components
            // that the GLB importer may have left behind.
            var rootCol = go.GetComponent<BoxCollider>();
            if (rootCol == null) rootCol = go.AddComponent<BoxCollider>();
            var collRenderers = go.GetComponentsInChildren<Renderer>();
            if (collRenderers.Length > 0 && rootCol != null)
            {
                Bounds b = collRenderers[0].bounds;
                foreach (var r in collRenderers) b.Encapsulate(r.bounds);
                rootCol.center = go.transform.InverseTransformPoint(b.center);
                rootCol.size = new Vector3(
                    b.size.x / go.transform.lossyScale.x,
                    b.size.y / go.transform.lossyScale.y,
                    b.size.z / go.transform.lossyScale.z);
            }

            _placedObjects.Add(placed);
            EventBus.Publish(new ObjectPlacedEvent { PlacedObject = placed });
            return placed;
        }

        public void RemoveObject(PlacedObject obj)
        {
            if (obj == null) return;
            if (_selectedPlaced == obj)
                SelectObject(null);
            _placedObjects.Remove(obj);
            EventBus.Publish(new ObjectRemovedEvent { PlacedObject = obj });

            // Disable colliders before destroying so settle raycasts ignore it
            foreach (var c in obj.GetComponentsInChildren<Collider>())
                c.enabled = false;
            Destroy(obj.gameObject);

            SettleFloatingObjects();
        }

        /// <summary>
        /// Re-settles any objects left floating after a removal.
        /// Processes lowest-Y first so cascading stacks resolve correctly.
        /// </summary>
        private void SettleFloatingObjects()
        {
            if (_sandMesh == null) return;

            var sorted = new List<PlacedObject>(_placedObjects);
            sorted.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

            foreach (var obj in sorted)
            {
                if (obj == null) continue;

                float sandY = _sandMesh.SampleWorldHeight(obj.transform.position);
                float bottomOffset = GetObjectBottomOffset(obj);
                float groundY = sandY + bottomOffset;

                // Only check objects whose bottom is above the sand surface
                if (obj.transform.position.y <= groundY + 0.01f) continue;

                // Raycast downward to see if still supported by another object
                var selfColliders = obj.GetComponentsInChildren<Collider>();
                foreach (var c in selfColliders) c.enabled = false;

                bool supported = false;
                Ray ray = new Ray(obj.transform.position, Vector3.down);
                foreach (var hit in Physics.RaycastAll(ray, obj.transform.position.y + 10f))
                {
                    var p = hit.collider.GetComponentInParent<PlacedObject>();
                    if (p != null && p != obj)
                    {
                        supported = true;
                        break;
                    }
                }

                foreach (var c in selfColliders) c.enabled = true;

                if (!supported)
                    SnapToGround(obj);
            }
        }

        public void RemoveSelected()
        {
            if (_selectedPlaced != null)
                RemoveObject(_selectedPlaced);
        }

        public void SelectObject(PlacedObject obj)
        {
            if (_selectedPlaced != null)
                _selectedPlaced.SetSelected(false);

            _selectedPlaced = obj;

            if (_selectedPlaced != null)
            {
                _selectedPlaced.SetSelected(true);
                // Deactivate sand tools when an object is selected.
                // Don't interrupt walk mode — selection is allowed while walking.
                if (GameManager.Instance != null && GameManager.Instance.CurrentTool != ToolMode.WalkMode)
                    GameManager.Instance.SetToolMode(ToolMode.ObjectSelect);
            }

            EventBus.Publish(new ObjectSelectedEvent { PlacedObject = _selectedPlaced });
        }

        /// <summary>
        /// Called by NetworkBootstrapper to show an object selection from another client.
        /// Does not send a network message (avoid loops).
        /// </summary>
        public void SelectNetworkObject(PlacedObject obj)
        {
            if (_selectedPlaced != null)
                _selectedPlaced.SetSelected(false);

            _selectedPlaced = obj;

            if (_selectedPlaced != null)
            {
                _selectedPlaced.SetSelected(true);
            }

            EventBus.Publish(new ObjectSelectedEvent { PlacedObject = _selectedPlaced });
        }

        /// <summary>
        /// Called by NetworkBootstrapper to clear network selection.
        /// </summary>
        public void DeselectNetworkSelection()
        {
            if (_selectedPlaced != null)
            {
                _selectedPlaced.SetSelected(false);
                _selectedPlaced = null;
                EventBus.Publish(new ObjectSelectedEvent { PlacedObject = null });
            }
        }

        private float GetObjectTopY(PlacedObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            float maxY = obj.transform.position.y + 0.3f;
            foreach (var r in renderers)
            {
                if (r.bounds.max.y > maxY)
                    maxY = r.bounds.max.y;
            }
            return maxY;
        }

        /// <summary>
        /// Move an object from the tray floor to a short distance above the sand.
        /// On release, the toolbar decides whether it should fall or remain in air.
        /// </summary>
        public void MoveObjectVertically(PlacedObject obj, float worldDeltaY)
        {
            if (obj == null || _sandMesh == null) return;

            float bottomOffset = GetObjectBottomOffset(obj);
            float sandY = _sandMesh.SampleWorldHeight(obj.transform.position);
            float minY = bottomOffset;
            float liftRange = GameManager.Instance != null && GameManager.Instance.Config != null
                ? Mathf.Max(1f, GameManager.Instance.Config.SandMaxHeight)
                : 2f;
            float maxY = sandY + bottomOffset + liftRange;

            Vector3 pos = obj.transform.position;
            pos.y = Mathf.Clamp(pos.y + worldDeltaY, minY, maxY);
            obj.transform.position = pos;
        }

        /// <summary>
        /// Drops an object onto the highest object beneath it, or onto the sand.
        /// Rotation is intentionally preserved so uneven sand does not unexpectedly tip models.
        /// </summary>
        public void DropObject(PlacedObject obj, Action onComplete = null)
        {
            if (obj == null || _sandMesh == null)
            {
                onComplete?.Invoke();
                return;
            }
            StartCoroutine(DropObjectRoutine(obj, onComplete));
        }

        private IEnumerator DropObjectRoutine(PlacedObject obj, Action onComplete)
        {
            float bottomOffset = GetObjectBottomOffset(obj);
            float landingY = _sandMesh.SampleWorldHeight(obj.transform.position) + bottomOffset;
            float currentBottom = obj.transform.position.y - bottomOffset;

            // Ignore the falling object's own colliders while finding support below it.
            var selfColliders = obj.GetComponentsInChildren<Collider>();
            foreach (var collider in selfColliders)
                collider.enabled = false;

            var rayOrigin = obj.transform.position + Vector3.up * 0.1f;
            foreach (var hit in Physics.RaycastAll(
                rayOrigin, Vector3.down, rayOrigin.y + 10f))
            {
                var support = hit.collider.GetComponentInParent<PlacedObject>();
                if (support == null || support == obj) continue;
                if (hit.point.y > currentBottom + 0.05f) continue;
                landingY = Mathf.Max(landingY, hit.point.y + bottomOffset);
            }

            foreach (var collider in selfColliders)
                collider.enabled = true;

            if (obj == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            Vector3 pos = obj.transform.position;
            if (pos.y <= landingY)
            {
                // The user intentionally pushed it into the sand; keep that depth.
                onComplete?.Invoke();
                yield break;
            }

            float fallSpeed = 0f;
            const float gravity = 18f;
            while (obj != null && obj.transform.position.y > landingY + 0.001f)
            {
                fallSpeed += gravity * Time.deltaTime;
                pos = obj.transform.position;
                pos.y = Mathf.Max(landingY, pos.y - fallSpeed * Time.deltaTime);
                obj.transform.position = pos;
                yield return null;
            }

            if (obj != null)
            {
                pos = obj.transform.position;
                pos.y = landingY;
                obj.transform.position = pos;
            }
            onComplete?.Invoke();
        }

        /// <summary>
        /// Returns the offset from the object's pivot to its bottom (always >= 0).
        /// Repositions a placed object so its bottom sits on the sand surface.
        /// </summary>
        public void SnapToGround(PlacedObject obj)
        {
            if (obj == null || _sandMesh == null) return;
            float sandY = _sandMesh.SampleWorldHeight(obj.transform.position);
            float bottomOffset = GetObjectBottomOffset(obj);
            Vector3 pos = obj.transform.position;
            pos.y = sandY + bottomOffset;
            obj.transform.position = pos;
        }

        /// <summary>
        /// Used to ensure the object's lowest point touches the floor/sand.
        /// </summary>
        private float GetObjectBottomOffset(PlacedObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
                return obj.transform.position.y - bounds.min.y;
            }
            return 0f;
        }

        /// <summary>
        /// Clamps a position so the object's renderer bounds stay inside the sandbox walls.
        /// </summary>
        private Vector3 ClampToBounds(Vector3 pos, GameObject obj)
        {
            var config = GameManager.Instance.Config;
            float halfW = config.SandboxWidth * 0.5f;
            float halfD = config.SandboxDepth * 0.5f;

            // Compute the object's extents relative to its pivot
            float extentX = 0f, extentZ = 0f;
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers)
                    b.Encapsulate(r.bounds);
                extentX = b.extents.x;
                extentZ = b.extents.z;
            }

            pos.x = Mathf.Clamp(pos.x, -halfW + extentX, halfW - extentX);
            pos.z = Mathf.Clamp(pos.z, -halfD + extentZ, halfD - extentZ);
            return pos;
        }

        public PlacedObject GetSelected() => _selectedPlaced;

        public void ClearAll()
        {
            SelectObject(null);
            foreach (var obj in _placedObjects.ToArray())
            {
                if (obj != null) Destroy(obj.gameObject);
            }
            _placedObjects.Clear();
        }

        private void OnCatalogObjectSelected(CatalogObjectSelectedEvent evt)
        {
            _selectedCatalogObject = evt.ObjectData;
            DestroyGhostPreview();
            if (_selectedCatalogObject != null)
                GameManager.Instance.SetToolMode(ToolMode.ObjectPlace);
        }

        private void OnToolModeChanged(ToolModeChangedEvent evt)
        {
            if (evt.NewMode != ToolMode.ObjectPlace)
            {
                _selectedCatalogObject = null;
                DestroyGhostPreview();
            }
        }

        private void CreateGhostPreview()
        {
            if (_selectedCatalogObject == null || _selectedCatalogObject.Prefab == null) return;
            _ghostPreview = Instantiate(_selectedCatalogObject.Prefab);
            _ghostPreview.SetActive(true);
            _ghostPreview.name = "GhostPreview";

            // Make translucent
            foreach (var r in _ghostPreview.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in r.materials)
                {
                    Color c = mat.color;
                    c.a = 0.5f;
                    mat.color = c;
                    mat.SetFloat("_Mode", 3); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
            }

            // Disable colliders on ghost
            foreach (var col in _ghostPreview.GetComponentsInChildren<Collider>())
                col.enabled = false;
        }

        private void DestroyGhostPreview()
        {
            if (_ghostPreview != null)
            {
                Destroy(_ghostPreview);
                _ghostPreview = null;
            }
        }
    }
}
