using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Sandplay.Data
{
    public sealed class BoardCheckpoint
    {
        public string Id, CreatedAt, SavedAt, Fingerprint;
        public int ObjectCount;
    }

    public partial class SessionManager
    {
        public const int MaximumBoardCheckpoints = 20;
        private string HistoryPath(string historyId)
        {
            if (!Guid.TryParseExact(historyId, "N", out _)) throw new InvalidDataException("versions.invalid");
            return Path.Combine(SavePath, ".versions", historyId);
        }
        private static string Fingerprint(string json)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "");
        }
        private static bool ValidCheckpointLayout(SessionData data) => data != null &&
            data.SandboxWidth > 0 && !float.IsInfinity(data.SandboxWidth) &&
            data.SandboxDepth > 0 && !float.IsInfinity(data.SandboxDepth) && data.HeightmapResolution > 0;

        public string BoardVersionFingerprint(string board)
        {
            var data = ReadSavedData(board, out _);
            if (data == null) throw new InvalidDataException("versions.invalid");
            return Fingerprint(JsonUtility.ToJson(data));
        }
        public List<BoardCheckpoint> GetBoardCheckpoints(string board)
        {
            var data = ReadSavedData(board, out _);
            if (data == null) throw new InvalidDataException("versions.invalid");
            if (string.IsNullOrEmpty(data.BoardHistoryId)) return new List<BoardCheckpoint>();
            string directory = HistoryPath(data.BoardHistoryId);
            if (!Directory.Exists(directory)) return new List<BoardCheckpoint>();
            var result = new List<BoardCheckpoint>();
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                if (!Guid.TryParseExact(id, "N", out _)) continue;
                var saved = ReadValidCopy(file, "Checkpoint");
                if (!ValidCheckpointLayout(saved) || saved.BoardHistoryId != data.BoardHistoryId) continue;
                result.Add(new BoardCheckpoint { Id = id, CreatedAt = saved.CreatedAt, SavedAt = saved.ModifiedAt,
                    ObjectCount = saved.PlacedObjects.Count, Fingerprint = Fingerprint(JsonUtility.ToJson(saved)) });
            }
            return result.OrderByDescending(c => c.CreatedAt, StringComparer.Ordinal).ToList();
        }
        public BoardCheckpoint CreateBoardCheckpoint(string board)
        {
            // Capture the saved board. Unsaved/live scene state is never represented as saved history.
            var data = ReadSavedData(board, out _);
            if (data == null) throw new InvalidDataException("versions.invalid");
            if (string.IsNullOrEmpty(data.BoardHistoryId))
            {
                data.BoardHistoryId = Guid.NewGuid().ToString("N");
                WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(board) + ".json"), JsonUtility.ToJson(data, true));
            }
            return WriteCheckpoint(data);
        }
        private BoardCheckpoint WriteCheckpoint(SessionData data)
        {
            if (!ValidCheckpointLayout(data)) throw new InvalidDataException("versions.invalid");
            string directory = HistoryPath(data.BoardHistoryId);
            string id = Guid.NewGuid().ToString("N");
            var saved = new SessionData {
                SessionName = "Checkpoint", BoardHistoryId = data.BoardHistoryId,
                CreatedAt = DateTime.UtcNow.ToString("o"), ModifiedAt = data.ModifiedAt,
                SandboxWidth = data.SandboxWidth, SandboxDepth = data.SandboxDepth,
                HeightmapResolution = data.HeightmapResolution, HeightmapBase64 = data.HeightmapBase64,
                SplatmapBase64 = data.SplatmapBase64, PlacedObjects = data.PlacedObjects
            };
            LocalRecordFile.Write(Path.Combine(directory, id + ".json"), JsonUtility.ToJson(saved, true));
            // Checkpoints contain board geometry only, never copies of reports/private client notes.
            var checkpoint = new BoardCheckpoint { Id = id, CreatedAt = saved.CreatedAt, SavedAt = saved.ModifiedAt,
                ObjectCount = saved.PlacedObjects.Count, Fingerprint = Fingerprint(JsonUtility.ToJson(saved)) };
            foreach (var old in GetBoardCheckpoints(data.SessionName).Skip(MaximumBoardCheckpoints))
            {
                try { File.Delete(Path.Combine(directory, old.Id + ".json")); }
                catch (IOException) { Debug.LogWarning("[Versions] Could not prune an old checkpoint."); }
                catch (UnauthorizedAccessException) { Debug.LogWarning("[Versions] Could not prune an old checkpoint."); }
            }
            return checkpoint;
        }
        public void RestoreBoardCheckpoint(string board, string checkpointId, string expectedBoard, string expectedCheckpoint)
        {
            if (AutoSaveActive && CurrentBoardName == board) throw new InvalidOperationException("versions.close_board");
            if (!Guid.TryParseExact(checkpointId, "N", out _)) throw new InvalidDataException("versions.invalid");
            var current = ReadSavedData(board, out _);
            if (current == null || Fingerprint(JsonUtility.ToJson(current)) != expectedBoard)
                throw new InvalidOperationException("versions.stale");
            string path = Path.Combine(HistoryPath(current.BoardHistoryId), checkpointId + ".json");
            var saved = ReadValidCopy(path, "Checkpoint");
            if (!ValidCheckpointLayout(saved) || saved.BoardHistoryId != current.BoardHistoryId || Fingerprint(JsonUtility.ToJson(saved)) != expectedCheckpoint)
                throw new InvalidDataException("versions.invalid");
            // A durable safety checkpoint must succeed before replacement of the current layout.
            WriteCheckpoint(current);
            current.SandboxWidth = saved.SandboxWidth; current.SandboxDepth = saved.SandboxDepth;
            current.HeightmapResolution = saved.HeightmapResolution; current.HeightmapBase64 = saved.HeightmapBase64;
            current.SplatmapBase64 = saved.SplatmapBase64; current.PlacedObjects = saved.PlacedObjects;
            current.ModifiedAt = DateTime.UtcNow.ToString("o");
            WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(board) + ".json"), JsonUtility.ToJson(current, true));
            if (CurrentBoardName == board) { _boardReady = false; _lastSavedContent = null; }
        }
    }
}
