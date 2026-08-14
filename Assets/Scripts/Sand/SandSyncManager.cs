using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Sand
{
    /// <summary>
    /// Syncs sand heightmap changes from host to clients.
    /// Subscribes to SandModifiedEvent, extracts the dirty region, and sends it over the network.
    /// Throttles to avoid flooding the network with per-frame updates.
    /// </summary>
    public class SandSyncManager : MonoBehaviour
    {
        /// <summary>
        /// When true, suppress network sends (used when network layer is applying changes
        /// to avoid duplicate broadcasts).
        /// </summary>
        public static bool SuppressNetworkSync { get; set; }

        private SandMesh _sandMesh;
        private float _sendInterval = 0.1f; // 10 updates per second max
        private float _lastSendTime;

        // Accumulated dirty region (heightmap grid space)
        private bool _hasPending;
        private int _pendingMinX, _pendingMaxX, _pendingMinZ, _pendingMaxZ;

        // Accumulated dirty region (splatmap pixel space 0–511)
        private bool _hasPendingPaint;
        private int _paintMinX, _paintMaxX, _paintMinY, _paintMaxY;

        public void Initialize(SandMesh sandMesh)
        {
            _sandMesh = sandMesh;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SandModifiedEvent>(OnSandModified);
            EventBus.Subscribe<SplatPaintedEvent>(OnSplatPainted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SandModifiedEvent>(OnSandModified);
            EventBus.Unsubscribe<SplatPaintedEvent>(OnSplatPainted);
        }

        private void OnSandModified(SandModifiedEvent evt)
        {
            if (SuppressNetworkSync) return;
            // Host always sends; in Therapist mode the Patient client also sends.
            if (!ShouldSyncSand()) return;

            // Accumulate dirty region
            int minX = Mathf.Max(0, evt.CenterX - evt.Radius);
            int maxX = Mathf.Min(_sandMesh.Resolution - 1, evt.CenterX + evt.Radius);
            int minZ = Mathf.Max(0, evt.CenterZ - evt.Radius);
            int maxZ = Mathf.Min(_sandMesh.Resolution - 1, evt.CenterZ + evt.Radius);

            if (!_hasPending)
            {
                _pendingMinX = minX;
                _pendingMaxX = maxX;
                _pendingMinZ = minZ;
                _pendingMaxZ = maxZ;
                _hasPending = true;
            }
            else
            {
                _pendingMinX = Mathf.Min(_pendingMinX, minX);
                _pendingMaxX = Mathf.Max(_pendingMaxX, maxX);
                _pendingMinZ = Mathf.Min(_pendingMinZ, minZ);
                _pendingMaxZ = Mathf.Max(_pendingMaxZ, maxZ);
            }
        }

        private void OnSplatPainted(SplatPaintedEvent evt)
        {
            if (SuppressNetworkSync) return;
            if (!ShouldSyncSand()) return;

            int maxX = evt.MinX + evt.Width - 1;
            int maxY = evt.MinY + evt.Height - 1;
            if (!_hasPendingPaint)
            {
                _paintMinX = evt.MinX; _paintMaxX = maxX;
                _paintMinY = evt.MinY; _paintMaxY = maxY;
                _hasPendingPaint = true;
            }
            else
            {
                _paintMinX = Mathf.Min(_paintMinX, evt.MinX);
                _paintMaxX = Mathf.Max(_paintMaxX, maxX);
                _paintMinY = Mathf.Min(_paintMinY, evt.MinY);
                _paintMaxY = Mathf.Max(_paintMaxY, maxY);
            }
        }

        private void Update()
        {
            if (!_hasPending && !_hasPendingPaint) return;
            if (Time.time - _lastSendTime < _sendInterval) return;

            var net = NetworkBootstrapper.Instance;
            if (net == null) return;
            bool isHost = net.IsHost;
            bool isPatientClient = !isHost && net.IsRelayMode
                && GameManager.Instance != null && GameManager.Instance.IsPatient;
            // Offline-but-recording: behave like a local host so the recorder captures
            // sand/paint deltas via NetworkBootstrapper.SendXxx (which records-then-skips
            // the actual transmit when _isOnline is false).
            bool offlineRecording = !net.IsOnline && IsRecordingActive();
            bool sendAsHost = isHost || offlineRecording;

            if (_hasPending && _sandMesh != null)
            {
                int width = _pendingMaxX - _pendingMinX + 1;
                int depth = _pendingMaxZ - _pendingMinZ + 1;
                float[] regionData = _sandMesh.ExtractHeightmapRegion(_pendingMinX, _pendingMinZ, width, depth);
                if (sendAsHost)
                    net.SendHeightmapRegion(_pendingMinX, _pendingMinZ, width, depth, regionData);
                else if (isPatientClient)
                    net.SendClientHeightmapPaint(_pendingMinX, _pendingMinZ, width, depth, regionData);
                _hasPending = false;
            }

            if (_hasPendingPaint)
            {
                var smc = SandMaterialController.Instance;
                if (smc != null)
                {
                    int w = _paintMaxX - _paintMinX + 1;
                    int h = _paintMaxY - _paintMinY + 1;
                    byte[] pixels = smc.GetSplatmapRegionBytes(_paintMinX, _paintMinY, w, h);
                    if (sendAsHost)
                        net.SendSplatmapRegion(_paintMinX, _paintMinY, w, h, pixels);
                    else if (isPatientClient)
                        net.SendClientSplatPaint(_paintMinX, _paintMinY, w, h, pixels);
                    _hasPendingPaint = false;
                }
            }

            _lastSendTime = Time.time;
        }

        /// <summary>
        /// True if local user is allowed to push sand/splat changes onto the network:
        /// either the authoritative host, or a Patient client in Therapist mode.
        /// Also returns true when a SessionRecorder is active so OFFLINE solo play
        /// still pumps deltas through NetworkBootstrapper (which records them and
        /// no-ops the actual transmit when not online).
        /// </summary>
        private static bool ShouldSyncSand()
        {
            if (IsRecordingActive()) return true;
            var net = NetworkBootstrapper.Instance;
            if (net == null || !net.IsOnline) return false;
            if (net.IsHost) return true;
            return net.IsRelayMode
                && GameManager.Instance != null
                && GameManager.Instance.IsPatient;
        }

        private static bool IsRecordingActive()
        {
            var rec = SessionRecorder.Instance;
            return rec != null && rec.IsRecording;
        }
    }
}
