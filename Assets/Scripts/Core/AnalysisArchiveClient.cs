using System;
using System.Collections.Generic;
using System.Linq;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Core
{
    [Serializable] internal sealed class PendingAnalysisArchive { public string board, reportId; }
    [Serializable] internal sealed class AnalysisArchiveQueue { public List<PendingAnalysisArchive> items = new(); }

    /// <summary>
    /// Durable, idempotent cloud backup queue for paid AI reflections. Text is
    /// already stored in the board file; the high-resolution image is its local
    /// sidecar. A network failure therefore never discards either asset.
    /// </summary>
    public sealed class AnalysisArchiveClient : MonoBehaviour
    {
        private static AnalysisArchiveClient _instance;
        public static AnalysisArchiveClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("AnalysisArchiveClient").AddComponent<AnalysisArchiveClient>();
                    if (Application.isPlaying) DontDestroyOnLoad(_instance.gameObject);
                }
                return _instance;
            }
        }

        private AnalysisArchiveQueue _queue = new();
        private int _user;
        private int _epoch = -1;
        private bool _recoveryPending;
        private string _token = "";
        private bool _sending;
        private float _nextSend;

        private string QueueKey(int user) => "analysis_archive_queue_" + user;

        public void EnsureAccount()
        {
            var backend = BackendClient.Instance;
            int user = backend.IsLoggedIn ? backend.UserId : 0;
            if (_user != user || _token != backend.AccessToken || _epoch != LocalAccountStorage.Epoch)
            {
                _user = user;
                _token = backend.AccessToken;
                _epoch = LocalAccountStorage.Epoch;
                _sending = false;
                _nextSend = 0;
                _recoveryPending = true;
                try { _queue = JsonUtility.FromJson<AnalysisArchiveQueue>(PlayerPrefs.GetString(QueueKey(user), "{}")) ?? new AnalysisArchiveQueue(); }
                catch { _queue = new AnalysisArchiveQueue(); }
                _queue.items ??= new List<PendingAnalysisArchive>();
            }
            // /me can resolve before the old scene has been replaced. Keep recovery
            // pending until SessionManager belongs to the newly initialized workspace.
            if (!_recoveryPending || !WorkspaceReady) return;
            RecoverUnbackedReports();
            _recoveryPending = false;
            SaveQueue();
        }

        private bool WorkspaceReady => _user > 0 && !LocalAccountStorage.RequiresRestart &&
            SessionManager.Instance != null && SessionManager.Instance.WorkspaceIsCurrent;

        private void RecoverUnbackedReports()
        {
            if (!WorkspaceReady) return;
            foreach (var board in SessionManager.Instance.GetSavedSessions(includeArchived: true))
            {
                SessionData data;
                try { data = SessionManager.Instance.LoadSessionData(board.SessionName); }
                catch { continue; }
                foreach (var report in data?.Reports ?? new List<AnalysisReport>())
                {
                    if (report == null || report.Source != "ai" || report.AuthorUserId != _user ||
                        !string.IsNullOrWhiteSpace(report.CloudId) || string.IsNullOrWhiteSpace(report.ReportId)) continue;
                    if (!_queue.items.Any(item => item.board == board.SessionName && item.reportId == report.ReportId))
                        _queue.items.Add(new PendingAnalysisArchive { board = board.SessionName, reportId = report.ReportId });
                }
            }
        }

        public void Queue(string board, AnalysisReport report)
        {
            EnsureAccount();
            if (!WorkspaceReady || string.IsNullOrWhiteSpace(board) || report == null ||
                report.Source != "ai" || string.IsNullOrWhiteSpace(report.ReportId) ||
                !string.IsNullOrWhiteSpace(report.CloudId)) return;
            if (!_queue.items.Any(item => item.board == board && item.reportId == report.ReportId))
                _queue.items.Add(new PendingAnalysisArchive { board = board, reportId = report.ReportId });
            SaveQueue();
            _nextSend = 0;
        }

        public void Cancel(string board, string reportId)
        {
            EnsureAccount();
            _queue.items.RemoveAll(item => item.board == board && item.reportId == reportId);
            SaveQueue();
        }

        private void SaveQueue()
        {
            PlayerPrefs.SetString(QueueKey(_user), JsonUtility.ToJson(_queue));
            PlayerPrefs.Save();
        }

        private void Update()
        {
            EnsureAccount();
            if (WorkspaceReady && !_sending && Time.unscaledTime >= _nextSend) SendNext();
        }

        private void SendNext()
        {
            _nextSend = Time.unscaledTime + 30f;
            if (_queue.items.Count == 0 || SessionManager.Instance == null) return;
            var item = _queue.items[0];
            AnalysisReport report;
            SessionData session;
            try
            {
                session = SessionManager.Instance.LoadSessionData(item.board);
                report = session?.Reports?.Find(value => value != null && value.ReportId == item.reportId);
            }
            catch { return; }
            if (report == null || report.Source != "ai")
            {
                _queue.items.RemoveAt(0); SaveQueue(); return;
            }
            if (!string.IsNullOrWhiteSpace(report.CloudId))
            {
                _queue.items.RemoveAt(0); SaveQueue(); return;
            }

            int requestUser = _user;
            int requestEpoch = _epoch;
            string requestToken = _token;
            string expectedText = report.ResultText;
            string screenshot = ScreenshotManager.LoadAnalysisImageBase64(report.ReportId);
            bool CurrentAccount() => this != null && requestEpoch == LocalAccountStorage.Epoch && WorkspaceReady &&
                _user == requestUser && _token == requestToken &&
                BackendClient.Instance.UserId == requestUser && BackendClient.Instance.AccessToken == requestToken;

            _sending = true;
            BackendClient.Instance.SaveAnalysisRecord(
                expectedText ?? "", screenshot, report.ModelUsed ?? "",
                session?.OrganizationId, session?.OrganizationClientId,
                report.ReportId, item.board,
                cloudId =>
                {
                    if (!CurrentAccount()) return;
                    _sending = false;
                    if (!string.IsNullOrWhiteSpace(cloudId) && SessionManager.Instance.TryAttachReportCloudId(
                        item.board, report.ReportId, expectedText, cloudId))
                        _queue.items.RemoveAll(value => value.board == item.board && value.reportId == item.reportId);
                    else
                    {
                        _queue.items.RemoveAt(0);
                        _queue.items.Add(item);
                    }
                    SaveQueue();
                    _nextSend = 0;
                },
                _ =>
                {
                    if (!CurrentAccount()) return;
                    _sending = false;
                    _queue.items.RemoveAt(0);
                    _queue.items.Add(item);
                    SaveQueue();
                    _nextSend = Time.unscaledTime + 30f;
                });
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
