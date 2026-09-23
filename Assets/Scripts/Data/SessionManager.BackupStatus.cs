using System;
using System.IO;
using UnityEngine;

namespace Sandplay.Data
{
    public partial class SessionManager
    {
        [Serializable] private sealed class BackupReceipt
        {
            public string ConfirmedHash, AttemptHash, State;
        }
        private string _uploadingBackup;
        private string BackupReceiptPath(SessionData data)
        {
            string identity = !string.IsNullOrEmpty(data.LocalCapacityId) ? data.LocalCapacityId :
                !string.IsNullOrEmpty(data.BoardHistoryId) ? data.BoardHistoryId : data.SessionName;
            return Path.Combine(SavePath, ".backup-status", LocalCapacityJournal.Hash(identity) + ".json");
        }
        private string BoardBackupState(SessionData data)
        {
            if (data == null) return "backup.unknown";
            string path = BackupReceiptPath(data);
            if (!File.Exists(path)) return string.IsNullOrEmpty(data.LocalCapacityId) ? "backup.unknown" : "backup.none";
            var receipt = JsonUtility.FromJson<BackupReceipt>(File.ReadAllText(path));
            if (receipt == null) return "backup.unknown";
            if (receipt.State == "uploading" && _uploadingBackup == path) return "backup.uploading";
            string hash = LocalCapacityJournal.Hash(JsonUtility.ToJson(data));
            if (receipt.ConfirmedHash == hash) return "backup.saved";
            if (receipt.State == "failed" || receipt.State == "uploading") return "backup.failed";
            return "backup.changed";
        }

        // Records only the explicitly uploaded snapshot. Later local changes remain unbacked-up.
        public void RecordBoardBackup(SessionData snapshot, string state)
        {
            try
            {
                RequireWorkspace();
                string path = BackupReceiptPath(snapshot);
                var receipt = File.Exists(path) ? JsonUtility.FromJson<BackupReceipt>(File.ReadAllText(path)) : new BackupReceipt();
                if (receipt == null) receipt = new BackupReceipt();
                receipt.State = state;
                receipt.AttemptHash = LocalCapacityJournal.Hash(JsonUtility.ToJson(snapshot));
                if (state == "saved") receipt.ConfirmedHash = receipt.AttemptHash;
                _uploadingBackup = state == "uploading" ? path : null;
                LocalRecordFile.Write(path, JsonUtility.ToJson(receipt));
                if (!string.IsNullOrEmpty(CurrentBoardName))
                    CurrentBoardBackupKey = BoardBackupState(ReadSavedData(CurrentBoardName, out _, false));
            }
            catch (Exception)
            {
                CurrentBoardBackupKey = "backup.unknown";
                Debug.LogWarning("[Backup] Could not record backup status; local board files are preserved.");
            }
        }
    }
}
