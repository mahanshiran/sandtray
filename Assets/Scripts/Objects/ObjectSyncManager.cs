using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Objects
{
    /// <summary>
    /// Syncs object spawn/move/remove from host to clients.
    /// Subscribes to EventBus events and routes them through NetworkBootstrapper.
    /// </summary>
    public class ObjectSyncManager : MonoBehaviour
    {
        /// <summary>
        /// When true, suppress network sends (used when network layer is spawning objects
        /// to avoid duplicate broadcasts).
        /// </summary>
        public static bool SuppressNetworkSync { get; set; }

        private void OnEnable()
        {
            EventBus.Subscribe<ObjectPlacedEvent>(OnObjectPlaced);
            EventBus.Subscribe<ObjectTransformedEvent>(OnObjectTransformed);
            EventBus.Subscribe<ObjectRemovedEvent>(OnObjectRemoved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ObjectPlacedEvent>(OnObjectPlaced);
            EventBus.Unsubscribe<ObjectTransformedEvent>(OnObjectTransformed);
            EventBus.Unsubscribe<ObjectRemovedEvent>(OnObjectRemoved);
        }

        private static bool IsRecordingActive()
        {
            var rec = SessionRecorder.Instance;
            return rec != null && rec.IsRecording;
        }

        private void OnObjectPlaced(ObjectPlacedEvent evt)
        {
            if (SuppressNetworkSync) return;

            var net = NetworkBootstrapper.Instance;
            if (net == null) return;
            // Allow this path to run when offline-but-recording so the SessionRecorder
            // can capture local solo-play spawns. NetworkBootstrapper.SendSpawnObject
            // mirrors the payload to the recorder before bailing on the network send.
            bool offlineRecording = !net.IsOnline && IsRecordingActive();
            if (!net.IsOnline && !offlineRecording) return;
            if (evt.PlacedObject == null) return;

            // Determine the id to send. Local catalog objects use DisplayName; API catalog
            // objects use the NetworkItem UUID (so the client can resolve them via
            // NetworkCatalogRegistry).
            string objectId = evt.PlacedObject.ObjectData != null
                ? evt.PlacedObject.ObjectData.DisplayName
                : evt.PlacedObject.NetworkItem != null
                    ? evt.PlacedObject.NetworkItem.id
                    : "";

            // Compute scale relative to whichever template was used to spawn this object.
            float templateScaleX = GetTemplateScaleX(evt.PlacedObject);
            float relativeScale = templateScaleX > 0.0001f
                ? evt.PlacedObject.transform.localScale.x / templateScaleX : 1f;

            if (net.IsHost)
            {
                // Host: assign NetId, register, and broadcast SpawnObject to all clients.
                if (evt.PlacedObject.NetworkId == 0)
                    evt.PlacedObject.NetworkId = net.NextNetId();
                net.RegisterNetworkObject(evt.PlacedObject.NetworkId, evt.PlacedObject);
                net.SendSpawnObject(
                    evt.PlacedObject.NetworkId,
                    objectId,
                    evt.PlacedObject.transform.position,
                    evt.PlacedObject.transform.rotation,
                    relativeScale
                );
            }
            else if (net.IsRelayMode && GameManager.Instance != null && GameManager.Instance.IsPatient)
            {
                // Patient client: keep the local object (NetworkId stays 0 until host echoes
                // back a SpawnObject — see NetworkBootstrapper.SpawnObjectLocally match logic).
                net.SendClientSpawnRequest(
                    objectId,
                    evt.PlacedObject.transform.position,
                    evt.PlacedObject.transform.rotation,
                    relativeScale
                );
            }
            else if (offlineRecording)
            {
                // Solo offline + recording: synthesize a local NetId so subsequent
                // move/remove events can correlate with this spawn during replay.
                if (evt.PlacedObject.NetworkId == 0)
                    evt.PlacedObject.NetworkId = net.NextNetId();
                net.SendSpawnObject(
                    evt.PlacedObject.NetworkId,
                    objectId,
                    evt.PlacedObject.transform.position,
                    evt.PlacedObject.transform.rotation,
                    relativeScale
                );
            }
        }

        private void OnObjectTransformed(ObjectTransformedEvent evt)
        {
            var net = NetworkBootstrapper.Instance;
            if (net == null) return;
            bool offlineRecording = !net.IsOnline && IsRecordingActive();
            if (!net.IsOnline && !offlineRecording) return;
            if (evt.PlacedObject == null || evt.PlacedObject.NetworkId == 0) return;

            float templateScaleX2 = GetTemplateScaleX(evt.PlacedObject);
            float relScale = templateScaleX2 > 0.0001f
                ? evt.PlacedObject.transform.localScale.x / templateScaleX2 : 1f;

            if (net.IsHost)
            {
                net.SendMoveObject(
                    evt.PlacedObject.NetworkId,
                    evt.PlacedObject.transform.position,
                    evt.PlacedObject.transform.rotation,
                    relScale
                );
            }
            else if (net.IsRelayMode && GameManager.Instance != null && GameManager.Instance.IsPatient)
            {
                net.SendClientMoveObject(
                    evt.PlacedObject.NetworkId,
                    evt.PlacedObject.transform.position,
                    evt.PlacedObject.transform.rotation,
                    relScale
                );
            }
            else if (offlineRecording)
            {
                net.SendMoveObject(
                    evt.PlacedObject.NetworkId,
                    evt.PlacedObject.transform.position,
                    evt.PlacedObject.transform.rotation,
                    relScale
                );
            }
        }

        /// <summary>
        /// Returns the X scale of whichever template (local prefab or downloaded GLB)
        /// was used to spawn the object. Used to derive the relative scale sent over
        /// the network so clients reconstruct the size correctly.
        /// </summary>
        private static float GetTemplateScaleX(PlacedObject placed)
        {
            if (placed.ObjectData != null && placed.ObjectData.Prefab != null)
                return placed.ObjectData.Prefab.transform.localScale.x;
            if (placed.NetworkItem != null && placed.NetworkItem.LoadedPrefab != null)
                return placed.NetworkItem.LoadedPrefab.transform.localScale.x;
            return 1f;
        }

        private void OnObjectRemoved(ObjectRemovedEvent evt)
        {
            var net = NetworkBootstrapper.Instance;
            if (net == null) return;
            bool offlineRecording = !net.IsOnline && IsRecordingActive();
            if (!net.IsOnline && !offlineRecording) return;
            if (evt.PlacedObject == null || evt.PlacedObject.NetworkId == 0) return;

            if (net.IsHost)
            {
                net.SendRemoveObject(evt.PlacedObject.NetworkId);
                net.UnregisterNetworkObject(evt.PlacedObject.NetworkId);
            }
            else if (net.IsRelayMode && GameManager.Instance != null && GameManager.Instance.IsPatient)
            {
                net.SendClientRemoveObject(evt.PlacedObject.NetworkId);
            }
            else if (offlineRecording)
            {
                net.SendRemoveObject(evt.PlacedObject.NetworkId);
            }
        }
    }
}
