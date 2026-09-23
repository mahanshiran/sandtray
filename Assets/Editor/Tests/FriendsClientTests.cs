using NUnit.Framework;
using Sandplay.Core;
using UnityEngine;
namespace Sandplay.Tests
{
    public class FriendsClientTests
    {
        [Test]
        public void AvatarCanBindBeforeAwakeOnHiddenFriendPage()
        {
            var root = new GameObject("Hidden friend avatar", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            root.SetActive(false);
            try
            {
                var avatar = root.AddComponent<Sandplay.UI.AccountAvatar>();
                Assert.DoesNotThrow(() => avatar.SetPerson(1, "", null));
                // Simulate the deferred Awake after configuration, then a refreshed binding.
                typeof(Sandplay.UI.AccountAvatar).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(avatar, null);
                avatar.SetPerson(2, "", null);
                Assert.AreEqual(1, root.GetComponents<UnityEngine.UI.Mask>().Length);
                Assert.AreEqual(1, root.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Length);
                var fallback = root.GetComponentInChildren<Sandplay.UI.DefaultAccountAvatar>(true);
                Assert.IsNotNull(fallback);
                Assert.IsFalse(fallback.enabled);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void HistoryPrependKeepsReadingPositionAndClampsShortContent()
        {
            float Offset(float old,float added,float content,float viewport)=>(float)typeof(SceneBootstrapper).GetMethod("PreserveFriendScrollOffset",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{old,added,content,viewport});
            Assert.AreEqual(420,Offset(120,300,1500,500));
            Assert.AreEqual(120,Offset(120,0,1500,500));
            Assert.AreEqual(0,Offset(120,300,200,500));
            Assert.AreEqual(100,Offset(120,300,600,500));
        }

        [Test]
        public void OldAccountCallbacksCannotReachTheNextAccount()
        {
            var backend=BackendClient.Instance;string token=backend.AccessToken;int user=backend.UserId;
            var root=new GameObject("Friends account test");
            try
            {
                typeof(BackendClient).GetProperty("AccessToken").SetValue(backend,"first-test-token");
                typeof(BackendClient).GetProperty("UserId").SetValue(backend,801);
                var client=root.AddComponent<FriendsClient>();
                System.Action<string> complete=null;bool delivered=false;
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                System.Action<string,object,System.Action<string>,System.Action<string>> transport=(path,body,success,failure)=>complete=success;
                typeof(FriendsClient).GetField("TestTransport",flags).SetValue(client,transport);
                client.Request<FriendResult>("state/",null,_=>delivered=true,_=>delivered=true);
                var nonce=client.PrepareMessage("friend","draft").nonce;int generation=client.Generation;
                typeof(BackendClient).GetProperty("UserId").SetValue(backend,802);
                complete("{\"ok\":true}");Assert.IsFalse(delivered);
                client.EnsureAccount();Assert.Greater(client.Generation,generation);
                Assert.IsNull(client.State);Assert.AreNotEqual(nonce,client.PrepareMessage("friend","draft").nonce);
            }
            finally
            {
                typeof(BackendClient).GetProperty("AccessToken").SetValue(backend,token);
                typeof(BackendClient).GetProperty("UserId").SetValue(backend,user);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RefreshDuringAnInFlightReadQueuesOneFollowup()
        {
            var backend=BackendClient.Instance;string token=backend.AccessToken;
            var root=new GameObject("Friends refresh test");
            try
            {
                typeof(BackendClient).GetProperty("AccessToken").SetValue(backend,"test-only-token");
                var client=root.AddComponent<FriendsClient>();
                System.Action<string> complete=null;int requests=0;
                System.Action<string,object,System.Action<string>,System.Action<string>> transport=(path,body,success,failure)=>
                {
                    if(path=="heartbeat/")success("{\"ok\":true}");
                    else {requests++;complete=success;}
                };
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(FriendsClient).GetField("TestTransport",flags).SetValue(client,transport);
                client.Refresh();client.Refresh();client.Refresh();Assert.AreEqual(1,requests);
                complete("{\"me\":{\"id\":1},\"people\":[],\"blocked\":[]}");
                Assert.AreEqual(0f,typeof(FriendsClient).GetField("nextPoll",flags).GetValue(client));
                client.Refresh();Assert.AreEqual(2,requests);
                complete("{\"me\":{\"id\":1},\"people\":[],\"blocked\":[]}");
                Assert.IsFalse((bool)typeof(FriendsClient).GetField("refreshQueued",flags).GetValue(client));
            }
            finally
            {
                typeof(BackendClient).GetProperty("AccessToken").SetValue(backend,token);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AmbiguousSendRetainsNonceUntilMatchingConfirmation()
        {
            var root=new GameObject("Friends retry test");
            try
            {
                var client=root.AddComponent<FriendsClient>();
                var first=client.PrepareMessage("alice","hello");
                Assert.AreEqual(first.nonce,client.PrepareMessage("alice","hello").nonce);
                Assert.AreNotEqual(first.nonce,client.PrepareMessage("bob","hello").nonce);
                client.ConfirmMessage("alice","unrelated-response");
                Assert.AreEqual(first.nonce,client.PrepareMessage("alice","hello").nonce);
                client.ConfirmMessage("alice",first.nonce);
                var next=client.PrepareMessage("alice","hello");
                Assert.AreNotEqual(first.nonce,next.nonce);
                Assert.AreNotEqual(next.nonce,client.PrepareMessage("alice","different message").nonce);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
