using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace Sandplay.Core
{
    public partial class NetworkBootstrapper
    {
        // Coordinated protocol-3 release required. Never automatically fall back to v2.
        public bool UseAuthenticatedRelay = true;
        private int _relayAttempt;
        private void EnqueueCurrentRelay(TcpClient socket, Action action)
        {
            int generation = _relayAttempt;
            EnqueueMain(() =>
            {
                if (generation == _relayAttempt && _isOnline && _relaySocket == socket) action();
            });
        }
        private sealed class RelayLoginRequiredException : IOException
        {
            public RelayLoginRequiredException(string message) : base(message) { }
        }

        private string FetchRelayTicket(string roomCode, int attempt)
        {
            var completion = new TaskCompletionSource<string>();
            EnqueueMain(() =>
            {
                if (attempt != _relayAttempt || (!_isOnline && !_isReconnecting) || BackendClient.Instance == null)
                { completion.TrySetException(new IOException("Session connection cancelled.")); return; }
                BackendClient.Instance.RequestRelayTicket(roomCode,
                    ticket =>
                    {
                        if (attempt != _relayAttempt) completion.TrySetException(new IOException("Session connection cancelled."));
                        else completion.TrySetResult(ticket);
                    },
                    error => completion.TrySetException(
                        error.IndexOf("sign in", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("account changed", StringComparison.OrdinalIgnoreCase) >= 0
                            ? new RelayLoginRequiredException(error) : new IOException(error)));
            });
            if (Task.WaitAny(new Task[] { completion.Task }, 15000) < 0)
                throw new IOException("Session authentication timed out.");
            return completion.Task.GetAwaiter().GetResult();
        }

        private Stream OpenRelayStream(TcpClient socket, string address)
        {
            Stream stream = socket.GetStream();
            try
            {
                stream.ReadTimeout = 10000;
                stream.WriteTimeout = 10000;
                if (UseAuthenticatedRelay)
                {
                    // Platform chain and hostname validation; no permissive callback.
                    var tls = new SslStream(stream, false);
                    stream = tls;
                    tls.AuthenticateAsClient(address, null, SslProtocols.Tls12, true);
                }
                RelayCompatibility.Check(stream, UseAuthenticatedRelay ? 4 : 2);
                return stream;
            }
            catch { stream.Dispose(); socket.Close(); throw; }
        }

        private byte[] RelayControlPayload(string ticket, string roomCode)
        {
            if (!UseAuthenticatedRelay) return roomCode == null ? NetSerializer.WriteCreateRoom() : NetSerializer.WriteJoinRoom(roomCode);
            using var body = new MemoryStream();
            using var writer = new BinaryWriter(body);
            writer.Write(ticket);
            if (roomCode != null) writer.Write(roomCode);
            return body.ToArray();
        }
    }
}
