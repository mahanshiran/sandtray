using System;
using System.Security.Cryptography;
using System.Text;
using Sandplay.Core;
using UnityEngine;

namespace Sandplay.Data
{
    public static class ReportSharingText
    {
        [Serializable] private sealed class ReviewContent
        {
            public string Source, Text;
            public ReportSections Sections;
        }
        public static string Fingerprint(AnalysisReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var json = JsonUtility.ToJson(new ReviewContent { Source = report.Source, Text = report.ResultText, Sections = report.Sections });
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "");
        }
        public static bool HasAI(AnalysisReport report) => report?.Source == "ai" || !string.IsNullOrWhiteSpace(report?.Sections?.AIReflection);
        public static string Draft(AnalysisReport report, bool includeAI)
        {
            if (report == null || report.Source == "ai" && !includeAI) return "";
            if (report.Sections == null) return (report.ResultText ?? "").Trim();
            var source = report.Sections;
            // There is intentionally no option to include the private practitioner-notes field.
            return SceneBootstrapper.StructuredReportText(new ReportSections {
                Observations = source.Observations, ClientPerspective = source.ClientPerspective,
                NextSteps = source.NextSteps, AIReflection = includeAI ? source.AIReflection : null
            });
        }
        public static string PdfDraft(AnalysisReport report, bool includeAI, bool includePrivate)
        {
            if (report == null || report.Source == "ai" && !includeAI) return "";
            if (!includePrivate || report.Sections == null) return Draft(report, includeAI);
            var source = report.Sections;
            return SceneBootstrapper.StructuredReportText(new ReportSections {
                Observations = source.Observations, ClientPerspective = source.ClientPerspective,
                PractitionerNotes = source.PractitionerNotes, NextSteps = source.NextSteps,
                AIReflection = includeAI ? source.AIReflection : null
            });
        }
        public static bool TryPublication(AnalysisReport report, out string text, out string errorKey)
        {
            text = null; errorKey = "sharing.review_required";
            if (report == null) return false;
            if (!string.IsNullOrEmpty(report.SharedReviewFingerprint) &&
                report.SharedReviewFingerprint == Fingerprint(report))
                text = report.SharedText;
            else if (report.Source != "ai" && report.Sections != null)
                text = Draft(report, false);
            else return false;
            if (!string.IsNullOrWhiteSpace(text)) return true;
            errorKey = "sharing.no_public_text";
            return false;
        }
    }
}
