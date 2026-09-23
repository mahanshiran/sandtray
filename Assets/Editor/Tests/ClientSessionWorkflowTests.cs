using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Data;

namespace Sandplay.Tests
{
    public class ClientSessionWorkflowTests
    {
        [Test]
        public void ClientEditsModeGrantsFirstJoinerAndReplacementAutomatically()
        {
            var previousGame = GameManager.Instance;
            var root = new GameObject("Automatic session editor regression");
            var config = ScriptableObject.CreateInstance<GameConfig>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var game = root.AddComponent<GameManager>();
                typeof(GameManager).GetProperty("Instance").SetValue(null, game);
                game.Initialize(config);
                var net = root.AddComponent<NetworkBootstrapper>();
                var type = typeof(NetworkBootstrapper);
                void Set(string name, object value) => type.GetField(name, flags).SetValue(net, value);
                var register = type.GetMethod("HandleSessionRoleRequest", flags);
                var depart = type.GetMethod("HandleSessionDeparture", flags);
                using var transport = new MemoryStream();
                Set("_relayMode", true);
                Set("_isHost", true);
                Set("_isOnline", true);
                Set("_therapistMode", true);
                Set("_autoAssignJoinerEditor", true);
                Set("_relayStream", transport);

                string first = Guid.NewGuid().ToString("N");
                register.Invoke(net, new object[] { NetworkBootstrapper.WriteSessionRole(PlayerRole.Observer, first, "First") });
                Assert.AreEqual(first, net.EditorToken);
                Assert.IsFalse(net.EditingPaused);
                Assert.AreEqual(PlayerRole.Psychologist, game.NetworkRole);
                transport.Position = 0;
                using (var reader = new BinaryReader(transport, System.Text.Encoding.UTF8, true))
                {
                    Assert.Greater(reader.ReadInt32(), 1);
                    Assert.AreEqual(NetMsgType.RevisionRole, (NetMsgType)reader.ReadByte());
                    Assert.Greater(reader.ReadInt64(), 0);
                    Assert.AreEqual(PlayerRole.Patient, (PlayerRole)reader.ReadByte());
                }
                transport.Position = transport.Length;

                using (var departure = new MemoryStream())
                using (var writer = new BinaryWriter(departure))
                {
                    writer.Write(first);
                    depart.Invoke(net, new object[] { departure.ToArray() });
                }
                Assert.IsTrue(net.EditingPaused);
                string replacement = Guid.NewGuid().ToString("N");
                register.Invoke(net, new object[] { NetworkBootstrapper.WriteSessionRole(PlayerRole.Observer, replacement, "Replacement") });
                Assert.AreEqual(replacement, net.EditorToken);
                Assert.IsFalse(net.EditingPaused);

                net.SetSessionEditor(null);
                using (var departure = new MemoryStream())
                using (var writer = new BinaryWriter(departure))
                {
                    writer.Write(replacement);
                    depart.Invoke(net, new object[] { departure.ToArray() });
                }
                string observer = Guid.NewGuid().ToString("N");
                register.Invoke(net, new object[] { NetworkBootstrapper.WriteSessionRole(PlayerRole.Observer, observer, "Observer") });
                Assert.IsNull(net.EditorToken, "Taking control must stop automatic handoff.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
                typeof(GameManager).GetProperty("Instance").SetValue(null, previousGame);
            }
        }

        [Test]
        public void CameraRotationPreservesPaddingAndRotatesOddAndEvenPlanes()
        {
            var rotate = typeof(AgoraManager).Assembly.GetType("Sandplay.Core.CameraPlaneRotation")
                .GetMethod("Rotate180", BindingFlags.Static | BindingFlags.NonPublic);
            byte[] padded = { 1, 2, 99, 3, 4, 98 };
            rotate.Invoke(null, new object[] { padded, 2, 2, 3 });
            CollectionAssert.AreEqual(new byte[] { 4, 3, 99, 2, 1, 98 }, padded);
            byte[] odd = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            rotate.Invoke(null, new object[] { odd, 3, 3, 3 });
            CollectionAssert.AreEqual(new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 }, odd);
            rotate.Invoke(null, new object[] { odd, 3, 3, 3 });
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, odd);
            Assert.Throws<TargetInvocationException>(() => rotate.Invoke(null, new object[] { odd, 4, 3, 3 }));
        }

        [Test]
        public void RevisionEditsRejectOldGrantsAndSnapshotWaitsForModels()
        {
            var root = new GameObject("Revision regression");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var net = root.AddComponent<NetworkBootstrapper>();
                void Set(string name, object value) => typeof(NetworkBootstrapper).GetField(name, flags).SetValue(net, value);
                object Get(string name) => typeof(NetworkBootstrapper).GetField(name, flags).GetValue(net);
                var receive = typeof(NetworkBootstrapper).GetMethod("ReceiveRevisionMessage", flags);
                Set("_relayMode", true); Set("_isHost", true); Set("_editorToken", "editor");
                var revisions = (System.Collections.Generic.Dictionary<string, long>)Get("_participantRevisions");
                var sequences = (System.Collections.Generic.Dictionary<string, long>)Get("_participantSequences");
                revisions["editor"] = 4; sequences["editor"] = 0;
                byte[] Edit(long revision, long sequence)
                {
                    using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
                    writer.Write("editor"); writer.Write(revision); writer.Write(sequence);
                    writer.Write((byte)NetMsgType.ClientRemoveObject); writer.Write(123u);
                    return bytes.ToArray();
                }
                receive.Invoke(net, new object[] { NetMsgType.RevisionEdit, Edit(3, 1) });
                Assert.AreEqual(0, sequences["editor"]);
                receive.Invoke(net, new object[] { NetMsgType.RevisionEdit, Edit(4, 1) });
                Assert.AreEqual(1, sequences["editor"]);
                receive.Invoke(net, new object[] { NetMsgType.RevisionEdit, Edit(4, 1) });
                Assert.AreEqual(1, sequences["editor"]);
                revisions["editor"] = 5;
                receive.Invoke(net, new object[] { NetMsgType.RevisionEdit, Edit(4, 2) });
                Assert.AreEqual(1, sequences["editor"], "Already queued host edits must be revalidated.");

                Set("_isHost", false); Set("_isOnline", true); Set("_snapshotId", 9L);
                Set("_snapshotApplied", true);
                var pending = (System.Collections.Generic.HashSet<uint>)Get("_loadingObjects");
                pending.Add(123);
                var ack = (System.Collections.IEnumerator)typeof(NetworkBootstrapper)
                    .GetMethod("AcknowledgeSnapshot", flags).Invoke(net, new object[] { 9L, (int)Get("_relayAttempt") });
                Assert.IsTrue(ack.MoveNext());
                Assert.IsFalse((bool)Get("_snapshotReady"));
                Set("_snapshotId", 10L); pending.Clear();
                Assert.IsFalse(ack.MoveNext());
                Assert.IsFalse((bool)Get("_snapshotReady"), "Stale completion must not unlock editing.");
                System.Collections.IEnumerator Ack() => (System.Collections.IEnumerator)typeof(NetworkBootstrapper)
                    .GetMethod("AcknowledgeSnapshot", flags).Invoke(net, new object[] { 10L, (int)Get("_relayAttempt") });
                Assert.IsFalse(Ack().MoveNext());
                Assert.IsFalse((bool)Get("_snapshotReady"), "Missing transport must not unlock editing.");
                var broken = new MemoryStream(); broken.Dispose();
                Set("_relayStream", broken);
                Assert.IsFalse(Ack().MoveNext());
                Assert.IsFalse((bool)Get("_snapshotReady"), "Failed write must not unlock editing.");
                var transport = new MemoryStream(); Set("_relayStream", transport);
                Set("_snapshotApplied", false);
                Assert.IsFalse(Ack().MoveNext());
                Assert.AreEqual(0, transport.Length, "Incomplete snapshots cannot be acknowledged.");
                Set("_snapshotApplied", true);
                Assert.IsFalse(Ack().MoveNext());
                Assert.IsTrue((bool)Get("_snapshotReady"));
                Assert.AreEqual((byte)NetMsgType.SnapshotAck, transport.ToArray()[4]);
                receive.Invoke(net, new object[] { NetMsgType.SnapshotBegin, BitConverter.GetBytes(9L) });
                Assert.AreEqual(10L, Get("_snapshotId"));
                Assert.IsTrue((bool)Get("_snapshotReady"), "Old begin must not reset the current snapshot.");
                receive.Invoke(net, new object[] { NetMsgType.SnapshotBegin, new byte[7] });
                Assert.AreEqual(10L, Get("_snapshotId"));
                Set("_relayStream", null); transport.Dispose();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private sealed class CountingCommand : ICommand
        {
            public int Executions, Undos;
            public void Execute() { Executions++; }
            public void Undo() { Undos++; }
        }

        [Test]
        public void HandoverClearsHistoryAndObserverCannotUndoOrRedo()
        {
            var previousGame = GameManager.Instance;
            var previousUndo = UndoManager.Instance;
            var root = new GameObject("Handover history regression");
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var game = root.AddComponent<GameManager>();
                typeof(GameManager).GetProperty("Instance").SetValue(null, game);
                game.Initialize(config);
                var undo = root.AddComponent<UndoManager>();
                typeof(UndoManager).GetProperty("Instance").SetValue(null, undo);
                var command = new CountingCommand();
                undo.Record(command);
                game.NetworkRole = PlayerRole.Patient;
                Assert.IsTrue(undo.CanUndo, "Repeated role messages must preserve history.");
                game.NetworkRole = PlayerRole.Observer;
                Assert.IsFalse(undo.CanUndo);
                undo.Record(command); // Direct calls must also enforce permissions.
                undo.UndoLast();
                Assert.AreEqual(0, command.Undos);
                game.NetworkRole = PlayerRole.Patient;
                Assert.IsFalse(undo.CanUndo);
                undo.Record(command);
                undo.UndoLast();
                Assert.AreEqual(1, command.Undos);
                game.NetworkRole = PlayerRole.Observer;
                undo.RedoLast();
                Assert.AreEqual(0, command.Executions);
                Assert.IsFalse(undo.CanRedo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
                typeof(GameManager).GetProperty("Instance").SetValue(null, previousGame);
                typeof(UndoManager).GetProperty("Instance").SetValue(null, previousUndo);
            }
        }

        [Test]
        public void ReconnectPreservesRealModelsAndClearsVoiceParticipants()
        {
            var previous = Sandplay.Objects.NetworkCatalogRegistry.Snapshot();
            var template = new GameObject("Real cached model");
            var go = new GameObject("Voice privacy regression");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var oldItem = new Sandplay.Objects.NetworkCatalogItem
                    { id = "reconnect-test", model_url = "https://example.invalid/model.glb", LoadedPrefab = template };
                Sandplay.Objects.NetworkCatalogRegistry.Register(new[] { oldItem });
                var incoming = new Sandplay.Objects.NetworkCatalogItem
                    { id = oldItem.id, model_url = oldItem.model_url };
                var manifest=new[] { incoming };
                Sandplay.Objects.NetworkCatalogRegistry.Merge(manifest);
                Assert.AreSame(oldItem,manifest[0],"Unchanged manifests retain the existing object and in-flight loads.");
                Assert.AreSame(template, manifest[0].LoadedPrefab);
                var changed = new Sandplay.Objects.NetworkCatalogItem
                    { id = oldItem.id, model_url = "https://example.invalid/new.glb" };
                Sandplay.Objects.NetworkCatalogRegistry.Merge(new[] { changed });
                Assert.IsNull(changed.LoadedPrefab);

                var voice = go.AddComponent<AgoraManager>();
                Assert.IsTrue(voice.MicMuted);
                var unavailable = new Sandplay.Objects.NetworkCatalogItem
                    { id = Guid.NewGuid().ToString("N"), display_name = "Unavailable test model" };
                var load = Sandplay.Objects.NetworkCatalogLoader.PreloadGlb(voice, unavailable);
                Assert.IsFalse(load.MoveNext());
                Assert.IsNull(unavailable.LoadedPrefab, "Failed loads must not become permanent cubes.");
                var peers = (System.Collections.Generic.HashSet<uint>)typeof(AgoraManager)
                    .GetField("_remoteUids", flags).GetValue(voice);
                peers.Add(123);
                int departures = 0;
                voice.OnRemoteUserLeft += uid => { Assert.AreEqual(123u, uid); departures++; };
                voice.LeaveChannel();
                voice.LeaveChannel();
                Assert.AreEqual(1, departures);
                Assert.IsEmpty(voice.RemoteUids);
                Assert.IsTrue(voice.MicMuted);
            }
            finally
            {
                Sandplay.Objects.NetworkCatalogRegistry.Register(previous);
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void DepartureIsIdempotentAndCredentialGuardsRejectReplacement()
        {
            var root = new GameObject("Reconnect regression");
            var account = BackendClient.Instance;
            string token = account.AccessToken;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var net = root.AddComponent<NetworkBootstrapper>();
                var roster = (System.Collections.Generic.List<NetworkBootstrapper.SessionParticipant>)
                    typeof(NetworkBootstrapper).GetField("_sessionParticipants", flags).GetValue(net);
                roster.Add(new NetworkBootstrapper.SessionParticipant { Token = "participant", Name = "Test" });
                int notifications = 0;
                net.OnClientCountChanged += _ => notifications++;
                using var bytes = new MemoryStream();
                using var writer = new BinaryWriter(bytes);
                writer.Write("participant");
                var departed = typeof(NetworkBootstrapper).GetMethod("HandleSessionDeparture", flags);
                departed.Invoke(net, new object[] { bytes.ToArray() });
                departed.Invoke(net, new object[] { bytes.ToArray() });
                Assert.AreEqual(1, notifications);
                Assert.AreEqual(0, net.ConnectedClients);
                var type = typeof(NetworkBootstrapper);
                var queue = (System.Collections.Generic.Queue<Action>)type.GetField("_mainThreadQueue", flags).GetValue(net);
                var enqueue = type.GetMethod("EnqueueCurrentRelay", flags);
                using var oldSocket = new System.Net.Sockets.TcpClient();
                using var newSocket = new System.Net.Sockets.TcpClient();
                type.GetField("_isOnline", flags).SetValue(net, true);
                type.GetField("_relaySocket", flags).SetValue(net, oldSocket);
                int applied = 0;
                enqueue.Invoke(net, new object[] { oldSocket, (Action)(() => applied++) });
                type.GetField("_relaySocket", flags).SetValue(net, newSocket);
                queue.Dequeue()();
                Assert.AreEqual(0, applied);
                enqueue.Invoke(net, new object[] { newSocket, (Action)(() => applied++) });
                queue.Dequeue()();
                Assert.AreEqual(1, applied);
                type.GetField("_isOnline", flags).SetValue(net, false);
                var guard = (Func<bool>)typeof(BackendClient).GetMethod("CaptureCredentialGuard", flags).Invoke(account, null);
                Assert.IsTrue(guard());
                typeof(BackendClient).GetProperty("AccessToken").SetValue(account, "replacement-test-token");
                Assert.IsFalse(guard());
            }
            finally
            {
                typeof(BackendClient).GetProperty("AccessToken").SetValue(account, token);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CatalogCategoryStripUsesAvailableCategoriesAndHorizontalScrolling()
        {
            var searchable = new Sandplay.Objects.NetworkCatalogItem {
                display_name = "Oak Tree", category = "Nature", tags = new[] { "forest", null, "green" } };
            Assert.IsTrue(SceneBootstrapper.CatalogMatchesSearch(searchable, " OAK "));
            Assert.IsTrue(SceneBootstrapper.CatalogMatchesSearch(searchable, "FOREST"));
            Assert.IsTrue(SceneBootstrapper.CatalogMatchesSearch(searchable, "nature"));
            Assert.IsTrue(SceneBootstrapper.CatalogMatchesSearch(searchable, " "));
            Assert.IsFalse(SceneBootstrapper.CatalogMatchesSearch(searchable, "animal"));
            Assert.IsFalse(SceneBootstrapper.CatalogMatchesSearch(null, ""));
            var root = new GameObject("Catalog test", typeof(RectTransform));
            try
            {
                var ui = root.AddComponent<SceneBootstrapper>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = typeof(SceneBootstrapper);
                type.GetField("_catalogPanel", flags).SetValue(ui, root);
                type.GetField("_catalogFilterItems", flags).SetValue(ui, new[] {
                    new Sandplay.Objects.NetworkCatalogItem { category = "animals" },
                    new Sandplay.Objects.NetworkCatalogItem { category = " People " },
                    new Sandplay.Objects.NetworkCatalogItem { category = "people" },
                    new Sandplay.Objects.NetworkCatalogItem { category = "abstract" } });
                type.GetMethod("CreateCatalogCategoryStrip", flags).Invoke(ui, null);
                var scroll = root.GetComponentInChildren<ScrollRect>();
                var input = root.GetComponentInChildren<TMP_InputField>();
                Assert.AreEqual("CatalogSearch", input.name);
                Assert.AreEqual(TMP_InputField.LineType.SingleLine, input.lineType);
                Assert.Greater(((RectTransform)input.transform).anchoredPosition.y,
                    ((RectTransform)scroll.transform).anchoredPosition.y);
                Assert.IsTrue(scroll.horizontal); Assert.IsFalse(scroll.vertical);
                Assert.AreEqual(4, scroll.content.childCount);
                Assert.AreEqual("Category_all", scroll.content.GetChild(0).name);
                Assert.AreEqual("Category_people", scroll.content.GetChild(1).name);
                Assert.AreEqual("Category_animals", scroll.content.GetChild(2).name);
                Assert.IsFalse(root.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Official Catalog"));
                type.GetField("_selectedCatalogCategory", flags).SetValue(ui, "removed");
                type.GetMethod("BuildCatalogCategoryChips", flags).Invoke(ui, null);
                Assert.IsNull(type.GetField("_selectedCatalogCategory", flags).GetValue(ui));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuthenticatedRelayPayloadIsScopedAndDisconnectInvalidatesAttempts()
        {
            var root = new GameObject("Relay authentication payload");
            try
            {
                var net = root.AddComponent<NetworkBootstrapper>();
                Assert.IsTrue(net.UseAuthenticatedRelay);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var encode = typeof(NetworkBootstrapper).GetMethod("RelayControlPayload", flags);
                byte[] Packet(string room) => (byte[])encode.Invoke(net, new object[] { "signed-ticket", room });
                using (var host = new BinaryReader(new MemoryStream(Packet(null))))
                {
                    Assert.AreEqual("signed-ticket", host.ReadString());
                    Assert.AreEqual(host.BaseStream.Length, host.BaseStream.Position);
                }
                using (var join = new BinaryReader(new MemoryStream(Packet("ABCDEF"))))
                {
                    Assert.AreEqual("signed-ticket", join.ReadString());
                    Assert.AreEqual("ABCDEF", join.ReadString());
                    Assert.AreEqual(join.BaseStream.Length, join.BaseStream.Position);
                }
                var generation = typeof(NetworkBootstrapper).GetField("_relayAttempt", flags);
                int before = (int)generation.GetValue(net);
                net.Disconnect();
                Assert.Greater((int)generation.GetValue(net), before);
                var reconnecting = typeof(NetworkBootstrapper).GetField("_isReconnecting", flags);
                var current = typeof(NetworkBootstrapper).GetMethod("IsCurrentRelayReconnect", flags);
                reconnecting.SetValue(net, true);
                int active = (int)generation.GetValue(net);
                Assert.IsTrue((bool)current.Invoke(net, new object[] { active }));
                Assert.IsFalse((bool)current.Invoke(net, new object[] { before }));
                reconnecting.SetValue(net, false);
                Assert.IsFalse((bool)current.Invoke(net, new object[] { active }));
                // A later reconnect being active must not resurrect an older attempt.
                generation.SetValue(net, active + 1);
                reconnecting.SetValue(net, true);
                Assert.IsFalse((bool)current.Invoke(net, new object[] { active }));
                Assert.IsTrue((bool)current.Invoke(net, new object[] { active + 1 }));
                reconnecting.SetValue(net, false);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavedReportGuardRejectsChangedTextIdentityAndAccount()
        {
            var account = BackendClient.Instance;
            string originalToken = account.AccessToken;
            var root = new GameObject("Saved report guard test");
            try
            {
                var ui = root.AddComponent<SceneBootstrapper>();
                var report = new AnalysisReport { ReportId = "one", ResultText = "Reviewed" };
                Func<bool> Guard() => (Func<bool>)ui.GetType().GetMethod("CaptureSavedReportGuard",
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, new object[] { report });
                var textGuard = Guard(); report.ResultText = "Unreviewed";
                Assert.IsFalse(textGuard());
                var idGuard = Guard(); report.ReportId = "two";
                Assert.IsFalse(idGuard());
                var accountGuard = Guard();
                typeof(BackendClient).GetProperty("AccessToken").SetValue(account, originalToken + "test-change");
                Assert.IsFalse(accountGuard());
                Assert.IsTrue(Guard()());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                typeof(BackendClient).GetProperty("AccessToken").SetValue(account, originalToken);
            }
        }

        [Test]
        public void ConnectionErrorClearsLoadingAndConnectingAndHasSeparateCloseButton()
        {
            var root = new GameObject("Connection error test", typeof(RectTransform), typeof(Canvas));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(900, 600);
                var ui = root.AddComponent<SceneBootstrapper>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                void Set(string field, object value) => ui.GetType().GetField(field, flags).SetValue(ui, value);
                Set("_safeArea", root);
                var loading = new GameObject("Loading"); loading.transform.SetParent(root.transform); Set("_boardLoadingOverlay", loading);
                var connecting = new GameObject("Connecting"); connecting.transform.SetParent(root.transform); Set("_netOverlayGo", connecting);
                var show = ui.GetType().GetMethod("ShowConnectionError", flags);
                show.Invoke(ui, new object[] { "Server requires an update" });
                Assert.IsTrue(loading == null);
                Assert.IsFalse(connecting.activeSelf);
                var message = root.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "ConnectionErrorMessage");
                Assert.AreEqual("Server requires an update", message.text);
                Assert.IsNull(message.GetComponent<Button>());
                show.Invoke(ui, new object[] { "Second error" });
                Assert.AreEqual(1, root.GetComponentsInChildren<TMP_Text>().Count(t => t.name == "ConnectionErrorMessage"));
                root.GetComponentsInChildren<Button>().Single(b => b.name == "DismissConnectionError").onClick.Invoke();
                Assert.IsFalse(root.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "ConnectionErrorMessage"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ResultGuardRejectsAccountAndBoardChanges()
        {
            var account = BackendClient.Instance;
            string oldToken = account.AccessToken;
            int oldId = account.UserId;
            var oldSession = SessionManager.Instance;
            var root = new GameObject("Account guard test");
            try
            {
                var session = root.AddComponent<SessionManager>();
                typeof(SessionManager).GetField("_requireWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(session, LocalAccountStorage.CaptureGuard());
                typeof(SessionManager).GetProperty("Instance").SetValue(null, session);
                session.CurrentBoardName = "A";
                var ui = root.AddComponent<Sandplay.UI.AnalysisUI>();
                Func<bool> Guard() => (Func<bool>)ui.GetType().GetMethod("CaptureResultGuard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                var tokenGuard = Guard();
                typeof(BackendClient).GetProperty("AccessToken").SetValue(account, "test-only-not-sent");
                Assert.IsFalse(tokenGuard());
                var userGuard = Guard();
                typeof(BackendClient).GetProperty("UserId").SetValue(account, oldId + 1);
                Assert.IsFalse(userGuard());
                var boardGuard = Guard(); session.CurrentBoardName = "B";
                Assert.IsFalse(boardGuard());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                typeof(SessionManager).GetProperty("Instance").SetValue(null, oldSession);
                typeof(BackendClient).GetProperty("AccessToken").SetValue(account, oldToken);
                typeof(BackendClient).GetProperty("UserId").SetValue(account, oldId);
            }
        }

        [Test]
        public void ResultGuardRejectsClosedReopenedAndReplacedResults()
        {
            var root = new GameObject("Result generation test");
            try
            {
                var ui = root.AddComponent<Sandplay.UI.AnalysisUI>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var type = ui.GetType();
                var text = type.GetField("_lastResultText", flags);
                text.SetValue(ui, "Report");
                Func<bool> Guard() => (Func<bool>)type.GetMethod("CaptureResultGuard", flags).Invoke(ui, null);
                var beforeClose = Guard();
                Assert.IsTrue(beforeClose());
                type.GetMethod("OnDisable", flags).Invoke(ui, null);
                Assert.IsFalse(beforeClose(), "Same text after reopening must not revive a callback");
                var beforeReset = Guard();
                type.GetMethod("ShowInitialState", flags).Invoke(ui, null);
                text.SetValue(ui, "Report");
                Assert.IsFalse(beforeReset());
                var beforeEdit = Guard();
                text.SetValue(ui, "Changed");
                Assert.IsFalse(beforeEdit());
                Assert.IsTrue(Guard()());
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReportPreviewTracksEditsAndDoesNotSplitEmojiOrRenderMarkup()
        {
            var go = new GameObject("Preview test", typeof(RectTransform));
            try
            {
                var label = go.AddComponent<TMPro.TextMeshProUGUI>();
                var preview = go.AddComponent<Sandplay.UI.ReportPreviewLabel>();
                var report = new AnalysisReport { ResultText = new string('a', 99) + "😀tail" };
                preview.Initialize(report, label);
                Assert.AreEqual(new string('a', 99) + "😀…", label.text);
                report.ResultText = "<b>Edited</b>"; preview.Refresh();
                Assert.AreEqual(report.ResultText, label.text);
                Assert.IsFalse(label.richText);
                report.ResultText = null; preview.Refresh();
                Assert.AreEqual("", label.text);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void LiveEditUpdatesResultInvalidatesCloudAndRejectsClosedPanel()
        {
            var root = new GameObject("Live editing test");
            try
            {
                var ui = root.AddComponent<Sandplay.UI.AnalysisUI>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                void Set(string name, object value) => typeof(Sandplay.UI.AnalysisUI).GetField(name, flags).SetValue(ui, value);
                object Get(string name) => typeof(Sandplay.UI.AnalysisUI).GetField(name, flags).GetValue(ui);
                var report = new AnalysisReport { ReportId = "r", ResultText = "Original" };
                Set("_localReport", report); Set("_localReportBoard", "Board");
                Set("_lastResultText", "Original"); Set("_lastAnalysisId", "old-cloud");
                Action saved = null; Func<bool> current = null;
                ui.EditResult = (r, board, guard, done) => { Assert.AreEqual("Board", board); current = guard; saved = done; };
                ui.RequestEditResult();
                Assert.IsTrue(current());
                report.ResultText = "Edited"; saved();
                Assert.AreEqual("Edited", Get("_lastResultText"));
                Assert.IsNull(Get("_lastAnalysisId"));
                Assert.IsFalse(current());
                ui.RequestEditResult(); root.SetActive(false);
                Assert.IsFalse(current());
                report.ResultText = "Obsolete"; saved();
                Assert.AreEqual("Edited", Get("_lastResultText"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void LiveReportReviewRequiresConfirmationAndRejectsStaleResults()
        {
            var root = new GameObject("Live review test");
            try
            {
                var ui = root.AddComponent<Sandplay.UI.AnalysisUI>();
                var result = typeof(Sandplay.UI.AnalysisUI).GetField("_lastResultText", BindingFlags.NonPublic | BindingFlags.Instance);
                result.SetValue(ui, "Original report");
                int exports = 0;
                Action confirm = null;
                ui.RequestPdfReview(() => exports++);
                Assert.AreEqual(0, exports, "Unwired review must fail closed");
                ui.ReviewBeforeExport = (text, callback) => { Assert.AreEqual("Original report", text); confirm = callback; };
                ui.RequestPdfReview(() => exports++);
                Assert.AreEqual(0, exports);
                confirm(); confirm();
                Assert.AreEqual(1, exports);
                ui.RequestPdfReview(() => exports++);
                result.SetValue(ui, "Changed report");
                confirm();
                Assert.AreEqual(1, exports, "New result needs a fresh review");
                result.SetValue(ui, "Original report");
                ui.RequestPdfReview(() => exports++);
                root.SetActive(false);
                confirm();
                Assert.AreEqual(1, exports, "Closed analysis must not export");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavedReportReviewDoesNotExportUntilConfirmed()
        {
            var root = new GameObject("Report review test", typeof(RectTransform), typeof(Canvas));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(1000, 800);
                var bootstrap = root.AddComponent<SceneBootstrapper>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(SceneBootstrapper).GetField("_safeArea", flags).SetValue(bootstrap, root);
                int exports = 0;
                Action confirm = () => exports++;
                var show = typeof(SceneBootstrapper).GetMethod("ShowReportExportReview", flags);
                string report = "<b>Literal report</b>\nSensitive text";
                show.Invoke(bootstrap, new object[] { report, confirm });
                var preview = root.GetComponentsInChildren<TMP_InputField>().Single(i => i.name == "ReportReviewText");
                Assert.AreEqual(report, preview.text);
                Assert.IsTrue(preview.readOnly);
                Assert.IsFalse(preview.textComponent.richText);
                Assert.AreEqual(0, exports);
                typeof(SceneBootstrapper).GetMethod("CloseClientDialog", flags).Invoke(bootstrap, null);
                Assert.AreEqual(0, exports);
                show.Invoke(bootstrap, new object[] { report, confirm });
                root.GetComponentsInChildren<Button>().Single(b => b.name == "ConfirmReportExport").onClick.Invoke();
                Assert.AreEqual(1, exports);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReportsSortAcrossBoardsAndNeverLoadOtherClients()
        {
            var tables = new[] {
                new SessionListEntry { ClientId = "a", SessionName = "Old board" },
                new SessionListEntry { ClientId = "a", SessionName = "New board" },
                new SessionListEntry { ClientId = "b", SessionName = "Private board" }
            };
            SessionData Load(string name)
            {
                Assert.AreNotEqual("Private board", name);
                var data = new SessionData();
                data.Reports.Add(null);
                data.Reports.Add(new AnalysisReport { ReportId = name, CreatedAt = name == "Old board"
                    ? "2026-09-12T10:00:00+08:00" : "2026-09-12T03:00:00Z" });
                data.Reports.Add(new AnalysisReport { ReportId = "undated", CreatedAt = "bad date" });
                return data;
            }
            var result = SceneBootstrapper.ClientReportHistory(tables, "a", "", Load);
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("New board", result[0].BoardName);
            Assert.AreEqual("Old board", result[1].BoardName);
            Assert.AreEqual("undated", result[2].Report.ReportId);
            Assert.AreEqual(2, SceneBootstrapper.ClientReportHistory(tables, "a", " OLD ", Load).Count);
            Assert.IsEmpty(SceneBootstrapper.ClientReportHistory(tables, "a", "absent", Load));
            Assert.IsEmpty(SceneBootstrapper.ClientReportHistory(tables, "a", "", _ => null));
        }

        [Test]
        public void ReportTextSearchIsTrimmedCaseInsensitiveAndClientScoped()
        {
            var tables = new[] {
                new SessionListEntry { ClientId = "a", SessionName = "Garden" },
                new SessionListEntry { ClientId = "b", SessionName = "Private" }
            };
            SessionData Load(string name)
            {
                Assert.AreEqual("Garden", name);
                return new SessionData { Reports = new System.Collections.Generic.List<AnalysisReport> {
                    new AnalysisReport { ReportId = "match", ResultText = "Client described a BRIDGE." },
                    new AnalysisReport { ReportId = "empty", ResultText = null }
                }};
            }
            var result = SceneBootstrapper.ClientReportHistory(tables, "a", " bridge ", Load);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("match", result[0].Report.ReportId);
            Assert.AreEqual(2, SceneBootstrapper.ClientReportHistory(tables, "a", "garden", Load).Count);
            Assert.IsEmpty(SceneBootstrapper.ClientReportHistory(tables, "a", "absent", Load));
        }

        [Test]
        public void HistorySearchIsClientScopedAndOrdersActualInstants()
        {
            var records = new[]
            {
                new SessionListEntry { ClientId = "a", SessionName = "Garden earlier", ModifiedAt = "2026-09-12T10:00:00+08:00" },
                new SessionListEntry { ClientId = "a", SessionName = "Garden latest", ModifiedAt = "2026-09-12T04:00:00Z" },
                new SessionListEntry { ClientId = "b", SessionName = "Garden other client", ModifiedAt = "2026-09-13T00:00:00Z" },
                new SessionListEntry { ClientId = "a", SessionName = "Garden undated", ModifiedAt = "invalid" },
                new SessionListEntry { ClientId = "a", SessionName = "Other board" },
                new SessionListEntry { ClientId = null, SessionName = "Personal" }
            };
            var result = SceneBootstrapper.FilterClientHistory(records, "a", " GARDEN ");
            CollectionAssert.AreEqual(new[] { "Garden latest", "Garden earlier", "Garden undated" }, result.Select(r => r.SessionName));
            Assert.AreEqual(0, SceneBootstrapper.FilterClientHistory(records, "a", "missing").Count);
            Assert.AreEqual("Personal", SceneBootstrapper.FilterClientHistory(records, null, null).Single().SessionName);
        }

        [Test]
        public void MoveRequiresConfirmationAndSearchResetsIt()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            string directory = Path.Combine(Path.GetTempPath(), "sandtray-workflow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var root = new GameObject("Workflow", typeof(RectTransform), typeof(Canvas));
            var previous = SessionManager.Instance;
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(1000, 800);
                var bootstrap = root.AddComponent<SceneBootstrapper>();
                var sessions = root.AddComponent<SessionManager>();
                typeof(SessionManager).GetField("_requireWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(sessions, LocalAccountStorage.CaptureGuard());
                typeof(SessionManager).GetProperty("Instance").SetValue(null, sessions);
                typeof(SessionManager).GetField("_savePath", flags).SetValue(sessions, directory);
                var store = new ClientRecordStore(Path.Combine(directory, "Clients"));
                var client = store.Save(new ClientRecord { Name = "Target client" });
                typeof(SceneBootstrapper).GetField("_clientStore", flags).SetValue(bootstrap, store);
                typeof(SceneBootstrapper).GetField("_safeArea", flags).SetValue(bootstrap, root);
                File.WriteAllText(Path.Combine(directory, "Table.json"), JsonUtility.ToJson(new SessionData
                    { SessionName = "Table", ClientId = "original", TherapistNotes = "private notes" }));
                typeof(SceneBootstrapper).GetMethod("ShowMoveTableDialog", flags).Invoke(bootstrap, new object[] { "Table" });
                Button Target() => root.GetComponentsInChildren<Button>().First(b => b.name == "Destination" &&
                    b.GetComponentInChildren<TMP_Text>().text.Contains("Target client"));
                Target().onClick.Invoke();
                Assert.AreEqual("original", sessions.LoadSessionData("Table").ClientId);
                var search = root.GetComponentInChildren<TMP_InputField>();
                search.onValueChanged.Invoke("Target");
                Target().onClick.Invoke();
                Assert.AreEqual("original", sessions.LoadSessionData("Table").ClientId);
                Target().onClick.Invoke();
                Assert.AreEqual(client.Id, sessions.LoadSessionData("Table").ClientId);
                Assert.AreEqual("private notes", sessions.LoadSessionData("Table").TherapistNotes);
                string saved = File.ReadAllText(Path.Combine(directory, "Table.json"));
                typeof(SceneBootstrapper).GetMethod("ShowClientAssignmentHistory", flags).Invoke(bootstrap, new object[] { "Table" });
                var historyRows = root.GetComponentsInChildren<RectTransform>().Where(r => r.name == "AssignmentChange").ToArray();
                Assert.AreEqual(1, historyRows.Length);
                Assert.IsTrue(historyRows[0].GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Target client")));
                Assert.IsTrue(historyRows[0].GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("original")));
                Assert.AreEqual(0, historyRows[0].GetComponentsInChildren<Button>().Length);
                Assert.AreEqual(saved, File.ReadAllText(Path.Combine(directory, "Table.json")), "Viewing history must not write the board");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                typeof(SessionManager).GetProperty("Instance").SetValue(null, previous);
                Directory.Delete(directory, true); // This test's uniquely generated fixtures only.
            }
        }

        [Test]
        public void HistoryLabelsDistinguishUnassignedMissingAndArchivedClients()
        {
            var records = new[] { new ClientRecord { Id = "a", Name = "Archived client", Archived = true } };
            Assert.AreEqual(Localization.Get("clients.history_unassigned"), SceneBootstrapper.AssignmentClientLabel(null, records));
            Assert.AreEqual(Localization.Get("clients.history_missing", "gone"), SceneBootstrapper.AssignmentClientLabel("gone", records));
            Assert.AreEqual("Archived client · " + Localization.Get("clients.archived"), SceneBootstrapper.AssignmentClientLabel("a", records));
        }

        [Test]
        public void LocalBoardSetupClearsPendingHostingAndOnlineSetupSelectsCloud()
        {
            var root = new GameObject("Hosting setup test");
            try
            {
                var bootstrap = root.AddComponent<SceneBootstrapper>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var setup = typeof(SceneBootstrapper).GetMethod("SetNewBoardHosting", flags);
                var mode = typeof(SceneBootstrapper).GetField("_pendingHostMode", flags);
                setup.Invoke(bootstrap, new object[] { true });
                Assert.AreEqual("Cloud", mode.GetValue(bootstrap).ToString());
                setup.Invoke(bootstrap, new object[] { false });
                Assert.AreEqual("None", mode.GetValue(bootstrap).ToString());
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
