using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class BoardClientLabelTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root, home, boards;
        private SceneBootstrapper bootstrap;
        private SessionManager sessions, previousSessions;
        private ClientRecordStore store;
        private ClientRecord client;
        private string directory, linkedName, otherName;
        private void Set(string field, object value) => typeof(SceneBootstrapper).GetField(field,Flags).SetValue(bootstrap,value);
        private void Refresh() => typeof(SceneBootstrapper).GetMethod("RefreshBoardList",Flags).Invoke(bootstrap,null);
        [SetUp]
        public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"sandtray-label-tests-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            root=new GameObject("BoardLabelsTest",typeof(RectTransform),typeof(Canvas));
            bootstrap=root.AddComponent<SceneBootstrapper>();
            previousSessions=SessionManager.Instance;
            sessions=root.AddComponent<SessionManager>();
            typeof(SessionManager).GetField("_requireWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(sessions, LocalAccountStorage.CaptureGuard());
            typeof(SessionManager).GetProperty("Instance").SetValue(null,sessions);
            typeof(SessionManager).GetField("_savePath",Flags).SetValue(sessions,directory);
            store=new ClientRecordStore(Path.Combine(directory,"Clients"));
            client=store.Save(new ClientRecord { Name="Alex <B> Rivera", Archived=true });
            Set("_clientStore",store);
            home=new GameObject("Home",typeof(RectTransform));home.transform.SetParent(root.transform,false);
            home.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,240);
            boards=new GameObject("MyBoards",typeof(RectTransform));boards.transform.SetParent(root.transform,false);
            boards.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,600);
            Set("_boardListContent",home);Set("_myBoardsListContent",boards);
            linkedName="Linked-"+Guid.NewGuid().ToString("N");otherName="Personal-"+Guid.NewGuid().ToString("N");
            Write(linkedName,client.Id);Write(otherName,null);
        }
        private void Write(string name,string id) => File.WriteAllText(Path.Combine(directory,name+".json"),
            JsonUtility.ToJson(new SessionData { SessionName=name,ClientId=id,ModifiedAt="2026-09-10T12:00:00Z" }));
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            typeof(SessionManager).GetProperty("Instance").SetValue(null,previousSessions);
            // Delete only this test's unique generated fixture directory.
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
        [Test]
        public void BothLists_LabelOnlyLinkedBoardsIncludingArchivedClients()
        {
            Refresh();
            foreach(var container in new[]{home,boards})
            {
                var labels=container.GetComponentsInChildren<TMP_Text>();
                int count=0;
                foreach(var label in labels)if(label.name=="ClientName")
                {
                    count++;StringAssert.Contains(client.Name,label.text);
                    Assert.IsFalse(label.richText);Assert.IsFalse(label.raycastTarget);
                    Assert.AreEqual(TextOverflowModes.Ellipsis,label.overflowMode);
                    Assert.IsFalse(label.transform.parent.GetComponent<Image>().raycastTarget);
                }
                Assert.AreEqual(1,count);
            }
            var first=boards.transform.GetChild(0).GetComponent<RectTransform>();
            var second=boards.transform.GetChild(1).GetComponent<RectTransform>();
            Assert.That(Mathf.Abs(second.anchoredPosition.y-first.anchoredPosition.y),Is.GreaterThanOrEqualTo(first.rect.height));
        }
        [Test]
        public void ClientRenameAndUnlink_RefreshBothLists()
        {
            Refresh(); client.Name="Updated client";store.Save(client);Refresh();
            foreach (var container in new[] { home, boards })
            {
                int count = 0;
                foreach (var label in container.GetComponentsInChildren<TMP_Text>())
                    if (label.name == "ClientName") { count++; StringAssert.Contains("Updated client", label.text); }
                Assert.AreEqual(1, count);
            }
            sessions.AssignClient(linkedName,"");Refresh();
            foreach(var label in root.GetComponentsInChildren<TMP_Text>())Assert.AreNotEqual("ClientName",label.name);
        }
        [Test]
        public void MissingClientRecord_ShowsLinkedFallbackWithoutExposingId()
        {
            Write(linkedName,"missing-private-id");Refresh();
            foreach (var container in new[] { home, boards })
            {
                int count = 0;
                foreach (var label in container.GetComponentsInChildren<TMP_Text>()) if (label.name == "ClientName")
                {
                    count++;
                    Assert.AreEqual(Localization.Get("board.client_linked"), label.text);
                    StringAssert.DoesNotContain("missing-private-id", label.text);
                }
                Assert.AreEqual(1, count);
            }
        }
    }
}
