using System;
using System.IO;
using UnityEngine;

namespace Sandplay.Data
{
    public partial class SessionManager
    {
        public void SetBoardArchived(string board, bool archived)
        {
            var data = ReadSavedData(board, out _);
            if (data == null) throw new InvalidDataException();
            if (data.Archived == archived) return;
            data.Archived = archived;
            // Archiving is organizational metadata; preserve the board's content date.
            WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(board) + ".json"), JsonUtility.ToJson(data, true));
        }

        public void SetReportArchived(string board, string reportId, bool archived)
        {
            var data = ReadSavedData(board, out _);
            var report = data?.Reports?.Find(r => r != null && r.ReportId == reportId);
            if (report == null) throw new InvalidDataException();
            if (!CanEditReport(report)) throw new UnauthorizedAccessException();
            if (report.Archived == archived) return;
            report.Archived = archived;
            WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(board) + ".json"), JsonUtility.ToJson(data, true));
        }
    }
}
