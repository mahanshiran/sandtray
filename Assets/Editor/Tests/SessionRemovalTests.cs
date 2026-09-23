using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Tests
{
    public class SessionRemovalTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);

        [Test]
        public void StatusIsValidatedAndCannotOverwriteHostState()
        {
            var go = new GameObject("Session status test");
            try
            {
                var net = go.AddComponent<NetworkBootstrapper>();
                Set(net, "_relayMode", true);
                byte[] payload;
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(true); writer.Write(false); writer.Write("Client 张");
                    payload = stream.ToArray();
                }
                net.HandleClientMessage(NetMsgType.SessionStatus, payload);
                Assert.AreEqual("Client 张", net.SessionEditorName);
                Assert.IsTrue(net.EditingPaused);
                Assert.IsFalse(net.SessionWaitingForEditor);
                net.HandleClientMessage(NetMsgType.SessionStatus, new byte[] { 0 });
                Assert.AreEqual("Client 张", net.SessionEditorName);
                Set(net, "_isHost", true);
                payload[0] = 0;
                net.HandleClientMessage(NetMsgType.SessionStatus, payload);
                Assert.IsTrue(net.EditingPaused);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void DeclineIsHostOnlyAndDoesNotRevokeAnExistingEditor()
        {
            var go = new GameObject("Session removal test");
            try
            {
                var net = go.AddComponent<NetworkBootstrapper>();
                Set(net, "_relayMode", true);
                var participant = new NetworkBootstrapper.SessionParticipant
                    { Token = Guid.NewGuid().ToString("N"), Name = "Client", RequestsEditing = true };
                var list = (List<NetworkBootstrapper.SessionParticipant>)typeof(NetworkBootstrapper)
                    .GetField("_sessionParticipants", Private).GetValue(net);
                list.Add(participant);
                net.DeclineSessionEditing(participant.Token);
                Assert.IsTrue(participant.RequestsEditing);
                Set(net, "_isHost", true);
                net.DeclineSessionEditing(participant.Token);
                Assert.IsFalse(participant.RequestsEditing);
                Set(net, "_editorToken", participant.Token);
                net.DeclineSessionEditing(participant.Token);
                Assert.AreEqual(participant.Token, net.EditorToken);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
