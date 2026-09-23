using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Data;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private GameObject _hostingWarning;
        private TMP_Text _hostingWarningLabel;
        private string _hostingWarningSession;
        private double _hostingWarningNextPoll, _hostingWarningStaleAt;
        private int _hostingWarningRequest;
        private bool _hostingWarningDisabled, _hostingWarningTracked;
        private double _hostingLeaseDeadline, _hostingRecoveryUntil;
        private bool _hostingLeaseKnown, _hostingLeaseFailed;
        private string _hostingRecoveryAccount;

        private void ClearHostingWarningAfterBoardExit()
        {
            // A completed exit has already persisted the board in ReturnToMenu.
            // Do not reinterpret its intentional network disconnect as a recovery
            // warning on the home or Multiplayer screens.
            ++_hostingWarningRequest;
            _hostingWarningSession = null;
            _hostingWarningNextPoll = _hostingWarningStaleAt = 0;
            _hostingWarningDisabled = _hostingWarningTracked = false;
            _hostingLeaseDeadline = _hostingRecoveryUntil = 0;
            _hostingLeaseKnown = _hostingLeaseFailed = false;
            _hostingRecoveryAccount = null;
            if (_hostingWarning != null) _hostingWarning.SetActive(false);
        }

        private void ReceiveHostingDeadline(int seconds, bool failed)
        {
            _hostingLeaseKnown=true; _hostingLeaseFailed=failed;
            _hostingLeaseDeadline=Time.realtimeSinceStartupAsDouble+seconds;
            if(failed)
                ShowHostingWarning(Localization.Get("access.host_connection"));
            else if(seconds <= 10)
                ShowHostingWarning(Localization.Get("access.host_deadline",seconds));
            else if(_hostingWarning != null)
                _hostingWarning.SetActive(false);
        }

        internal static long HostingTimeLeft(AccessSnapshot snapshot)
        {
            if (snapshot == null || snapshot.enforcement != "partial" || !snapshot.active || snapshot.capabilities == null)
                return -1;
            foreach (var item in snapshot.capabilities)
                if (item.key == "sessions.host_minutes" && item.usage_ready && item.usage_unit == "seconds" && !item.unlimited)
                {
                    if (item.remaining < 0 || item.reserved < 0 || item.remaining > long.MaxValue - item.reserved) return -1;
                    // Current lease time is still usable by this room.
                    return item.remaining + item.reserved;
                }
            return -1;
        }

        internal static long HostingAccessWarningSeconds(AccessSnapshot snapshot)
        {
            if (snapshot == null || snapshot.enforcement != "partial" || snapshot.capabilities == null ||
                !DateTimeOffset.TryParse(snapshot.server_time, out var server)) return -1;
            foreach (var item in snapshot.capabilities)
                if (item.key == "sessions.host_minutes" && item.usage_ready &&
                    DateTimeOffset.TryParse(item.hosting_access_warning_at, out var end))
                    return Math.Max(0, (long)Math.Ceiling((end - server).TotalSeconds));
            return -1;
        }

        private void UpdateHostingWarning()
        {
            var net = NetworkBootstrapper.Instance;
            var client = BackendClient.Instance;
            bool host = net != null && net.IsOnline && net.IsHost && net.IsRelayMode && client.IsLoggedIn;
            string session = host ? client.UserId + ":" + client.AccessToken + ":" + net.RoomCode : null;
            if (session != _hostingWarningSession)
            {
                bool recovery = !host && _hostingWarningSession != null && _hostingLeaseKnown &&
                    client.IsLoggedIn && _hostingWarningSession.StartsWith(client.UserId + ":" + client.AccessToken + ":", StringComparison.Ordinal);
                _hostingRecoveryAccount=recovery ? client.UserId + ":" + client.AccessToken : null;
                _hostingRecoveryUntil=recovery ? Time.realtimeSinceStartupAsDouble+300 : 0;
                _hostingLeaseKnown=_hostingLeaseFailed=false;
                _hostingWarningSession = session;
                ++_hostingWarningRequest;
                _hostingWarningNextPoll = _hostingWarningStaleAt = 0;
                _hostingWarningDisabled = _hostingWarningTracked = false;
                if (_hostingWarning != null) _hostingWarning.SetActive(false);
                if(recovery) ShowHostingWarning(Localization.Get("access.host_recovery"));
            }
            if(!host && _hostingRecoveryUntil > 0 && (Time.realtimeSinceStartupAsDouble >= _hostingRecoveryUntil ||
                _hostingRecoveryAccount != client.UserId + ":" + client.AccessToken))
            {
                _hostingRecoveryUntil=0;
                if(_hostingWarning != null) _hostingWarning.SetActive(false);
            }
            if (!host) return;
            double now = Time.realtimeSinceStartupAsDouble;
            bool leaseWarning = _hostingLeaseKnown && (_hostingLeaseFailed || _hostingLeaseDeadline-now <= 10);
            if(leaseWarning)
                ShowHostingWarning(_hostingLeaseFailed
                    ? Localization.Get("access.host_connection")
                    : Localization.Get("access.host_deadline",(int)Math.Ceiling(Math.Max(0,_hostingLeaseDeadline-now))));
            if (_hostingWarningDisabled) return;
            if (_hostingWarningStaleAt > 0 && now >= _hostingWarningStaleAt)
            {
                _hostingWarningStaleAt = 0;
                ShowHostingWarning(Localization.Get("access.host_unknown"));
            }
            if (now < _hostingWarningNextPoll) return;
            _hostingWarningNextPoll = now + 15;
            int request = ++_hostingWarningRequest;
            bool Current() => this != null && request == _hostingWarningRequest && session == _hostingWarningSession &&
                net != null && net.IsOnline && net.IsHost && net.IsRelayMode &&
                session == client.UserId + ":" + client.AccessToken + ":" + net.RoomCode;
            client.FetchAccessSnapshot(snapshot =>
            {
                if (!Current()) return;
                bool tracked = false;
                foreach (var item in snapshot.capabilities)
                    if (item.key == "sessions.host_minutes" && item.usage_ready && item.usage_unit == "seconds") tracked = true;
                if (!tracked)
                {
                    // A coordinated rollout/new room will retry. Do not poll an old backend throughout a session.
                    _hostingWarningDisabled = true;
                    if (_hostingWarning != null) _hostingWarning.SetActive(false);
                    return;
                }
                _hostingWarningTracked = true;
                _hostingWarningStaleAt = Time.realtimeSinceStartupAsDouble + 30;
                if (_hostingLeaseKnown && (_hostingLeaseFailed || _hostingLeaseDeadline-Time.realtimeSinceStartupAsDouble <= 10)) return;
                long remaining = HostingTimeLeft(snapshot);
                long accessEnd = HostingAccessWarningSeconds(snapshot);
                if (accessEnd >= 0 && accessEnd <= 300)
                    ShowHostingWarning(Localization.Get("access.host_ending", (long)Math.Ceiling(accessEnd / 60d)));
                else if (remaining >= 0 && remaining <= 300)
                    ShowHostingWarning(Localization.Get("access.host_low", (long)Math.Ceiling(remaining / 60d)));
                else if (_hostingWarning != null) _hostingWarning.SetActive(false);
            }, error =>
            {
                if (Current() && _hostingWarningTracked)
                {
                    _hostingWarningStaleAt = 0;
                    ShowHostingWarning(Localization.Get("access.host_unknown"));
                }
            });
        }

        private void ShowHostingWarning(string message)
        {
            if (_safeArea == null) return;
            if (_hostingWarning == null)
            {
                var panel = ClientRect(_safeArea.transform, "HostingTimeWarning", .23f,.88f,.54f,.075f);
                _hostingWarning = panel.gameObject;
                panel.gameObject.AddComponent<Image>().color = HomeCard;
                _hostingWarningLabel = ClientText(panel, message, 14, .035f,.12f,.70f,.76f, HomeText);
                _hostingWarningLabel.richText = false;
                _hostingWarningLabel.enableWordWrapping = true;
                ClientButton(panel, Localization.Get("access.save_now"), .78f,.18f,.19f,.64f, () =>
                {
                    var manager = SessionManager.Instance;
                    bool saved = manager != null && manager.AutoSaveActive && manager.TrySaveCurrentBoard();
                    _hostingWarningLabel.text = Localization.Get(saved ? "access.saved" : "access.save_failed");
                    if (saved) _hostingWarning.SetActive(false);
                });
            }
            _hostingWarningLabel.text = message;
            _hostingWarning.SetActive(true);
        }
    }
}
