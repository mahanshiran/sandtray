using System;
using System.Collections.Generic;
using System.IO;
using Sandplay.Core;
using UnityEngine;

namespace Sandplay.Data
{
    public partial class SessionManager
    {
        private float _nextBoardQuotaScan, _nextBoardQuotaRequest, _quotaRequestStarted;
        private bool _boardQuotaBusy;
        private int _quotaRequestGeneration;
        private Queue<string> _boardQuotaFiles;
        private readonly Dictionary<string, string> _boardQuotaStatuses = new Dictionary<string, string>();
        private string _openBoardCapacityId, _quotaPolicySignature;
        internal Action<LocalCapacityRequest, Action<LocalCapacityReceipt>, Action<string>> BoardQuotaTransport { get; set; }
        public string CurrentBoardQuotaKey { get; private set; } = "quota.none";
        public string CurrentBoardBackupKey { get; private set; } = "backup.unknown";

        private void RefreshBoardPersistenceStatus(SessionData data)
        {
            _openBoardCapacityId = data?.LocalCapacityId;
            CurrentBoardQuotaKey = string.IsNullOrEmpty(_openBoardCapacityId) ? "quota.legacy" :
                _boardQuotaStatuses.TryGetValue(_openBoardCapacityId, out var state) ? state : "quota.pending";
            // Backup tracking is separate from quota; errors must never invalidate a local save.
            try { CurrentBoardBackupKey = BoardBackupState(data); }
            catch (Exception) { CurrentBoardBackupKey = "backup.unknown"; }
        }

        public void RetryBoardQuota()
        {
            try
            {
                RequireWorkspace();
                LocalAccountStorage.ExistingCapacityJournal()?.RetryLocalBoardsNow();
                _nextBoardQuotaScan = _nextBoardQuotaRequest = 0;
                foreach (var id in new List<string>(_boardQuotaStatuses.Keys)) _boardQuotaStatuses[id] = "quota.pending";
                if (!string.IsNullOrEmpty(_openBoardCapacityId)) CurrentBoardQuotaKey = "quota.pending";
            }
            catch (Exception) { CurrentBoardQuotaKey = "quota.recovery"; }
        }

        private void SetBoardQuotaStatus(string id, string key)
        {
            _boardQuotaStatuses[id] = key;
            if (_openBoardCapacityId == id) CurrentBoardQuotaKey = key;
        }

        internal static string BoardQuotaErrorKey(string error)
        {
            string text = (error ?? "").ToLowerInvariant();
            if (text.Contains("capacity reached") || text.Contains("quota") || text.Contains("allowance")) return "quota.full";
            if (text.Contains("401") || text.Contains("token") || text.Contains("sign in") || text.Contains("authenticated")) return "quota.signin";
            return "quota.offline";
        }

        // Runs independently of board-open coroutines. Scan at most one board per frame.
        private void BoardQuotaTick(float now)
        {
            if (!WorkspaceIsCurrent) return;
            var client = BackendClient.Instance;
            if (!client.IsLoggedIn || client.UserId <= 0) return;
            try
            {
                if (!LocalAccountStorage.IsIsolated) return;
                if (_boardQuotaBusy)
                {
                    if (now - _quotaRequestStarted < 25) return;
                    // Unknown outcomes keep their operation ID. Stale callbacks are ignored.
                    _quotaRequestGeneration++; _boardQuotaBusy = false;
                    _nextBoardQuotaRequest = now + 30;
                }
                if (now < _nextBoardQuotaRequest) return;
                var snapshot = client.CurrentAccess;
                bool enforced = snapshot != null ? snapshot.local_capacity_enforced : CapacityEnforced;
                if (snapshot != null)
                {
                    var allowance = Array.Find(snapshot.capabilities, c => c.key == "tables.capacity");
                    string signature = snapshot.local_capacity_enforced + ":" + snapshot.revision + ":" + allowance?.remaining;
                    if (_quotaPolicySignature != null && signature != _quotaPolicySignature &&
                        allowance != null && (allowance.unlimited || allowance.remaining > 0))
                        LocalAccountStorage.ExistingCapacityJournal()?.RetryLocalBoardsNow();
                    _quotaPolicySignature = signature;
                }
                if (_boardQuotaFiles == null && now >= _nextBoardQuotaScan)
                {
                    var paths = new HashSet<string>(Directory.GetFiles(SavePath, "*.json"));
                    foreach (var backup in Directory.GetFiles(SavePath, "*.json.bak"))
                        paths.Add(backup.Substring(0, backup.Length - 4));
                    _boardQuotaFiles = new Queue<string>(paths);
                    _nextBoardQuotaScan = now + 30;
                }
                if (_boardQuotaFiles != null && _boardQuotaFiles.Count > 0)
                {
                    string path = _boardQuotaFiles.Dequeue();
                    var data = ReadSavedData(Path.GetFileNameWithoutExtension(path), out _, false);
                    if (data != null && !string.IsNullOrEmpty(data.LocalCapacityId))
                    {
                        if (enforced)
                        {
                            var journal = LocalAccountStorage.OpenCapacityJournal();
                            var op = journal.RegisterLocalBoard("Sessions/" + Path.GetFileName(path), data.LocalCapacityId);
                            SetBoardQuotaStatus(op.Id, op.State == "active" ? "quota.confirmed" :
                                string.IsNullOrEmpty(op.LastError) ? "quota.pending" : op.LastError);
                        }
                        else SetBoardQuotaStatus(data.LocalCapacityId, snapshot == null ? "quota.pending" : "quota.none");
                    }
                    return;
                }
                _boardQuotaFiles = null;
                var pendingJournal = LocalAccountStorage.ExistingCapacityJournal();
                if (pendingJournal == null) { _nextBoardQuotaRequest = now + 5; return; }
                foreach (var op in pendingJournal.Read())
                {
                    if (!op.LocalFirst || op.State == "released" || (op.State == "active" && !op.LocalDeletePending)) continue;
                    if (op.State == "renaming") { SetBoardQuotaStatus(op.Id, "quota.recovery"); continue; }
                    if (!enforced && !op.HasLease && !op.LocalDeletePending) continue;
                    if (DateTimeOffset.TryParse(op.RetryAfter, out var retry) && retry > DateTimeOffset.UtcNow) continue;
                    SendBoardQuota(pendingJournal, op, now);
                    return;
                }
                _nextBoardQuotaRequest = now + 5;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(_openBoardCapacityId)) CurrentBoardQuotaKey = "quota.recovery";
                _nextBoardQuotaRequest = now + 30;
                Debug.LogWarning("[Board quota] Local files remain available: " + ex.GetType().Name);
            }
        }

        private void SendBoardQuota(LocalCapacityJournal journal, LocalCapacityOperation op, float now)
        {
            var client = BackendClient.Instance;
            int user = client.UserId, generation = ++_quotaRequestGeneration;
            string token = client.AccessToken;
            var guard = LocalAccountStorage.CaptureGuard();
            _boardQuotaBusy = true; _quotaRequestStarted = now;
            bool done = false;
            bool Current()
            {
                if (this == null || generation != _quotaRequestGeneration || client.UserId != user || client.AccessToken != token) return false;
                try { guard(); return true; } catch { return false; }
            }
            void Finish(string error)
            {
                if (done || !Current()) return;
                done = true; _boardQuotaBusy = false;
                _nextBoardQuotaRequest = Time.realtimeSinceStartup + 1;
                try
                {
                    string key = error == null ? null : BoardQuotaErrorKey(error);
                    journal.RecordLocalBoardRetry(op.Id, key, DateTimeOffset.UtcNow);
                    var current = Array.Find(journal.Read(), x => x.Id == op.Id);
                    SetBoardQuotaStatus(op.Id, key ?? (current.State == "active" ? "quota.confirmed" : "quota.pending"));
                }
                catch (Exception) { SetBoardQuotaStatus(op.Id, "quota.recovery"); }
            }
            new LocalCapacitySync(journal, (request, success, failure) =>
            {
                if (!Current()) return;
                var transport = BoardQuotaTransport ?? client.RequestLocalCapacity;
                transport(request, response => { if (Current()) success(response); },
                    error => { if (Current()) failure(error); });
            }).Retry(op.Id, () => Finish(null), Finish);
        }
    }
}
