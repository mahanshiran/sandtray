using System;
using System.Linq;
using System.IO;
using UnityEngine;
using Sandplay.Core;
namespace Sandplay.Data
{
    public partial class SessionManager
    {
        // Test seam and one place to route report-capacity reconciliation.
        internal Action<LocalCapacityRequest,Action<LocalCapacityReceipt>,Action<string>> ReportCapacityTransport;
        private Action<LocalCapacityRequest,Action<LocalCapacityReceipt>,Action<string>> ReportTransport =>
            ReportCapacityTransport ?? BackendClient.Instance.RequestLocalCapacity;

        public void DeleteSessionAsync(string board,Action saved,Action<string> failed)
        {
            if(!CapacityEnforced){try{DeleteSessionCore(board);saved?.Invoke();}catch(Exception ex){failed?.Invoke(ex.Message);}return;}
            try
            {
                var guard=LocalAccountStorage.CaptureGuard();
                var data=ReadSavedData(board,out _);
                var ids=(data.Reports ?? new System.Collections.Generic.List<AnalysisReport>()).Select(r=>r.ReportId).ToArray();
                var store=new AggregateCapacityStore(LocalAccountStorage.OpenCapacityJournal(),guard,BackendClient.Instance.RequestLocalCapacity);
                store.Write(AggregateCapacityStore.ReportAggregate(data),"reports.capacity",ids,Array.Empty<string>(),
                    ()=>{guard();DeleteSessionCore(board);},saved,failed);
            }
            catch(Exception ex){failed?.Invoke(ex.Message);}
        }

        public void DeleteAnalysisReportAsync(string board,string reportId,Action saved,Action<string> failed)
        {
            if(!CapacityEnforced){DeleteAnalysisReport(board,reportId);saved?.Invoke();return;}
            try
            {
                var guard=LocalAccountStorage.CaptureGuard();
                var data=ReadSavedData(board,out _);
                if(data?.Reports==null || !data.Reports.Any(r=>r.ReportId==reportId)){saved?.Invoke();return;}
                string path=Path.Combine(SavePath,SanitizeFileName(board)+".json");
                string expected=File.ReadAllText(path);
                var before=data.Reports.Select(r=>r.ReportId).ToArray();
                data.Reports.RemoveAll(r=>r.ReportId==reportId);
                var store=new AggregateCapacityStore(LocalAccountStorage.OpenCapacityJournal(),guard,BackendClient.Instance.RequestLocalCapacity);
                store.Write(AggregateCapacityStore.ReportAggregate(data),"reports.capacity",before,data.Reports.Select(r=>r.ReportId),()=>
                {
                    guard();if(File.ReadAllText(path)!=expected)throw new InvalidOperationException("Table changed while deleting report.");
                    TableCapacity.Write(path,JsonUtility.ToJson(data,true));
                    ScreenshotManager.DeleteAnalysisImage(reportId);
                    AnalysisArchiveClient.Instance.Cancel(board,reportId);
                    if (CurrentBoardName == board) RefreshBoardPersistenceStatus(data);
                },saved,failed);
            }
            catch(Exception ex){failed?.Invoke(ex.Message);}
        }

        public void AppendAnalysisReportAsync(string board, AnalysisReport report, Action saved, Action<string> failed)
        {
            if(!CapacityEnforced){if(AppendAnalysisReport(board,report))saved?.Invoke();else failed?.Invoke("Report could not be saved.");return;}
            try
            {
                var guard=LocalAccountStorage.CaptureGuard();
                if(report==null || string.IsNullOrWhiteSpace(report.ResultText) || !BackendClient.Instance.IsLoggedIn)
                    throw new InvalidOperationException("A signed-in author and report text are required.");
                if(string.IsNullOrWhiteSpace(report.ReportId))report.ReportId=Guid.NewGuid().ToString("N");
                var data=ReadSavedData(board,out _);
                if(data.Reports==null)data.Reports=new System.Collections.Generic.List<AnalysisReport>();
                var existing=data.Reports.FirstOrDefault(r=>r.ReportId==report.ReportId);
                if(existing!=null) {if(existing.ResultText!=report.ResultText)throw new InvalidOperationException("Report ID already exists with different text.");saved?.Invoke();return;}
                string path=Path.Combine(SavePath,SanitizeFileName(board)+".json");
                var before=data.Reports.Select(r=>r.ReportId).ToArray();
                report.AuthorUserId=BackendClient.Instance.UserId;report.AuthorName=BackendClient.Instance.UserName;
                data.Reports.Add(report);string content=JsonUtility.ToJson(data,true);

                // A generated AI reflection is a paid result. Never hide or lose it
                // because the separate capacity service is offline: commit it to the
                // device first, then reconcile its storage slot in the background.
                string expected=LocalCapacityJournal.Hash(File.ReadAllText(path));
                // Preserve the proposed aggregate for explicit recovery if allocation or the process is interrupted.
                LocalRecordFile.Write(path+".interrupted-"+LocalCapacityJournal.Hash(report.ReportId),content);
                var store=new AggregateCapacityStore(LocalAccountStorage.OpenCapacityJournal(),guard,BackendClient.Instance.RequestLocalCapacity);
                store.Write(AggregateCapacityStore.ReportAggregate(data),"reports.capacity",before,data.Reports.Select(r=>r.ReportId),()=>
                {
                    guard();if(LocalCapacityJournal.Hash(File.ReadAllText(path))!=expected)throw new InvalidOperationException("Table changed while reserving report capacity; recover the saved draft.");
                    TableCapacity.Write(path,content);
                    if (CurrentBoardName == board) RefreshBoardPersistenceStatus(data);
                },()=>{if(this!=null){ReportDeliveryClient.Instance.Queue(board,report);AnalysisArchiveClient.Instance.Queue(board,report);saved?.Invoke();}},failed);
            }
            catch(Exception ex){failed?.Invoke(ex.Message);}
        }
    }
}
