using System;
using System.Collections;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class BackendClient
    {
        [Serializable] public class SocialProviders { public bool google; public bool apple; }
        [Serializable] private class SocialStart { public string attempt_id; public string poll_secret; public string authorization_url; public int expires_in; }
        [Serializable] private class SocialResult { public string status; public string error; public string access; public string refresh; public MeResponse user; }
        private Coroutine _socialRoutine;
        private SocialStart _socialAttempt;

        public void FetchSocialProviders(Action<SocialProviders> done)
        {
            StartCoroutine(Get(BaseUrl + "/auth/social/providers/", null,
                json => done?.Invoke(JsonUtility.FromJson<SocialProviders>(json)),
                error => done?.Invoke(new SocialProviders())));
        }

        public void BeginSocialLogin(string provider, Action<string> ready, Action<string> success, Action<string> error)
        {
            CancelSocialLogin();
            if (provider != "google" && provider != "apple") { error?.Invoke(Localization.Get("social.failed")); return; }
            _socialRoutine = StartCoroutine(SocialLoginRoutine(provider, ready, success, error));
        }

        public void CancelSocialLogin()
        {
            if (_socialRoutine != null) StopCoroutine(_socialRoutine);
            _socialRoutine = null;
            if (_socialAttempt != null)
            {
                string body = "{\"poll_secret\":\"" + Escape(_socialAttempt.poll_secret) + "\",\"cancel\":true}";
                StartCoroutine(Post(BaseUrl + "/auth/social/attempts/" + _socialAttempt.attempt_id + "/", body, null, _ => { }, _ => { }));
            }
            _socialAttempt = null;
        }

        public static bool IsValidSocialAuthorizationUrl(string provider, string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) return false;
            return (provider == "google" && uri.Host == "accounts.google.com" && uri.AbsolutePath == "/o/oauth2/v2/auth") ||
                   (provider == "apple" && uri.Host == "appleid.apple.com" && uri.AbsolutePath == "/auth/authorize");
        }

        private IEnumerator SocialLoginRoutine(string provider, Action<string> ready, Action<string> success, Action<string> error)
        {
            string json = null, failure = null;
            yield return Post(BaseUrl + "/auth/social/" + provider + "/start/", "{}", null, value => json = value, value => failure = value);
            if (failure != null) { _socialRoutine = null; error?.Invoke(Localization.Get("social.unavailable")); yield break; }
            var attempt = JsonUtility.FromJson<SocialStart>(json);
            if (attempt == null || !Guid.TryParse(attempt.attempt_id, out _) || string.IsNullOrEmpty(attempt.poll_secret) ||
                !IsValidSocialAuthorizationUrl(provider, attempt.authorization_url))
            { _socialRoutine = null; error?.Invoke(Localization.Get("social.failed")); yield break; }
            _socialAttempt = attempt;
            ready?.Invoke(attempt.authorization_url);
            float deadline = Time.realtimeSinceStartup + Mathf.Clamp(attempt.expires_in, 1, 300);
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(3);
                json = null; failure = null;
                yield return Post(BaseUrl + "/auth/social/attempts/" + attempt.attempt_id + "/",
                    "{\"poll_secret\":\"" + Escape(attempt.poll_secret) + "\"}", null, value => json = value, value => failure = value);
                if (failure != null) continue; // brief offline/background transitions can recover
                var result = JsonUtility.FromJson<SocialResult>(json);
                if (result == null || result.status == "pending") continue;
                _socialRoutine = null; _socialAttempt = null;
                if (result.status != "complete" || string.IsNullOrEmpty(result.access) || string.IsNullOrEmpty(result.refresh) || result.user == null)
                {
                    error?.Invoke(Localization.Get(result.error == "existing_account" ? "social.existing" :
                        result.status == "expired" ? "social.expired" : result.error == "cancelled" ? "social.cancelled" : "social.failed"));
                    yield break;
                }
                if (!Sandplay.Data.LocalAccountStorage.SaveBeforeIdentityChange())
                { error?.Invoke("Save your current table before switching accounts."); yield break; }
                var me = result.user;
                AccessToken = result.access; RefreshToken = result.refresh;
                UserId = me.id; UserName = me.name; UserEmail = me.email; UserType = me.user_type;
                AccountTypeSelected = me.account_type_selected;
                UserAvatarUrl = me.profile?.avatar_url ?? "";
                RevenueCatUserId = me.revenuecatAppUserId;
                StoreSubscriptionState = me.subscriptionState;
                SubscriptionExpiresAt = DateTime.TryParse(me.subscriptionExpireDate, out var expires) ? expires : (DateTime?)null;
                SaveToPrefs();
                RevenueCatManager.Instance?.IdentifyUser(RevenueCatAppUserId);
                success?.Invoke(UserName);
                yield break;
            }
            _socialRoutine = null; _socialAttempt = null;
            error?.Invoke(Localization.Get("social.expired"));
        }
    }
}
