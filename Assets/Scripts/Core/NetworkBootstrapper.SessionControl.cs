using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sandplay.Core
{
    public partial class NetworkBootstrapper
    {
        public sealed class SessionParticipant
        {
            public string Token;
            public string Name;
            public bool RequestsEditing;
        }

        private string _participantToken = Guid.NewGuid().ToString("N");
        private string _participantName = "Participant";
        private readonly List<SessionParticipant> _sessionParticipants = new List<SessionParticipant>();
        private string _editorToken;
        private float _nextHeartbeat;
        private bool _removedFromSession;
        private string _hostDisplayName = "Host";
        public string SessionEditorName { get; private set; } = "";
        public bool SessionWaitingForEditor { get; private set; } = true;

        private void PublishSessionStatus()
        {
            if (!_isHost || !_relayMode) return;
            var editor = _sessionParticipants.Find(p => p.Token == _editorToken);
            SessionWaitingForEditor = editor == null && _therapistMode;
            SessionEditorName = editor != null ? editor.Name : SessionWaitingForEditor ? "" : _hostDisplayName;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(EditingPaused);
                writer.Write(SessionWaitingForEditor);
                writer.Write(SessionEditorName);
                SendToRelay(NetSerializer.Pack(NetMsgType.SessionStatus, stream.ToArray()));
            }
        }

        private void ReceiveSessionStatus(byte[] payload)
        {
            if (_isHost || !_relayMode || payload == null || payload.Length > 512) return;
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(payload)))
                {
                    bool paused = reader.ReadBoolean(), waiting = reader.ReadBoolean();
                    string name = reader.ReadString();
                    if (name.Length > 100 || reader.BaseStream.Position != reader.BaseStream.Length) return;
                    // Display state only. Never grant editing from a status packet.
                    EditingPaused = paused;
                    SessionWaitingForEditor = waiting;
                    SessionEditorName = name;
                }
            }
            catch (IOException) { }
            catch (ArgumentException) { }
        }

        public void DeclineSessionEditing(string token)
        {
            if (!_isHost || !_relayMode) return;
            var participant = _sessionParticipants.Find(p => p.Token == token);
            if (participant == null || token == _editorToken) return;
            participant.RequestsEditing = false;
            SendParticipantRole(participant);
        }

        public void RemoveSessionParticipant(string token)
        {
            if (!_isHost || !_relayMode || !_sessionParticipants.Exists(p => p.Token == token)) return;
            // Stop local acceptance immediately; relay also revokes before closing the socket.
            if (_editorToken == token) SetSessionEditingPaused(true);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(token);
                SendToRelay(NetSerializer.Pack(NetMsgType.RemoveParticipant, stream.ToArray()));
            }
        }

        private void UpdateRelayHeartbeat()
        {
            if (!_relayMode || !_isOnline || _relayStream == null) return;
            if (Time.realtimeSinceStartup < _nextHeartbeat) return;
            _nextHeartbeat = Time.realtimeSinceStartup + 5f;
            SendToRelay(NetSerializer.Pack(NetMsgType.RelayPing, Array.Empty<byte>()));
        }
        public bool EditingPaused { get; private set; }
        public IReadOnlyList<SessionParticipant> SessionParticipants => _sessionParticipants;
        public string EditorToken => _editorToken;

        private void ResetSessionControl()
        {
            ClearSessionProfiles();
            _sessionParticipants.Clear();
            _editorToken = null;
            EditingPaused = false;
            SessionEditorName = "";
            SessionWaitingForEditor = true;
            _hostDisplayName = BackendClient.Instance?.UserName ?? "Host";
            if (_hostDisplayName.Length > 100) _hostDisplayName = _hostDisplayName.Substring(0, 100);
            SessionWaitingForEditor = _therapistMode;
            SessionEditorName = _therapistMode ? "" : _hostDisplayName;
        }

        // A routing token, NOT authenticated identity. Relay-side identity is a separate milestone.
        public static byte[] WriteSessionRole(PlayerRole role, string token, string name)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)role);
                writer.Write(token);
                writer.Write(name ?? "Participant");
                return stream.ToArray();
            }
        }

        public static bool TryReadSessionRole(byte[] payload, out PlayerRole role, out string token, out string name)
        {
            role = PlayerRole.Observer; token = null; name = null;
            if (payload == null || payload.Length < 3 || payload.Length > 1024) return false;
            try
            {
                using (var stream = new MemoryStream(payload))
                using (var reader = new BinaryReader(stream))
                {
                    role = (PlayerRole)reader.ReadByte(); token = reader.ReadString(); name = reader.ReadString();
                    return Guid.TryParseExact(token, "N", out _) && name.Length <= 100 && Enum.IsDefined(typeof(PlayerRole), role);
                }
            }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
        }

        private bool IsRoleAssignmentForThisParticipant(byte[] payload)
        {
            return TryReadSessionRole(payload, out _, out var token, out _) && token == _participantToken;
        }

        private void HandleSessionRoleRequest(byte[] payload)
        {
            if (!_isHost || !_relayMode) return;
            if (!TryReadSessionRole(payload, out var requested, out var token, out var name))
            {
                Debug.LogWarning("[Session] Participant needs the updated app for editing approval.");
                // Legacy clients remain observers. Never broadcast an untargeted editor grant.
                return;
            }
            var participant = _sessionParticipants.Find(p => p.Token == token);
            if (participant == null)
            {
                participant = new SessionParticipant { Token = token, Name = name };
                _sessionParticipants.Add(participant);
                _connectedClients = _sessionParticipants.Count;
                OnClientCountChanged?.Invoke(_connectedClients);
            }
            participant.RequestsEditing = requested == PlayerRole.Patient && token != _editorToken;
            // In "Client edits" mode, the first joiner becomes editor on registration.
            // SetSessionEditor sends both the role revision and a fresh snapshot.
            if (_autoAssignJoinerEditor && _editorToken == null && _sessionParticipants.Count == 1)
            {
                SetSessionEditor(token);
                return;
            }
            SendParticipantRole(participant);
            SendFullStateViaRelay(token);
            PublishSessionStatus();
        }

        private void SendSnapshotPart(string recipient, NetMsgType type, byte[] payload)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(recipient);
                writer.Write((byte)type);
                writer.Write(payload);
                SendToRelay(NetSerializer.Pack(NetMsgType.TargetedSnapshot, stream.ToArray()));
            }
        }

        public void SetSessionEditor(string token)
        {
            if (!_isHost || !_relayMode) return;
            if (token != null && !_sessionParticipants.Exists(p => p.Token == token)) return;
            if (token == null) _autoAssignJoinerEditor = false;
            if (token == _editorToken && !EditingPaused && _therapistMode == (token != null)) return;
            string previousEditor = _editorToken;
            _editorToken = token;
            _therapistMode = token != null;
            _patientAssigned = token != null;
            EditingPaused = false;
            GameManager.Instance.NetworkRole = token == null ? PlayerRole.Patient : PlayerRole.Psychologist;
            foreach (var participant in _sessionParticipants)
            {
                if (participant.Token == token) participant.RequestsEditing = false;
                SendParticipantRole(participant);
                // Reconcile speculative editor state without rebuilding observers' models.
                if (participant.Token == previousEditor || participant.Token == token)
                    SendFullStateViaRelay(participant.Token);
            }
            PublishSessionStatus();
        }

        private void HandleSessionDeparture(byte[] payload)
        {
            using (var reader = new BinaryReader(new MemoryStream(payload)))
            {
                string token = reader.ReadString();
                int removed = _sessionParticipants.RemoveAll(p => p.Token == token);
                if (removed == 0) return;
                _connectedClients = _sessionParticipants.Count;
                OnClientCountChanged?.Invoke(_connectedClients);
                if (_editorToken == token)
                {
                    _editorToken = null;
                    _patientAssigned = false;
                    SetSessionEditingPaused(true, preserveAutoAssignment: true);
                }
            }
        }

        public void SetSessionEditingPaused(bool paused, bool preserveAutoAssignment = false)
        {
            if (!_isHost || !_relayMode) return;
            if (paused && !preserveAutoAssignment) _autoAssignJoinerEditor = false;
            if (EditingPaused == paused) return;
            EditingPaused = paused;
            GameManager.Instance.NetworkRole = !paused && !_therapistMode ? PlayerRole.Patient : PlayerRole.Psychologist;
            foreach (var participant in _sessionParticipants)
            {
                SendParticipantRole(participant);
                if (participant.Token == _editorToken)
                    SendFullStateViaRelay(participant.Token);
            }
            PublishSessionStatus();
        }

        private void SendParticipantRole(SessionParticipant participant)
        {
            var role = !EditingPaused && participant.Token == _editorToken ? PlayerRole.Patient : PlayerRole.Observer;
            SendToRelay(NetSerializer.Pack(NetMsgType.RevisionRole, RevisionRole(participant, role)));
        }
    }
}
