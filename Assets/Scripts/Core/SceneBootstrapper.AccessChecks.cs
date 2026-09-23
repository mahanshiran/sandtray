using System;
using Sandplay.Data;
using UnityEngine;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private double _nextPolicyRefresh;
        private bool _policyRefreshing;
        private int _accessActionRequest;
        private double _recordingPolicyDeadline;
        private bool HasCapability(string key) => AccessPolicy.Evaluate(BackendClient.Instance.CurrentAccess,
            key, checkUsage: false) == AccessDecision.Allowed;

        private void UpdateAccessPolicy()
        {
            var client = BackendClient.Instance;
            if (Time.realtimeSinceStartupAsDouble >= _recordingPolicyDeadline) SessionRecorder.Instance?.StopRecording();
            if (!client.IsLoggedIn || client.UserId <= 0 || _policyRefreshing ||
                Time.realtimeSinceStartupAsDouble < _nextPolicyRefresh) return;
            _policyRefreshing = true;
            _nextPolicyRefresh = Time.realtimeSinceStartupAsDouble + 20;
            client.FetchAccessSnapshot(snapshot =>
            {
                if (this == null) return;
                _policyRefreshing = false;
                // Policy refresh is not an account change; do not tear down the workspace.
                if (snapshot.local_capacity_enforced && _config != null)
                    _config.LocalTableCapacityEnforcement = true;
                if (_activeHomeNav == "replays" && IsHomeMenuActive) RefreshHomeReplaysPage();
                if (!HasCapability("replays.record")) SessionRecorder.Instance?.StopRecording();
                else _recordingPolicyDeadline = client.AccessCacheDeadline;
            }, error => { if (this != null) { _policyRefreshing = false; _nextPolicyRefresh = Time.realtimeSinceStartupAsDouble + 10; } }, true);
        }

        private void WithAccess(string key, Action allowed, bool checkUsage = false, bool forceRefresh = true)
        {
            var client = BackendClient.Instance;
            if (!client.IsLoggedIn) { OpenLoginScreen(() => WithAccess(key, allowed, checkUsage, forceRefresh)); return; }
            var loading = BeginUiOperation();
            int user = client.UserId; string token = client.AccessToken;
            bool current() => client != null && client.UserId == user && client.AccessToken == token;
            int epoch = LocalAccountStorage.Epoch, request = ++_accessActionRequest;
            bool Current() => this != null && request == _accessActionRequest && current() && epoch == LocalAccountStorage.Epoch && !LocalAccountStorage.RequiresRestart;
            client.FetchAccessSnapshot(snapshot =>
            {
                if (!CompleteUiOperation(loading)) return;
                if (!Current()) return;
                var decision = AccessPolicy.Evaluate(snapshot, key, checkUsage: checkUsage);
                if (decision == AccessDecision.Allowed) { allowed(); return; }
                string message = decision == AccessDecision.NotIncluded ? F("Your current access does not include this feature.", "当前权限不包含此功能。")
                    : decision == AccessDecision.LimitReached ? F("Your allowance is used up. Check Usage for your remaining balance and reset date.", "额度已用完，请查看用量和重置日期。")
                    : F("Usage could not be verified. Please reconnect and retry.", "无法验证用量，请联网后重试。");
                ShowLockedFeatureDialog(message, offerUpgrade: decision == AccessDecision.NotIncluded || decision == AccessDecision.LimitReached);
            }, error =>
            {
                if (!CompleteUiOperation(loading)) return;
                if (Current()) ShowLockedFeatureDialog(F("Access could not be refreshed. Please retry.", "无法刷新权限，请重试。"), offerUpgrade: false);
            }, forceRefresh);
        }
    }
}
