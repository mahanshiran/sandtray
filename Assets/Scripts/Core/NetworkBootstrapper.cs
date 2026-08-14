using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Sandplay.Sand;
using Sandplay.Objects;

namespace Sandplay.Core
{
    /// <summary>
    /// Lightweight TCP networking for LAN multiplayer.
    /// Host runs a TcpListener, clients connect via TcpClient.
    /// Messages use length-prefixed binary format (see NetSerializer).
    /// Works entirely with built-in .NET — no external packages needed.
    /// </summary>
    public class NetworkBootstrapper : MonoBehaviour
    {
        public static NetworkBootstrapper Instance { get; private set; }

        private bool _isOnline;
        private bool _isHost;

        public bool IsOnline => _isOnline;
        public bool IsHost => _isHost;

        // Callbacks for UI updates
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<int> OnClientCountChanged;

        private int _connectedClients;
        public int ConnectedClients => _connectedClients;

        // Track network objects for late join
        private readonly Dictionary<uint, PlacedObject> _networkObjects = new();
        private readonly Dictionary<string, Color> _materialColors = new();
        private uint _nextNetId = 1;

        // ── Server state ──
        private TcpListener _server;
        private Thread _acceptThread;
        private readonly List<TcpClient> _clients = new();
        private readonly object _clientsLock = new();

        // ── Client state ──
        private TcpClient _clientSocket;
        private Thread _clientReceiveThread;

        // ── Reconnection state ──
        private string _lastServerAddress;
        private bool _isReconnecting;
        private Thread _reconnectThread;
        public event Action<int> OnReconnectAttempt; // passes attempt number
        public event Action OnReconnectGaveUp;
        private const int MaxReconnectAttempts = 10;
        private const float ReconnectIntervalSec = 3f;

        // ── Relay state ──
        private bool _relayMode;
        private TcpClient _relaySocket;
        private NetworkStream _relayStream;
        private string _roomCode;
        private string _lastRelayAddress;
        private string _lastRoomCode;

        public bool IsRelayMode => _relayMode;
        public string RoomCode => _roomCode;

        public event Action<string> OnRoomCreated; // passes room code

        // ── Requested role (set before connecting) ──
        private PlayerRole _requestedRole = PlayerRole.Observer;
        public PlayerRole RequestedRole
        {
            get => _requestedRole;
            set => _requestedRole = value;
        }

        // ── Therapist mode (host plays as Psychologist; patient client gets Patient role) ──
        private bool _therapistMode;
        private bool _patientAssigned; // host-side: whether a Patient role has already been granted to a client
        public bool TherapistMode => _therapistMode;

        // ── Patient pointer hover (Therapist mode "patient is acting" indicator) ──
        // Fired on the host (and any peer) whenever a PointerHover packet arrives.
        // Vector3 is the patient's last known cursor world position; the kind
        // tells subscribers what the patient is doing (sculpt / paint / place)
        // so they can color-code the indicator. Subscribers are responsible for
        // showing a marker / fading it out after inactivity.
        public event Action<Vector3, PatientPointerKind> OnPatientPointerHover;
        // Throttle outgoing pointer packets on the patient to ~15 Hz to keep
        // bandwidth tiny while still feeling live. Place pulses bypass the
        // throttle so a therapist never misses an object placement.
        private const float PointerHoverSendIntervalSec = 1f / 15f;
        private float _lastPointerHoverSentAt = -1f;

        // ── Thread-safe message queue (processed on main thread) ──
        private readonly Queue<Action> _mainThreadQueue = new();
        private readonly object _queueLock = new();

        private const int Port = 7777;
        private const int MaxNetworkResolution = 512;
        private const float MaxNetworkBoardSize = 100f;
        private const float MaxNetworkObjectScale = 20f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // Drain queued actions on the main thread
            while (true)
            {
                Action action;
                lock (_queueLock)
                {
                    if (_mainThreadQueue.Count == 0) break;
                    action = _mainThreadQueue.Dequeue();
                }

                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void EnqueueMain(Action action)
        {
            lock (_queueLock) _mainThreadQueue.Enqueue(action);
        }

        // ══════════════════════════════════════════════
        // Public API — same interface as before
        // ══════════════════════════════════════════════

        public uint NextNetId() => _nextNetId++;

        public void RegisterNetworkObject(uint netId, PlacedObject obj)
        {
            _networkObjects[netId] = obj;
        }

        public void UnregisterNetworkObject(uint netId)
        {
            _networkObjects.Remove(netId);
        }

        public PlacedObject GetNetworkObject(uint netId)
        {
            _networkObjects.TryGetValue(netId, out var obj);
            return obj;
        }

        /// <summary>
        /// Clear the network object registry. Used by the replay system to start
        /// each playback from a clean slate.
        /// </summary>
        public void ClearNetworkObjects()
        {
            _networkObjects.Clear();
        }

        public void SetMaterialColor(string materialName, Color color)
        {
            _materialColors[materialName] = color;
        }

        // ──────────────────────────────────────────────
        // Host / Join / Disconnect
        // ──────────────────────────────────────────────

        public void StartHost()
        {
            // Tear down any previous session so the port is released
            if (_isOnline)
                Disconnect();
            else
                StopServer();   // clean up a lingering listener even if not flagged online

            try
            {
                _server = new TcpListener(IPAddress.Any, Port);
                _server.Server.SetSocketOption(
                    SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _server.Start();
                _isOnline = true;
                _isHost = true;
                _connectedClients = 0;
                GameManager.Instance.NetworkRole = PlayerRole.Patient;

                _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
                _acceptThread.Start();

                OnConnected?.Invoke();
                Debug.Log("[Network] Started as Host on port " + Port);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Network] Failed to start host: " + ex.Message);
            }
        }

        public void StartClient(string address, PlayerRole requestedRole = PlayerRole.Observer)
        {
            try
            {
                _requestedRole = requestedRole;
                _lastServerAddress = address;
                _clientSocket = new TcpClient();
                _clientSocket.BeginConnect(address, Port, OnClientConnect, _clientSocket);
                _isOnline = true;
                _isHost = false;
                Debug.Log($"[Network] Connecting to {address}:{Port}...");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Network] Failed to connect: " + ex.Message);
                EnqueueMain(() => OnDisconnected?.Invoke());
            }
        }

        public string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch (Exception) { }
            return "127.0.0.1";
        }

        // ──────────────────────────────────────────────
        // Relay: host creates room
        // ──────────────────────────────────────────────

        public void StartHostRelay(string relayAddress, bool therapistMode = false)
        {
            try
            {
                _relayMode = true;
                _lastRelayAddress = relayAddress;
                _isOnline = true;
                _isHost = true;
                _connectedClients = 0;
                _therapistMode = therapistMode;
                _patientAssigned = false;
                // In Therapist mode the host acts as the Psychologist (can select but not sculpt);
                // the patient client will be assigned the Patient role on join.
                GameManager.Instance.NetworkRole = therapistMode
                    ? PlayerRole.Psychologist
                    : PlayerRole.Patient;

                var thread = new Thread(() => RelayHostConnect(relayAddress)) { IsBackground = true };
                thread.Start();

                Debug.Log($"[Network] Connecting to relay at {relayAddress}:{Port}... (TherapistMode={therapistMode})");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Network] Failed to start relay host: " + ex.Message);
                _relayMode = false;
                _isOnline = false;
                _isHost = false;
            }
        }

        private void RelayHostConnect(string relayAddress)
        {
            try
            {
                var socket = new TcpClient();
                socket.Connect(relayAddress, Port);
                socket.NoDelay = true;
                var stream = socket.GetStream();

                // Send CreateRoom
                var createPayload = NetSerializer.WriteCreateRoom();
                var createPacket = NetSerializer.Pack(NetMsgType.CreateRoom, createPayload);
                stream.Write(createPacket, 0, createPacket.Length);
                stream.Flush();

                // Read RoomCreated response
                byte[] lenBuf = new byte[4];
                if (!ReadExact(stream, lenBuf, 4)) throw new Exception("Failed to read relay response");
                int msgLen = BitConverter.ToInt32(lenBuf, 0);
                byte[] body = new byte[msgLen];
                if (!ReadExact(stream, body, msgLen)) throw new Exception("Failed to read relay response body");

                NetMsgType type = (NetMsgType)body[0];
                byte[] payload = new byte[msgLen - 1];
                Buffer.BlockCopy(body, 1, payload, 0, payload.Length);

                if (type != NetMsgType.RoomCreated)
                    throw new Exception($"Expected RoomCreated, got {type}");

                string roomCode = NetSerializer.ReadRoomCreated(payload);

                EnqueueMain(() =>
                {
                    _relaySocket = socket;
                    _relayStream = stream;
                    _roomCode = roomCode;

                    OnConnected?.Invoke();
                    OnRoomCreated?.Invoke(roomCode);
                    Debug.Log($"[Network] Relay host ready! Room code: {roomCode}");
                });

                // Start receive loop — reads client messages forwarded by relay
                RelayHostReceiveLoop(socket, stream);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Network] Relay host connect failed: " + ex.Message);
                EnqueueMain(() =>
                {
                    _isOnline = false;
                    _isHost = false;
                    _relayMode = false;
                    OnDisconnected?.Invoke();
                });
            }
        }

        private void RelayHostReceiveLoop(TcpClient socket, NetworkStream stream)
        {
            try
            {
                byte[] lenBuf = new byte[4];
                while (_isOnline && socket.Connected)
                {
                    if (!ReadExact(stream, lenBuf, 4)) break;
                    int msgLen = BitConverter.ToInt32(lenBuf, 0);
                    if (msgLen <= 0 || msgLen > 16 * 1024 * 1024) break;

                    byte[] body = new byte[msgLen];
                    if (!ReadExact(stream, body, msgLen)) break;

                    NetMsgType type = (NetMsgType)body[0];
                    byte[] payload = new byte[msgLen - 1];
                    Buffer.BlockCopy(body, 1, payload, 0, payload.Length);

                    // Mirror to session recorder (background-thread safe).
                    // On the host, we tag relay-forwarded messages as Incoming so a replay
                    // can distinguish "this came from a peer" from "this was generated locally".
                    SessionRecorder.Instance?.Record(SessionRecorder.Direction.Incoming, type, payload);

                    // Handle client-to-host messages (forwarded by relay)
                    if (type == NetMsgType.RoleRequest)
                    {
                        var requestedRole = NetSerializer.ReadRoleRequest(payload);
                        // In Therapist mode, the FIRST client requesting Patient gets it.
                        // Subsequent Patient requests are downgraded to Observer.
                        // Outside Therapist mode, Patient is reserved for the host (so always downgrade).
                        PlayerRole assignedRole;
                        if (requestedRole == PlayerRole.Patient)
                        {
                            if (_therapistMode && !_patientAssigned)
                            {
                                assignedRole = PlayerRole.Patient;
                                _patientAssigned = true;
                            }
                            else
                            {
                                assignedRole = PlayerRole.Observer;
                            }
                        }
                        else
                        {
                            assignedRole = requestedRole;
                        }
                        var rolePayload = NetSerializer.WriteRoleAssignment(assignedRole);
                        EnqueueMain(() =>
                        {
                            try { SendToRelay(NetSerializer.Pack(NetMsgType.RoleAssignment, rolePayload)); }
                            catch { }
                        });

                        // Also send full state to the newly joined client
                        // In relay mode, we broadcast — the relay sends to all clients.
                        // The new client needs the full state.
                        EnqueueMain(() =>
                        {
                            try { SendFullStateViaRelay(); }
                            catch { }
                            _connectedClients++;
                            OnClientCountChanged?.Invoke(_connectedClients);
                        });

                        Debug.Log($"[Network] Relay: client requested {requestedRole}, assigned {assignedRole}");
                    }
                    else if (type == NetMsgType.ObjectSelection)
                    {
                        // Client sent object selection, broadcast to all clients
                        EnqueueMain(() =>
                        {
                            try { SendToRelay(NetSerializer.Pack(NetMsgType.ObjectSelection, payload)); }
                            catch { }
                        });
                    }
                    else if (type == NetMsgType.ClientHeightmapPaint)
                    {
                        // Patient drew sand — apply locally on host, then broadcast as
                        // HeightmapRegion to all peers (so observers + the patient see it).
                        var capturedPayload = payload;
                        EnqueueMain(() =>
                        {
                            try { ApplyAndRebroadcastHeightmap(capturedPayload); }
                            catch (Exception ex) { Debug.LogWarning("[Net] HeightmapPaint apply: " + ex.Message); }
                        });
                    }
                    else if (type == NetMsgType.ClientSplatPaint)
                    {
                        var capturedPayload = payload;
                        EnqueueMain(() =>
                        {
                            try { ApplyAndRebroadcastSplat(capturedPayload); }
                            catch (Exception ex) { Debug.LogWarning("[Net] SplatPaint apply: " + ex.Message); }
                        });
                    }
                    else if (type == NetMsgType.ClientSpawnRequest)
                    {
                        var capturedPayload = payload;
                        EnqueueMain(() =>
                        {
                            try { ApplyAndRebroadcastSpawn(capturedPayload); }
                            catch (Exception ex) { Debug.LogWarning("[Net] SpawnRequest apply: " + ex.Message); }
                        });
                    }
                    else if (type == NetMsgType.ClientMoveObject)
                    {
                        var capturedPayload = payload;
                        EnqueueMain(() =>
                        {
                            try { ApplyAndRebroadcastMove(capturedPayload); }
                            catch (Exception ex) { Debug.LogWarning("[Net] MoveObject apply: " + ex.Message); }
                        });
                    }
                    else if (type == NetMsgType.ClientRemoveObject)
                    {
                        var capturedPayload = payload;
                        EnqueueMain(() =>
                        {
                            try { ApplyAndRebroadcastRemove(capturedPayload); }
                            catch (Exception ex) { Debug.LogWarning("[Net] RemoveObject apply: " + ex.Message); }
                        });
                    }
                    else if (type == NetMsgType.ClientColorChange)
                    {
                        var capturedPayload = payload;
                        EnqueueMain(() =>
                        {
                            try { ApplyAndRebroadcastColor(capturedPayload); }
                            catch (Exception ex) { Debug.LogWarning("[Net] ColorChange apply: " + ex.Message); }
                        });
                    }
                    else if (type == NetMsgType.PointerHover)
                    {
                        // Cheap, idempotent: fire local event for HUD on host AND
                        // re-broadcast so observers can also see the patient cursor.
                        var capturedPayload = payload;
                        EnqueueMain(() =>
                        {
                            try
                            {
                                NetSerializer.ReadPointerHover(capturedPayload, out var pos, out var kind);
                                OnPatientPointerHover?.Invoke(pos, kind);
                                SendToRelay(NetSerializer.Pack(NetMsgType.PointerHover, capturedPayload));
                            }
                            catch (Exception ex) { Debug.LogWarning("[Net] PointerHover relay: " + ex.Message); }
                        });
                    }
                }
            }
            catch (Exception) { /* disconnected from relay */ }
            finally
            {
                EnqueueMain(() =>
                {
                    if (_isOnline)
                    {
                        Cleanup();
                        _relayMode = false;
                        OnDisconnected?.Invoke();
                        Debug.Log("[Network] Relay host disconnected");
                    }
                });
            }
        }

        /// <summary>
        /// Builds a SpawnObjectData list from ALL placed objects in the scene (not just
        /// those in _networkObjects). Objects that were placed locally without a NetId
        /// (e.g. loaded from a saved board before hosting started) are assigned one here
        /// and registered so future delta-syncs work correctly.
        /// </summary>
        private List<SpawnObjectData> BuildPlacedObjectList()
        {
            var placer = FindAnyObjectByType<ObjectPlacer>();
            var result = new List<SpawnObjectData>();
            if (placer == null) return result;

            foreach (var placed in placer.PlacedObjects)
            {
                if (placed == null) continue;

                // Assign a NetId if this object was placed locally without one.
                if (placed.NetworkId == 0)
                {
                    placed.NetworkId = NextNetId();
                    RegisterNetworkObject(placed.NetworkId, placed);
                }

                result.Add(new SpawnObjectData
                {
                    NetId = placed.NetworkId,
                    ObjectId = placed.ObjectData != null ? placed.ObjectData.DisplayName
                             : placed.NetworkItem != null ? placed.NetworkItem.id
                             : "",
                    Position = placed.transform.position,
                    Rotation = placed.transform.rotation,
                    Scale = GetRelativeScale(placed)
                });
            }
            return result;
        }

        /// <summary>
        /// Build a FullState payload containing current sand heightmap, placed objects,
        /// and material colors. Used by replay system to capture initial sandbox snapshot.
        /// </summary>
        public byte[] BuildFullStatePayload()
        {
            var sandMesh = SandMesh.Instance;
            byte[] heightmapBytes = Array.Empty<byte>();
            if (sandMesh != null)
            {
                heightmapBytes = new byte[sandMesh.Heightmap.Length * 4];
                Buffer.BlockCopy(sandMesh.Heightmap, 0, heightmapBytes, 0, heightmapBytes.Length);
            }

            var placedObjects = BuildPlacedObjectList();

            var colors = new List<ColorSyncData>();
            foreach (var kvp in _materialColors)
                colors.Add(new ColorSyncData { MaterialName = kvp.Key, Color = kvp.Value });

            var config = GameManager.Instance?.Config;
            float bw = config != null ? config.SandboxWidth : 10f;
            float bd = config != null ? config.SandboxDepth : 10f;
            int res = sandMesh != null ? sandMesh.Resolution : (config != null ? config.HeightmapResolution : 128);

            return NetSerializer.WriteFullState(bw, bd, res, heightmapBytes,
                placedObjects.ToArray(), colors.ToArray());
        }

        private void SendFullStateViaRelay()
        {
            var payload = BuildFullStatePayload();
            SendToRelay(NetSerializer.Pack(NetMsgType.FullState, payload));

            // Also send the current splatmap to the relay so joining clients get painted areas
            var smc = SandMaterialController.Instance;
            if (smc != null)
            {
                int sr = SandMaterialController.SplatResolution;
                byte[] splatPixels = smc.GetSplatmapRegionBytes(0, 0, sr, sr);
                var splatPayload = NetSerializer.WriteSplatmapRegion(0, 0, sr, sr, splatPixels);
                SendToRelay(NetSerializer.Pack(NetMsgType.SplatmapRegion, splatPayload));
            }
        }

        // ──────────────────────────────────────────────
        // Relay: client joins room
        // ──────────────────────────────────────────────

        public void StartClientRelay(string relayAddress, string roomCode, PlayerRole requestedRole = PlayerRole.Observer)
        {
            try
            {
                _relayMode = true;
                _requestedRole = requestedRole;
                _lastRelayAddress = relayAddress;
                _lastRoomCode = roomCode;
                _isOnline = true;
                _isHost = false;

                var thread = new Thread(() => RelayClientConnect(relayAddress, roomCode)) { IsBackground = true };
                thread.Start();

                Debug.Log($"[Network] Joining room {roomCode} via relay {relayAddress}:{Port}...");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Network] Failed to start relay client: " + ex.Message);
                _relayMode = false;
                _isOnline = false;
                EnqueueMain(() => OnDisconnected?.Invoke());
            }
        }

        private void RelayClientConnect(string relayAddress, string roomCode)
        {
            try
            {
                var socket = new TcpClient();
                socket.Connect(relayAddress, Port);
                socket.NoDelay = true;
                var stream = socket.GetStream();

                // Send JoinRoom
                var joinPayload = NetSerializer.WriteJoinRoom(roomCode);
                var joinPacket = NetSerializer.Pack(NetMsgType.JoinRoom, joinPayload);
                stream.Write(joinPacket, 0, joinPacket.Length);
                stream.Flush();

                // Read JoinResult
                byte[] lenBuf = new byte[4];
                if (!ReadExact(stream, lenBuf, 4)) throw new Exception("Failed to read relay response");
                int msgLen = BitConverter.ToInt32(lenBuf, 0);
                byte[] body = new byte[msgLen];
                if (!ReadExact(stream, body, msgLen)) throw new Exception("Failed to read relay response body");

                NetMsgType type = (NetMsgType)body[0];
                byte[] payload = new byte[msgLen - 1];
                Buffer.BlockCopy(body, 1, payload, 0, payload.Length);

                if (type != NetMsgType.JoinResult)
                    throw new Exception($"Expected JoinResult, got {type}");

                NetSerializer.ReadJoinResult(payload, out bool success, out string reason);
                if (!success)
                    throw new Exception($"Join failed: {reason}");

                EnqueueMain(() =>
                {
                    _relaySocket = socket;
                    _relayStream = stream;
                    _clientSocket = socket; // so existing code paths work
                    _roomCode = roomCode;

                    OnConnected?.Invoke();
                    EventBus.Publish(new NetworkConnectedEvent());
                    Debug.Log($"[Network] Joined room {roomCode} via relay");

                    // Send role request through relay to host
                    SendRoleRequest(_requestedRole);
                });

                // Enter standard client receive loop — messages from host arrive via relay
                // Reuse the existing ClientReceiveLoop by setting _clientSocket
                EnqueueMain(() =>
                {
                    _clientReceiveThread = new Thread(() => RelayClientReceiveLoop(socket, stream)) { IsBackground = true };
                    _clientReceiveThread.Start();
                });
            }
            catch (Exception ex)
            {
                Debug.LogError("[Network] Relay client connect failed: " + ex.Message);
                EnqueueMain(() =>
                {
                    _isOnline = false;
                    _relayMode = false;
                    OnDisconnected?.Invoke();
                    EventBus.Publish(new NetworkDisconnectedEvent());
                });
            }
        }

        private void RelayClientReceiveLoop(TcpClient socket, NetworkStream stream)
        {
            try
            {
                byte[] lenBuf = new byte[4];
                while (_isOnline && socket.Connected)
                {
                    if (!ReadExact(stream, lenBuf, 4)) break;
                    int msgLen = BitConverter.ToInt32(lenBuf, 0);
                    if (msgLen <= 0 || msgLen > 16 * 1024 * 1024) break;

                    byte[] body = new byte[msgLen];
                    if (!ReadExact(stream, body, msgLen)) break;

                    NetMsgType type = (NetMsgType)body[0];
                    byte[] payload = new byte[msgLen - 1];
                    Buffer.BlockCopy(body, 1, payload, 0, payload.Length);

                    // Mirror to session recorder (background-thread safe)
                    SessionRecorder.Instance?.Record(SessionRecorder.Direction.Incoming, type, payload);

                    EnqueueMain(() => HandleClientMessage(type, payload));
                }
            }
            catch (Exception) { /* disconnected */ }
            finally
            {
                EnqueueMain(() =>
                {
                    if (_isOnline)
                    {
                        string addr = _lastRelayAddress;
                        string code = _lastRoomCode;
                        Cleanup();
                        _relayMode = false;
                        OnDisconnected?.Invoke();
                        EventBus.Publish(new NetworkDisconnectedEvent());
                        Debug.Log("[Network] Disconnected from relay");

                        // Attempt auto-reconnection via relay
                        if (!string.IsNullOrEmpty(addr) && !string.IsNullOrEmpty(code))
                            StartRelayReconnection(addr, code);
                    }
                });
            }
        }

        private void SendToRelay(byte[] packet)
        {
            if (_relayStream == null) return;
            try
            {
                lock (_relayStream)
                {
                    _relayStream.Write(packet, 0, packet.Length);
                    _relayStream.Flush();
                }
                // Mirror outgoing traffic to the session recorder if active.
                // packet layout: [4-byte len][1-byte type][payload]
                var rec = SessionRecorder.Instance;
                if (rec != null && rec.IsRecording && packet.Length >= 5)
                {
                    var type = (NetMsgType)packet[4];
                    int payloadLen = packet.Length - 5;
                    byte[] payload = new byte[payloadLen];
                    if (payloadLen > 0) Buffer.BlockCopy(packet, 5, payload, 0, payloadLen);
                    rec.Record(SessionRecorder.Direction.Outgoing, type, payload);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Network] Failed to send to relay: " + ex.Message);
            }
        }

        // ──────────────────────────────────────────────
        // Server: accept loop + per-client receive
        // ──────────────────────────────────────────────

        private void AcceptLoop()
        {
            try
            {
                while (_isOnline && _server != null)
                {
                    if (!_server.Pending())
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    var client = _server.AcceptTcpClient();
                    client.NoDelay = true;
                    lock (_clientsLock) _clients.Add(client);

                    EnqueueMain(() =>
                    {
                        _connectedClients++;
                        OnClientCountChanged?.Invoke(_connectedClients);
                        Debug.Log($"[Network] Client connected. Total: {_connectedClients}");
                    });

                    // Send role assignment + full state on main thread
                    EnqueueMain(() => SendInitToClient(client));

                    // Start receive thread for this client
                    var t = new Thread(() => ServerReceiveLoop(client)) { IsBackground = true };
                    t.Start();
                }
            }
            catch (SocketException) { /* server stopped */ }
            catch (Exception ex) { Debug.LogError("[Network] Accept error: " + ex.Message); }
        }

        private void ServerReceiveLoop(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                byte[] lenBuf = new byte[4];

                while (_isOnline && client.Connected)
                {
                    if (!ReadExact(stream, lenBuf, 4)) break;
                    int msgLen = BitConverter.ToInt32(lenBuf, 0);
                    if (msgLen <= 0 || msgLen > 16 * 1024 * 1024) break;

                    byte[] body = new byte[msgLen];
                    if (!ReadExact(stream, body, msgLen)) break;

                    NetMsgType type = (NetMsgType)body[0];
                    byte[] payload = new byte[msgLen - 1];
                    Buffer.BlockCopy(body, 1, payload, 0, payload.Length);

                    // Handle client-to-server messages
                    if (type == NetMsgType.RoleRequest)
                    {
                        var requestedRole = NetSerializer.ReadRoleRequest(payload);
                        // Assign the requested role (Patient is reserved for host)
                        var assignedRole = (requestedRole == PlayerRole.Patient)
                            ? PlayerRole.Observer : requestedRole;
                        var rolePayload = NetSerializer.WriteRoleAssignment(assignedRole);
                        EnqueueMain(() =>
                        {
                            try { SendToClient(client, NetSerializer.Pack(NetMsgType.RoleAssignment, rolePayload)); }
                            catch { }
                        });
                        Debug.Log($"[Network] Client requested {requestedRole}, assigned {assignedRole}");
                    }
                    else if (type == NetMsgType.ObjectSelection)
                    {
                        // Client sent object selection, broadcast to all clients
                        EnqueueMain(() =>
                        {
                            try { BroadcastToClients(NetSerializer.Pack(NetMsgType.ObjectSelection, payload)); }
                            catch { }
                        });
                    }
                }
            }
            catch (Exception) { /* client disconnected */ }
            finally
            {
                lock (_clientsLock) _clients.Remove(client);
                try { client.Close(); } catch { }
                EnqueueMain(() =>
                {
                    _connectedClients = Math.Max(0, _connectedClients - 1);
                    OnClientCountChanged?.Invoke(_connectedClients);
                    Debug.Log($"[Network] Client disconnected. Total: {_connectedClients}");
                });
            }
        }

        private void SendInitToClient(TcpClient client)
        {
            try
            {
                // Role assignment
                var rolePayload = NetSerializer.WriteRoleAssignment(PlayerRole.Observer);
                SendToClient(client, NetSerializer.Pack(NetMsgType.RoleAssignment, rolePayload));

                // Full state
                SendFullStateToClient(client);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Network] Failed to send init: " + ex.Message);
            }
        }

        // ──────────────────────────────────────────────
        // Client: connect + receive loop
        // ──────────────────────────────────────────────

        private void OnClientConnect(IAsyncResult ar)
        {
            try
            {
                var client = (TcpClient)ar.AsyncState;
                client.EndConnect(ar);
                client.NoDelay = true;

                _clientReceiveThread = new Thread(ClientReceiveLoop) { IsBackground = true };
                _clientReceiveThread.Start();

                EnqueueMain(() =>
                {
                    OnConnected?.Invoke();
                    EventBus.Publish(new NetworkConnectedEvent());
                    Debug.Log("[Network] Connected to server");

                    // Send role request to server
                    SendRoleRequest(_requestedRole);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError("[Network] Connection failed: " + ex.Message);
                EnqueueMain(() =>
                {
                    _isOnline = false;
                    OnDisconnected?.Invoke();
                    EventBus.Publish(new NetworkDisconnectedEvent());
                });
            }
        }

        private void ClientReceiveLoop()
        {
            try
            {
                var stream = _clientSocket.GetStream();
                byte[] lenBuf = new byte[4];

                while (_isOnline && _clientSocket.Connected)
                {
                    // Read 4-byte length header
                    if (!ReadExact(stream, lenBuf, 4)) break;
                    int msgLen = BitConverter.ToInt32(lenBuf, 0);
                    if (msgLen <= 0 || msgLen > 16 * 1024 * 1024) break; // sanity: max 16 MB

                    // Read message body
                    byte[] body = new byte[msgLen];
                    if (!ReadExact(stream, body, msgLen)) break;

                    NetMsgType type = (NetMsgType)body[0];
                    byte[] payload = new byte[msgLen - 1];
                    Buffer.BlockCopy(body, 1, payload, 0, payload.Length);

                    // Dispatch on main thread
                    EnqueueMain(() => HandleClientMessage(type, payload));
                }
            }
            catch (Exception) { /* disconnected */ }
            finally
            {
                EnqueueMain(() =>
                {
                    if (_isOnline)
                    {
                        bool wasOnline = _isOnline;
                        string addr = _lastServerAddress;
                        Cleanup();
                        OnDisconnected?.Invoke();
                        EventBus.Publish(new NetworkDisconnectedEvent());
                        Debug.Log("[Network] Disconnected from server");

                        // Attempt auto-reconnection
                        if (wasOnline && !string.IsNullOrEmpty(addr))
                            StartReconnection(addr);
                    }
                });
            }
        }

        /// <summary>
        /// Apply a network message locally. Public so the replay system
        /// (<see cref="SessionPlayer"/>) can re-emit recorded events through the
        /// same code paths the live receive loop uses.
        /// </summary>
        public void HandleClientMessage(NetMsgType type, byte[] payload)
        {
            switch (type)
            {
                case NetMsgType.RoleAssignment:
                    {
                        // During replay, ignore role flips — they can re-mark the
                        // viewer as Patient and drop subsequent sand events.
                        if (SessionPlayer.IsReplayActive)
                            break;
                        var role = NetSerializer.ReadRoleAssignment(payload);
                        if (GameManager.Instance != null)
                            GameManager.Instance.NetworkRole = role;
                        Debug.Log($"[Network] Role assigned: {role}");
                        break;
                    }
                case NetMsgType.FullState:
                    {
                        NetSerializer.ReadFullState(payload, out float bw, out float bd,
                            out int res, out byte[] hm, out SpawnObjectData[] objs, out ColorSyncData[] cols);
                        if (!IsValidNetworkBoard(bw, bd, res))
                        {
                            Debug.LogWarning($"[Network] Ignoring invalid FullState board: {bw}x{bd} res={res}");
                            break;
                        }

                        long expectedHeightmapBytes = (long)res * res * 4;
                        if (hm != null && hm.Length > 0 && hm.Length != expectedHeightmapBytes)
                        {
                            Debug.LogWarning($"[Network] Ignoring FullState with invalid heightmap byte length: {hm.Length}");
                            break;
                        }

                        // Clear any locally loaded objects before applying the host's state.
                        // Without this, objects from the board the client had open persist
                        // alongside the objects received from the host.
                        var placer = FindAnyObjectByType<ObjectPlacer>();
                        placer?.ClearAll();
                        _networkObjects.Clear();

                        // Resize board to match host
                        if (SandMesh.Instance != null && res > 0)
                        {
                            bool needsResize = SandMesh.Instance.Resolution != res
                                || Mathf.Abs(SandMesh.Instance.Width - bw) > 0.01f
                                || Mathf.Abs(SandMesh.Instance.Depth - bd) > 0.01f;

                            // Always sync config so SandboxFrame.Rebuild() reads the correct size.
                            var cfg = GameManager.Instance?.Config;
                            if (cfg != null)
                            {
                                cfg.SandboxWidth = bw;
                                cfg.SandboxDepth = bd;
                            }

                            if (needsResize)
                                SandMesh.Instance.ReinitializeFromNetwork(bw, bd, res);

                            // Rebuild the physical frame (walls + floor) to the host's dimensions.
                            var frame = FindAnyObjectByType<SandboxFrame>();
                            frame?.Rebuild();
                        }

                        if (hm != null && hm.Length > 0)
                        {
                            if (hm.Length % 4 != 0)
                            {
                                Debug.LogWarning($"[Network] Ignoring heightmap with invalid byte length: {hm.Length}");
                                break;
                            }

                            float[] heightmap = new float[hm.Length / 4];
                            Buffer.BlockCopy(hm, 0, heightmap, 0, hm.Length);
                            Sandplay.Sand.SandSyncManager.SuppressNetworkSync = true;
                            SandMesh.Instance?.SetHeightmap(heightmap);
                            Sandplay.Sand.SandSyncManager.SuppressNetworkSync = false;
                        }
                        if (objs != null)
                            foreach (var o in objs)
                                SpawnObjectLocally(o.ObjectId, o.NetId, o.Position, o.Rotation, o.Scale);
                        if (cols != null)
                            foreach (var c in cols)
                                ApplyColorLocally(c.MaterialName, c.Color);

                        Debug.Log("[Network] Full state applied");
                        break;
                    }
                case NetMsgType.HeightmapRegion:
                    {
                        // Patient client is the only sculptor in Therapist mode and is
                        // already authoritative for its own changes locally. Ignoring the
                        // host's echo prevents stale state from overwriting in-progress edits.
                        if (GameManager.Instance != null && GameManager.Instance.IsPatient)
                            break;
                        NetSerializer.ReadHeightmapRegion(payload, out int sx, out int sz,
                            out int w, out int d, out byte[] h);
                        if (SandMesh.Instance != null)
                        {
                            if (h == null || h.Length % 4 != 0)
                            {
                                Debug.LogWarning($"[Network] Ignoring heightmap region with invalid byte length: {h?.Length ?? 0}");
                                break;
                            }

                            float[] regionData = new float[h.Length / 4];
                            Buffer.BlockCopy(h, 0, regionData, 0, h.Length);
                            Sandplay.Sand.SandSyncManager.SuppressNetworkSync = true;
                            SandMesh.Instance.ApplyHeightmapRegion(sx, sz, w, d, regionData);
                            Sandplay.Sand.SandSyncManager.SuppressNetworkSync = false;
                        }
                        break;
                    }
                case NetMsgType.SplatmapRegion:
                    {
                        // Patient client is authoritative for its own splat paint - ignore echoes.
                        if (GameManager.Instance != null && GameManager.Instance.IsPatient)
                            break;
                        NetSerializer.ReadSplatmapRegion(payload, out int spx, out int spy,
                            out int spw, out int sph, out byte[] pixels);
                        Sandplay.Sand.SandSyncManager.SuppressNetworkSync = true;
                        SandMaterialController.Instance?.ApplySplatmapRegion(spx, spy, spw, sph, pixels);
                        Sandplay.Sand.SandSyncManager.SuppressNetworkSync = false;
                        break;
                    }
                case NetMsgType.SpawnObject:
                    {
                        NetSerializer.ReadSpawnObject(payload, out uint nid, out string oid,
                            out Vector3 pos, out Quaternion rot, out float scale);
                        SpawnObjectLocally(oid, nid, pos, rot, scale);
                        break;
                    }
                case NetMsgType.MoveObject:
                    {
                        NetSerializer.ReadMoveObject(payload, out uint nid,
                            out Vector3 pos, out Quaternion rot, out float scale);
                        scale = SanitizeNetworkScale(scale);
                        var obj = GetNetworkObject(nid);
                        if (obj != null)
                        {
                            obj.transform.position = pos;
                            obj.transform.rotation = rot;
                            // scale is relative; multiply by whichever template (local prefab or
                            // downloaded GLB) the object was spawned from.
                            Vector3 tplScale = Vector3.one;
                            if (obj.ObjectData != null && obj.ObjectData.Prefab != null)
                                tplScale = obj.ObjectData.Prefab.transform.localScale;
                            else if (obj.NetworkItem != null && obj.NetworkItem.LoadedPrefab != null)
                                tplScale = obj.NetworkItem.LoadedPrefab.transform.localScale;
                            obj.transform.localScale = tplScale * scale;
                        }
                        break;
                    }
                case NetMsgType.RemoveObject:
                    {
                        uint nid = NetSerializer.ReadRemoveObject(payload);
                        var obj = GetNetworkObject(nid);
                        if (obj != null)
                        {
                            UnregisterNetworkObject(nid);
                            Destroy(obj.gameObject);
                        }
                        break;
                    }
                case NetMsgType.ColorSync:
                    {
                        NetSerializer.ReadColorSync(payload, out string mat, out Color col);
                        ApplyColorLocally(mat, col);
                        break;
                    }
                case NetMsgType.UndoRedoSync:
                    {
                        NetSerializer.ReadUndoRedoSync(payload, out byte[] hm, out SpawnObjectData[] objs);
                        ApplyUndoRedoState(hm, objs);
                        break;
                    }
                case NetMsgType.ObjectSelection:
                    {
                        uint netId = NetSerializer.ReadObjectSelection(payload);
                        ApplyObjectSelection(netId);
                        break;
                    }
                case NetMsgType.PointerHover:
                    {
                        // Patient sees its own echo — ignore. Observers/therapist
                        // (when they are the non-host) get to render it.
                        if (GameManager.Instance != null && GameManager.Instance.IsPatient)
                            break;
                        NetSerializer.ReadPointerHover(payload, out var pos, out var kind);
                        OnPatientPointerHover?.Invoke(pos, kind);
                        break;
                    }
            }
        }

        // ──────────────────────────────────────────────
        // Server: broadcast to all clients
        // ──────────────────────────────────────────────

        /// <summary>
        /// Mirror an already-serialized outgoing payload to the SessionRecorder
        /// (for .sandlog replay). No-op when no recording is active.
        /// Called from the public Send* methods so OFFLINE solo play and ONLINE
        /// host actions both end up captured — recordings no longer require an
        /// active relay socket.
        /// </summary>
        private static void RecordOutgoingIfActive(NetMsgType type, byte[] payload)
        {
            var rec = SessionRecorder.Instance;
            if (rec != null && rec.IsRecording)
                rec.Record(SessionRecorder.Direction.Outgoing, type, payload);
        }

        public void SendHeightmapRegion(int startX, int startZ, int width, int depth, float[] regionData)
        {
            byte[] bytes = new byte[regionData.Length * 4];
            Buffer.BlockCopy(regionData, 0, bytes, 0, bytes.Length);

            var payload = NetSerializer.WriteHeightmapRegion(startX, startZ, width, depth, bytes);
            RecordOutgoingIfActive(NetMsgType.HeightmapRegion, payload);

            if (!_isOnline || !_isHost) return;
            BroadcastToClients(NetSerializer.Pack(NetMsgType.HeightmapRegion, payload));
        }

        public void SendSplatmapRegion(int startX, int startY, int width, int height, byte[] pixels)
        {
            var payload = NetSerializer.WriteSplatmapRegion(startX, startY, width, height, pixels);
            RecordOutgoingIfActive(NetMsgType.SplatmapRegion, payload);

            if (!_isOnline || !_isHost) return;
            BroadcastToClients(NetSerializer.Pack(NetMsgType.SplatmapRegion, payload));
        }

        public void SendSpawnObject(uint netId, string objectId, Vector3 pos, Quaternion rot, float scale)
        {
            var payload = NetSerializer.WriteSpawnObject(netId, objectId, pos, rot, scale);
            RecordOutgoingIfActive(NetMsgType.SpawnObject, payload);

            if (!_isOnline || !_isHost) return;
            BroadcastToClients(NetSerializer.Pack(NetMsgType.SpawnObject, payload));
        }

        public void SendMoveObject(uint netId, Vector3 pos, Quaternion rot, float scale)
        {
            var payload = NetSerializer.WriteMoveObject(netId, pos, rot, scale);
            RecordOutgoingIfActive(NetMsgType.MoveObject, payload);

            if (!_isOnline || !_isHost) return;
            BroadcastToClients(NetSerializer.Pack(NetMsgType.MoveObject, payload));
        }

        public void SendRemoveObject(uint netId)
        {
            var payload = NetSerializer.WriteRemoveObject(netId);
            RecordOutgoingIfActive(NetMsgType.RemoveObject, payload);

            if (!_isOnline || !_isHost) return;
            BroadcastToClients(NetSerializer.Pack(NetMsgType.RemoveObject, payload));
        }

        public void SendColorSync(string materialName, Color color)
        {
            var payload = NetSerializer.WriteColorSync(materialName, color);
            RecordOutgoingIfActive(NetMsgType.ColorSync, payload);

            if (!_isOnline) return;
            if (_isHost)
            {
                SetMaterialColor(materialName, color);
                BroadcastToClients(NetSerializer.Pack(NetMsgType.ColorSync, payload));
            }
            else if (_relayMode && GameManager.Instance != null && GameManager.Instance.IsPatient)
            {
                // Patient client: forward to host who will broadcast.
                SendClientColorChange(materialName, color);
            }
        }

        /// <summary>
        /// Broadcast full undo/redo state snapshot to all clients.
        /// Called by UndoManager after undo/redo executes.
        /// </summary>
        public void SendUndoRedoSync()
        {
            if (!_isOnline || !_isHost) return;

            var sandMesh = SandMesh.Instance;
            byte[] heightmapBytes = Array.Empty<byte>();
            if (sandMesh != null)
            {
                heightmapBytes = new byte[sandMesh.Heightmap.Length * 4];
                Buffer.BlockCopy(sandMesh.Heightmap, 0, heightmapBytes, 0, heightmapBytes.Length);
            }

            var objList = new List<SpawnObjectData>();
            foreach (var kvp in _networkObjects)
            {
                if (kvp.Value == null) continue;
                objList.Add(new SpawnObjectData
                {
                    NetId = kvp.Key,
                    ObjectId = kvp.Value.ObjectData != null ? kvp.Value.ObjectData.DisplayName : "",
                    Position = kvp.Value.transform.position,
                    Rotation = kvp.Value.transform.rotation,
                    Scale = GetRelativeScale(kvp.Value)
                });
            }

            var payload = NetSerializer.WriteUndoRedoSync(heightmapBytes, objList.ToArray());
            BroadcastToClients(NetSerializer.Pack(NetMsgType.UndoRedoSync, payload));
        }

        /// <summary>
        /// Send object selection (for Psychologist role to highlight selections to all clients).
        /// netId = 0 means deselect.
        /// If host: broadcasts to all clients.
        /// If client: sends to server/relay which will broadcast.
        /// </summary>
        public void SendObjectSelection(uint netId)
        {
            if (!_isOnline) return;

            var payload = NetSerializer.WriteObjectSelection(netId);
            var packet = NetSerializer.Pack(NetMsgType.ObjectSelection, payload);

            if (_isHost)
            {
                // Host broadcasts to all clients
                BroadcastToClients(packet);
            }
            else
            {
                // Client sends to server/relay
                try
                {
                    if (_relayMode)
                    {
                        SendToRelay(packet);
                    }
                    else
                    {
                        if (_clientSocket == null || !_clientSocket.Connected) return;
                        var stream = _clientSocket.GetStream();
                        stream.Write(packet, 0, packet.Length);
                        stream.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Network] Failed to send object selection: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Send role request from client to server.
        /// </summary>
        private void SendRoleRequest(PlayerRole role)
        {
            if (_isHost) return;
            try
            {
                var payload = NetSerializer.WriteRoleRequest(role);
                var packet = NetSerializer.Pack(NetMsgType.RoleRequest, payload);

                if (_relayMode)
                {
                    SendToRelay(packet);
                }
                else
                {
                    if (_clientSocket == null || !_clientSocket.Connected) return;
                    var stream = _clientSocket.GetStream();
                    stream.Write(packet, 0, packet.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Network] Failed to send role request: " + ex.Message);
            }
        }

        // ──────────────────────────────────────────────
        // Patient → Host action requests (Therapist mode)
        // ──────────────────────────────────────────────
        // These let a Patient client mutate the shared sandbox state. The patient applies
        // the action locally for instant feedback AND sends the same delta to the host.
        // The host re-applies and re-broadcasts so all peers (including the patient as
        // an idempotent confirm) stay in sync.

        private bool CanSendClientAction()
        {
            // Only patient clients in relay mode can forward actions to the host.
            return _isOnline && !_isHost && _relayMode;
        }

        private void SendToHostViaRelay(NetMsgType type, byte[] payload)
        {
            if (!CanSendClientAction()) return;
            try
            {
                var packet = NetSerializer.Pack(type, payload);
                SendToRelay(packet);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Network] SendToHost ({type}) failed: {ex.Message}");
            }
        }

        public void SendClientHeightmapPaint(int startX, int startZ, int width, int depth, float[] regionData)
        {
            if (!CanSendClientAction()) return;
            byte[] bytes = new byte[regionData.Length * 4];
            Buffer.BlockCopy(regionData, 0, bytes, 0, bytes.Length);
            var payload = NetSerializer.WriteHeightmapRegion(startX, startZ, width, depth, bytes);
            SendToHostViaRelay(NetMsgType.ClientHeightmapPaint, payload);
        }

        public void SendClientSplatPaint(int startX, int startY, int width, int height, byte[] pixels)
        {
            if (!CanSendClientAction()) return;
            var payload = NetSerializer.WriteSplatmapRegion(startX, startY, width, height, pixels);
            SendToHostViaRelay(NetMsgType.ClientSplatPaint, payload);
        }

        public void SendClientSpawnRequest(string objectId, Vector3 pos, Quaternion rot, float scale)
        {
            if (!CanSendClientAction()) return;
            // NetId=0 — host allocates the real one
            var payload = NetSerializer.WriteSpawnObject(0u, objectId, pos, rot, scale);
            SendToHostViaRelay(NetMsgType.ClientSpawnRequest, payload);
        }

        public void SendClientMoveObject(uint netId, Vector3 pos, Quaternion rot, float scale)
        {
            if (!CanSendClientAction() || netId == 0u) return;
            var payload = NetSerializer.WriteMoveObject(netId, pos, rot, scale);
            SendToHostViaRelay(NetMsgType.ClientMoveObject, payload);
        }

        public void SendClientRemoveObject(uint netId)
        {
            if (!CanSendClientAction() || netId == 0u) return;
            var payload = NetSerializer.WriteRemoveObject(netId);
            SendToHostViaRelay(NetMsgType.ClientRemoveObject, payload);
        }

        public void SendClientColorChange(string materialName, Color color)
        {
            if (!CanSendClientAction()) return;
            var payload = NetSerializer.WriteColorSync(materialName, color);
            SendToHostViaRelay(NetMsgType.ClientColorChange, payload);
        }

        /// <summary>
        /// Patient-side: broadcast a live cursor world position so the therapist
        /// (and observers) can see "patient is acting here". Throttled internally
        /// to <see cref="PointerHoverSendIntervalSec"/>, so callers may invoke it
        /// every frame without flooding the network. <paramref name="kind"/>
        /// describes what the patient is doing (sculpt / paint / place). Place
        /// events bypass the throttle since they're rare and important.
        /// </summary>
        public void SendClientPointerHover(Vector3 worldPos, PatientPointerKind kind = PatientPointerKind.Sculpt)
        {
            if (!CanSendClientAction()) return;
            float now = Time.unscaledTime;
            if (kind != PatientPointerKind.Place
                && _lastPointerHoverSentAt > 0f
                && now - _lastPointerHoverSentAt < PointerHoverSendIntervalSec)
                return;
            _lastPointerHoverSentAt = now;
            var payload = NetSerializer.WritePointerHover(worldPos, kind);
            SendToHostViaRelay(NetMsgType.PointerHover, payload);
            // Also fire locally so the patient sees the same indicator as the
            // therapist — reinforces the shared-workspace feel.
            OnPatientPointerHover?.Invoke(worldPos, kind);
        }

        // ──────────────────────────────────────────────
        // Host: apply patient's action locally + re-broadcast to everyone via relay
        // (Re-broadcasting via the relay also reaches the patient — the apply is
        //  idempotent for height/splat/color and a no-op echo for move/remove/spawn-by-id.)
        // ──────────────────────────────────────────────

        private void ApplyAndRebroadcastHeightmap(byte[] payload)
        {
            NetSerializer.ReadHeightmapRegion(payload, out int sx, out int sz,
                out int w, out int d, out byte[] h);
            if (SandMesh.Instance != null)
            {
                float[] regionData = new float[h.Length / 4];
                Buffer.BlockCopy(h, 0, regionData, 0, h.Length);
                Sandplay.Sand.SandSyncManager.SuppressNetworkSync = true;
                SandMesh.Instance.ApplyHeightmapRegion(sx, sz, w, d, regionData);
                Sandplay.Sand.SandSyncManager.SuppressNetworkSync = false;
            }
            // Rebroadcast as authoritative HeightmapRegion to all peers
            SendToRelay(NetSerializer.Pack(NetMsgType.HeightmapRegion, payload));
        }

        private void ApplyAndRebroadcastSplat(byte[] payload)
        {
            NetSerializer.ReadSplatmapRegion(payload, out int spx, out int spy,
                out int spw, out int sph, out byte[] pixels);
            Sandplay.Sand.SandSyncManager.SuppressNetworkSync = true;
            SandMaterialController.Instance?.ApplySplatmapRegion(spx, spy, spw, sph, pixels);
            Sandplay.Sand.SandSyncManager.SuppressNetworkSync = false;
            SendToRelay(NetSerializer.Pack(NetMsgType.SplatmapRegion, payload));
        }

        private void ApplyAndRebroadcastSpawn(byte[] payload)
        {
            NetSerializer.ReadSpawnObject(payload, out uint _, out string oid,
                out Vector3 pos, out Quaternion rot, out float scale);
            scale = SanitizeNetworkScale(scale);
            // Allocate authoritative NetId on host
            uint newId = NextNetId();
            // Spawn locally on host
            SpawnObjectLocally(oid, newId, pos, rot, scale);
            // Broadcast to all clients (including the originating patient) so they
            // can match-or-spawn with this NetId.
            var outPayload = NetSerializer.WriteSpawnObject(newId, oid, pos, rot, scale);
            SendToRelay(NetSerializer.Pack(NetMsgType.SpawnObject, outPayload));
        }

        private void ApplyAndRebroadcastMove(byte[] payload)
        {
            NetSerializer.ReadMoveObject(payload, out uint nid,
                out Vector3 pos, out Quaternion rot, out float scale);
            scale = SanitizeNetworkScale(scale);
            var obj = GetNetworkObject(nid);
            if (obj != null)
            {
                obj.transform.position = pos;
                obj.transform.rotation = rot;
                Vector3 tplScale = Vector3.one;
                if (obj.ObjectData != null && obj.ObjectData.Prefab != null)
                    tplScale = obj.ObjectData.Prefab.transform.localScale;
                else if (obj.NetworkItem != null && obj.NetworkItem.LoadedPrefab != null)
                    tplScale = obj.NetworkItem.LoadedPrefab.transform.localScale;
                obj.transform.localScale = tplScale * scale;
            }
            SendToRelay(NetSerializer.Pack(NetMsgType.MoveObject, payload));
        }

        private void ApplyAndRebroadcastRemove(byte[] payload)
        {
            uint nid = NetSerializer.ReadRemoveObject(payload);
            var obj = GetNetworkObject(nid);
            if (obj != null)
            {
                UnregisterNetworkObject(nid);
                Destroy(obj.gameObject);
            }
            SendToRelay(NetSerializer.Pack(NetMsgType.RemoveObject, payload));
        }

        private void ApplyAndRebroadcastColor(byte[] payload)
        {
            NetSerializer.ReadColorSync(payload, out string mat, out Color col);
            ApplyColorLocally(mat, col);
            SendToRelay(NetSerializer.Pack(NetMsgType.ColorSync, payload));
        }

        // ──────────────────────────────────────────────
        // Full state for late join
        // ──────────────────────────────────────────────

        private void SendFullStateToClient(TcpClient client)
        {
            var sandMesh = SandMesh.Instance;
            byte[] heightmapBytes = Array.Empty<byte>();
            if (sandMesh != null)
            {
                heightmapBytes = new byte[sandMesh.Heightmap.Length * 4];
                Buffer.BlockCopy(sandMesh.Heightmap, 0, heightmapBytes, 0, heightmapBytes.Length);
            }

            var placedObjects = BuildPlacedObjectList();

            var colors = new List<ColorSyncData>();
            foreach (var kvp in _materialColors)
                colors.Add(new ColorSyncData { MaterialName = kvp.Key, Color = kvp.Value });

            var config = GameManager.Instance?.Config;
            float bw = config != null ? config.SandboxWidth : 10f;
            float bd = config != null ? config.SandboxDepth : 10f;
            int res = sandMesh != null ? sandMesh.Resolution : (config != null ? config.HeightmapResolution : 128);

            var payload = NetSerializer.WriteFullState(bw, bd, res, heightmapBytes,
                placedObjects.ToArray(), colors.ToArray());
            SendToClient(client, NetSerializer.Pack(NetMsgType.FullState, payload));

            // Send the current splatmap so the joining client sees any painted areas
            var smc = SandMaterialController.Instance;
            if (smc != null)
            {
                int sr = SandMaterialController.SplatResolution;
                byte[] splatPixels = smc.GetSplatmapRegionBytes(0, 0, sr, sr);
                var splatPayload = NetSerializer.WriteSplatmapRegion(0, 0, sr, sr, splatPixels);
                SendToClient(client, NetSerializer.Pack(NetMsgType.SplatmapRegion, splatPayload));
            }
        }

        // ──────────────────────────────────────────────
        // Low-level send/receive helpers
        // ──────────────────────────────────────────────

        private void BroadcastToClients(byte[] packet)
        {
            // In relay mode, send once to relay — it fans out to all clients
            if (_relayMode)
            {
                SendToRelay(packet);
                return;
            }

            lock (_clientsLock)
            {
                for (int i = _clients.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        SendToClient(_clients[i], packet);
                    }
                    catch
                    {
                        // Remove dead clients
                        try { _clients[i].Close(); } catch { }
                        _clients.RemoveAt(i);
                    }
                }
            }
        }

        private void SendToClient(TcpClient client, byte[] packet)
        {
            if (client == null || !client.Connected) return;
            var stream = client.GetStream();
            stream.Write(packet, 0, packet.Length);
            stream.Flush();
        }

        private static bool ReadExact(NetworkStream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0) return false;
                offset += read;
            }
            return true;
        }

        // ──────────────────────────────────────────────
        // Local application helpers
        // ──────────────────────────────────────────────

        private void SpawnObjectLocally(string objectId, uint netId, Vector3 pos, Quaternion rot, float scale)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                Debug.LogWarning("[Network] Ignoring spawn with missing object id.");
                return;
            }

            scale = SanitizeNetworkScale(scale);

            var placer = FindAnyObjectByType<ObjectPlacer>();
            if (placer == null) return;

            // ── Patient match-or-spawn: a Patient client may have placed this object
            //    locally already (with NetworkId=0) before the host echoed it back with
            //    an authoritative NetId. Match by (objectId + nearby position) and just
            //    bind the NetId to avoid creating a duplicate.
            if (!_isHost)
            {
                var existing = FindLocalPendingPlacement(placer, objectId, pos);
                if (existing != null)
                {
                    existing.NetworkId = netId;
                    RegisterNetworkObject(netId, existing);
                    return;
                }
            }

            // 1. Try local ScriptableObject catalog first (matched by ObjectId or DisplayName)
            if (placer.Catalog != null)
            {
                SandplayObject catalogObj = null;
                foreach (var obj in placer.Catalog.Objects)
                {
                    if (obj.ObjectId == objectId || obj.DisplayName == objectId)
                    { catalogObj = obj; break; }
                }
                if (catalogObj != null && catalogObj.Prefab != null)
                {
                    Sandplay.Objects.ObjectSyncManager.SuppressNetworkSync = true;
                    var placed = placer.PlaceObject(catalogObj, pos, rot, scale, skipOffset: true);
                    Sandplay.Objects.ObjectSyncManager.SuppressNetworkSync = false;
                    if (placed != null)
                    {
                        placed.NetworkId = netId;
                        RegisterNetworkObject(netId, placed);
                    }
                    return;
                }
            }

            // 2. Try API catalog (objectId is the UUID from the server)
            if (NetworkCatalogRegistry.TryGet(objectId, out var netItem))
            {
                StartCoroutine(SpawnNetworkItemCoroutine(netItem, netId, pos, rot, scale));
                return;
            }

            // Registry may not be loaded yet (client received FullState before catalog HTTP response).
            // Queue a retry that waits up to 20 s for the registry to become ready.
            if (!NetworkCatalogRegistry.IsLoaded)
            {
                StartCoroutine(SpawnObjectWhenRegistryReady(objectId, netId, pos, rot, scale));
                return;
            }

            Debug.LogWarning($"[Network] Could not find catalog object: {objectId}");
        }

        /// <summary>
        /// Patient-side: find a recently placed local object with NetworkId==0 matching
        /// the given objectId near the given position. Used to bind host-allocated NetIds
        /// to optimistically-placed client objects.
        /// </summary>
        private static PlacedObject FindLocalPendingPlacement(ObjectPlacer placer, string objectId, Vector3 pos)
        {
            const float matchRadiusSq = 0.05f * 0.05f; // 5 cm tolerance
            PlacedObject best = null;
            float bestDistSq = matchRadiusSq;
            foreach (var p in placer.PlacedObjects)
            {
                if (p == null || p.NetworkId != 0u) continue;
                string pid = p.ObjectData != null ? p.ObjectData.DisplayName
                           : p.NetworkItem != null ? p.NetworkItem.id
                           : "";
                if (pid != objectId) continue;
                float distSq = (p.transform.position - pos).sqrMagnitude;
                if (distSq <= bestDistSq)
                {
                    best = p;
                    bestDistSq = distSq;
                }
            }
            return best;
        }

        private System.Collections.IEnumerator SpawnObjectWhenRegistryReady(
            string objectId, uint netId, Vector3 pos, Quaternion rot, float scale)
        {
            float elapsed = 0f;
            while (!NetworkCatalogRegistry.IsLoaded && elapsed < 20f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            // Re-enter the normal spawn path now that the registry should be populated.
            SpawnObjectLocally(objectId, netId, pos, rot, scale);
        }

        private System.Collections.IEnumerator SpawnNetworkItemCoroutine(
            NetworkCatalogItem item, uint netId, Vector3 pos, Quaternion rot, float scale)
        {
            scale = SanitizeNetworkScale(scale);

            // Wait until the GLB model is downloaded/cached
            yield return StartCoroutine(NetworkCatalogRegistry.EnsureLoaded(this, item));

            var placer = FindAnyObjectByType<ObjectPlacer>();
            if (placer == null || item.LoadedPrefab == null) yield break;

            Sandplay.Objects.ObjectSyncManager.SuppressNetworkSync = true;
            var placed = placer.PlaceNetworkObject(item, pos, rot, scale);
            Sandplay.Objects.ObjectSyncManager.SuppressNetworkSync = false;
            if (placed != null)
            {
                placed.NetworkId = netId;
                RegisterNetworkObject(netId, placed);
            }
        }

        private void ApplyColorLocally(string materialName, Color color)
        {
            if (string.IsNullOrEmpty(materialName)) return;

            _materialColors[materialName] = color;
            var bootstrapper = FindAnyObjectByType<SceneBootstrapper>();
            if (bootstrapper != null)
                bootstrapper.ApplyNetworkColor(materialName, color);
        }

        private static bool IsValidNetworkBoard(float width, float depth, int resolution)
        {
            return resolution >= 2
                && resolution <= MaxNetworkResolution
                && width > 0f
                && depth > 0f
                && width <= MaxNetworkBoardSize
                && depth <= MaxNetworkBoardSize
                && !float.IsNaN(width)
                && !float.IsNaN(depth)
                && !float.IsInfinity(width)
                && !float.IsInfinity(depth);
        }

        private static float SanitizeNetworkScale(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0.0001f)
                return 1f;
            return Mathf.Clamp(scale, 0.01f, MaxNetworkObjectScale);
        }

        private static float GetRelativeScale(PlacedObject placed)
        {
            if (placed == null) return 1f;
            float templateX = 1f;
            if (placed.ObjectData != null && placed.ObjectData.Prefab != null)
                templateX = placed.ObjectData.Prefab.transform.localScale.x;
            else if (placed.NetworkItem != null && placed.NetworkItem.LoadedPrefab != null)
                templateX = placed.NetworkItem.LoadedPrefab.transform.localScale.x;
            return templateX > 0.0001f ? placed.transform.localScale.x / templateX : 1f;
        }

        /// <summary>
        /// Apply a full undo/redo state snapshot on the client.
        /// Replaces heightmap and reconciles placed objects.
        /// </summary>
        private void ApplyUndoRedoState(byte[] heightmapBytes, SpawnObjectData[] objects)
        {
            // Apply heightmap
            if (heightmapBytes != null && heightmapBytes.Length > 0)
            {
                float[] heightmap = new float[heightmapBytes.Length / 4];
                Buffer.BlockCopy(heightmapBytes, 0, heightmap, 0, heightmapBytes.Length);
                Sandplay.Sand.SandSyncManager.SuppressNetworkSync = true;
                SandMesh.Instance?.SetHeightmap(heightmap);
                Sandplay.Sand.SandSyncManager.SuppressNetworkSync = false;
            }

            // Reconcile objects: remove ones not in new state, spawn missing ones
            var newIds = new HashSet<uint>();
            if (objects != null)
                foreach (var o in objects)
                    newIds.Add(o.NetId);

            // Remove objects no longer present
            var toRemove = new List<uint>();
            foreach (var kvp in _networkObjects)
            {
                if (!newIds.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove)
            {
                var obj = GetNetworkObject(id);
                if (obj != null) Destroy(obj.gameObject);
                UnregisterNetworkObject(id);
            }

            // Add/update objects from new state
            if (objects != null)
            {
                foreach (var o in objects)
                {
                    var existing = GetNetworkObject(o.NetId);
                    if (existing != null)
                    {
                        existing.transform.position = o.Position;
                        existing.transform.rotation = o.Rotation;
                        existing.transform.localScale = (existing.ObjectData != null && existing.ObjectData.Prefab != null)
                            ? existing.ObjectData.Prefab.transform.localScale * o.Scale
                            : Vector3.one * o.Scale;
                    }
                    else
                    {
                        SpawnObjectLocally(o.ObjectId, o.NetId, o.Position, o.Rotation, o.Scale);
                    }
                }
            }

            Debug.Log("[Network] Undo/redo state applied");
        }

        /// <summary>
        /// Apply object selection highlight on the client.
        /// netId = 0 means deselect all.
        /// </summary>
        private void ApplyObjectSelection(uint netId)
        {
            var placer = FindAnyObjectByType<ObjectPlacer>();
            if (placer == null) return;

            if (netId == 0)
            {
                // Deselect
                placer.DeselectNetworkSelection();
            }
            else
            {
                // Select
                var obj = GetNetworkObject(netId);
                if (obj != null)
                {
                    placer.SelectNetworkObject(obj);
                }
            }
        }

        // ──────────────────────────────────────────────
        // Reconnection
        // ──────────────────────────────────────────────

        private void StartReconnection(string address)
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            _reconnectThread = new Thread(() => ReconnectLoop(address)) { IsBackground = true };
            _reconnectThread.Start();
        }

        public void StopReconnection()
        {
            _isReconnecting = false;
        }

        private void ReconnectLoop(string address)
        {
            for (int attempt = 1; attempt <= MaxReconnectAttempts && _isReconnecting; attempt++)
            {
                int a = attempt;
                EnqueueMain(() =>
                {
                    OnReconnectAttempt?.Invoke(a);
                    Debug.Log($"[Network] Reconnect attempt {a}/{MaxReconnectAttempts}...");
                });

                try
                {
                    var testClient = new TcpClient();
                    var result = testClient.BeginConnect(address, Port, null, null);
                    bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(ReconnectIntervalSec));

                    if (connected && testClient.Connected)
                    {
                        testClient.EndConnect(result);
                        testClient.NoDelay = true;

                        EnqueueMain(() =>
                        {
                            _isReconnecting = false;
                            _clientSocket = testClient;
                            _isOnline = true;
                            _isHost = false;

                            _clientReceiveThread = new Thread(ClientReceiveLoop) { IsBackground = true };
                            _clientReceiveThread.Start();

                            OnConnected?.Invoke();
                            EventBus.Publish(new NetworkConnectedEvent());
                            SendRoleRequest(_requestedRole);
                            Debug.Log("[Network] Reconnected successfully!");
                        });
                        return;
                    }

                    testClient.Close();
                }
                catch { /* retry */ }

                // Wait before next attempt
                Thread.Sleep((int)(ReconnectIntervalSec * 1000));
            }

            _isReconnecting = false;
            EnqueueMain(() =>
            {
                OnReconnectGaveUp?.Invoke();
                Debug.Log("[Network] Gave up reconnecting");
            });
        }

        // ──────────────────────────────────────────────
        // Cleanup
        // ──────────────────────────────────────────────

        private void StopServer()
        {
            try { _server?.Stop(); } catch { }
            _server = null;

            lock (_clientsLock)
            {
                foreach (var c in _clients)
                    try { c.Close(); } catch { }
                _clients.Clear();
            }
        }

        private void StopClient()
        {
            try { _clientSocket?.Close(); } catch { }
            _clientSocket = null;
        }

        private void StopRelay()
        {
            try { _relaySocket?.Close(); } catch { }
            _relaySocket = null;
            _relayStream = null;
            _roomCode = null;
        }

        private void Cleanup()
        {
            _isOnline = false;
            _isHost = false;
            _connectedClients = 0;
            _nextNetId = 1;
            _networkObjects.Clear();
            _materialColors.Clear();
            _therapistMode = false;
            _patientAssigned = false;
            if (GameManager.Instance != null)
                GameManager.Instance.NetworkRole = PlayerRole.Patient;
        }

        public void Disconnect()
        {
            StopReconnection();
            if (!_isOnline) return;

            if (_relayMode)
                StopRelay();
            else if (_isHost)
                StopServer();
            else
                StopClient();

            Cleanup();
            _relayMode = false;
            Debug.Log("[Network] Disconnected");
        }

        // ──────────────────────────────────────────────
        // Relay reconnection
        // ──────────────────────────────────────────────

        private void StartRelayReconnection(string relayAddress, string roomCode)
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            _reconnectThread = new Thread(() => RelayReconnectLoop(relayAddress, roomCode)) { IsBackground = true };
            _reconnectThread.Start();
        }

        private void RelayReconnectLoop(string relayAddress, string roomCode)
        {
            for (int attempt = 1; attempt <= MaxReconnectAttempts && _isReconnecting; attempt++)
            {
                int a = attempt;
                EnqueueMain(() =>
                {
                    OnReconnectAttempt?.Invoke(a);
                    Debug.Log($"[Network] Relay reconnect attempt {a}/{MaxReconnectAttempts}...");
                });

                try
                {
                    var socket = new TcpClient();
                    var result = socket.BeginConnect(relayAddress, Port, null, null);
                    bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(ReconnectIntervalSec));

                    if (connected && socket.Connected)
                    {
                        socket.EndConnect(result);
                        socket.NoDelay = true;
                        var stream = socket.GetStream();

                        // Send JoinRoom
                        var joinPayload = NetSerializer.WriteJoinRoom(roomCode);
                        var joinPacket = NetSerializer.Pack(NetMsgType.JoinRoom, joinPayload);
                        stream.Write(joinPacket, 0, joinPacket.Length);
                        stream.Flush();

                        // Read JoinResult
                        byte[] lenBuf = new byte[4];
                        if (!ReadExact(stream, lenBuf, 4)) { socket.Close(); continue; }
                        int msgLen = BitConverter.ToInt32(lenBuf, 0);
                        byte[] body = new byte[msgLen];
                        if (!ReadExact(stream, body, msgLen)) { socket.Close(); continue; }

                        NetMsgType type = (NetMsgType)body[0];
                        byte[] payload = new byte[msgLen - 1];
                        Buffer.BlockCopy(body, 1, payload, 0, payload.Length);

                        if (type == NetMsgType.JoinResult)
                        {
                            NetSerializer.ReadJoinResult(payload, out bool success, out string reason);
                            if (success)
                            {
                                EnqueueMain(() =>
                                {
                                    _isReconnecting = false;
                                    _relaySocket = socket;
                                    _relayStream = stream;
                                    _clientSocket = socket;
                                    _roomCode = roomCode;
                                    _relayMode = true;
                                    _isOnline = true;
                                    _isHost = false;

                                    _clientReceiveThread = new Thread(() => RelayClientReceiveLoop(socket, stream)) { IsBackground = true };
                                    _clientReceiveThread.Start();

                                    OnConnected?.Invoke();
                                    EventBus.Publish(new NetworkConnectedEvent());
                                    SendRoleRequest(_requestedRole);
                                    Debug.Log($"[Network] Relay reconnected to room {roomCode}!");
                                });
                                return;
                            }
                        }
                        socket.Close();
                    }
                    else
                    {
                        socket.Close();
                    }
                }
                catch { /* retry */ }

                Thread.Sleep((int)(ReconnectIntervalSec * 1000));
            }

            _isReconnecting = false;
            EnqueueMain(() =>
            {
                OnReconnectGaveUp?.Invoke();
                Debug.Log("[Network] Gave up relay reconnecting");
            });
        }

        private void OnDestroy()
        {
            if (_isOnline) Disconnect();
            if (Instance == this) Instance = null;
        }
    }
}
