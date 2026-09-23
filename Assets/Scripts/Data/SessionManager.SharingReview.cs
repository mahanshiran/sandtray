using System;
using System.IO;
using UnityEngine;

namespace Sandplay.Data
{
    public partial class SessionManager
    {
        public void ReviewReportPdf(string board, string reportId, string expectedFingerprint, bool includeAI, bool includePrivate, string expectedText)
        {
            var data = ReadSavedData(board, out _);
            var report = data?.Reports?.Find(r => r != null && r.ReportId == reportId);
            if (report == null || !CanEditReport(report)) throw new UnauthorizedAccessException();
            if (ReportSharingText.Fingerprint(report) != expectedFingerprint ||
                ReportSharingText.PdfDraft(report, includeAI, includePrivate) != expectedText || string.IsNullOrWhiteSpace(expectedText))
                throw new InvalidOperationException("reports.edit_stale");
            report.PdfReviewText = expectedText;
            report.PdfReviewFingerprint = expectedFingerprint;
            report.PdfReviewedAt = DateTime.UtcNow.ToString("o");
            WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(board) + ".json"), JsonUtility.ToJson(data, true));
        }

        public AnalysisReport ReviewReportForSharing(string board, string reportId, string expectedFingerprint, bool includeAI, string expectedSharedText)
        {
            var data = ReadSavedData(board, out _);
            var report = data?.Reports?.Find(r => r != null && r.ReportId == reportId);
            if (report == null) throw new InvalidDataException("sharing.review_required");
            if (!CanEditReport(report)) throw new UnauthorizedAccessException();
            if (ReportSharingText.Fingerprint(report) != expectedFingerprint)
                throw new InvalidOperationException("reports.edit_stale");
            string shared = ReportSharingText.Draft(report, includeAI);
            if (string.IsNullOrWhiteSpace(shared)) throw new InvalidOperationException("sharing.no_public_text");
            if (!string.Equals(shared, expectedSharedText, StringComparison.Ordinal))
                throw new InvalidOperationException("reports.edit_stale");
            report.SharedText = shared;
            report.SharedReviewFingerprint = expectedFingerprint;
            report.SharedReviewedAt = DateTime.UtcNow.ToString("o");
            WriteTableRecord(Path.Combine(SavePath, SanitizeFileName(board) + ".json"), JsonUtility.ToJson(data, true));
            return report;
        }
    }
}
