using UnityEngine;
using Sandplay.Objects;

namespace Sandplay.Core
{
    public partial class NetworkBootstrapper
    {
        // Layout is encoded in a bounded, versioned object ID, so every existing
        // spawn/snapshot/recording carries a complete state without mesh transfers.
        public void SendLinearObjectState(PlacedObject placed)
        {
            if (placed == null || placed.GetComponent<SmartLinearObject>() == null) return;
            if (!_isOnline && !(SessionRecorder.Instance?.IsRecording ?? false)) return;
            if (placed.NetworkId == 0)
            {
                if (_isOnline) return; // Wait for the host to allocate the placement ID.
                placed.NetworkId = NextNetId();
                RegisterNetworkObject(placed.NetworkId, placed);
            }
            var data=placed.Serialize();
            if (_isHost || !_isOnline)
                SendSpawnObject(placed.NetworkId,data.ObjectId,placed.transform.position,placed.transform.rotation,data.Scale);
            else if (_relayMode && GameManager.Instance != null && GameManager.Instance.IsPatient)
                SendToHostViaRelay(NetMsgType.ClientSpawnRequest, NetSerializer.WriteSpawnObject(
                    placed.NetworkId,data.ObjectId,placed.transform.position,placed.transform.rotation,data.Scale));
        }
        private bool ApplyLinearStateToExisting(PlacedObject placed,string id,Vector3 pos,Quaternion rot,float scale)
        {
            var smart=placed.GetComponent<SmartLinearObject>();
            if(smart==null || !LinearObjectState.TryParse(id,out var state) || smart.State.DefinitionId!=state.DefinitionId) return false;
            placed.transform.SetPositionAndRotation(pos,rot);
            placed.transform.localScale=Vector3.one*SanitizeNetworkScale(scale);
            smart.Apply(state);
            return true;
        }
    }
}
