using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sandplay.Core;
using Sandplay.Data;
using Sandplay.Objects;
using Sandplay.Sand;

namespace Sandplay.UI
{
    /// <summary>
    /// Attach to a catalog item row. Supports both local SandplayObject (legacy)
    /// and NetworkCatalogItem (loaded from API).
    /// Vertical drags scroll the parent list; pulling an item out places it.
    /// </summary>
    public class CatalogDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IInitializePotentialDragHandler, IScrollHandler
    {
        public static bool IsDragging { get; private set; }
        private static CatalogDragHandler _dragOwner;
        private int _dragGeneration;
        private bool CanEdit => GameManager.Instance == null || !GameManager.Instance.IsSpectator;

        private void OnEnable() { EventBus.Subscribe<NetworkRoleAssignedEvent>(OnRoleChanged); }
        private void OnDisable()
        {
            EventBus.Unsubscribe<NetworkRoleAssignedEvent>(OnRoleChanged);
            CancelDrag();
        }
        private void OnRoleChanged(NetworkRoleAssignedEvent evt)
        {
            if (evt.Role != PlayerRole.Patient) CancelDrag();
        }
        public void CancelDrag()
        {
            if (_passingToScroll && _parentScroll != null && _activeEvent != null)
                _parentScroll.OnEndDrag(_activeEvent);
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _canPlace = false;
            _dragScale = 1;
            ResetDragState();
        }

        // Set one of these — not both
        public SandplayObject ObjectData { get; set; }
        public NetworkCatalogItem NetworkItem { get; set; }
        public Action OnDownloadStarted { get; set; }
        public Action<bool> OnDownloadFinished { get; set; }

        private GameObject _ghost;
        private UnityEngine.Camera _cam;
        private SandMesh _sand;
        private bool _canPlace;
        private float _dragScale = 1f;

        private ScrollRect _parentScroll;
        private bool _dragDecided;
        private bool _passingToScroll;
        private bool _objectDragStarted;
        private Vector2 _pressPos;
        private PointerEventData _activeEvent;

        private const float ScaleMin = 1f;
        private const float ScaleMax = 5f;
        private const float ScaleStep = 0.25f;
        private const float DecideThresholdPx = 12f;

        private void Awake()
        {
            _parentScroll = GetComponentInParent<ScrollRect>();
        }

        // ── Drag entry ────────────────────────────────────────────────────────

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            _parentScroll?.OnInitializePotentialDrag(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_parentScroll != null)
                ExecuteEvents.Execute(_parentScroll.gameObject, eventData, ExecuteEvents.scrollHandler);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanEdit) { CancelDrag(); return; }
            _dragGeneration++;
            _pressPos = eventData.position;
            _dragDecided = false;
            _passingToScroll = false;
            _objectDragStarted = false;
            _activeEvent = eventData;
            _canPlace = false;
            // Defer: vertical = scroll list, pull-out = place object.
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!CanEdit) { CancelDrag(); return; }
            _activeEvent = eventData;

            if (!_dragDecided)
            {
                Vector2 delta = eventData.position - _pressPos;
                bool outsideCatalog = IsOutsideCatalogPanel(eventData);

                if (!outsideCatalog && delta.magnitude < DecideThresholdPx)
                    return;

                _dragDecided = true;

                // Prefer scrolling when the gesture is mostly vertical and still
                // inside the catalog panel. Pulling sideways / out of the panel
                // starts an object-placement drag.
                bool preferScroll = _parentScroll != null
                    && !outsideCatalog
                    && Mathf.Abs(delta.y) >= Mathf.Abs(delta.x);

                if (preferScroll)
                {
                    _passingToScroll = true;
                    ExecuteEvents.Execute(_parentScroll.gameObject, eventData,
                        ExecuteEvents.beginDragHandler);
                }
                else
                {
                    BeginObjectDrag();
                }
            }

            if (_passingToScroll && _parentScroll != null)
            {
                ExecuteEvents.Execute(_parentScroll.gameObject, eventData,
                    ExecuteEvents.dragHandler);
                return;
            }

            if (_objectDragStarted && _ghost != null)
                UpdateGhostPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!CanEdit) { CancelDrag(); return; }
            if (_passingToScroll && _parentScroll != null)
            {
                ExecuteEvents.Execute(_parentScroll.gameObject, eventData,
                    ExecuteEvents.endDragHandler);
            }
            else if (_objectDragStarted)
            {
                FinishObjectDrag(eventData);
            }

            ResetDragState();
        }

        private void BeginObjectDrag()
        {
            if (_objectDragStarted || !CanEdit) return;
            if (_dragOwner != null && _dragOwner != this) _dragOwner.CancelDrag();
            _dragOwner = this;
            _objectDragStarted = true;
            IsDragging = true;
            _cam = UnityEngine.Camera.main;
            _sand = SandMesh.Instance;
            _canPlace = false;

            if (NetworkItem != null)
            {
                if (NetworkItem.LoadedPrefab != null)
                {
                    StartDragWithGO(NetworkItem.LoadedPrefab);
                }
                else
                {
                    // A drag is also an explicit request to use the object.
                    // Start the same cache/network load used by the download
                    // button; once it finishes, the ghost is created at the
                    // current pointer position and the drag can continue.
                    OnDownloadStarted?.Invoke();
                    StartCoroutine(LoadThenDrag(NetworkItem, _dragGeneration));
                }
                return;
            }

            if (ObjectData?.Prefab != null)
                StartDragWithGO(ObjectData.Prefab);
            else
            {
                IsDragging = false;
                _objectDragStarted = false;
            }
        }

        private void FinishObjectDrag(PointerEventData eventData)
        {
            IsDragging = false;
            if (_ghost == null) return;

            if (_canPlace && _cam != null && _sand != null)
            {
                Ray ray = _cam.ScreenPointToRay(eventData.position);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f) &&
                    hit.collider.gameObject == _sand.gameObject)
                {
                    Vector3 pos = hit.point;
                    pos.y = _sand.SampleWorldHeight(pos);
                    PlaceAtPosition(pos);
                }
            }

            Destroy(_ghost);
            _ghost = null;
            _dragScale = 1f;
        }

        private void ResetDragState()
        {
            _dragGeneration++;
            if (_dragOwner == this) { IsDragging = false; _dragOwner = null; }
            _dragDecided = false;
            _passingToScroll = false;
            _objectDragStarted = false;
            _activeEvent = null;
        }

        private bool IsOutsideCatalogPanel(PointerEventData eventData)
        {
            if (_parentScroll == null) return false;
            var panel = _parentScroll.transform as RectTransform;
            if (panel == null) return false;
            return !RectTransformUtility.RectangleContainsScreenPoint(
                panel, eventData.position, eventData.pressEventCamera);
        }

        private IEnumerator LoadThenDrag(NetworkCatalogItem item, int generation)
        {
            yield return StartCoroutine(
                NetworkCatalogLoader.PreloadGlb(this, item, null));
            bool loaded = item.LoadedPrefab != null;
            OnDownloadFinished?.Invoke(loaded);
            if (generation != _dragGeneration || _dragOwner != this || !CanEdit) yield break;
            if (loaded && IsDragging && _objectDragStarted)
                StartDragWithGO(item.LoadedPrefab);
            else
                IsDragging = false;
        }

        private void Update()
        {
            if (_ghost == null) return;
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                _dragScale = Mathf.Clamp(_dragScale + Mathf.Sign(scroll) * ScaleStep, ScaleMin, ScaleMax);
                ApplyScaleToGhost();
            }
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private void StartDragWithGO(GameObject prefab)
        {
            if (!CanEdit || !_objectDragStarted || _dragOwner != this) return;
            GameManager.Instance?.SetToolMode(ToolMode.ObjectPlace);

            _ghost = Instantiate(prefab);
            _ghost.SetActive(true);
            _ghost.name = "DragGhost";
            _ghost.transform.localScale = prefab.transform.localScale * _dragScale;

            SetGhostTint(true);
            foreach (var col in _ghost.GetComponentsInChildren<Collider>())
                col.enabled = false;

            if (_activeEvent != null)
                UpdateGhostPosition(_activeEvent);
        }

        private void ApplyScaleToGhost()
        {
            if (_ghost == null) return;
            GameObject src = NetworkItem?.LoadedPrefab ?? ObjectData?.Prefab;
            Vector3 baseScale = src != null ? src.transform.localScale : Vector3.one;
            _ghost.transform.localScale = baseScale * _dragScale;
        }

        private void PlaceAtPosition(Vector3 pos)
        {
            if (!CanEdit) return;
            var placer = FindAnyObjectByType<ObjectPlacer>();
            if (placer == null) return;

            if (NetworkItem?.LoadedPrefab != null)
            {
                placer.PlaceNetworkObject(NetworkItem, pos, Quaternion.identity, _dragScale);
            }
            else if (ObjectData != null)
            {
                var cmd = new PlaceObjectCommand(placer, ObjectData, pos, Quaternion.identity, _dragScale);
                UndoManager.Instance?.Execute(cmd);
            }
        }

        private void UpdateGhostPosition(PointerEventData eventData)
        {
            if (_cam == null || _sand == null || _ghost == null) return;

            Ray ray = _cam.ScreenPointToRay(eventData.position);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f) &&
                hit.collider.gameObject == _sand.gameObject)
            {
                Vector3 pos = hit.point;
                pos.y = _sand.SampleWorldHeight(pos);

                var renderers = _ghost.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    pos.y += _ghost.transform.position.y - bounds.min.y;
                }

                _ghost.transform.position = pos;
                _ghost.SetActive(true);

                if (!_canPlace) { _canPlace = true; SetGhostTint(true); }
            }
            else
            {
                _ghost.SetActive(true);
                if (_canPlace) { _canPlace = false; SetGhostTint(false); }
            }
        }

        private void SetGhostTint(bool ok)
        {
            if (_ghost == null) return;
            Color tint = ok
                ? new Color(0.3f, 1f, 0.3f, 0.5f)
                : new Color(1f, 0.3f, 0.3f, 0.5f);

            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in r.materials)
                {
                    mat.color = tint;
                    mat.SetFloat("_Mode", 3);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
            }
        }
    }
}
