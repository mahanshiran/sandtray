using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Objects;
using Sandplay.Sand;

namespace Sandplay.Data
{
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [SerializeField] private ObjectPlacer _objectPlacer;
        [SerializeField] private SandMesh _sandMesh;
        [SerializeField] private ObjectCatalog _catalog;

        private string _savePath;

        public void Initialize(ObjectCatalog catalog, ObjectPlacer objectPlacer, SandMesh sandMesh)
        {
            _catalog = catalog;
            _objectPlacer = objectPlacer;
            _sandMesh = sandMesh;
        }

        private void Awake()
        {
            Instance = this;
            _savePath = Path.Combine(Application.persistentDataPath, "Sessions");
            if (!Directory.Exists(_savePath))
                Directory.CreateDirectory(_savePath);
        }

        public SessionData CaptureSession(string sessionName)
        {
            if (_sandMesh == null || _objectPlacer == null)
            {
                Debug.LogError("[Session] Cannot capture session: required scene references are missing.");
                return null;
            }

            var data = new SessionData
            {
                SessionName = sessionName,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                ModifiedAt = DateTime.UtcNow.ToString("o"),
                SandboxWidth = _sandMesh.Width,
                SandboxDepth = _sandMesh.Depth,
                HeightmapResolution = _sandMesh.Resolution
            };

            data.EncodeHeightmap(_sandMesh.Heightmap);
            data.EncodeSplatmap();

            foreach (var obj in _objectPlacer.PlacedObjects)
            {
                if (obj != null)
                    data.PlacedObjects.Add(obj.Serialize());
            }

            return data;
        }

        public void SaveSession(string sessionName)
        {
            var data = CaptureSession(sessionName);
            if (data == null) return;

            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(_savePath, sanitized + ".json");

            // Preserve any reports already appended to this file so they aren't wiped
            if (File.Exists(filePath))
            {
                try
                {
                    var existing = JsonUtility.FromJson<SessionData>(File.ReadAllText(filePath));
                    if (existing?.Reports != null && existing.Reports.Count > 0)
                        data.Reports = existing.Reports;
                }
                catch { /* leave Reports empty on corrupt file */ }
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);

            EventBus.Publish(new SessionSavedEvent { SessionName = sessionName });
            Debug.Log($"Session saved: {filePath}");
        }

        public void LoadSession(string sessionName)
        {
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(_savePath, sanitized + ".json");
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Session file not found: {filePath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonUtility.FromJson<SessionData>(json);
                if (data == null)
                {
                    Debug.LogError($"[Session] Failed to parse session file: {filePath}");
                    return;
                }

                ApplySession(data);
                EventBus.Publish(new SessionLoadedEvent { SessionName = sessionName });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Session] Failed to load session '{sessionName}': {ex.Message}");
            }
        }

        public void ApplySession(SessionData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[Session] ApplySession called with null data.");
                return;
            }

            if (_sandMesh == null)
            {
                Debug.LogError("[Session] _sandMesh is NULL — session cannot be applied.");
                return;
            }

            if (data.PlacedObjects == null)
                data.PlacedObjects = new List<PlacedObjectData>();

            PendingNetworkRestores = 0;

            // Restore board dimensions
            if (data.SandboxWidth > 0 && data.SandboxDepth > 0)
            {
                var config = GameManager.Instance?.Config;
                if (config != null)
                {
                    config.SandboxWidth = data.SandboxWidth;
                    config.SandboxDepth = data.SandboxDepth;
                }
                _sandMesh.Reinitialize(data.SandboxWidth, data.SandboxDepth);
                var frame = FindAnyObjectByType<SandboxFrame>();
                frame?.Rebuild();
                var cam = FindAnyObjectByType<Sandplay.Camera.SandboxCamera>();
                cam?.FitToBoard();
                // Notify bootstrapper to update material references
                var bootstrapper = FindAnyObjectByType<SceneBootstrapper>();
                if (bootstrapper != null && frame != null)
                    bootstrapper.UpdateMaterialReferences(frame);
            }

            // Restore heightmap
            float[] heightmap = data.DecodeHeightmap();
            if (heightmap != null)
                _sandMesh.SetHeightmap(heightmap);

            // Restore splatmap paint
            data.DecodeSplatmap();

            // Clear and restore objects
            if (_objectPlacer == null) { Debug.LogError("[Session] _objectPlacer is NULL — objects cannot be restored!"); return; }
            _objectPlacer.ClearAll();

            // Prime the network catalog registry from the on-disk cache BEFORE iterating.
            // Otherwise, on a cold start with no cache yet primed, every network UUID object
            // falls into the 20-second async wait inside RestoreNetworkObjectCoroutine and
            // can be silently dropped if the API fetch is slow or offline.
            if (!NetworkCatalogRegistry.IsLoaded)
            {
                try
                {
                    var cached = Sandplay.Objects.NetworkCatalogCache.LoadCached();
                    if (cached != null && cached.Length > 0)
                    {
                        NetworkCatalogRegistry.Register(cached);
                        Debug.Log($"[Session] Primed NetworkCatalogRegistry from disk cache ({cached.Length} items) before restore.");
                    }
                    else
                    {
                        Debug.LogWarning("[Session] NetworkCatalogRegistry not loaded and no on-disk cache available. Network items will wait for live API fetch.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Session] Failed to prime registry from cache: {ex.Message}");
                }
            }

            int localPlaced = 0, networkQueued = 0;
            Debug.Log($"[Session] Restoring {data.PlacedObjects.Count} objects. _catalog={((_catalog == null) ? "NULL" : $"OK ({_catalog.Objects.Count} items)")} registryLoaded={NetworkCatalogRegistry.IsLoaded}");
            foreach (var objData in data.PlacedObjects)
            {
                if (objData == null || string.IsNullOrEmpty(objData.ObjectId))
                {
                    Debug.LogWarning("[Session] Skipping saved object with missing ObjectId.");
                    continue;
                }

                // If the network catalog is already loaded, prioritize it.
                // This handles cases where a saved board contains API objects that also exist
                // (potentially with bad scaling) in the local Resources-based catalog.
                if (NetworkCatalogRegistry.IsLoaded && NetworkCatalogRegistry.TryGet(objData.ObjectId, out _))
                {
                    Debug.Log($"[Session] '{objData.ObjectId}' found in pre-loaded network registry → using coroutine");
                    networkQueued++;
                    PendingNetworkRestores++;
                    StartCoroutine(RestoreNetworkObjectCoroutine(objData));
                    continue;
                }

                // 1. Try local ScriptableObject catalog (built-in objects).
                var sandplayObj = _catalog != null ? _catalog.GetById(objData.ObjectId) : null;
                if (sandplayObj != null)
                {
                    // Migration: old saves stored absolute localScale.x (e.g. 0.003)
                    // New saves store relative scale (typically 0.5 - 3.0).
                    float loadScale = objData.Scale;
                    if (sandplayObj.Prefab != null)
                    {
                        float tplScale = sandplayObj.Prefab.transform.localScale.x;
                        if (tplScale > 0f && tplScale < 0.1f && loadScale > 0f && loadScale < 0.1f)
                            loadScale /= tplScale;
                    }
                    Debug.Log($"[Session] Placing local '{objData.ObjectId}' at {objData.Position} scale={loadScale:F3}");
                    _objectPlacer.PlaceObject(
                        sandplayObj,
                        objData.Position,
                        Quaternion.Euler(objData.Rotation),
                        loadScale,
                        skipOffset: true
                    );
                    localPlaced++;
                    continue;
                }

                // 2. Fallback to API catalog (objectId is a UUID or not-yet-loaded item).
                Debug.Log($"[Session] '{objData.ObjectId}' not in local catalog → trying API registry coroutine");
                networkQueued++;
                PendingNetworkRestores++;
                StartCoroutine(RestoreNetworkObjectCoroutine(objData));
            }
            Debug.Log($"[Session] Restore dispatch complete: {localPlaced} placed locally, {networkQueued} awaiting network catalog (async).");
        }

        /// <summary>
        /// Restore an API catalog object: wait for the registry, then for the GLB
        /// to download/cache, then place it on the sand.
        /// </summary>
        private System.Collections.IEnumerator RestoreNetworkObjectCoroutine(PlacedObjectData objData)
        {
            try
            {
                if (objData == null || string.IsNullOrEmpty(objData.ObjectId))
                {
                    Debug.LogWarning("[Session] Cannot restore network object with missing ObjectId.");
                    yield break;
                }

                Debug.Log($"[Session] RestoreNetworkObjectCoroutine START id={objData.ObjectId}");
                // Wait up to 20 s for the registry to be populated by SceneBootstrapper.
                float elapsed = 0f;
                while (!NetworkCatalogRegistry.IsLoaded && elapsed < 20f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (!NetworkCatalogRegistry.IsLoaded)
                {
                    Debug.LogWarning($"[Session] Catalog not ready for '{objData.ObjectId}' — skipping object, board still loads.");
                    yield break;
                }
                Debug.Log($"[Session] Registry ready after {elapsed:F2}s for id={objData.ObjectId}");

                if (!NetworkCatalogRegistry.TryGet(objData.ObjectId, out var item))
                {
                    Debug.LogWarning($"[Session] Skipping missing catalog object '{objData.ObjectId}' — board still loads.");
                    yield break;
                }

                // Download / load the GLB if needed (fail soft — board continues without it).
                Debug.Log($"[Session] EnsureLoaded for '{item.display_name}' (LoadedPrefab={(item.LoadedPrefab != null ? "ready" : "null")})");
                yield return StartCoroutine(NetworkCatalogRegistry.EnsureLoaded(this, item));
                Debug.Log($"[Session] EnsureLoaded done for '{item.display_name}' (LoadedPrefab={(item.LoadedPrefab != null ? "ready" : "STILL NULL")})");

                if (item.LoadedPrefab == null)
                {
                    Debug.LogWarning($"[Session] Could not load '{item.display_name}' — skipping object, board still loads.");
                    yield break;
                }

                if (_objectPlacer == null)
                {
                    Debug.LogWarning($"[Session] _objectPlacer is null when trying to place '{item.display_name}' — looking up");
                    _objectPlacer = FindAnyObjectByType<ObjectPlacer>();
                    if (_objectPlacer == null)
                    {
                        Debug.LogError($"[Session] No ObjectPlacer in scene — cannot place '{item.display_name}'");
                        yield break;
                    }
                }

                var placed = _objectPlacer.PlaceNetworkObject(
                    item,
                    objData.Position,
                    Quaternion.Euler(objData.Rotation),
                    NormalizeNetworkScale(objData.Scale, item),
                    skipOffset: true
                );
                Debug.Log($"[Session] PlaceNetworkObject returned {(placed != null ? "OK" : "NULL")} for '{item.display_name}' at {objData.Position} scale={objData.Scale:F3}");
            }
            finally
            {
                if (PendingNetworkRestores > 0)
                    PendingNetworkRestores--;
            }
        }

        /// <summary>
        /// Migration helper: older saves stored the absolute transform.localScale.x for network
        /// items, which causes objects to shrink to invisibility on every save/load cycle because
        /// PlaceNetworkObject re-multiplies by LoadedPrefab.localScale. Detect that case and
        /// convert back to a relative multiplier.
        /// </summary>
        private static float NormalizeNetworkScale(float savedScale, NetworkCatalogItem item)
        {
            if (item?.LoadedPrefab == null) return savedScale;
            float tplScale = item.LoadedPrefab.transform.localScale.x;
            if (tplScale <= 0f) return savedScale;

            // If saved scale is in the same tiny range as the template, it was saved as absolute.
            // Convert to relative so PlaceNetworkObject's multiplier produces the original size.
            if (savedScale > 0f && savedScale < tplScale * 5f)
                return savedScale / tplScale;
            return savedScale;
        }

        public List<SessionListEntry> GetSavedSessions()
        {
            var entries = new List<SessionListEntry>();
            if (!Directory.Exists(_savePath)) return entries;

            foreach (string file in Directory.GetFiles(_savePath, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var data = JsonUtility.FromJson<SessionData>(json);
                    entries.Add(new SessionListEntry
                    {
                        SessionName = data.SessionName,
                        FilePath = file,
                        ModifiedAt = data.ModifiedAt
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to read session file: {file} - {e.Message}");
                }
            }

            return entries;
        }

        public void DeleteSession(string sessionName)
        {
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(_savePath, sanitized + ".json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        public void RenameSession(string oldName, string newName)
        {
            string oldSanitized = SanitizeFileName(oldName);
            string oldPath = Path.Combine(_savePath, oldSanitized + ".json");
            if (!File.Exists(oldPath)) return;

            // Read, update name, write to new file
            string json = File.ReadAllText(oldPath);
            var data = JsonUtility.FromJson<SessionData>(json);
            data.SessionName = newName;
            string newSanitized = SanitizeFileName(newName);
            string newPath = Path.Combine(_savePath, newSanitized + ".json");
            File.WriteAllText(newPath, JsonUtility.ToJson(data, true));

            // Delete old file (unless same sanitized name)
            if (oldSanitized != newSanitized && File.Exists(oldPath))
                File.Delete(oldPath);

            // Rename thumbnail if it exists
            string thumbDir = Path.Combine(Application.persistentDataPath, "Thumbnails");
            string oldThumb = Path.Combine(thumbDir, oldSanitized + ".png");
            string newThumb = Path.Combine(thumbDir, newSanitized + ".png");
            if (File.Exists(oldThumb) && oldSanitized != newSanitized)
            {
                if (File.Exists(newThumb)) File.Delete(newThumb);
                File.Move(oldThumb, newThumb);
            }
        }

        /// <summary>The board currently open in the sandbox. Set by SceneBootstrapper on enter.</summary>
        public string CurrentBoardName { get; set; }

        /// <summary>Network catalog objects still being downloaded/placed after a load.</summary>
        public int PendingNetworkRestores { get; private set; }

        public bool IsRestoringNetworkObjects => PendingNetworkRestores > 0;

        /// <summary>Load and return session data without applying it to the scene.</summary>
        public SessionData LoadSessionData(string sessionName)
        {
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(_savePath, sanitized + ".json");
            if (!File.Exists(filePath)) return null;
            try { return JsonUtility.FromJson<SessionData>(File.ReadAllText(filePath)); }
            catch (Exception e) { Debug.LogWarning($"Failed to read session data: {e.Message}"); return null; }
        }

        /// <summary>Append an analysis report to the board's saved session file.</summary>
        public void AppendAnalysisReport(string sessionName, AnalysisReport report)
        {
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(_savePath, sanitized + ".json");
            if (!File.Exists(filePath)) { Debug.LogWarning($"Session file not found for report append: {filePath}"); return; }
            try
            {
                var data = JsonUtility.FromJson<SessionData>(File.ReadAllText(filePath));
                if (data.Reports == null) data.Reports = new List<AnalysisReport>();
                data.Reports.Add(report);
                File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e) { Debug.LogWarning($"Failed to append analysis report: {e.Message}"); }
        }

        /// <summary>Remove a report entry by ReportId from the saved session file.</summary>
        public void DeleteAnalysisReport(string sessionName, string reportId)
        {
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(_savePath, sanitized + ".json");
            if (!File.Exists(filePath)) return;
            try
            {
                var data = JsonUtility.FromJson<SessionData>(File.ReadAllText(filePath));
                if (data.Reports == null) return;
                data.Reports.RemoveAll(r => r.ReportId == reportId);
                File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e) { Debug.LogWarning($"Failed to delete analysis report: {e.Message}"); }
        }

        /// <summary>Persist a cloud record ID onto an existing local report entry.</summary>
        public void AppendCloudId(string sessionName, string reportId, string cloudId)
        {
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(_savePath, sanitized + ".json");
            if (!File.Exists(filePath)) return;
            try
            {
                var data = JsonUtility.FromJson<SessionData>(File.ReadAllText(filePath));
                if (data.Reports == null) return;
                foreach (var r in data.Reports)
                {
                    if (r.ReportId == reportId) { r.CloudId = cloudId; break; }
                }
                File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e) { Debug.LogWarning($"Failed to persist CloudId: {e.Message}"); }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
