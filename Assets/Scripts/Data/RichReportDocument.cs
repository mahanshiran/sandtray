using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Data
{
    /// <summary>Portable HTML is stored beside plain text for older clients and search.</summary>
    public static class RichReportDocument
    {
        [Serializable] private sealed class DraftRecord {public string BaseFingerprint;public AnalysisReport Report;}
        public static bool IsRich(AnalysisReport report)=>report?.Sections?.RichTextVersion==1;
        private static string Paragraphs(string text)
        {
            if(string.IsNullOrWhiteSpace(text))return "<p></p>";
            return "<p>"+WebUtility.HtmlEncode(text).Replace("\r","").Replace("\n\n","</p><p>").Replace("\n","<br>")+"</p>";
        }
        public static string PublicHtml(AnalysisReport report)
        {
            if(IsRich(report))return report.Sections.RichHtml??"<p></p>";
            if(report!=null&&report.Sections==null)return Paragraphs(report.ResultText);
            var value=new StringBuilder();
            void Section(string heading,string body){value.Append("<h2>").Append(WebUtility.HtmlEncode(Localization.Get(heading))).Append("</h2>").Append(Paragraphs(body));}
            Section("report.observations",report?.Sections?.Observations);
            Section("report.client_voice",report?.Sections?.ClientPerspective);
            Section("report.next_steps",report?.Sections?.NextSteps);
            return value.ToString();
        }
        public static string PrivateHtml(AnalysisReport report)=>IsRich(report)?report.Sections.PrivateHtml??"<p></p>":Paragraphs(report?.Sections?.PractitionerNotes);
        public static ReportSections Sections(string html,string text,string privateHtml,string privateText)=>new ReportSections
        {RichTextVersion=1,RichHtml=html,PrivateHtml=privateHtml,Observations=text,PractitionerNotes=privateText};
        public static string ExportHtml(AnalysisReport report,bool includePrivate=false)
        {
            if(!IsRich(report))return null;
            return report.Sections.RichHtml+(includePrivate&&!string.IsNullOrWhiteSpace(report.Sections.PractitionerNotes)?
                "<h2>"+WebUtility.HtmlEncode(Localization.Get("report.practitioner_notes"))+"</h2>"+report.Sections.PrivateHtml:"");
        }
        public static string DraftPath(string board,string reportId)
        {
            using var sha=SHA256.Create();
            var id=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(board+"\n"+(reportId??"new")))).Replace("-","");
            return Path.Combine(LocalAccountStorage.Root,"ReportDrafts",id+".json");
        }
        public static void WriteDraft(string board,string reportId,AnalysisReport draft,string baseFingerprint=null)
        {
            var path=DraftPath(board,reportId);Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary=path+".tmp";File.WriteAllText(temporary,JsonUtility.ToJson(new DraftRecord {Report=draft,BaseFingerprint=baseFingerprint}),Encoding.UTF8);
            if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
        }
        public static AnalysisReport LoadDraft(string board,string reportId,string expectedFingerprint=null)
        {
            foreach(var suffix in new[]{"",".bak"})
            {
                try
                {
                    var path=DraftPath(board,reportId)+suffix;
                    if(!File.Exists(path))continue;
                    var record=JsonUtility.FromJson<DraftRecord>(File.ReadAllText(path));
                    if(record?.BaseFingerprint==expectedFingerprint&&record?.Report!=null)return record.Report;
                }
                catch(IOException){}
                catch(ArgumentException){}
            }
            return null;
        }
        public static void DeleteDraft(string board,string reportId)
        {
            var path=DraftPath(board,reportId);
            foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);
        }
    }
}
