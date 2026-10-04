using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sandplay.AI
{
    public static class ReflectionPresentation
    {
        // Hide appendices in archived reports too, without altering the saved original.
        public static string WithoutReferences(string text)
        {
            var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "PRACTICE CONTEXT", "SOURCES", "METHOD AND SOURCES", "REFERENCES",
                "实践参考", "资料来源", "方法与来源", "参考资料", "参考文献" };
            var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "SUMMARY", "OBSERVATIONS", "CONCLUSION", "SCENE COMPOSITION",
                "OPTIONAL HYPOTHESES", "QUESTIONS FOR REFLECTION",
                "摘要", "观察", "总结", "画面结构", "可选假设", "反思问题" };
            var result = new List<string>();
            bool skipping = false;
            foreach (var line in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var heading = line.Trim().Trim('#', '*', ':', '：').Trim();
                if (references.Contains(heading)) { skipping = true; continue; }
                if (sections.Contains(heading)) skipping = false;
                if (!skipping) result.Add(Regex.Replace(line, @"\[\d+\]", ""));
            }
            return string.Join("\n", result).Trim();
        }
    }
}
