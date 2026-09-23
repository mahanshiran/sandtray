using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Objects;
using Sandplay.Sand;

namespace Sandplay.Data
{
    public partial class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [SerializeField] private ObjectPlacer _objectPlacer;
        [SerializeField] private SandMesh _sandMesh;
        [SerializeField] private ObjectCatalog _catalog;

        private string _savePath;
        private Action _requireWorkspace;
        private void RequireWorkspace()
        {
            if (_requireWorkspace == null) throw new UnauthorizedAccessException("Record workspace is not initialized.");
            _requireWorkspace();
        }
        private bool WorkspaceIsCurrent
        {
            get
            {
                try { RequireWorkspace(); return true; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }
        private string SavePath { get { RequireWorkspace(); return _savePath; } }

        private LocalTableCapacity TableCapacity => new LocalTableCapacity(LocalAccountStorage.ExistingCapacityJournal());
        private void WriteTableRecord(string path, string json)
        {
            RequireWorkspace();
            var data = JsonUtility.FromJson<SessionData>(json);
            if (CapacityEnforced && string.IsNullOrEmpty(data?.LocalCapacityId) && !File.Exists(path) && !File.Exists(path + ".bak") && TableCapacity.Find(Path.GetFileName(path)) == null)
                throw new InvalidOperationException("New tables require a capacity reservation before saving.");
            if(CapacityEnforced && File.Exists(path)) AggregateCapacityStore.RequireSameReports(File.ReadAllText(path),json);
            // Scene durability does not depend on quota bookkeeping.
            LocalRecordFile.Write(path, json);
            if (CurrentBoardName == data?.SessionName) RefreshBoardPersistenceStatus(data);
        }

        public void Initialize(ObjectCatalog catalog, ObjectPlacer objectPlacer, SandMesh sandMesh)
        {
            _catalog = catalog;
            _objectPlacer = objectPlacer;
            _sandMesh = sandMesh;
        }

        private void Awake()
        {
            Instance = this;
            _requireWorkspace = LocalAccountStorage.CaptureGuard();
            _savePath = Path.Combine(LocalAccountStorage.Root, "Sessions");
            if (!Directory.Exists(SavePath))
                Directory.CreateDirectory(SavePath);
        }

        public SessionData CaptureSession(string sessionName)
        {
            RequireWorkspace();
            if (_sandMesh == null || _objectPlacer == null)
            {
                Debug.LogError("[Session] Cannot capture session: required scene references are missing.");
                return null;
            }

            var data = new SessionData
            {
                SessionName = sessionName,
                ClientId = CurrentClientId,
                OrganizationId = CurrentOrganizationId,
                OrganizationClientId = CurrentOrganizationClientId,
                CreatorUserId = CurrentCreatorUserId,
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

            // Keep unavailable catalog entries in the save, even when they cannot be
            // displayed offline. A background save must never silently remove them.
            data.PlacedObjects.AddRange(_unrestoredObjects);

            return data;
        }

        private SessionData CaptureValidatedSession(string sessionName)
        {
            var data = CaptureSession(sessionName);
            if (data == null) throw new InvalidOperationException("Board is not ready to save.");
            if (!data.HasFiniteObjectTransforms())
                throw new InvalidDataException("Object transforms are invalid; existing saves were preserved.");
            if (_sandMesh.Heightmap != null)
                foreach (float height in _sandMesh.Heightmap)
                    if (float.IsNaN(height) || float.IsInfinity(height))
                        throw new InvalidDataException("Terrain contains non-finite heights; existing saves were preserved.");

            return data;
        }

        public void SaveSession(string sessionName)
        {
            var data = CaptureValidatedSession(sessionName);

            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(SavePath, sanitized + ".json");

            // Preserve organizational metadata and reports on every scene save.
            if (File.Exists(filePath) || File.Exists(filePath + ".bak"))
            {
                var existing = ReadSavedData(sessionName, out _);
                if (existing == null) throw new InvalidDataException("Existing table could not be read.");
                data.Reports = existing.Reports ?? new List<AnalysisReport>();
                data.ClientId = existing.ClientId;
                data.OrganizationId = existing.OrganizationId;
                data.OrganizationClientId = existing.OrganizationClientId;
                data.CreatorUserId = existing.CreatorUserId;
                data.ClientAssignmentHistory = existing.ClientAssignmentHistory ?? new List<ClientAssignmentChange>();
                data.CreatedAt = existing.CreatedAt;
                data.TherapistNotes = existing.TherapistNotes;
                data.Archived = existing.Archived;
                data.BoardHistoryId = existing.BoardHistoryId;
                data.LocalCapacityId = existing.LocalCapacityId;
            }

            string json = JsonUtility.ToJson(data, true);
            WriteTableRecord(filePath, json);
            _boardReady = true;
            _lastSavedContent = SceneSignature(data);
            RecordLastLocalSave(data.ModifiedAt);
            RefreshBoardPersistenceStatus(data);

            EventBus.Publish(new SessionSavedEvent { SessionName = sessionName });
            Debug.Log($"Session saved: {filePath}");
        }

        public void LoadSession(string sessionName)
        {
            _boardReady = false;
            LastLoadRecovered = false;
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(SavePath, sanitized + ".json");
            if (!File.Exists(filePath) && !File.Exists(filePath + ".bak"))
            {
                Debug.LogError($"Session file not found: {filePath}");
                return;
            }

            try
            {
                var data = ReadSavedData(sessionName, out bool recovered);
                if (data == null)
                {
                    Debug.LogError($"[Session] Failed to parse session file: {filePath}");
                    return;
                }

                ApplySession(data);
                RecordLastLocalSave(data.ModifiedAt);
                _boardReady = _sandMesh != null && _objectPlacer != null;
                LastLoadRecovered = recovered;
                RefreshBoardPersistenceStatus(data);
                EventBus.Publish(new SessionLoadedEvent { SessionName = sessionName });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Session] Failed to load session '{sessionName}': {ex.Message}");
            }
        }

        public void ApplySession(SessionData data)
        {
            RequireWorkspace();
            if (data == null)
            {
                Debug.LogWarning("[Session] ApplySession called with null data.");
                return;
            }

            CurrentClientId = data.ClientId;
            CurrentOrganizationId = data.OrganizationId;
            CurrentOrganizationClientId = data.OrganizationClientId;
            CurrentCreatorUserId = data.CreatorUserId;

            if (_sandMesh == null)
            {
                Debug.LogError("[Session] _sandMesh is NULL — session cannot be applied.");
                return;
            }

            if (data.PlacedObjects == null)
                data.PlacedObjects = new List<PlacedObjectData>();

            PendingNetworkRestores = 0;
            StopAllCoroutines();
            _unrestoredObjects.Clear();

            // Restore board dimensions
            if (data.SandboxWidth > 0 && data.SandboxDepth > 0)
            {
                var config = GameManager.Instance?.Config;
                if (config != null)
                {
                    config.SandboxWidth = data.SandboxWidth;
                    config.SandboxDepth = data.SandboxDepth;
                }
                if (data.HeightmapResolution > 1 && data.HeightmapResolution != _sandMesh.Resolution)
                    _sandMesh.ReinitializeFromNetwork(data.SandboxWidth, data.SandboxDepth, data.HeightmapResolution);
                else
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
                        var placementItems = Sandplay.Objects.NetworkCatalogCache.LoadCachedForPlacement();
                        NetworkCatalogRegistry.Register(placementItems);
                        NetworkCatalogRegistry.RetainDependencies(cached);
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
                    _unrestoredObjects.Add(objData);
                    StartCoroutine(RestoreNetworkObjectCoroutine(objData));
                    continue;
                }

                // 1. Try local ScriptableObject catalog (built-in objects).
                var sandplayObj = _catalog != null ? _catalog.GetById(objData.ObjectId) : null;
                if (sandplayObj != null)
                {
                    // Serialize stores a relative multiplier, including very small sizes.
                    // Its magnitude cannot identify a legacy absolute-scale save.
                    float loadScale = objData.Scale;
                    Debug.Log($"[Session] Placing local '{objData.ObjectId}' at {objData.Position} scale={loadScale:F3}");
                    var placed = _objectPlacer.PlaceObject(
                        sandplayObj,
                        objData.Position,
                        Quaternion.Euler(objData.Rotation),
                        loadScale,
                        skipOffset: true, placementFeedback: false
                    );
                    if (placed == null) _unrestoredObjects.Add(objData);
                    localPlaced++;
                    continue;
                }

                // 2. Fallback to API catalog (objectId is a UUID or not-yet-loaded item).
                Debug.Log($"[Session] '{objData.ObjectId}' not in local catalog → trying API registry coroutine");
                networkQueued++;
                PendingNetworkRestores++;
                _unrestoredObjects.Add(objData);
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
                if (!WorkspaceIsCurrent) yield break;
                if (objData == null || string.IsNullOrEmpty(objData.ObjectId))
                {
                    Debug.LogWarning("[Session] Cannot restore network object with missing ObjectId.");
                    yield break;
                }

                Debug.Log($"[Session] RestoreNetworkObjectCoroutine START id={objData.ObjectId}");
                NetworkCatalogItem item = null;
                if (NetworkCatalogRegistry.TryGet(objData.ObjectId, out var registered))
                    item = registered;

                // New saves carry enough immutable metadata to restore a placed
                // object even when its source catalog is disabled and therefore
                // absent from the active API response.
                if (item == null && !string.IsNullOrEmpty(objData.NetworkModelUrl))
                {
                    item = new NetworkCatalogItem
                    {
                        id = objData.ObjectId,
                        catalog = objData.NetworkCatalogId,
                        display_name = string.IsNullOrEmpty(objData.NetworkDisplayName)
                            ? objData.ObjectId : objData.NetworkDisplayName,
                        model_url = objData.NetworkModelUrl,
                        model_hash = objData.NetworkModelHash,
                        thumbnail_url = objData.NetworkThumbnailUrl
                    };
                    Debug.Log($"[Session] Using saved catalog dependency metadata for '{objData.ObjectId}'.");
                }

                // Legacy saves have only an object ID. Wait up to 20 s for the
                // registry in that case, preserving the old fail-soft behavior.
                float elapsed = 0f;
                while (item == null && !NetworkCatalogRegistry.IsLoaded && elapsed < 20f)
                {
                    if (!WorkspaceIsCurrent) yield break;
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (!WorkspaceIsCurrent) yield break;
                if (item == null && !NetworkCatalogRegistry.IsLoaded)
                {
                    Debug.LogWarning($"[Session] Catalog not ready for '{objData.ObjectId}' — skipping object, board still loads.");
                    yield break;
                }
                Debug.Log($"[Session] Registry ready after {elapsed:F2}s for id={objData.ObjectId}");

                if (item == null && !NetworkCatalogRegistry.TryGet(objData.ObjectId, out item))
                {
                    Debug.LogWarning($"[Session] Skipping missing catalog object '{objData.ObjectId}' — board still loads.");
                    yield break;
                }

                // Download / load the GLB if needed (fail soft — board continues without it).
                Debug.Log($"[Session] EnsureLoaded for '{item.display_name}' (LoadedPrefab={(item.LoadedPrefab != null ? "ready" : "null")})");
                yield return StartCoroutine(NetworkCatalogRegistry.EnsureLoaded(this, item));
                if (!WorkspaceIsCurrent) yield break;
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
                    objData.Scale,
                    skipOffset: true, placementFeedback: false
                );
                if (placed != null) _unrestoredObjects.Remove(objData);
                Debug.Log($"[Session] PlaceNetworkObject returned {(placed != null ? "OK" : "NULL")} for '{item.display_name}' at {objData.Position} scale={objData.Scale:F3}");
            }
            finally
            {
                if (PendingNetworkRestores > 0)
                    PendingNetworkRestores--;
            }
        }

        public List<SessionListEntry> GetSavedSessions(bool includeArchived = false)
        {
            var entries = new List<SessionListEntry>();
            if (!Directory.Exists(SavePath)) return entries;

            var files = new HashSet<string>(Directory.GetFiles(SavePath, "*.json"));
            foreach (var backup in Directory.GetFiles(SavePath, "*.json.bak"))
                files.Add(backup.Substring(0, backup.Length - 4));
            foreach (string file in files)
            {
                try
                {
                    var data = ReadSavedData(Path.GetFileNameWithoutExtension(file), out _, false);
                    if (data == null || string.IsNullOrWhiteSpace(data.SessionName) || (!includeArchived && data.Archived)) continue;
                    entries.Add(new SessionListEntry
                    {
                        SessionName = data.SessionName,
                        Archived = data.Archived,
                        FilePath = file,
                        ModifiedAt = data.ModifiedAt,
                        ClientId = data.ClientId,
                        OrganizationId = data.OrganizationId,
                        OrganizationClientId = data.OrganizationClientId,
                        ReportCount = data.Reports?.FindAll(r => r != null && !r.Archived).Count ?? 0
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
            if(CapacityEnforced)throw new InvalidOperationException("Use the reserved table deletion flow.");
            DeleteSessionCore(sessionName);
        }
        private void DeleteSessionCore(string sessionName)
        {
            RequireWorkspace();
            var capacity = TableCapacity;
            var deletedData = ReadSavedData(sessionName, out _, false);
            bool localFirst = !string.IsNullOrEmpty(deletedData?.LocalCapacityId);
            string capacityOperation = null;
            if (localFirst) LocalAccountStorage.ExistingCapacityJournal()?.BeginLocalBoardDeletion(deletedData.LocalCapacityId);
            else capacityOperation = capacity.BeginDelete(SanitizeFileName(sessionName) + ".json");
            if (!string.IsNullOrEmpty(deletedData?.BoardHistoryId))
            {
                var history = HistoryPath(deletedData.BoardHistoryId);
                if (Directory.Exists(history)) Directory.Delete(history, true);
            }
            if (CurrentBoardName == sessionName)
            {
                EndAutoSave();
                CurrentBoardName = null;
                _boardReady = false;
            }
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(SavePath, sanitized + ".json");
            if (File.Exists(filePath))
                File.Delete(filePath);
            // Explicit deletion also removes recovery copies, so deleted boards do not return.
            if (File.Exists(filePath + ".bak")) File.Delete(filePath + ".bak");
            capacity.Deleted(capacityOperation);
            foreach (var report in deletedData?.Reports ?? new List<AnalysisReport>())
            {
                ScreenshotManager.DeleteAnalysisImage(report?.ReportId);
                AnalysisArchiveClient.Instance.Cancel(sessionName, report?.ReportId);
            }
            _nextBoardQuotaScan = 0;
        }

        public bool RenameSession(string oldName, string newName)
        {
            string oldSanitized = SanitizeFileName(oldName);
            string oldPath = Path.Combine(SavePath, oldSanitized + ".json");
            if (!File.Exists(oldPath) && !File.Exists(oldPath + ".bak")) return false;

            // Read, update name, write to new file
            var data = ReadSavedData(oldName, out _);
            if (data == null) return false;
            data.SessionName = newName;
            string newSanitized = SanitizeFileName(newName);
            string newPath = Path.Combine(SavePath, newSanitized + ".json");
            if (oldSanitized != newSanitized && (File.Exists(newPath) || File.Exists(newPath + ".bak"))) return false;
            data.ModifiedAt = DateTime.UtcNow.ToString("o");
            if (oldSanitized != newSanitized) TableCapacity.Rename(oldSanitized + ".json", newSanitized + ".json");
            WriteTableRecord(newPath, JsonUtility.ToJson(data, true));

            // Delete old file (unless same sanitized name)
            if (oldSanitized != newSanitized && File.Exists(oldPath))
                File.Delete(oldPath);
            if (oldSanitized != newSanitized && File.Exists(oldPath + ".bak")) File.Delete(oldPath + ".bak");

            // Rename thumbnail if it exists
            string thumbDir = Path.Combine(LocalAccountStorage.Root, "Thumbnails");
            string oldThumb = Path.Combine(thumbDir, oldSanitized + ".png");
            string newThumb = Path.Combine(thumbDir, newSanitized + ".png");
            if (File.Exists(oldThumb) && oldSanitized != newSanitized)
            {
                if (File.Exists(newThumb)) File.Delete(newThumb);
                File.Move(oldThumb, newThumb);
            }
            if (CurrentBoardName == oldName) CurrentBoardName = newName;
            return true;
        }

        /// <summary>The board currently open in the sandbox. Set by SceneBootstrapper on enter.</summary>
        public string CurrentBoardName { get; set; }
        public string CurrentClientId { get; set; }
        public string CurrentOrganizationId { get; set; }
        public string CurrentOrganizationClientId { get; set; }
        public int CurrentCreatorUserId { get; set; }

        public string GetAvailableSessionName(string proposed)
        {
            string name = SanitizeFileName((proposed ?? "").Trim());
            if (string.IsNullOrWhiteSpace(name)) name = "Table";
            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var op in LocalAccountStorage.ExistingCapacityJournal()?.Read() ?? Array.Empty<LocalCapacityOperation>())
                    if (op.State != "released")
                    {
                        reserved.Add(op.Path);
                        if (!string.IsNullOrEmpty(op.RenameTarget)) reserved.Add(op.RenameTarget);
                    }
            }
            catch (Exception) { /* Quota recovery must not prevent a local creation. */ }
            string result = name;
            for (int suffix = 2; File.Exists(Path.Combine(SavePath, result + ".json")) || File.Exists(Path.Combine(SavePath, result + ".json.bak")) ||
                reserved.Contains("Sessions/" + result + ".json"); suffix++)
                result = name + " (" + suffix + ")";
            return result;
        }

        /// <summary>Move only the association; retain table data, thumbnail and every report.</summary>
        public void AssignClient(string sessionName, string clientId)
        {
            var data = LoadSessionData(sessionName);
            if (data == null) throw new InvalidDataException("Table could not be read.");
            string previousClientId = data.ClientId ?? "";
            string nextClientId = clientId ?? "";
            if (previousClientId == nextClientId)
            {
                if (CurrentBoardName == sessionName) CurrentClientId = nextClientId;
                return;
            }
            data.ClientAssignmentHistory ??= new List<ClientAssignmentChange>();
            data.ClientAssignmentHistory.Add(new ClientAssignmentChange
            {
                ChangeId = Guid.NewGuid().ToString("N"),
                ChangedAt = DateTime.UtcNow.ToString("o"),
                PreviousClientId = previousClientId,
                ClientId = nextClientId
            });
            data.ClientId = clientId ?? "";
            data.ModifiedAt = DateTime.UtcNow.ToString("o");
            WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(sessionName) + ".json"),
                JsonUtility.ToJson(data, true));
            if (CurrentBoardName == sessionName) CurrentClientId = data.ClientId;
        }

        /// <summary>Network catalog objects still being downloaded/placed after a load.</summary>
        public int PendingNetworkRestores { get; private set; }

        public bool IsRestoringNetworkObjects => PendingNetworkRestores > 0;

        /// <summary>Load and return session data without applying it to the scene.</summary>
        public SessionData LoadSessionData(string sessionName)
        {
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(SavePath, sanitized + ".json");
            try { return ReadSavedData(sessionName, out _); }
            catch (Exception e) { Debug.LogWarning($"Failed to read session data: {e.Message}"); return null; }
        }

        /// <summary>Append an analysis report to the board's saved session file.</summary>
        public bool AppendAnalysisReport(string sessionName, AnalysisReport report)
        {
            if (CapacityEnforced) return false; // All new reports must use the reserved asynchronous flow.

            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(SavePath, sanitized + ".json");
            if (!File.Exists(filePath) && !File.Exists(filePath + ".bak")) { Debug.LogWarning($"Session file not found for report append: {filePath}"); return false; }
            try
            {
                var data = ReadSavedData(sessionName, out _);
                if (data.Reports == null) data.Reports = new List<AnalysisReport>();
                if (report == null || string.IsNullOrWhiteSpace(report.ResultText) || data.Reports.Exists(r => r != null && r.ReportId == report.ReportId)) return false;
                var author = BackendClient.Instance;
                if (author == null || !author.IsLoggedIn || author.UserId <= 0) return false;
                report.AuthorUserId = author.UserId;
                report.AuthorName = author.UserName;
                data.Reports.Add(report);
                WriteTableRecord(filePath, JsonUtility.ToJson(data, true));
                if(Application.isPlaying)
                {
                    ReportDeliveryClient.Instance.Queue(sessionName,report);
                    AnalysisArchiveClient.Instance.Queue(sessionName,report);
                }
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"Failed to append analysis report: {e.Message}"); return false; }
        }

        public static bool CanEditReport(AnalysisReport report)
        {
            var user = BackendClient.Instance;
            return report != null && report.AuthorUserId > 0 && user != null && user.IsLoggedIn && user.UserId == report.AuthorUserId;
        }

        /// <summary>Persist a text revision atomically; never reuse the previous text's cloud PDF.</summary>
        public AnalysisReport UpdateAnalysisReportText(string sessionName, string reportId, string expectedText, string text, ReportSections sections = null)
        {
            if (string.IsNullOrWhiteSpace(reportId) || string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("A report ID and non-empty report text are required.");
            var data = ReadSavedData(sessionName, out _);
            var report = data?.Reports?.Find(r => r != null && r.ReportId == reportId);
            if (report == null) throw new InvalidDataException("Report no longer exists.");
            if (!CanEditReport(report)) throw new UnauthorizedAccessException("Only the report author can edit this report.");
            if (!string.Equals(report.ResultText, expectedText, StringComparison.Ordinal))
                throw new InvalidOperationException("Report changed. Reopen it before editing.");
            if (string.Equals(report.ResultText, text, StringComparison.Ordinal)) return report;
            string now = DateTime.UtcNow.ToString("o");
            if (report.Revisions == null) report.Revisions = new List<ReportTextRevision>();
            report.Revisions.Add(new ReportTextRevision { ReplacedAt = now, ResultText = report.ResultText, Sections = report.Sections, EditedByUserId = BackendClient.Instance.UserId });
            report.Sections = sections;
            report.ResultText = text;
            report.EditedAt = now;
            report.ReviewedAt = null;
            report.SharedText = report.SharedReviewFingerprint = report.SharedReviewedAt = null;
            report.PdfReviewText = report.PdfReviewFingerprint = report.PdfReviewedAt = null;
            report.CloudId = null;
            data.ModifiedAt = now;
            WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(sessionName) + ".json"), JsonUtility.ToJson(data, true));
            if(Application.isPlaying) ReportDeliveryClient.Instance.Queue(sessionName,report);
            return report;
        }

        public bool IsCurrentReportText(string sessionName, string reportId, string expectedText)
        {
            try
            {
                var data = ReadSavedData(sessionName, out _, false);
                var report = data?.Reports?.Find(r => r != null && r.ReportId == reportId);
                return report != null && string.Equals(report.ResultText, expectedText, StringComparison.Ordinal);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public string ConfirmReportReview(string sessionName, string reportId, string expectedText)
        {
            var data = ReadSavedData(sessionName, out _);
            var report = data?.Reports?.Find(r => r != null && r.ReportId == reportId);
            if (report == null || !string.Equals(report.ResultText, expectedText, StringComparison.Ordinal))
                throw new InvalidOperationException("Report changed. Reopen and review it again.");
            report.ReviewedAt = DateTime.UtcNow.ToString("o");
            WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(sessionName) + ".json"), JsonUtility.ToJson(data, true));
            return report.ReviewedAt;
        }

        /// <summary>Remove a report entry by ReportId from the saved session file.</summary>
        public void DeleteAnalysisReport(string sessionName, string reportId)
        {
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(SavePath, sanitized + ".json");
            if (!File.Exists(filePath) && !File.Exists(filePath + ".bak")) return;
            try
            {
                var data = ReadSavedData(sessionName, out _);
                if (data.Reports == null) return;
                data.Reports.RemoveAll(r => r.ReportId == reportId);
                WriteTableRecord(filePath, JsonUtility.ToJson(data, true));
                ScreenshotManager.DeleteAnalysisImage(reportId);
                AnalysisArchiveClient.Instance.Cancel(sessionName, reportId);
            }
            catch (Exception e) { Debug.LogWarning($"Failed to delete analysis report: {e.Message}"); }
        }

        /// <summary>Persist a cloud record ID onto an existing local report entry.</summary>
        public bool TryAttachReportCloudId(string sessionName, string reportId, string expectedText, string cloudId)
        {
            if (string.IsNullOrWhiteSpace(reportId) || string.IsNullOrWhiteSpace(cloudId)) return false;
            var data = ReadSavedData(sessionName, out _);
            var report = data?.Reports?.Find(r => r != null && r.ReportId == reportId);
            if (report == null || !string.Equals(report.ResultText, expectedText, StringComparison.Ordinal)) return false;
            if (report.CloudId == cloudId) return true;
            report.CloudId = cloudId;
            WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(sessionName) + ".json"), JsonUtility.ToJson(data, true));
            return true;
        }

        /// <summary>Legacy caller compatibility; new exports must use the expected-text overload.</summary>
        public void AppendCloudId(string sessionName, string reportId, string cloudId)
        {
            string sanitized = SanitizeFileName(sessionName);
            string filePath = Path.Combine(SavePath, sanitized + ".json");
            if (!File.Exists(filePath) && !File.Exists(filePath + ".bak")) return;
            try
            {
                var data = ReadSavedData(sessionName, out _);
                if (data.Reports == null) return;
                foreach (var r in data.Reports)
                {
                    if (r.ReportId == reportId) { r.CloudId = cloudId; break; }
                }
                WriteTableRecord(filePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e) { Debug.LogWarning($"Failed to persist CloudId: {e.Message}"); }
        }

        internal static string SanitizeFileName(string name)
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
