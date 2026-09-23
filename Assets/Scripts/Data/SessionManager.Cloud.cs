using System;
using System.IO;
using UnityEngine;
using Sandplay.Core;
using System.Linq;
namespace Sandplay.Data
{
    public partial class SessionManager
    {
        // Restores a new local copy; never overwrites an existing board or opens a
        // received share as editable. Only the owner's backup UI calls this path.
        private SessionData PrepareCloudCopy(SessionData original, int expectedUser)
        {
            if(BackendClient.Instance.UserId!=expectedUser || !BackendClient.Instance.IsLoggedIn)
                throw new InvalidOperationException("Account changed. Restore cancelled.");
            if(original==null || !original.HasFiniteObjectTransforms() || original.SandboxWidth<=0 || original.SandboxDepth<=0 ||
                float.IsNaN(original.SandboxWidth) || float.IsNaN(original.SandboxDepth) ||
                original.SandboxWidth>1000 || original.SandboxDepth>1000 || original.HeightmapResolution<1 || original.HeightmapResolution>2048)
                throw new InvalidDataException("Invalid cloud table.");
            var copy=JsonUtility.FromJson<SessionData>(JsonUtility.ToJson(original));
            copy.LocalCapacityId = null;
            if(!string.IsNullOrEmpty(copy.HeightmapBase64))
            {
                var heights=copy.DecodeHeightmap();
                if(heights==null || heights.Length!=(long)copy.HeightmapResolution*copy.HeightmapResolution)
                    throw new InvalidDataException("Invalid cloud terrain.");
                foreach(float value in heights) if(float.IsNaN(value)||float.IsInfinity(value)) throw new InvalidDataException("Invalid cloud terrain.");
            }
            if(!string.IsNullOrEmpty(copy.SplatmapBase64))
            {
                int length=Convert.FromBase64String(copy.SplatmapBase64).Length;
                if(length!=512*512*4 && length!=512*512*8)
                    throw new InvalidDataException("Invalid cloud surface data.");
            }
            copy.SessionName=GetAvailableSessionName(copy.SessionName+" (cloud copy)");
            copy.BoardHistoryId=Guid.NewGuid().ToString("N");
            // Device-local client IDs do not identify the same person on another device.
            copy.ClientId=null; copy.ClientAssignmentHistory=new System.Collections.Generic.List<ClientAssignmentChange>();
            copy.OrganizationId = copy.OrganizationClientId = null;
            copy.CreatorUserId = expectedUser;
            copy.Archived=false;
            if(copy.Reports!=null) foreach(var report in copy.Reports)
            {
                if(report==null) throw new InvalidDataException("Invalid cloud report.");
                report.ReportId=Guid.NewGuid().ToString("N");
                report.CloudId=null;
                report.SharedText=report.SharedReviewFingerprint=report.SharedReviewedAt=null;
                report.PdfReviewText=report.PdfReviewFingerprint=report.PdfReviewedAt=null;
            }
            return copy;
        }
        private bool capacityRestorePending;
        public void RestoreCloudBackupAsync(SessionData original, int expectedUser, Action<string> saved, Action<string> failed)
        {
            if (capacityRestorePending) { failed?.Invoke("A restore is already pending."); return; }
            try
            {
                if (!CapacityEnforced) { saved?.Invoke(RestoreCloudBackup(original, expectedUser)); return; }
                var journal = LocalAccountStorage.OpenCapacityJournal();
                var client = BackendClient.Instance;
                string token = client.AccessToken;
                bool Current() => this != null && client.IsLoggedIn && client.UserId == expectedUser && client.AccessToken == token;
                var guard=LocalAccountStorage.CaptureGuard();
                string sourceHash=LocalCapacityJournal.Hash(JsonUtility.ToJson(original));
                var intents=new CloudRestoreIntent(LocalAccountStorage.Root,guard);
                var intent=intents.Prepare(sourceHash,()=>PrepareCloudCopy(original,expectedUser));
                var copy=JsonUtility.FromJson<SessionData>(intent.Content);
                capacityRestorePending = true;
                bool localSaved = false;
                var store = new LocalCapacityFileStore(journal, (request, ok, error) =>
                {
                    if (!Current()) { error("Account changed; the restore operation is preserved."); return; }
                    client.RequestLocalCapacity(request, response =>
                    { if (Current()) ok(response); else error("Account changed; the restore remains pending."); }, error);
                });
                void SaveTable()
                {
                    guard();
                    store.Create(intent.Path, "tables.capacity", intent.Content, id =>
                    {
                        guard();intents.Confirm(intent.Id);
                        localSaved = true; capacityRestorePending = false;
                        if (Current()) saved?.Invoke(copy.SessionName);
                    }, id => capacityRestorePending = false, error =>
                    {
                        capacityRestorePending = false;
                        if (localSaved) Debug.LogWarning("[Capacity] Restored table is saved; confirmation remains pending.");
                        else failed?.Invoke(error);
                    });
                }
                var reportIds=(copy.Reports??new System.Collections.Generic.List<AnalysisReport>()).Select(r=>r.ReportId).ToArray();
                new AggregateCapacityStore(journal,guard,(request,ok,error)=>
                {
                    if(!Current()){error("Account changed; the restore operation is preserved.");return;}
                    client.RequestLocalCapacity(request,response=>{if(Current())ok(response);else error("Account changed; the restore remains pending.");},error);
                }).Write(AggregateCapacityStore.ReportAggregate(copy),"reports.capacity",Array.Empty<string>(),reportIds,
                    ()=>{},SaveTable,error=>{capacityRestorePending=false;failed?.Invoke(error);});
            }
            catch (Exception ex) { capacityRestorePending = false; failed?.Invoke(ex.Message); }
        }

        public string RestoreCloudBackup(SessionData original, int expectedUser)
        {
            if (CapacityEnforced) throw new InvalidOperationException("Use the reserved cloud-restore flow when capacity enforcement is enabled.");
            var copy = PrepareCloudCopy(original, expectedUser);
            string path=Path.Combine(SavePath,SanitizeFileName(copy.SessionName)+".json");
            WriteTableRecord(path,JsonUtility.ToJson(copy,true));
            return copy.SessionName;
        }
    }
}
