using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sandplay.Core
{
    public partial class NetworkBootstrapper
    {
        private long _revisionCounter, _editRevision, _editSequence, _snapshotId;
        private bool _snapshotReady, _snapshotApplied;
        public bool IsSynchronizing => _relayMode && _isOnline && !_isHost && !_snapshotReady;
        private PlayerRole _pendingEditorRole = PlayerRole.Observer;
        private readonly Dictionary<string, long> _participantRevisions = new Dictionary<string, long>();
        private readonly Dictionary<string, long> _participantSequences = new Dictionary<string, long>();

        private byte[] RevisionRole(SessionParticipant participant, PlayerRole role)
        {
            long revision = ++_revisionCounter;
            _participantRevisions[participant.Token] = revision;
            _participantSequences[participant.Token] = 0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(revision);
            writer.Write(WriteSessionRole(role, participant.Token, participant.Name));
            return stream.ToArray();
        }

        private byte[] WrapRevisionEdit(byte[] packet)
        {
            if (!_snapshotReady || _pendingEditorRole != PlayerRole.Patient) return null;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(_participantToken);
            writer.Write(_editRevision);
            writer.Write(++_editSequence);
            writer.Write(packet, 4, packet.Length - 4);
            return NetSerializer.Pack(NetMsgType.RevisionEdit, stream.ToArray());
        }

        private void ReceiveRevisionMessage(NetMsgType type, byte[] payload)
        {
            if (!_relayMode || SessionPlayer.IsReplayActive) return;
            if (payload == null || payload.Length < 8) return;
            if ((type == NetMsgType.SnapshotBegin || type == NetMsgType.SnapshotComplete) && payload.Length != 8) return;
            using var stream = new MemoryStream(payload);
            using var reader = new BinaryReader(stream);
            if (type == NetMsgType.RevisionEdit)
            {
                if (!_isHost) return;
                string token = reader.ReadString();
                long revision = reader.ReadInt64(), sequence = reader.ReadInt64();
                byte inner = reader.ReadByte();
                if (EditingPaused || token != _editorToken || inner < 16 || inner > 21 ||
                    !_participantRevisions.TryGetValue(token, out var expected) || revision != expected ||
                    !_participantSequences.TryGetValue(token, out var last) || sequence <= last) return;
                _participantSequences[token] = sequence;
                byte[] edit = reader.ReadBytes((int)(stream.Length - stream.Position));
                switch ((NetMsgType)inner)
                {
                    case NetMsgType.ClientHeightmapPaint: ApplyAndRebroadcastHeightmap(edit); break;
                    case NetMsgType.ClientSplatPaint: ApplyAndRebroadcastSplat(edit); break;
                    case NetMsgType.ClientSpawnRequest: ApplyAndRebroadcastSpawn(edit); break;
                    case NetMsgType.ClientMoveObject: ApplyAndRebroadcastMove(edit); break;
                    case NetMsgType.ClientRemoveObject: ApplyAndRebroadcastRemove(edit); break;
                    case NetMsgType.ClientColorChange: ApplyAndRebroadcastColor(edit); break;
                }
                return;
            }
            if (_isHost) return;
            long id = reader.ReadInt64();
            if (id <= 0) return;
            if (type == NetMsgType.RevisionRole)
            {
                var roleBytes = reader.ReadBytes((int)(stream.Length - stream.Position));
                if (!IsRoleAssignmentForThisParticipant(roleBytes) || id <= _editRevision) return;
                _editRevision = id;
                _editSequence = 0;
                _pendingEditorRole = NetSerializer.ReadRoleAssignment(roleBytes);
                if (GameManager.Instance != null)
                    GameManager.Instance.NetworkRole = _snapshotReady ? _pendingEditorRole : PlayerRole.Observer;
            }
            else if (type == NetMsgType.SnapshotBegin)
            {
                if (id <= _snapshotId) return;
                _snapshotId = id;
                _snapshotReady = _snapshotApplied = false;
                ResetSnapshotObjectProgress();
                // Downloads from the previous snapshot must not place objects
                // while the replacement catalog and board are still arriving.
                ResetPendingModels();
                if (GameManager.Instance != null) GameManager.Instance.NetworkRole = PlayerRole.Observer;
            }
            else if (type == NetMsgType.SnapshotComplete && id == _snapshotId)
                StartCoroutine(AcknowledgeSnapshot(id, _relayAttempt));
        }

        private IEnumerator AcknowledgeSnapshot(long id, int attempt)
        {
            while (_isOnline && attempt == _relayAttempt && id == _snapshotId && _loadingObjects.Count > 0)
                yield return null;
            if (!_isOnline || attempt != _relayAttempt || id != _snapshotId) yield break;
            if (!_snapshotApplied || _snapshotObjectFailed || _snapshotObjectsPending.Count > 0)
            {
                if (!_snapshotTerminalNotified)
                {
                    _snapshotTerminalNotified = true;
                    OnSnapshotLoadFailed?.Invoke(string.IsNullOrEmpty(_snapshotFailureReason)
                        ? "The session could not be loaded completely."
                        : _snapshotFailureReason);
                }
                yield break;
            }
            // TCP ordering ensures this acknowledgment precedes any new edits.
            if (!SendToRelay(NetSerializer.Pack(NetMsgType.SnapshotAck, BitConverter.GetBytes(id))))
            {
                if (!_snapshotTerminalNotified)
                {
                    _snapshotTerminalNotified = true;
                    OnSnapshotLoadFailed?.Invoke("The session was loaded, but readiness could not be confirmed.");
                }
                yield break;
            }
            _snapshotReady = true;
            if (GameManager.Instance != null) GameManager.Instance.NetworkRole = _pendingEditorRole;
            if (!_snapshotTerminalNotified)
            {
                _snapshotTerminalNotified = true;
                OnSnapshotLoadProgress?.Invoke(_snapshotObjectTotal, _snapshotObjectTotal);
                OnSnapshotReady?.Invoke();
            }
        }
    }
}
