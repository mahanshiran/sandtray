using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace Sandplay.Core
{
    [Serializable] public class SessionProfile
    {
        public string token, name, user_type, avatar_url, image_data;
        public TherapistProfileData therapist;
    }
    [Serializable] public class SessionProfileList
    {
        public SessionProfile[] profiles;
        public string rtc_grant;
        public long rtc_grant_expires_at;
    }
    public partial class NetworkBootstrapper
    {
        public string LocalProfileToken => IsHost ? "host" : _participantToken;
        public SessionProfile[] LiveProfiles { get; private set; } = new SessionProfile[0];
        public int ProfileVersion { get; private set; }
        private string _profileJson;
        private float _profileRequestTime;
        private string _rtcGrant;
        private long _rtcGrantExpiresAt;
        private readonly List<Action<string>> _rtcGrantCallbacks = new();
        public void RequestSessionProfiles(bool force = false)
        {
            if (!_isOnline || !_relayMode || (!force && Time.realtimeSinceStartup < _profileRequestTime)) return;
            _profileRequestTime = Time.realtimeSinceStartup + 10;
            SendToRelay(NetSerializer.Pack(NetMsgType.SessionProfilesRequest, Array.Empty<byte>()));
        }
        public void RequestRtcGrant(Action<string> onSuccess, Action<string> onError)
        {
            if (!_isOnline || !_relayMode || string.IsNullOrEmpty(_roomCode))
            { onError?.Invoke("No active relay room."); return; }
            if (!string.IsNullOrEmpty(_rtcGrant) && _rtcGrantExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5)
            { onSuccess?.Invoke(_rtcGrant); return; }
            _rtcGrantCallbacks.Add(onSuccess);
            RequestSessionProfiles(true);
            StartCoroutine(RtcGrantTimeout(onSuccess, onError));
        }
        private IEnumerator RtcGrantTimeout(Action<string> onSuccess, Action<string> onError)
        {
            // The relay limits profile requests to one every five seconds.
            // A request made just after a background poll may be ignored, so
            // retry while waiting instead of leaving media startup stuck.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                yield return new WaitForSecondsRealtime(3);
                if (!_rtcGrantCallbacks.Contains(onSuccess)) yield break;
                if (attempt < 3) RequestSessionProfiles(true);
            }
            if (_rtcGrantCallbacks.Remove(onSuccess))
                onError?.Invoke("Could not verify current room membership.");
        }
        private void ReceiveSessionProfiles(byte[] payload)
        {
            if (!_isOnline || !_relayMode || payload == null || payload.Length > 512 * 1024) return;
            try
            {
                string json = Encoding.UTF8.GetString(payload);
                if (json == _profileJson) return;
                var data = JsonUtility.FromJson<SessionProfileList>(json);
                if (data?.profiles == null || data.profiles.Length > 11) return;
                foreach (var profile in data.profiles)
                    if (profile == null || string.IsNullOrEmpty(profile.token) || profile.token.Length > 64) return;
                _profileJson = json; LiveProfiles = data.profiles; ProfileVersion++;
                if (!string.IsNullOrEmpty(data.rtc_grant) && data.rtc_grant_expires_at > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5)
                {
                    _rtcGrant = data.rtc_grant;
                    _rtcGrantExpiresAt = data.rtc_grant_expires_at;
                    var callbacks = _rtcGrantCallbacks.ToArray();
                    _rtcGrantCallbacks.Clear();
                    foreach (var callback in callbacks) callback?.Invoke(_rtcGrant);
                }
            }
            catch (ArgumentException) { }
        }
        private void ClearSessionProfiles()
        {
            LiveProfiles = new SessionProfile[0]; _profileJson = null; _profileRequestTime = 0;
            _rtcGrant = null; _rtcGrantExpiresAt = 0; _rtcGrantCallbacks.Clear(); ProfileVersion++;
        }
    }
}
