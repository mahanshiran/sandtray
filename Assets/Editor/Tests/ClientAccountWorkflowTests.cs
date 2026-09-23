using System;
using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Tests
{
    public class ClientAccountWorkflowTests
    {
        [Test]
        public void FriendSelectionPreservesDraftAndCancelDoesNotModifySavedRecord()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var backend = BackendClient.Instance;
            var token = backend.AccessToken;
            var service = FriendsClient.Instance;
            var transport = typeof(FriendsClient).GetField("TestTransport", flags);
            var oldTransport = transport.GetValue(service);
            var root = new GameObject("ClientAccountTest", typeof(RectTransform), typeof(Canvas));
            var scene = root.AddComponent<SceneBootstrapper>();
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 900);
            var original = new ClientRecord { Id = "local-client", Name = "Therapist alias", Notes = "Private notes", Email = "manual@example.test" };
            string directory = Path.Combine(Path.GetTempPath(), "client-photo-test-" + Guid.NewGuid().ToString("N"));
            var store = new ClientRecordStore(directory);
            original.Id = null;
            store.Save(original);
            typeof(SceneBootstrapper).GetField("_clientStore", flags).SetValue(scene, store);
            try
            {
                typeof(BackendClient).GetProperty("AccessToken").SetValue(backend, "test-only");
                service.EnsureAccount();
                var image = new Texture2D(4, 4);
                byte[] imageBytes;
                try { imageBytes = image.EncodeToPNG(); } finally { UnityEngine.Object.DestroyImmediate(image); }
                ProfileImageCache.Shared.Get(ProfileImageCache.AccountScope, "public:https://example.test/avatar.png",
                    done => done(imageBytes, 300), bytes => { });
                transport.SetValue(service, (Action<string, object, Action<string>, Action<string>>)((path, body, success, failure) =>
                {
                    Assert.AreEqual("state/", path);
                    success("{\"me\":{\"id\":1},\"people\":[{\"id\":99999,\"code\":\"stable-identity\",\"friend_code\":\"ABCDEF\",\"name\":\"Public name\",\"state\":\"accepted\",\"avatar_url\":\"https://example.test/avatar.png\"},{\"id\":88888,\"code\":\"pending\",\"name\":\"Pending\",\"state\":\"pending\"}],\"blocked\":[]}");
                }));
                typeof(SceneBootstrapper).GetField("_safeArea", flags).SetValue(scene, root);
                typeof(SceneBootstrapper).GetMethod("ShowClientEditor", flags).Invoke(scene, new object[] { original });
                Button ButtonWith(string label) => root.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMP_Text>()?.text == label);
                ButtonWith(FriendsClient.Text("From friends", "从好友选择")).onClick.Invoke();
                Assert.AreEqual(1, root.GetComponentsInChildren<Button>().Count(b => b.GetComponentInChildren<TMP_Text>()?.text == FriendsClient.Text("Use this account", "使用此账号")));
                ButtonWith(FriendsClient.Text("Use this account", "使用此账号")).onClick.Invoke();
                var values = root.GetComponentsInChildren<TMP_InputField>().Select(i => i.text).ToArray();
                CollectionAssert.Contains(values, "Therapist alias");
                CollectionAssert.Contains(values, "Private notes");
                CollectionAssert.Contains(values, "manual@example.test");
                Assert.IsTrue(root.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("ABCDEF")));
                var clientPhoto = root.GetComponentsInChildren<Sandplay.UI.ClientAvatarTexture>().Single().GetComponent<RawImage>();
                Assert.IsTrue(clientPhoto.enabled, "Friend photo must appear in the client draft.");
                Assert.AreEqual(256, clientPhoto.texture.width);
                Assert.AreEqual(256, clientPhoto.texture.height);
                Assert.IsTrue(string.IsNullOrEmpty(original.PhotoFile), "Selecting an account must not persist its photo before Save.");
                ButtonWith(Localization.Get("clients.remove_photo")).onClick.Invoke();
                ButtonWith(FriendsClient.Text("From friends", "从好友选择")).onClick.Invoke();
                ButtonWith(FriendsClient.Text("Use this account", "使用此账号")).onClick.Invoke();
                Assert.IsFalse(root.GetComponentsInChildren<Sandplay.UI.ClientAvatarTexture>().Single().GetComponent<RawImage>().enabled,
                    "An explicit photo removal must not be overwritten by account selection.");
                Assert.IsNull(original.Account, "Selection must remain a draft until Save.");
                typeof(SceneBootstrapper).GetMethod("CloseClientDialog", flags).Invoke(scene, null);
                Assert.IsNull(original.Account);
                Assert.IsTrue(string.IsNullOrEmpty(store.GetAll()[0].PhotoFile));
                typeof(SceneBootstrapper).GetMethod("ShowClientEditor", flags).Invoke(scene, new object[] { original });
                ButtonWith(FriendsClient.Text("From friends", "从好友选择")).onClick.Invoke();
                ButtonWith(FriendsClient.Text("Use this account", "使用此账号")).onClick.Invoke();
                ButtonWith(Localization.Get("clients.save")).onClick.Invoke();
                var saved = new ClientRecordStore(directory).GetAll().Single();
                Assert.AreEqual(original.Id, saved.Id);
                Assert.AreEqual("Private notes", saved.Notes);
                Assert.AreEqual(99999, saved.Account.UserId);
                var savedPhoto = store.ReadPhoto(saved.PhotoFile);
                Assert.IsNotNull(savedPhoto);
                Assert.Greater(savedPhoto.Length, 8);
                CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, savedPhoto.Take(8).ToArray());
                var render = typeof(SceneBootstrapper).GetMethod("ClientRecordAvatar", flags);
                Transform Render(ClientRecord record) => (Transform)render.Invoke(scene, new object[] { root.transform, record, 0f, 0f, .2f, .2f });
                Assert.IsTrue(Render(saved).GetComponentInChildren<RawImage>().enabled, "Saved photo appears in list/header avatar.");
                saved.PhotoFile = "";
                var person = new FriendPerson { id = 99999, code = "stable-identity", friend_code = "ABCDEF", avatar_url = "https://example.test/avatar.png" };
                ProfileImageCache.Shared.Get(ProfileImageCache.AccountScope, "client-account-lookup:99999:ABCDEF",
                    done => done(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(person)), 300), bytes => { });
                Assert.IsTrue(Render(saved).GetComponentInChildren<RawImage>().enabled, "Older linked records resolve the cached account photo.");
                saved.AccountPhotoSuppressed = true;
                Assert.IsFalse(Render(saved).GetComponentInChildren<RawImage>().enabled, "Explicit photo removal suppresses remote fallback.");
                Assert.IsFalse(Render(new ClientRecord { Name = "Manual" }).GetComponentInChildren<RawImage>().enabled);
                Assert.IsFalse(Render(null).GetComponentInChildren<RawImage>().enabled, "Unassigned tables are not an account.");
            }
            finally
            {
                ProfileImageCache.Shared.Clear();
                transport.SetValue(service, oldTransport);
                typeof(BackendClient).GetProperty("AccessToken").SetValue(backend, token);
                service.EnsureAccount();
                UnityEngine.Object.DestroyImmediate(root);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
