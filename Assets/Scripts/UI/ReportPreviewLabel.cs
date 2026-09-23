using TMPro;
using UnityEngine;
using Sandplay.Data;

namespace Sandplay.UI
{
    public sealed class ReportPreviewLabel : MonoBehaviour
    {
        private AnalysisReport report;
        private TextMeshProUGUI label;
        private string previous;
        public void Initialize(AnalysisReport value, TextMeshProUGUI text)
        {
            report = value; label = text; Refresh();
        }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (label == null) return;
            string value = report?.ResultText ?? "";
            if (value == previous) return;
            previous = value;
            label.richText = false;
            // StringInfo avoids splitting emoji/surrogate pairs at the preview boundary.
            var text = new System.Globalization.StringInfo(value);
            label.text = text.LengthInTextElements > 100 ? text.SubstringByTextElements(0, 100) + "…" : value;
        }
    }
}
