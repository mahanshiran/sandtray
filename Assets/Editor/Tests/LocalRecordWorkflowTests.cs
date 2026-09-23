using System;
using System.IO;
using NUnit.Framework;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Tests
{
    public class LocalRecordWorkflowTests
    {
        string root, guest, account; bool current;
        void Guard() { if (!current) throw new UnauthorizedAccessException(); }
        string Board(string name = "Guest table") => JsonUtility.ToJson(new SessionData { SessionName=name, SandboxWidth=10, SandboxDepth=10, HeightmapResolution=1, ClientId="private-client", TherapistNotes="retained notes" });
        [SetUp] public void Setup()
        {
            root=Path.Combine(Path.GetTempPath(),"record-workflows-"+Guid.NewGuid().ToString("N"));
            guest=Path.Combine(root,"guest"); account=Path.Combine(root,"account"); current=true;
            Directory.CreateDirectory(Path.Combine(guest,"Sessions")); Directory.CreateDirectory(Path.Combine(account,"Sessions"));
            File.WriteAllText(Path.Combine(guest,"Sessions","one.json"),Board());
        }
        [TearDown] public void Cleanup() { Directory.Delete(root,true); }
        [Test] public void ImportPreservesSourceAndRetryDoesNotDuplicateOrOverwriteEdits()
        {
            var imports=new LocalTableImport(guest,account,Guard); var review=imports.Preview("one.json"); var op=imports.Prepare(review);
            string path=imports.Finish(op.Id); Assert.AreEqual(Board(),File.ReadAllText(Path.Combine(guest,"Sessions","one.json")));
            var data=JsonUtility.FromJson<SessionData>(File.ReadAllText(Path.Combine(account,path)));
            Assert.IsTrue(Guid.TryParseExact(data.BoardHistoryId,"N",out _));
            Assert.IsTrue(string.IsNullOrEmpty(data.ClientId)); Assert.AreEqual("retained notes",data.TherapistNotes);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(path),data.SessionName);
            File.WriteAllText(Path.Combine(account,path),"edited by owner");
            var reopened=new LocalTableImport(guest,account,Guard); reopened.Finish(reopened.Prepare(review).Id);
            Assert.AreEqual("edited by owner",File.ReadAllText(Path.Combine(account,path)));
            Assert.AreEqual(1,Directory.GetFiles(Path.Combine(account,"Sessions"),"*.json").Length);
        }
        [Test] public void ChangedSourceRequiresReviewAgain()
        {
            var imports=new LocalTableImport(guest,account,Guard); var review=imports.Preview("one.json");
            File.WriteAllText(Path.Combine(guest,"Sessions","one.json"),Board("changed"));
            Assert.Throws<InvalidOperationException>(()=>imports.Prepare(review));
        }
        [Test] public void InterruptedImportReopensAndFinishesSameDestination()
        {
            var imports=new LocalTableImport(guest,account,Guard); var op=imports.Prepare(imports.Preview("one.json"));
            File.WriteAllText(Path.Combine(account,op.Path),op.Content); // crash after file installation, before acknowledgement
            var restarted=new LocalTableImport(guest,account,Guard);
            Assert.AreEqual(1,restarted.Pending().Length); restarted.Finish(op.Id);
            Assert.AreEqual(0,restarted.Pending().Length); Assert.AreEqual(1,Directory.GetFiles(Path.Combine(account,"Sessions"),"*.json").Length);
        }
        [Test] public void ImportConflictPreservesBothCopies()
        {
            var imports=new LocalTableImport(guest,account,Guard); var op=imports.Prepare(imports.Preview("one.json"));
            File.WriteAllText(Path.Combine(account,op.Path),"other copy");
            Assert.Throws<IOException>(()=>imports.Finish(op.Id));
            Assert.AreEqual("other copy",File.ReadAllText(Path.Combine(account,op.Path)));
            Assert.AreEqual(op.Content,imports.Read(op.Id).Content);
        }
        [Test] public void StaleAccountCannotFinishPreparedImport()
        {
            var imports=new LocalTableImport(guest,account,Guard); var op=imports.Prepare(imports.Preview("one.json"));current=false;
            Assert.Throws<UnauthorizedAccessException>(()=>imports.Finish(op.Id));
            Assert.IsFalse(File.Exists(Path.Combine(account,op.Path)));
        }
        [Test] public void TraversalAndInvalidTerrainAreRejected()
        {
            var imports=new LocalTableImport(guest,account,Guard);
            Assert.Throws<InvalidDataException>(()=>imports.Preview("../one.json"));
            File.WriteAllText(Path.Combine(guest,"Sessions","bad.json"),"{}");
            Assert.Throws<InvalidDataException>(()=>imports.Preview("bad.json"));
        }
        [Test] public void RecoveryPreservesBothVersionsAndOffersHistory()
        {
            string path=Path.Combine(account,"Sessions","board.json");
            File.WriteAllText(path,Board("current"));File.WriteAllText(path+".tmp",Board("unfinished"));
            var recovery=new LocalRecordRecovery(account,Guard); var review=recovery.Preview("Sessions/board.json.tmp");
            recovery.Restore(review,(p,c)=>File.WriteAllText(p,c));
            Assert.AreEqual(Board("unfinished"),File.ReadAllText(path));
            string[] histories=Directory.GetDirectories(Path.Combine(account,".record-recovery"));Assert.AreEqual(1,histories.Length);
            Assert.AreEqual(Board("current"),File.ReadAllText(Path.Combine(histories[0],"previous.json")));
            string previous=Array.Find(recovery.List(),p=>p.EndsWith("previous.json"));Assert.IsNotNull(previous);
            recovery.Restore(recovery.Preview(previous),(p,c)=>File.WriteAllText(p,c));Assert.AreEqual(Board("current"),File.ReadAllText(path));
        }
        [Test] public void RecoveryDetectsDiskChangesAndRejectsStaleAccount()
        {
            string path=Path.Combine(account,"Sessions","board.json");File.WriteAllText(path,Board());File.WriteAllText(path+".bak",Board("old"));
            var recovery=new LocalRecordRecovery(account,Guard);var review=recovery.Preview("Sessions/board.json.bak");File.WriteAllText(path,Board("new"));
            Assert.Throws<InvalidOperationException>(()=>recovery.Restore(review,(p,c)=>File.WriteAllText(p,c)));
            current=false;Assert.Throws<UnauthorizedAccessException>(()=>recovery.Preview("Sessions/board.json.bak"));
        }
        [Test] public void FailedRestoreKeepsRecoveryHistoryAndCandidate()
        {
            string path=Path.Combine(account,"Sessions","board.json");File.WriteAllText(path,Board());File.WriteAllText(path+".bak",Board("old"));
            var recovery=new LocalRecordRecovery(account,Guard);
            Assert.Throws<IOException>(()=>recovery.Restore(recovery.Preview("Sessions/board.json.bak"),(p,c)=>throw new IOException("full disk")));
            Assert.AreEqual(Board(),File.ReadAllText(path));Assert.IsTrue(File.Exists(path+".bak"));
            Assert.AreEqual(1,Directory.GetDirectories(Path.Combine(account,".record-recovery")).Length);
        }
        [Test] public void LaterSavePreservesAnInterruptedTemporaryRecord()
        {
            string path=Path.Combine(account,"Sessions","board.json");
            File.WriteAllText(path,Board("old")); File.WriteAllText(path+".tmp",Board("interrupted"));
            // Exercise the existing public store, which uses the shared atomic writer.
            string clients=Path.Combine(account,"Clients");Directory.CreateDirectory(clients);
            File.WriteAllText(Path.Combine(clients,"clients.json.tmp"),"{\"Clients\":[]}");
            new ClientRecordStore(clients,Guard).Save(new ClientRecord {Name="New client"});
            string[] preserved=Directory.GetFiles(clients,"clients.json.interrupted-*");Assert.AreEqual(1,preserved.Length);
            Assert.AreEqual("{\"Clients\":[]}",File.ReadAllText(preserved[0]));
            Assert.IsTrue(Array.Exists(new LocalRecordRecovery(account,Guard).List(),x=>x.Contains("clients.json.interrupted-")));
        }
        [Test] public void ExplicitInvalidationCannotReviveAnOldAccountHandle()
        {
            int user=42;var old=new LocalStorageScope(account,user,()=>user);old.Invalidate();
            var next=new LocalStorageScope(account,user,()=>user);
            Assert.Throws<UnauthorizedAccessException>(()=>old.RequireCurrent());Assert.DoesNotThrow(()=>next.RequireCurrent());
        }
    }
}
