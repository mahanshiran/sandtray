using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Sandplay.Data
{
    // Diagnostic only: legacy inventory does not need quota registration.
    public static class LocalCapacityReadiness
    {
        public static bool IsRegistered(string root, LocalCapacityJournal journal)
        {
            var entries=journal.Read();
            bool Registered(string path) => entries.Any(op=>op.Path==path && (op.State=="saved" || op.State=="active"));
            string sessions=Path.Combine(root,"Sessions");
            if(Directory.Exists(sessions))
            {
                foreach(var backup in Directory.GetFiles(sessions,"*.json.bak"))
                    if(!File.Exists(backup.Substring(0,backup.Length-4)))return false;
                foreach(var file in Directory.GetFiles(sessions,"*.json"))
                {
                    LocalTableImport.Safe(root,file);
                    if(!Registered("Sessions/"+Path.GetFileName(file)))return false;
                    var data=JsonUtility.FromJson<SessionData>(File.ReadAllText(file));
                    if(data==null)return false;
                    foreach(var report in data.Reports ?? new System.Collections.Generic.List<AnalysisReport>())
                        if(report==null || string.IsNullOrEmpty(report.ReportId) || !Registered(AggregateCapacityStore.PathFor(
                            AggregateCapacityStore.ReportAggregate(data),"reports.capacity",report.ReportId)))return false;
                }
            }
            string clients=Path.Combine(root,"Clients/clients.json");
            LocalTableImport.Safe(root,clients);
            if(!File.Exists(clients) && File.Exists(clients+".bak"))return false;
            if(File.Exists(clients))foreach(var id in ClientRecordStore.RecordIds(File.ReadAllText(clients)))
                if(!Registered(AggregateCapacityStore.PathFor("Clients/clients.json","clients.capacity",id)))return false;
            string reports=Path.Combine(root,"Reports");
            if(Directory.Exists(reports) && Directory.GetFiles(reports,"*.json").Length>0)return false;
            return true;
        }
    }
}
