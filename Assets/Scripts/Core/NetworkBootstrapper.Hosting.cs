using System;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class NetworkBootstrapper
    {
        public event Action<int, bool> OnHostingLeaseStatus;
        internal static bool TryReadHostingLeaseStatus(byte[] payload, out int seconds, out bool renewalFailed)
        {
            seconds=0; renewalFailed=false;
            if(payload == null || payload.Length != 5 || payload[0] > 1) return false;
            seconds=BitConverter.ToInt32(payload,1); renewalFailed=payload[0]==1;
            return seconds >= 0 && seconds <= 60;
        }
        private void ReceiveHostingLeaseStatus(byte[] payload)
        {
            if (!_isOnline || !_isHost || !_relayMode || SessionPlayer.IsReplayActive) return;
            if(TryReadHostingLeaseStatus(payload,out int seconds,out bool failed))
                OnHostingLeaseStatus?.Invoke(seconds,failed);
        }
    }
}
