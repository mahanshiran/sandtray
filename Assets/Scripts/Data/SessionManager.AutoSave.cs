using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.Data
{
    public struct BoardSaveStatusEvent { public string LocalizationKey; }

    public partial class SessionManager
    {
        public const float AutoSaveIntervalSeconds = 30f;
        private const float FailedSaveRetrySeconds = 5f;
        public bool AutoSaveActive { get; private set; }
        public bool LastLoadRecovered { get; private set; }
        public bool BoardReady => _boardReady;
        public bool HasOpenBoard => _objectPlacer != null && _objectPlacer.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(CurrentBoardName);
        private bool _boardReady;
        private float _nextAutoSave;
        public float SecondsUntilNextAutoSave => AutoSaveActive
            ? Mathf.Max(0, _nextAutoSave - Time.realtimeSinceStartup)
            : -1;
        public DateTimeOffset? LastLocalSaveAt { get; private set; }
        private string _lastSavedContent;
        private bool? _lastRemoteSaveMode;
        public string CurrentSaveStatusKey { get; private set; } = "save.inactive";
        private bool HostManagesSaving => NetworkBootstrapper.Instance != null &&
            NetworkBootstrapper.Instance.IsOnline && !NetworkBootstrapper.Instance.IsHost;
        private readonly List<PlacedObjectData> _unrestoredObjects = new List<PlacedObjectData>();

        public void PrepareBoard(string name, string clientId,
            string organizationId = null, string organizationClientId = null)
        {
            EndAutoSave();
            _boardReady = false;
            LastLoadRecovered = false;
            _lastSavedContent = null;
            LastLocalSaveAt = null;
            CurrentBoardName = name;
            CurrentClientId = clientId;
            CurrentOrganizationId = organizationId;
            CurrentOrganizationClientId = organizationClientId;
            CurrentCreatorUserId = BackendClient.Instance.IsLoggedIn ? BackendClient.Instance.UserId : 0;
            _openBoardCapacityId = null;
            CurrentBoardQuotaKey = "quota.none";
            CurrentBoardBackupKey = "backup.unknown";
        }

        public void BeginAutoSave()
        {
            AutoSaveActive = _boardReady && !string.IsNullOrWhiteSpace(CurrentBoardName);
            _nextAutoSave = Time.realtimeSinceStartup + AutoSaveIntervalSeconds;
            _lastRemoteSaveMode = HostManagesSaving;
            PublishSaveStatus(SaveModeStatus(AutoSaveActive, HostManagesSaving, LastLoadRecovered));
        }

        public static string SaveModeStatus(bool active, bool remote, bool recovered) =>
            remote ? "save.host_managed" : !active ? "save.inactive" : recovered ? "save.recovered" : "save.automatic";

        public void EndAutoSave()
        {
            AutoSaveActive = false;
            _lastRemoteSaveMode = null;
            CurrentSaveStatusKey = "save.inactive";
            StopAllCoroutines();
            PendingNetworkRestores = 0;
            _unrestoredObjects.Clear();
        }

        private bool CanSaveActiveBoard => WorkspaceIsCurrent && AutoSaveActive && _boardReady &&
            !string.IsNullOrWhiteSpace(CurrentBoardName) &&
            _objectPlacer != null && _objectPlacer.gameObject.activeInHierarchy &&
            !(NetworkBootstrapper.Instance != null && NetworkBootstrapper.Instance.IsOnline &&
              !NetworkBootstrapper.Instance.IsHost);

        private void Update()
        {
            AutoSaveTick(Time.realtimeSinceStartup);
            BoardQuotaTick(Time.realtimeSinceStartup);
        }

        // Unscaled clock: saves continue while a menu pauses simulation time.
        private void AutoSaveTick(float now)
        {
            bool remote = HostManagesSaving;
            if (_lastRemoteSaveMode != remote)
            {
                _lastRemoteSaveMode = remote;
                PublishSaveStatus(SaveModeStatus(AutoSaveActive, remote, LastLoadRecovered));
            }
            if (!CanSaveActiveBoard || now < _nextAutoSave) return;
            _nextAutoSave = now + AutoSaveIntervalSeconds;
            if (!IsRestoringNetworkObjects) TrySaveCurrentBoard();
        }

        public bool TrySaveCurrentBoard()
        {
            if (!CanSaveActiveBoard) return false;
            try
            {
                var snapshot = CaptureSession(CurrentBoardName);
                if (snapshot == null) throw new InvalidOperationException("Board is not ready.");
                string content = SceneSignature(snapshot);
                if (content == _lastSavedContent)
                {
                    // A failed change may have been undone back to the durable
                    // version. Clear stale failure UI without another write.
                    PublishSaveStatus("save.saved");
                    return true;
                }
                SaveSession(CurrentBoardName);
                _lastSavedContent = content;
                PublishSaveStatus("save.saved");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Session] Automatic save failed: " + ex.GetType().Name);
                PublishSaveStatus("save.failed");
                _nextAutoSave = Time.realtimeSinceStartup + FailedSaveRetrySeconds;
                return false;
            }
        }

        private static string SceneSignature(SessionData data)
        {
            // Reports/client notes are written independently; compare scene content only.
            return Hash128.Compute(JsonUtility.ToJson(new SessionData
            {
                SessionName = data.SessionName, SandboxWidth = data.SandboxWidth,
                SandboxDepth = data.SandboxDepth, HeightmapResolution = data.HeightmapResolution,
                HeightmapBase64 = data.HeightmapBase64, SplatmapBase64 = data.SplatmapBase64,
                PlacedObjects = data.PlacedObjects
            })).ToString();
        }

        private void RecordLastLocalSave(string modifiedAt)
        {
            LastLocalSaveAt = DateTimeOffset.TryParse(modifiedAt, out var saved)
                ? saved.ToUniversalTime()
                : DateTimeOffset.UtcNow;
        }

        private void OnApplicationPause(bool paused) { if (paused && CanSaveActiveBoard) TrySaveCurrentBoard(); }
        private void OnApplicationFocus(bool focused) { if (!focused && CanSaveActiveBoard) TrySaveCurrentBoard(); }
        private void OnApplicationQuit() { if (CanSaveActiveBoard) TrySaveCurrentBoard(); }

        private void PublishSaveStatus(string key)
        {
            CurrentSaveStatusKey = HostManagesSaving ? "save.host_managed" : key;
            EventBus.Publish(new BoardSaveStatusEvent { LocalizationKey = CurrentSaveStatusKey });
        }

        private SessionData ReadSavedData(string name, out bool recovered, bool repair = true)
        {
            recovered = false;
            string path = Path.Combine(SavePath, SanitizeFileName(name) + ".json");
            var data = ReadValidCopy(path, name);
            if (data != null)
            {
                if (repair) RecoverInterruptedAiReports(path, name, data);
                return data;
            }
            data = ReadValidCopy(path + ".bak", name);
            if (data == null) return null;
            recovered = true;
            if (repair)
            {
                // Retain damaged bytes for manual recovery; never overwrite the good backup
                // with a damaged primary during the next atomic save.
                if (File.Exists(path)) File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                WriteTableRecord(path, JsonUtility.ToJson(data, true));
            }
            return data;
        }

        /// <summary>
        /// Older AI saves preserved the completed paid result as an interrupted draft
        /// when capacity reconciliation was offline. Recover only missing AI reports;
        /// never replace the current board scene, notes, assignments, or newer reports.
        /// </summary>
        private void RecoverInterruptedAiReports(string path, string name, SessionData current)
        {
            try
            {
                if (current == null || !File.Exists(path)) return;
                string[] candidates = Directory.GetFiles(Path.GetDirectoryName(path),
                    Path.GetFileName(path) + ".interrupted-*");
                if (candidates.Length == 0) return;
                current.Reports ??= new List<AnalysisReport>();
                bool changed = false;
                var usedCandidates = new List<string>();
                foreach (string candidatePath in candidates)
                {
                    if (candidatePath.EndsWith(".bak", StringComparison.Ordinal) ||
                        candidatePath.EndsWith(".write-lock", StringComparison.Ordinal)) continue;
                    var candidate = ReadValidCopy(candidatePath, name);
                    if (candidate == null || string.IsNullOrWhiteSpace(candidate.BoardHistoryId) ||
                        candidate.BoardHistoryId != current.BoardHistoryId) continue;
                    bool used = false;
                    foreach (var report in candidate.Reports ?? new List<AnalysisReport>())
                    {
                        if (report == null || report.Source != "ai" ||
                            string.IsNullOrWhiteSpace(report.ReportId) ||
                            string.IsNullOrWhiteSpace(report.ResultText)) continue;
                        var existing = current.Reports.Find(item => item != null && item.ReportId == report.ReportId);
                        if (existing != null) continue;
                        current.Reports.Add(report);
                        changed = used = true;
                    }
                    if (used) usedCandidates.Add(candidatePath);
                }
                if (!changed) return;
                // This is recovery of already-generated content, not creation of a new
                // result. Capacity reconciliation will resume independently.
                TableCapacity.Write(path, JsonUtility.ToJson(current, true));
                foreach (string candidate in usedCandidates)
                {
                    string archived = candidate + ".recovered-ai";
                    if (!File.Exists(archived)) File.Move(candidate, archived);
                }
                Debug.Log("[Reports] Recovered interrupted AI reflection history for '" + name + "'.");
            }
            catch (Exception ex)
            {
                // Recovery must never make an otherwise valid board unreadable.
                Debug.LogWarning("[Reports] Interrupted AI reflection recovery is pending: " + ex.Message);
            }
        }

        private static SessionData ReadValidCopy(string path, string name)
        {
            if (!File.Exists(path)) return null;
            // I/O failures (permissions/disk errors) must surface, not be mistaken for corruption.
            string json = File.ReadAllText(path);
            try
            {
                var data = JsonUtility.FromJson<SessionData>(json);
                if (data == null || string.IsNullOrWhiteSpace(data.SessionName) ||
                    SanitizeFileName(data.SessionName) != SanitizeFileName(name) || data.PlacedObjects == null)
                    return null;
                if (!data.HasFiniteObjectTransforms()) return null;
                if (!string.IsNullOrEmpty(data.HeightmapBase64))
                {
                    var bytes = Convert.FromBase64String(data.HeightmapBase64);
                    if (bytes.Length == 0 || bytes.Length % 4 != 0 ||
                        (data.HeightmapResolution > 0 && (long)data.HeightmapResolution * data.HeightmapResolution * 4 != bytes.Length)) return null;
                    for (int offset = 0; offset < bytes.Length; offset += 4)
                    {
                        float height = BitConverter.ToSingle(bytes, offset);
                        if (float.IsNaN(height) || float.IsInfinity(height)) return null;
                    }
                }
                if (!string.IsNullOrEmpty(data.SplatmapBase64))
                {
                    int length = Convert.FromBase64String(data.SplatmapBase64).Length;
                    int pixels = Sandplay.Sand.SandMaterialController.SplatResolution * Sandplay.Sand.SandMaterialController.SplatResolution;
                    if (length != pixels * 4 && length != pixels * 8) return null;
                }
                return data;
            }
            catch (ArgumentException) { return null; }
            catch (FormatException) { return null; }
        }
    }
}
