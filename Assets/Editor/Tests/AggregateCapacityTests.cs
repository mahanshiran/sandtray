using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sandplay.Data;
namespace Sandplay.Tests
{
    public class AggregateCapacityTests
    {
        string root;LocalCapacityJournal journal;bool current;int allocations;
        void Guard(){if(!current)throw new UnauthorizedAccessException();}
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"aggregate-capacity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);current=true;allocations=0;journal=new LocalCapacityJournal(root,"https://example.com",42,Guard);}
        [TearDown] public void Cleanup(){Directory.Delete(root,true);}
        void Transport(LocalCapacityRequest request,Action<LocalCapacityReceipt> success,Action<string> fail)
        {
            var op=journal.Read().First(x=>x.Id==request.operation_id || (x.HasLease && x.Lease.lease_id==request.lease_id));
            var receipt=op.HasLease ? op.Lease : new LocalCapacityReceipt{user_id=42,device_id=journal.DeviceId,resource_id=op.Id,capability=op.Capability,lease_id=Guid.NewGuid().ToString(),issued_at=DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o"),expires_at=DateTimeOffset.UtcNow.AddHours(1).ToString("o"),receipt="signed"};
            if(request.action=="allocate")allocations++;
            receipt.state=request.action=="release"?"released":request.action=="allocate"?"reserved":"active";success(receipt);
        }
        AggregateCapacityStore Store()=>new AggregateCapacityStore(journal,Guard,Transport);
        [Test] public void ExistingClientStoreUsesPolicyEnabledAfterOpening()
        {
            bool enabled=false;
            var clients=new ClientRecordStore(Path.Combine(root,"Clients"),Guard,
                capacityProvider:()=>enabled ? Store() : null);
            var legacy=clients.Save(new ClientRecord{Name="Legacy"});
            enabled=true;
            Assert.Throws<InvalidOperationException>(()=>clients.Save(new ClientRecord{Name="No reservation"}));
            clients.SaveAsync(new ClientRecord{Name="New"},null,false,true,c=>{},e=>Assert.Fail(e));
            Assert.AreEqual(1,allocations);Assert.AreEqual(2,clients.GetAll().Count);
            legacy.Name="Edited";clients.Save(legacy);
            Assert.AreEqual(1,allocations);
        }
        [Test] public void ExistingClientStoreCannotBypassNewlyEnabledLimit()
        {
            bool enabled=false;string error=null;
            var denied=new AggregateCapacityStore(journal,Guard,(request,ok,fail)=>fail("Limit reached"));
            var clients=new ClientRecordStore(Path.Combine(root,"Clients"),Guard,
                capacityProvider:()=>enabled ? denied : null);
            clients.Save(new ClientRecord{Name="Legacy"});enabled=true;
            clients.SaveAsync(new ClientRecord{Name="New"},null,false,true,c=>Assert.Fail(),e=>error=e);
            Assert.AreEqual("Limit reached",error);Assert.AreEqual(1,clients.GetAll().Count);
        }
        [Test] public void CreationGuardDoesNotBlockEditingExistingClients()
        {
            var directory=Path.Combine(root,"Clients");
            var existing=new ClientRecordStore(directory,Guard).Save(new ClientRecord{Name="Before"});
            var guarded=new ClientRecordStore(directory,Guard,null,()=>throw new InvalidOperationException("setup required"));
            existing.Name="After";guarded.Save(existing);
            Assert.AreEqual("After",guarded.GetAll()[0].Name);
            Assert.Throws<InvalidOperationException>(()=>guarded.Save(new ClientRecord{Name="New"}));
            Assert.AreEqual(1,guarded.GetAll().Count);
        }
        [TestCase("clients.capacity", "Clients/clients.json")]
        [TestCase("reports.capacity", "Boards/one")]
        public void LegacyInventoryOnlyChargesNewRecords(string capability,string aggregate)
        {
            int writes=0;
            Store().Write(aggregate,capability,new[]{"old"},new[]{"old","new"},()=>writes++,()=>{},e=>Assert.Fail(e));
            Assert.AreEqual(1,writes);Assert.AreEqual(1,allocations);
            Store().Write(aggregate,capability,new[]{"old","new"},new[]{"old","new"},()=>writes++,()=>{},e=>Assert.Fail(e));
            Assert.AreEqual(2,writes);Assert.AreEqual(1,allocations);
            Store().Write(aggregate,capability,new[]{"old","new"},new[]{"new"},()=>writes++,()=>{},e=>Assert.Fail(e));
            Assert.AreEqual("active",journal.Read().Single().State);
            Store().Write(aggregate,capability,new[]{"new"},Array.Empty<string>(),()=>writes++,()=>{},e=>Assert.Fail(e));
            Assert.AreEqual("released",journal.Read().Single().State);
            Assert.AreEqual(4,writes);Assert.AreEqual(1,allocations);
        }
        [Test] public void LegacyInventoryDoesNotBypassDeniedAllocation()
        {
            int writes=0;string error=null;
            var store=new AggregateCapacityStore(journal,Guard,(request,ok,fail)=>fail("Limit reached"));
            store.Write("Clients/clients.json","clients.capacity",new[]{"old"},new[]{"old","new"},()=>writes++,()=>{},e=>error=e);
            Assert.AreEqual("Limit reached",error);Assert.AreEqual(0,writes);
        }
        [Test] public void PendingTrackedRecordCannotBecomeLegacy()
        {
            var path="RecordSlots/reports.capacity/"+LocalCapacityJournal.Hash("Boards/one\ntracked")+".json";
            journal.Prepare(path,"reports.capacity");
            string error=null;int writes=0;
            Store().Write("Boards/one","reports.capacity",new[]{"tracked"},new[]{"tracked","new"},()=>writes++,()=>{},e=>error=e);
            Assert.IsNotNull(error);Assert.AreEqual(0,writes);Assert.AreEqual(0,allocations);
        }
        [Test] public void ReadinessDetectsUnregisteredInventory()
        {
            Assert.IsTrue(LocalCapacityReadiness.IsRegistered(root,journal));
            Directory.CreateDirectory(Path.Combine(root,"Clients"));
            File.WriteAllText(Path.Combine(root,"Clients/clients.json"),"{\"Clients\":[{\"Id\":\"a\",\"Name\":\"A\"}]}");
            Assert.IsFalse(LocalCapacityReadiness.IsRegistered(root,journal));
            Store().Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a"},()=>{},()=>{},e=>Assert.Fail(e));
            Assert.IsTrue(LocalCapacityReadiness.IsRegistered(root,journal));
        }
        [Test] public void ReservesEachEmbeddedRecordAndEditsDoNotAllocateAgain()
        {
            int writes=0;string error=null;
            Store().Write("Boards/one","reports.capacity",Array.Empty<string>(),new[]{"a","b"},()=>writes++,()=>{},e=>error=e);
            Assert.IsNull(error);Assert.AreEqual(1,writes);Assert.AreEqual(2,allocations);
            Store().Write("Boards/one","reports.capacity",new[]{"a","b"},new[]{"a","b"},()=>writes++,()=>{},e=>error=e);
            Assert.IsNull(error);Assert.AreEqual(2,writes);Assert.AreEqual(2,allocations);
        }
        [Test] public void SaveFailureRetainsSlotsAndRetryDoesNotDoubleCount()
        {
            string error=null;
            Store().Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a"},()=>throw new IOException("disk full"),()=>{},e=>error=e);
            Assert.IsNotNull(error);Assert.AreEqual(1,allocations);
            Store().Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a"},()=>{},()=>{},e=>Assert.Fail(e));Assert.AreEqual(1,allocations);
        }
        [Test] public void RemovalReleasesOnlyAfterPersistence()
        {
            var store=Store();store.Write("Boards/one","reports.capacity",Array.Empty<string>(),new[]{"a"},()=>{},()=>{},e=>Assert.Fail(e));
            store.Write("Boards/one","reports.capacity",new[]{"a"},Array.Empty<string>(),()=>throw new IOException(),()=>{},e=>{});
            Assert.AreEqual("active",journal.Read()[0].State);
            Store().Write("Boards/one","reports.capacity",new[]{"a"},Array.Empty<string>(),()=>{},()=>{},e=>Assert.Fail(e));
            Assert.AreEqual("released",journal.Read()[0].State);
        }
        [Test] public void ClientCreationEditAndArchiveReuseTheSlot()
        {
            var clients=new ClientRecordStore(Path.Combine(root,"Clients"),Guard,Store());
            var client=new ClientRecord{Name="First"};int saved=0;
            clients.SaveAsync(client,null,false,true,c=>saved++,e=>Assert.Fail(e));
            Assert.AreEqual(1,saved);Assert.AreEqual(1,allocations);
            client.Name="Edited";client.Archived=true;
            clients.Save(client);
            Assert.AreEqual(1,clients.GetAll().Count);Assert.IsTrue(clients.GetAll()[0].Archived);
            Assert.AreEqual(1,allocations);
            Assert.Throws<InvalidOperationException>(()=>clients.Save(new ClientRecord{Name="Bypass"}));
        }
        [Test] public void DelayedClientReservationCannotOverwriteNewerRecords()
        {
            Action reply=null;
            var store=new AggregateCapacityStore(journal,Guard,(request,ok,fail)=>reply=()=>Transport(request,ok,fail));
            var clients=new ClientRecordStore(Path.Combine(root,"Clients"),Guard,store);
            string error=null;
            clients.SaveAsync(new ClientRecord{Name="Pending"},null,false,true,c=>Assert.Fail("Must not overwrite"),e=>error=e);
            new ClientRecordStore(Path.Combine(root,"Clients"),Guard).Save(new ClientRecord{Name="Other"});
            for(int n=0;n<3 && reply!=null;n++){var callback=reply;reply=null;callback();}
            Assert.IsNotNull(error);Assert.AreEqual("Other",clients.GetAll()[0].Name);
        }
        [Test] public void AccountChangePreventsDelayedAggregateSave()
        {
            Action reply=null;bool written=false;
            var store=new AggregateCapacityStore(journal,Guard,(request,ok,fail)=>reply=()=>Transport(request,ok,fail));
            store.Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a"},()=>written=true,()=>{},e=>{});
            current=false;
            Assert.Throws<UnauthorizedAccessException>(()=>reply());
            Assert.IsFalse(written);
        }
        [Test] public void ReportIdentitySurvivesRenameButUnreservedChangesAreBlocked()
        {
            var a=new SessionData{BoardHistoryId="stable",SessionName="Before",Reports=new System.Collections.Generic.List<AnalysisReport>{new AnalysisReport{ReportId="a"}}};
            string before=UnityEngine.JsonUtility.ToJson(a);a.SessionName="After";
            Assert.AreEqual("Boards/stable",AggregateCapacityStore.ReportAggregate(a));
            AggregateCapacityStore.RequireSameReports(before,UnityEngine.JsonUtility.ToJson(a));
            a.Reports.Add(new AnalysisReport{ReportId="b"});
            Assert.Throws<InvalidOperationException>(()=>AggregateCapacityStore.RequireSameReports(before,UnityEngine.JsonUtility.ToJson(a)));
        }
        [Test] public void AbandonedCommittedBatchCanReleaseEverySlotAfterRestart()
        {
            Store().Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a","b"},()=>throw new IOException("interrupted"),()=>{},e=>{});
            var restarted=new LocalCapacityJournal(root,"https://example.com",42,Guard);
            foreach(var op in restarted.Read())
            {
                Assert.IsTrue(restarted.CanReleaseAggregateSlot(op.Id));
                new AggregateCapacityRecovery(restarted,Transport).Release(op.Id,()=>{},e=>Assert.Fail(e));
            }
            Assert.IsTrue(restarted.Read().All(x=>x.State=="released"));
            Assert.AreEqual(2,allocations);
        }
        [Test] public void LiveClientCannotLoseItsReservation()
        {
            var clients=new ClientRecordStore(Path.Combine(root,"Clients"),Guard,Store());
            clients.SaveAsync(new ClientRecord{Name="Live"},null,false,true,c=>{},e=>Assert.Fail(e));
            var op=journal.Read()[0];Assert.IsFalse(journal.CanReleaseAggregateSlot(op.Id));
            string error=null;new AggregateCapacityRecovery(journal,Transport).Release(op.Id,()=>Assert.Fail(),e=>error=e);
            Assert.IsNotNull(error);Assert.AreEqual("active",journal.Read()[0].State);
        }
        [Test] public void UncertainAllocationRetriesSameOperationBeforeRelease()
        {
            var op=journal.Prepare("RecordSlots/clients.capacity/"+LocalCapacityJournal.Hash("Clients/clients.json\na")+".json","clients.capacity");
            journal.BindSlot(op.Id,"Clients/clients.json","a");string operation=null;
            new AggregateCapacityRecovery(journal,(request,ok,fail)=>{if(request.action=="allocate")operation=request.operation_id;Transport(request,ok,fail);})
                .Release(op.Id,()=>{},e=>Assert.Fail(e));
            Assert.AreEqual(op.Id,operation);Assert.AreEqual("released",journal.Read()[0].State);
            Assert.IsFalse(Directory.Exists(Path.Combine(root,"Clients")));
        }
        [Test] public void InterruptedReleaseAndLostAcknowledgementAreRetryable()
        {
            Store().Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a"},()=>throw new IOException(),()=>{},e=>{});
            var op=journal.Read()[0];journal.BeginDeletion(op.Id);journal.DeleteSlotFile(op.Id);
            new AggregateCapacityRecovery(journal,(request,ok,fail)=>fail("offline")).Release(op.Id,()=>Assert.Fail(),e=>{});
            Assert.AreEqual("release_pending",journal.Read()[0].State);
            new AggregateCapacityRecovery(journal,Transport).Release(op.Id,()=>{},e=>Assert.Fail(e));
            Assert.AreEqual("released",journal.Read()[0].State);
        }
        [Test] public void CorruptInventoryPreventsRelease()
        {
            Store().Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a"},()=>throw new IOException(),()=>{},e=>{});
            Directory.CreateDirectory(Path.Combine(root,"Clients"));File.WriteAllText(Path.Combine(root,"Clients/clients.json"),"{}");
            string error=null;new AggregateCapacityRecovery(journal,Transport).Release(journal.Read()[0].Id,()=>Assert.Fail(),e=>error=e);
            Assert.IsNotNull(error);Assert.AreEqual("active",journal.Read()[0].State);
        }
        [Test] public void InventoryRecheckedAfterDelayedAllocation()
        {
            var op=journal.Prepare("RecordSlots/clients.capacity/"+LocalCapacityJournal.Hash("Clients/clients.json\na")+".json","clients.capacity");journal.BindSlot(op.Id,"Clients/clients.json","a");
            Action reply=null;string error=null;
            new AggregateCapacityRecovery(journal,(request,ok,fail)=>reply=()=>Transport(request,ok,fail)).Release(op.Id,()=>Assert.Fail(),e=>error=e);
            Directory.CreateDirectory(Path.Combine(root,"Clients"));File.WriteAllText(Path.Combine(root,"Clients/clients.json"),"{\"Clients\":[{\"Id\":\"a\",\"Name\":\"Live\"}]}");
            reply();Assert.IsNotNull(error);Assert.AreEqual("leased",journal.Read()[0].State);
        }
        [Test] public void CancelledSlotCannotBeUsedByDelayedBatchWriter()
        {
            Action resume=null;int requests=0;bool written=false;string error=null;
            var store=new AggregateCapacityStore(journal,Guard,(request,ok,fail)=>
            {
                if(request.action=="allocate" && ++requests==2)resume=()=>Transport(request,ok,fail);
                else Transport(request,ok,fail);
            });
            store.Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a","b"},()=>written=true,()=>{},e=>error=e);
            var first=journal.Read().First(x=>x.Record=="a");
            new AggregateCapacityRecovery(journal,Transport).Release(first.Id,()=>{},e=>Assert.Fail(e));
            resume();Assert.IsFalse(written);Assert.IsNotNull(error);
        }
        [Test] public void MissingPrimaryWithBackupPreventsOrphanRelease()
        {
            Store().Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a"},()=>throw new IOException(),()=>{},e=>{});
            Directory.CreateDirectory(Path.Combine(root,"Clients"));File.WriteAllText(Path.Combine(root,"Clients/clients.json.bak"),"{}");
            Assert.Throws<IOException>(()=>journal.CanReleaseAggregateSlot(journal.Read()[0].Id));
        }
        LocalInventoryMigration Migration()=>new LocalInventoryMigration(root,journal,Guard,Transport);
        string WriteInventory()
        {
            Directory.CreateDirectory(Path.Combine(root,"Sessions"));
            string json="{\"SandboxWidth\":10,\"SandboxDepth\":10,\"HeightmapResolution\":1,\"SessionName\":\"Original\",\"FutureField\":\"preserved\",\"Reports\":[{\"ResultText\":\"original report\"}]}";
            File.WriteAllText(Path.Combine(root,"Sessions/a.json"),json);
            new ClientRecordStore(Path.Combine(root,"Clients"),Guard).Save(new ClientRecord{Name="Existing client"});
            return json;
        }
        [Test] public void InventoryMigrationPreservesContentsAndRegistersEachRecordOnce()
        {
            string original=WriteInventory();var migration=Migration();var plan=migration.Review();
            Assert.AreEqual(1,plan.Tables);Assert.AreEqual(1,plan.Reports);Assert.AreEqual(1,plan.Clients);
            Assert.AreEqual(original,File.ReadAllText(Path.Combine(root,"Sessions/a.json")));
            bool done=false;migration.Run(()=>done=true,e=>Assert.Fail(e));Assert.IsTrue(done);Assert.AreEqual(3,allocations);
            var json=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(root,"Sessions/a.json")));
            Assert.AreEqual("preserved",(string)json["FutureField"]);Assert.IsNotEmpty((string)json["BoardHistoryId"]);
            Assert.AreEqual(original,plan.Entries.First(e=>e.Path=="Sessions/a.json").Original);
            Migration().Review();Migration().Run(()=>{},e=>Assert.Fail(e));Assert.AreEqual(3,allocations);
        }
        [Test] public void InterruptedMigrationResumesWithSameIdentitiesAndSlots()
        {
            WriteInventory();Migration().Review();bool failOnce=true;string error=null;
            var interrupted=new LocalInventoryMigration(root,journal,Guard,(request,ok,fail)=>
            {
                if(request.action=="allocate" && request.capability=="reports.capacity" && failOnce){failOnce=false;fail("quota or network");}
                else Transport(request,ok,fail);
            });
            interrupted.Run(()=>Assert.Fail(),e=>error=e);Assert.IsNotNull(error);
            var before=journal.Read().Select(x=>x.Id).ToArray();string normalized=File.ReadAllText(Path.Combine(root,"Sessions/a.json"));
            Migration().Run(()=>{},e=>Assert.Fail(e));
            Assert.IsTrue(before.All(id=>journal.Read().Any(x=>x.Id==id)));Assert.AreEqual(3,allocations);
            Assert.AreEqual(normalized,File.ReadAllText(Path.Combine(root,"Sessions/a.json")));
        }
        [Test] public void ExplicitNewReviewResumesAfterReducingInterruptedInventory()
        {
            WriteInventory();Migration().Review();
            var interrupted=new LocalInventoryMigration(root,journal,Guard,(request,ok,fail)=>
            { if(request.action=="allocate" && request.capability=="reports.capacity")fail("quota"); else Transport(request,ok,fail); });
            interrupted.Run(()=>Assert.Fail(),e=>{});
            string path=Path.Combine(root,"Sessions/a.json");
            var json=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            json["Reports"]=new Newtonsoft.Json.Linq.JArray();File.WriteAllText(path,json.ToString());
            var review=Migration().Review();Assert.AreEqual(0,review.Reports);
            bool done=false;Migration().Run(()=>done=true,e=>Assert.Fail(e));
            Assert.IsTrue(done);Assert.IsTrue(LocalCapacityReadiness.IsRegistered(root,journal));
            Assert.AreEqual(2,allocations);
        }
        [Test] public void MigrationRejectsChangedReviewWithoutWritingOrAllocating()
        {
            WriteInventory();Migration().Review();string path=Path.Combine(root,"Sessions/a.json");File.AppendAllText(path," ");
            string changed=File.ReadAllText(path),error=null;Migration().Run(()=>Assert.Fail(),e=>error=e);
            Assert.IsNotNull(error);Assert.AreEqual(0,allocations);Assert.AreEqual(changed,File.ReadAllText(path));
        }
        [Test] public void MigrationRejectsDuplicateTableIdentity()
        {
            WriteInventory();var path=Path.Combine(root,"Sessions/a.json");var json=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));json["BoardHistoryId"]="duplicate";
            File.WriteAllText(path,json.ToString());File.WriteAllText(Path.Combine(root,"Sessions/b.json"),json.ToString());
            Assert.Throws<InvalidDataException>(()=>Migration().Review());Assert.AreEqual(0,allocations);
        }
        [Test] public void CloudRestoreIntentKeepsIdsAcrossRetryAndStartsFreshAfterCompletion()
        {
            var intents=new CloudRestoreIntent(root,Guard);
            var first=intents.Prepare("source",()=>new SessionData
            {
                SessionName="Cloud copy",BoardHistoryId=Guid.NewGuid().ToString("N"),SandboxWidth=10,SandboxDepth=10,
                HeightmapResolution=1,Reports=new System.Collections.Generic.List<AnalysisReport>
                    {new AnalysisReport{ReportId=Guid.NewGuid().ToString("N"),ResultText="Report"}}
            });
            var retry=intents.Prepare("source",()=>throw new Exception("Must reuse"));
            Assert.AreEqual(first.Id,retry.Id);Assert.AreEqual(first.Content,retry.Content);
            Directory.CreateDirectory(Path.Combine(root,"Sessions"));
            File.WriteAllText(Path.Combine(root,first.Path),first.Content);
            intents.Confirm(first.Id);
            var next=intents.Prepare("source",()=>new SessionData
            {
                SessionName="Another copy",BoardHistoryId=Guid.NewGuid().ToString("N"),SandboxWidth=10,SandboxDepth=10,HeightmapResolution=1
            });
            Assert.AreNotEqual(first.Id,next.Id);
        }
        [Test] public void MissingClientAggregateRestoresAfterSlotsAndRetryIsIdempotent()
        {
            string target=Path.Combine(root,"Clients/clients.json");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            var source=new ClientRecordStore(Path.Combine(root,"Source"),Guard);
            source.Save(new ClientRecord{Name="Recovered"});
            string content=File.ReadAllText(Path.Combine(root,"Source/clients.json"));
            string candidate=target+".interrupted-test";File.WriteAllText(candidate,content);
            var records=new LocalRecordRecovery(root,Guard);var review=records.Preview("Clients/clients.json.interrupted-test");
            bool saved=false;
            new MissingAggregateRecovery(root,journal,Guard,Transport).Restore(records,review,()=>saved=true,e=>Assert.Fail(e));
            Assert.IsTrue(saved);Assert.AreEqual(1,allocations);Assert.AreEqual("Recovered",new ClientRecordStore(Path.Combine(root,"Clients"),Guard).GetAll()[0].Name);
            new MissingAggregateRecovery(root,journal,Guard,Transport).Restore(records,review,()=>saved=true,e=>Assert.Fail(e));
            Assert.AreEqual(1,allocations);
        }
        [Test] public void MissingTableRecoveryReservesReportsBeforeTable()
        {
            string sessions=Path.Combine(root,"Sessions");Directory.CreateDirectory(sessions);
            var data=new SessionData{SessionName="Recovered",BoardHistoryId=Guid.NewGuid().ToString("N"),SandboxWidth=10,SandboxDepth=10,HeightmapResolution=1,
                Reports=new System.Collections.Generic.List<AnalysisReport>{new AnalysisReport{ReportId="report",ResultText="text"}}};
            File.WriteAllText(Path.Combine(sessions,"lost.json.interrupted-test"),UnityEngine.JsonUtility.ToJson(data));
            var records=new LocalRecordRecovery(root,Guard);var review=records.Preview("Sessions/lost.json.interrupted-test");
            new MissingAggregateRecovery(root,journal,Guard,Transport).Restore(records,review,()=>{},e=>Assert.Fail(e));
            Assert.AreEqual(2,allocations);Assert.IsTrue(File.Exists(Path.Combine(sessions,"lost.json")));
            Assert.AreEqual("tables.capacity",journal.Read().Last(x=>x.State=="active").Capability);
        }
        [Test] public void MissingAggregateDoesNotOverwriteBackupOrNewPrimary()
        {
            string sessions=Path.Combine(root,"Sessions");Directory.CreateDirectory(sessions);
            var data=new SessionData{SessionName="Recovered",BoardHistoryId=Guid.NewGuid().ToString("N"),SandboxWidth=10,SandboxDepth=10,HeightmapResolution=1};
            string candidate=Path.Combine(sessions,"lost.json.interrupted-test");File.WriteAllText(candidate,UnityEngine.JsonUtility.ToJson(data));
            var records=new LocalRecordRecovery(root,Guard);var review=records.Preview("Sessions/lost.json.interrupted-test");
            File.WriteAllText(Path.Combine(sessions,"lost.json.bak"),"backup");
            string error=null;new MissingAggregateRecovery(root,journal,Guard,Transport).Restore(records,review,()=>Assert.Fail(),e=>error=e);
            Assert.IsNotNull(error);Assert.AreEqual(0,allocations);
        }
        [Test] public void MatchingUnregisteredPrimaryCannotBypassRecoveryCapacity()
        {
            Directory.CreateDirectory(Path.Combine(root,"Clients"));
            var source=new ClientRecordStore(Path.Combine(root,"Source"),Guard);
            source.Save(new ClientRecord{Name="Recovered"});
            string content=File.ReadAllText(Path.Combine(root,"Source/clients.json"));
            string candidate=Path.Combine(root,"Clients/clients.json.interrupted-test");File.WriteAllText(candidate,content);
            var records=new LocalRecordRecovery(root,Guard);var review=records.Preview("Clients/clients.json.interrupted-test");
            File.WriteAllText(Path.Combine(root,"Clients/clients.json"),content);
            string error=null;new MissingAggregateRecovery(root,journal,Guard,Transport).Restore(records,review,()=>Assert.Fail(),e=>error=e);
            Assert.IsNotNull(error);Assert.AreEqual(0,allocations);
        }
        [Test] public void DuplicateIdsFailClosed()
        {
            int writes=0;string error=null;
            Store().Write("Clients/clients.json","clients.capacity",Array.Empty<string>(),new[]{"a","a"},()=>writes++,()=>{},e=>error=e);
            Assert.AreEqual(0,allocations);Assert.AreEqual(0,writes);
        }
    }
}
