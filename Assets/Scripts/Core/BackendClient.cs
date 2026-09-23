using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Sandplay.Core
{
    /// <summary>
    /// Singleton that talks to the Django REST API.
    /// Persists JWT credentials in the operating system credential store.
    /// </summary>
    public partial class BackendClient : MonoBehaviour
    {
        private static BackendClient _instance;
        public static BackendClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("BackendClient");
                    _instance = go.AddComponent<BackendClient>();
                }
                return _instance;
            }
        }

        internal const string BaseUrl = "https://api.sandtraypro.com/api";

        private const string PrefKeyToken = "backend_access_token";
        private const string PrefKeyRefreshToken = "backend_refresh_token";
        private const string PrefKeyName = "backend_user_name";
        private const string PrefKeyEmail = "backend_user_email";
        private const string PrefKeyUserId = "backend_user_id";
        private const string PrefKeyRevenueCatUserId = "backend_revenuecat_user_id";
        private const string PrefKeyUserType = "backend_user_type";
        private const string PrefKeySubscriptionExpires = "backend_subscription_expires";
        private const string PrefKeyManagedTherapist = "backend_managed_therapist";
        private const string PrefKeyManagedExternalContacts = "backend_managed_external_contacts";
        private const string PrefKeyAccountTypeSelected = "backend_account_type_selected";

        public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

        [Serializable] private class RelayTicketResponse { public string ticket; public int expires_in; }

        public void RequestRelayTicket(string roomCode, Action<string> success, Action<string> failure)
            => RequestRelayTicketInternal(roomCode, success, failure, true);

        private void RequestRelayTicketInternal(string roomCode, Action<string> success, Action<string> failure, bool allowRefresh)
        {
            if (!IsLoggedIn || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            { failure?.Invoke("Secure sign-in is required for sessions."); return; }
            string token = AccessToken;
            int user = UserId;
            string body;
            if (string.IsNullOrEmpty(roomCode))
            {
                string organizationId = IsManagedTherapist ? ManagedOrganizationId : ActiveHostingOrganizationId;
                body = string.IsNullOrEmpty(organizationId)
                    ? "{\"purpose\":\"host\"}"
                    : "{\"purpose\":\"host\",\"organization_id\":\"" + EscapeForJson(organizationId) + "\"}";
            }
            else body = "{\"purpose\":\"join\",\"room_code\":\"" + EscapeForJson(roomCode) + "\"}";
            StartCoroutine(Post($"{BaseUrl}/auth/relay/ticket/", body, token, json =>
            {
                if (AccessToken != token || UserId != user)
                { failure?.Invoke("Account changed. Please connect again."); return; }
                var response = JsonUtility.FromJson<RelayTicketResponse>(json);
                if (response == null || string.IsNullOrEmpty(response.ticket) || response.ticket.Length > 4096)
                { failure?.Invoke("Invalid session credential response."); return; }
                success?.Invoke(response.ticket);
            }, error =>
            {
                if (AccessToken != token || UserId != user)
                { failure?.Invoke("Account changed. Please connect again."); return; }
                if (allowRefresh && IsAuthenticationError(error))
                {
                    RefreshAccessToken(() => RequestRelayTicketInternal(roomCode, success, failure, false), failure);
                    return;
                }
                if (IsAuthenticationError(error))
                    failure?.Invoke("Session expired. Please sign in again.");
                else if ((IsOrganizationTherapist || !string.IsNullOrEmpty(ActiveHostingOrganizationId)) &&
                         !string.IsNullOrWhiteSpace(error))
                    failure?.Invoke(error);
                else
                    failure?.Invoke("Session service unavailable. Please try again.");
            }));
        }
        public string AccessToken { get; private set; }
        private string RefreshToken { get; set; }
        public int UserId { get; private set; }
        public string UserName { get; private set; }
        public string UserAvatarUrl { get; private set; }
        public string UserEmail { get; private set; }
        public string UserType { get; private set; }  // public: normal/psychologist/organization; internal: therapist_org/admin
        public bool IsTherapistAccount => UserType == "psychologist" || UserType == "therapist_org";
        public bool IsOrganizationTherapist => UserType == "therapist_org";
        public bool IsUpdatingUserType { get; private set; }
        public bool IsManagedTherapist { get; private set; }
        public bool AccountTypeSelected { get; private set; }
        public bool MustChangePassword { get; private set; }
        public string ManagedOrganizationName { get; private set; }
        public string ManagedOrganizationId { get; private set; }
        // Empty means personal hosting. Independent therapists may select an
        // organization workspace explicitly; managed accounts are forced above.
        public string ActiveHostingOrganizationId { get; set; }
        public bool ManagedCanCreateClients { get; private set; }
        public bool ManagedCanCreateSchedules { get; private set; }
        public bool ManagedCanHostSessions { get; private set; }
        public bool ManagedCanCreateReports { get; private set; }
        public bool ManagedCanInviteClients { get; private set; }
        public bool ManagedAllowExternalContacts { get; private set; }
        // Independent therapist accounts may belong to an organization and still
        // freelance. Only organization-created therapist_org identities are
        // restricted by the organization's external-contact permission.
        public bool ExternalContactsAllowed => !IsOrganizationTherapist || ManagedAllowExternalContacts;
        private int _userTypeRevision;
        public DateTime? SubscriptionExpiresAt { get; private set; }
        public string AccessSource { get; private set; }
        private string RevenueCatUserId { get; set; }
        public string RevenueCatAppUserId =>
            !string.IsNullOrEmpty(RevenueCatUserId)
                ? RevenueCatUserId
                : (UserId > 0 ? $"sandtray_user_{UserId}" : "");
        public const string FeatureAiAnalysis = "ai_analysis";
        public const string FeaturePdfExport = "pdf_export";
        public const string FeatureHostSession = "host_session";

        /// <summary>Check if user has active subscription (admin-granted or RevenueCat).</summary>
        public bool IsSubscribed
        {
            get
            {
                // An active admin grant or an active store entitlement provides VIP.
                if (SubscriptionExpiresAt.HasValue &&
                    DateTime.UtcNow < SubscriptionExpiresAt.Value)
                    return true;
                return RevenueCatManager.Instance != null && RevenueCatManager.Instance.IsSubscribed;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadFromPrefs();
        }

        private void Start()
        {
            // On app launch, if already logged in, refresh subscription status
            if (IsLoggedIn)
            {
                FetchMe(null, err => Debug.LogWarning($"[BackendClient] Failed to refresh subscription on launch: {err}"));
            }
        }

        private void LoadFromPrefs()
        {
            LoadCredentials();
            UserId = PlayerPrefs.GetInt(PrefKeyUserId, 0);
            RevenueCatUserId = PlayerPrefs.GetString(PrefKeyRevenueCatUserId, "");
            UserName = PlayerPrefs.GetString(PrefKeyName, "");
            UserAvatarUrl = PlayerPrefs.GetString("backend_avatar_url", "");
            UserEmail = PlayerPrefs.GetString(PrefKeyEmail, "");
            UserType = PlayerPrefs.GetString(PrefKeyUserType, "");
            IsManagedTherapist = PlayerPrefs.GetInt(PrefKeyManagedTherapist, 0) == 1;
            ManagedAllowExternalContacts = PlayerPrefs.GetInt(PrefKeyManagedExternalContacts, 0) == 1;
            AccountTypeSelected = PlayerPrefs.GetInt(PrefKeyAccountTypeSelected, 1) == 1;

            var expiresStr = PlayerPrefs.GetString(PrefKeySubscriptionExpires, "");
            if (!string.IsNullOrEmpty(expiresStr) && DateTime.TryParse(expiresStr, out DateTime expires))
                SubscriptionExpiresAt = expires;
            else
                SubscriptionExpiresAt = null;
        }

        private void SaveToPrefs()
        {
            InvalidateAccess();
            Sandplay.Data.LocalAccountStorage.ObserveIdentity();
            SaveCredentials();
            PlayerPrefs.SetInt(PrefKeyUserId, UserId);
            PlayerPrefs.SetString(PrefKeyRevenueCatUserId, RevenueCatUserId ?? "");
            PlayerPrefs.SetString(PrefKeyName, UserName ?? "");
            PlayerPrefs.SetString("backend_avatar_url", UserAvatarUrl ?? "");
            PlayerPrefs.SetString(PrefKeyEmail, UserEmail ?? "");
            PlayerPrefs.SetString(PrefKeyUserType, UserType ?? "");
            PlayerPrefs.SetInt(PrefKeyManagedTherapist, IsManagedTherapist ? 1 : 0);
            PlayerPrefs.SetInt(PrefKeyManagedExternalContacts, ManagedAllowExternalContacts ? 1 : 0);
            PlayerPrefs.SetInt(PrefKeyAccountTypeSelected, AccountTypeSelected ? 1 : 0);
            PlayerPrefs.SetString(PrefKeySubscriptionExpires,
                SubscriptionExpiresAt?.ToString("o") ?? "");
            PlayerPrefs.Save();
        }

        private void LoadCredentials()
        {
            string legacyAccess = PlayerPrefs.GetString(PrefKeyToken, "");
            string legacyRefresh = PlayerPrefs.GetString(PrefKeyRefreshToken, "");
            bool readAccess = SecureCredentialStore.TryRead(PrefKeyToken, out string secureAccess);
            bool readRefresh = SecureCredentialStore.TryRead(PrefKeyRefreshToken, out string secureRefresh);

            if (readAccess && readRefresh &&
                (!string.IsNullOrEmpty(secureAccess) || !string.IsNullOrEmpty(secureRefresh)))
            {
                AccessToken = secureAccess;
                RefreshToken = secureRefresh;
            }
            else if (readAccess && readRefresh &&
                     (!string.IsNullOrEmpty(legacyAccess) || !string.IsNullOrEmpty(legacyRefresh)))
            {
                bool migratedAccess = SecureCredentialStore.TryWrite(PrefKeyToken, legacyAccess);
                bool migratedRefresh = SecureCredentialStore.TryWrite(PrefKeyRefreshToken, legacyRefresh);
                if (migratedAccess && migratedRefresh)
                {
                    AccessToken = legacyAccess;
                    RefreshToken = legacyRefresh;
                }
                else
                {
                    SecureCredentialStore.TryDelete(PrefKeyToken);
                    SecureCredentialStore.TryDelete(PrefKeyRefreshToken);
                    AccessToken = RefreshToken = "";
                    Debug.LogWarning("[BackendClient] Secure credential migration failed. Please sign in again.");
                }
            }
            else
            {
                AccessToken = RefreshToken = "";
                if (!readAccess || !readRefresh)
                    Debug.LogWarning("[BackendClient] Secure credentials could not be read. Please sign in again.");
            }

            // Tokens must never remain in plaintext, even when native storage fails.
            if (PlayerPrefs.HasKey(PrefKeyToken) || PlayerPrefs.HasKey(PrefKeyRefreshToken))
            {
                PlayerPrefs.DeleteKey(PrefKeyToken);
                PlayerPrefs.DeleteKey(PrefKeyRefreshToken);
                PlayerPrefs.Save();
            }
        }

        private void SaveCredentials()
        {
            bool savedAccess = SecureCredentialStore.TryWrite(PrefKeyToken, AccessToken ?? "");
            bool savedRefresh = SecureCredentialStore.TryWrite(PrefKeyRefreshToken, RefreshToken ?? "");
            if (!savedAccess || !savedRefresh)
            {
                SecureCredentialStore.TryDelete(PrefKeyToken);
                SecureCredentialStore.TryDelete(PrefKeyRefreshToken);
                Debug.LogWarning("[BackendClient] Credentials are available for this session only; secure persistence failed.");
            }

            PlayerPrefs.DeleteKey(PrefKeyToken);
            PlayerPrefs.DeleteKey(PrefKeyRefreshToken);
        }

        private int _loginAttempt;

        public void SignOut()
        {
            if (!Sandplay.Data.LocalAccountStorage.SaveBeforeIdentityChange()) return;
            ++_loginAttempt;
            Sandplay.UI.AccountAvatar.RefreshPhotos();
            MobilePushClient.Instance.Revoke();
            CancelSocialLogin();
            RevenueCatManager.Instance?.ClearUserIdentity();
            AccessToken = RefreshToken = UserName = UserEmail = UserType = "";
            UserAvatarUrl = "";
            UserId = 0;
            RevenueCatUserId = "";
            SubscriptionExpiresAt = null;
            AccessSource = "";
            StoreSubscriptionState = null;
            IsManagedTherapist = MustChangePassword = AccountTypeSelected = false;
            ManagedOrganizationId = ManagedOrganizationName = "";
            ActiveHostingOrganizationId = "";
            ManagedCanCreateClients = ManagedCanCreateSchedules = ManagedCanHostSessions =
                ManagedCanCreateReports = ManagedCanInviteClients = ManagedAllowExternalContacts = false;
            SaveToPrefs();
        }

        /// <summary>Clear the local identity after the server has permanently erased it.</summary>
        public void ForgetDeletedAccount()
        {
            ++_loginAttempt;
            Sandplay.UI.AccountAvatar.RefreshPhotos();
            MobilePushClient.Instance.Revoke();
            CancelSocialLogin();
            RevenueCatManager.Instance?.ClearUserIdentity();
            AccessToken = RefreshToken = UserName = UserEmail = UserType = "";
            UserAvatarUrl = "";
            UserId = 0;
            RevenueCatUserId = "";
            SubscriptionExpiresAt = null;
            AccessSource = "";
            StoreSubscriptionState = null;
            IsManagedTherapist = MustChangePassword = AccountTypeSelected = false;
            ManagedOrganizationId = ManagedOrganizationName = "";
            ActiveHostingOrganizationId = "";
            ManagedCanCreateClients = ManagedCanCreateSchedules = ManagedCanHostSessions =
                ManagedCanCreateReports = ManagedCanInviteClients = ManagedAllowExternalContacts = false;
            SaveToPrefs();
        }

        // ── Public API calls ──────────────────────────────────────────────────

        public void Login(string email, string password,
            Action<string> onSuccess, Action<string> onError)
        {
            int attempt = ++_loginAttempt;
            var body = $"{{\"email\":\"{Escape(email)}\",\"password\":\"{Escape(password)}\"}}";
            StartCoroutine(Post($"{BaseUrl}/auth/login/", body, null, json =>
            {
                if (attempt != _loginAttempt) return;
                var resp = JsonUtility.FromJson<TokenResponse>(json);
                if (string.IsNullOrEmpty(resp.access))
                {
                    onError?.Invoke("Invalid response from server.");
                    return;
                }
                if (!Sandplay.Data.LocalAccountStorage.SaveBeforeIdentityChange()) { onError?.Invoke("Save your current table before switching accounts."); return; }
                UserId = 0; // A new credential must never temporarily borrow the old account's local identity.
                UserName = UserEmail = UserType = "";
                AccessSource = "";
                IsManagedTherapist = MustChangePassword = AccountTypeSelected = false;
                ManagedOrganizationId = ManagedOrganizationName = "";
                ActiveHostingOrganizationId = "";
                AccessToken = resp.access;
                Sandplay.Data.LocalAccountStorage.ObserveIdentity();
                RefreshToken = resp.refresh ?? "";
                FetchMe(onSuccess, error =>
                {
                    // Leave the transition shield only after an identity is resolved or the failed login is cleared.
                    if (attempt != _loginAttempt) return;
                    AccessToken = RefreshToken = ""; UserId = 0; SaveToPrefs();
                    onError?.Invoke(error);
                });
            }, onError));
        }

        public void Register(string email, string name, string password, string userType,
            Action<string> onSuccess, Action<string> onError)
        {
            var body = $"{{\"email\":\"{Escape(email)}\",\"name\":\"{Escape(name)}\"," +
                       $"\"password\":\"{Escape(password)}\",\"user_type\":\"{Escape(userType)}\",\"language\":\"{(Localization.Current == Language.Chinese ? "zh" : "en")}\"}}";
            StartCoroutine(Post($"{BaseUrl}/auth/register/", body, null, json =>
            {
                // No tokens until email ownership has been verified.
                onSuccess?.Invoke(email);
            }, onError));
        }

        private void FetchMe(Action<string> onSuccess, Action<string> onError)
        {
            FetchMeInternal(onSuccess, onError, true);
        }

        public void RefreshAccountState(Action onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn) { onError?.Invoke("Please sign in first."); return; }
            FetchMe(_ => onSuccess?.Invoke(), onError);
        }

        /// <summary>Updates the signed-in account, not a device-only preference.</summary>
        [Serializable] private class AccountProfileUpdate { public string name,email; }
        [Serializable]
        public class PersonalProfileData
        {
            public string name = "", email = "", preferred_name = "", age = "", gender = "", pronouns = "";
            public string languages = "", country = "", city = "", occupation = "", phone = "";
            public string emergency_contact = "", goals = "", accessibility = "", image_data = "";
        }

        public void PersonalProfile(PersonalProfileData data, Action<PersonalProfileData> success, Action<string> failure) =>
            StartCoroutine(PersonalProfileRequest(data, success, failure));

        private IEnumerator PersonalProfileRequest(PersonalProfileData data, Action<PersonalProfileData> success, Action<string> failure)
        {
            if (!IsLoggedIn) { failure("Please sign in first."); yield break; }
            var guard = CaptureCredentialGuard();
            using var request = new UnityWebRequest(BaseUrl + "/auth/personal-profile/", data == null ? "GET" : "PATCH");
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 30;
            request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            if (data != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            yield return request.SendWebRequest();
            if (!guard()) yield break;
            if (request.result != UnityWebRequest.Result.Success)
            {
                failure(request.responseCode == 404 ? "Profile editing needs the updated server. Please try again after deployment." : ExtractError(request.downloadHandler.text, request.responseCode));
                yield break;
            }
            PersonalProfileData profile = null;
            try { profile = JsonUtility.FromJson<PersonalProfileData>(request.downloadHandler.text); } catch (Exception) { }
            if (profile == null) { failure("Unable to read profile."); yield break; }
            if (data != null) { UserName = profile.name; UserEmail = profile.email; SaveToPrefs(); }
            success(profile);
        }

        public void UpdateAccountProfile(string name,string email,Action success,Action<string> failure)
        {
            if(!IsLoggedIn){failure?.Invoke("Please sign in first.");return;}
            StartCoroutine(UpdateAccountProfileRequest(JsonUtility.ToJson(new AccountProfileUpdate{name=name,email=email}),UserId,true,success,failure));
        }
        private IEnumerator UpdateAccountProfileRequest(string body,int user,bool refresh,Action success,Action<string> failure)
        {
            if(!IsLoggedIn||UserId!=user){failure?.Invoke("Account changed. Please sign in again.");yield break;}
            using(var request=new UnityWebRequest($"{BaseUrl}/auth/me/","PATCH"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler=new DownloadHandlerBuffer();request.timeout=30;
                request.SetRequestHeader("Content-Type","application/json");
                request.SetRequestHeader("Authorization","Bearer "+AccessToken);
                var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;
                yield return request.SendWebRequest();
                if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;
                if(!IsLoggedIn||UserId!=user){failure?.Invoke("Account changed. Please sign in again.");yield break;}
                if(request.responseCode==401&&refresh)
                {RefreshAccessToken(()=>StartCoroutine(UpdateAccountProfileRequest(body,user,false,success,failure)),failure);yield break;}
                if(request.result!=UnityWebRequest.Result.Success)
                {failure?.Invoke(ExtractError(request.downloadHandler.text,request.responseCode));yield break;}
                MeResponse response=null;
                try{response=JsonUtility.FromJson<MeResponse>(request.downloadHandler.text);}catch(Exception){}
                if(response==null||response.id!=user||string.IsNullOrEmpty(response.name)||string.IsNullOrEmpty(response.email))
                {failure?.Invoke("Unexpected account response. Please try again.");yield break;}
                UserName=response.name;UserEmail=response.email;SaveToPrefs();success?.Invoke();
            }
        }

        public void UpdateUserType(string userType, Action onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn) { onError?.Invoke("Please sign in first."); return; }
            if ((userType != "normal" && userType != "psychologist" && userType != "organization") || UserType == "admin")
            { onError?.Invoke("This account type cannot be changed here."); return; }
            if (IsUpdatingUserType) { onError?.Invoke("An account update is already in progress."); return; }
            IsUpdatingUserType = true;
            int accountId = UserId;
            StartCoroutine(UpdateUserTypeRequest(userType, accountId, true,
                () => { IsUpdatingUserType = false; onSuccess?.Invoke(); },
                error => { IsUpdatingUserType = false; onError?.Invoke(error); }));
        }

        private IEnumerator UpdateUserTypeRequest(string userType, int accountId, bool allowRefresh,
            Action onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn || UserId != accountId)
            { onError?.Invoke("Account changed. Please try again."); yield break; }
            using (var request = new UnityWebRequest($"{BaseUrl}/auth/me/", "PATCH"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                    $"{{\"user_type\":\"{userType}\"}}"));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
                request.timeout = 30;
                var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;
                yield return request.SendWebRequest();
                if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;
                if (!IsLoggedIn || UserId != accountId)
                { onError?.Invoke("Account changed. Please try again."); yield break; }
                if (request.responseCode == 401 && allowRefresh)
                {
                    RefreshAccessToken(() => StartCoroutine(UpdateUserTypeRequest(
                        userType, accountId, false, onSuccess, onError)), onError);
                    yield break;
                }
                if (request.result != UnityWebRequest.Result.Success)
                { onError?.Invoke(ExtractError(request.downloadHandler.text, request.responseCode)); yield break; }
                MeResponse response = null;
                try { response = JsonUtility.FromJson<MeResponse>(request.downloadHandler.text); }
                catch (Exception) { /* Report malformed responses without changing local state. */ }
                if (response == null || response.id != accountId || response.user_type != userType ||
                    !response.account_type_selected)
                { onError?.Invoke("Unexpected account response. Please try again."); yield break; }
                UserType = response.user_type;
                AccountTypeSelected = response.account_type_selected;
                _userTypeRevision++;
                SaveToPrefs();
                onSuccess?.Invoke();
            }
        }

        private void FetchMeInternal(Action<string> onSuccess, Action<string> onError,
            bool allowRefresh)
        {
            int userTypeRevision = _userTypeRevision;
            var credentialsCurrent = CaptureCredentialGuard();
            StartCoroutine(Get($"{BaseUrl}/auth/me/", AccessToken, json =>
            {
                if (!credentialsCurrent()) return;
                var me = JsonUtility.FromJson<MeResponse>(json);
                UserId = me.id;
                RevenueCatUserId = me.revenuecatAppUserId;
                StoreSubscriptionState = me.subscriptionState;
                AccessSource = me.access_source ?? "";
                UserName = me.name;
                UserAvatarUrl = me.profile?.avatar_url ?? "";
                UserEmail = me.email;
                MustChangePassword = me.must_change_password;
                AccountTypeSelected = me.account_type_selected;
                IsManagedTherapist = me.managed_therapist != null;
                ManagedOrganizationId = me.managed_therapist?.organization_id ?? "";
                ManagedOrganizationName = me.managed_therapist?.organization_name ?? "";
                ManagedCanCreateClients = me.managed_therapist?.can_create_clients ?? false;
                ManagedCanCreateSchedules = me.managed_therapist?.can_create_schedules ?? false;
                ManagedCanHostSessions = me.managed_therapist?.can_host_sessions ?? false;
                ManagedCanCreateReports = me.managed_therapist?.can_create_reports ?? false;
                ManagedCanInviteClients = me.managed_therapist?.can_invite_clients ?? false;
                ManagedAllowExternalContacts = me.managed_therapist?.allow_external_contacts ?? false;
                // A launch-time GET may finish after the Settings PATCH.
                if (userTypeRevision == _userTypeRevision) UserType = me.user_type;

                // Parse subscription expiration if present
                if (!string.IsNullOrEmpty(me.subscriptionExpireDate))
                {
                    if (DateTime.TryParse(me.subscriptionExpireDate, out DateTime expires))
                        SubscriptionExpiresAt = expires;
                    else
                        SubscriptionExpiresAt = null;
                }
                else
                {
                    SubscriptionExpiresAt = null;
                }

                SaveToPrefs();
                RevenueCatManager.Instance?.IdentifyUser(RevenueCatAppUserId);
                onSuccess?.Invoke(UserName);
            }, error =>
            {
                if (!credentialsCurrent()) return;
                if (allowRefresh && IsAuthenticationError(error))
                {
                    RefreshAccessToken(
                        () => FetchMeInternal(onSuccess, onError, false),
                        onError);
                    return;
                }
                onError?.Invoke(error);
            }));
        }

        /// <summary>
        /// Fetches all objects from all public catalogs. No auth required.
        /// </summary>
        public void FetchPublicCatalog(
            Action<Sandplay.Objects.NetworkCatalogItem[]> onSuccess,
            Action<string> onError)
        {
            StartCoroutine(Get($"{BaseUrl}/catalogs/public/", null, json =>
            {
                var resp = JsonUtility.FromJson<Sandplay.Objects.PublicCatalogResponse>(json);
                onSuccess?.Invoke(resp?.objects ?? new Sandplay.Objects.NetworkCatalogItem[0]);
            }, onError));
        }

        /// <summary>
        /// Fetches the signed-in user's complete usable library: official/public,
        /// their own catalogs, and active therapist-clinic catalogs.
        /// </summary>
        public void FetchLibraryCatalog(
            Action<Sandplay.Objects.NetworkCatalogItem[]> onSuccess,
            Action<string> onError)
        {
            if (!IsLoggedIn)
            {
                FetchPublicCatalog(onSuccess, onError);
                return;
            }

            StartCoroutine(Get($"{BaseUrl}/catalogs/library/", AccessToken, json =>
            {
                var resp = JsonUtility.FromJson<Sandplay.Objects.PublicCatalogResponse>(json);
                onSuccess?.Invoke(resp?.objects ?? new Sandplay.Objects.NetworkCatalogItem[0]);
            }, onError));
        }

        /// <summary>
        /// Generate an AI-assisted reflection through the authenticated backend.
        /// The Bailian API key and safety prompt stay on the server.
        /// </summary>
        public void RequestAiReflection(Sandplay.AI.AnalysisPayload payload,
            string screenshotB64, string language,
            Action<string, string> onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn) { onError?.Invoke("Not logged in"); return; }

            string sessionJson = payload == null ? "{}" : JsonUtility.ToJson(payload);
            var body = "{" +
                "\"language\":\"" + EscapeForJson(language) + "\"," +
                "\"session_data\":" + sessionJson + "," +
                "\"screenshot_base64\":\"" + EscapeForJson(screenshotB64) + "\"" +
                "}";

            int requestUser = UserId; string requestToken = AccessToken;
            string key = ReflectionRequestKey(requestUser, requestToken, body);
            string operationId = GetReflectionOperationId(key);
            body = "{\"operation_id\":\"" + operationId + "\"," + body.Substring(1);
            bool Current() => UserId == requestUser && AccessToken == requestToken;
            StartCoroutine(Post($"{BaseUrl}/analysis/reflect/", body, requestToken, json =>
            {
                if (!Current()) return;
                var response = JsonUtility.FromJson<AiReflectionResponse>(json);
                if (response == null || string.IsNullOrEmpty(response.reflection))
                {
                    onError?.Invoke("The reflection service returned an empty response.");
                    return;
                }
                ForgetReflectionOperation(key);
                onSuccess?.Invoke(response.reflection, response.model);
            }, error =>
            {
                if (!Current()) return;
                // Known terminal failures can start a new operation. Ambiguous
                // network failures retain the ID so retry cannot charge twice.
                if (ReflectionFailureFinalized(error))
                    ForgetReflectionOperation(key);
                onError?.Invoke(error);
            }));
        }

        /// <summary>
        /// Save an AI-assisted reflection record to the cloud. Returns the new record UUID via onSuccess.
        /// </summary>
        public void SaveAnalysisRecord(string resultText, string screenshotB64, string modelUsed,
            Action<string> onSuccess, Action<string> onError)
        {
            SaveAnalysisRecord(resultText, screenshotB64, modelUsed, null, null, null, null,
                onSuccess, onError);
        }

        public void SaveAnalysisRecord(string resultText, string screenshotB64, string modelUsed,
            string organizationId, string organizationClientId, string operationId, string tableName,
            Action<string> onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn) { onError?.Invoke("Not logged in"); return; }

            // screenshot_b64 is pure base64 (no quoting issues); result text needs full escape
            var body = "{" +
                "\"model_used\":\"" + EscapeForJson(modelUsed) + "\"," +
                "\"prompt_snapshot\":\"" + EscapeForJson(resultText) + "\"," +
                "\"raw_response\":\"" + EscapeForJson(resultText) + "\"," +
                "\"parsed_result\":{}," +
                "\"tokens_used\":0," +
                "\"cost_fen\":0," +
                "\"screenshot_b64\":\"" + (screenshotB64 ?? "") + "\"" +
                (string.IsNullOrEmpty(operationId) ? "" :
                    ",\"operation_id\":\"" + EscapeForJson(operationId) + "\"") +
                (string.IsNullOrEmpty(tableName) ? "" :
                    ",\"table_name\":\"" + EscapeForJson(tableName) + "\"") +
                (string.IsNullOrEmpty(organizationId) ? "" :
                    ",\"organization_id\":\"" + EscapeForJson(organizationId) + "\"," +
                    "\"organization_client_id\":\"" + EscapeForJson(organizationClientId) + "\"") +
                "}";

            StartCoroutine(Post($"{BaseUrl}/analysis/", body, AccessToken, json =>
            {
                var id = ParseField(json, "id");
                onSuccess?.Invoke(id ?? "");
            }, onError));
        }

        /// <summary>
        /// Download the PDF report for an analysis record. Returns raw PDF bytes via onSuccess.
        /// </summary>
        public void DownloadAnalysisPdf(string analysisId,
            Action<byte[]> onSuccess, Action<string> onError)
        {
            StartCoroutine(GetBytes($"{BaseUrl}/analysis/{analysisId}/pdf/", AccessToken,
                onSuccess, onError));
        }

        /// <summary>
        /// Compatibility notification for older flows. The server decides whether the operation was already metered.
        /// </summary>
        public void ConsumeFreeFeature(string feature, Action<int> onAllowed,
            Action onLimitReached, Action<string> onError)
        {
            if (!IsLoggedIn)
            {
                onError?.Invoke("Not logged in");
                return;
            }

            var body = $"{{\"feature\":\"{Escape(feature)}\"}}";
            StartCoroutine(Post($"{BaseUrl}/auth/usage/", body, AccessToken, json =>
            {
                var response = JsonUtility.FromJson<FreeTierConsumeResponse>(json);
                if (response != null && response.allowed)
                    onAllowed?.Invoke(response.remaining);
                else
                    onLimitReached?.Invoke();
            }, error =>
            {
                if (!string.IsNullOrEmpty(error) &&
                    (error.IndexOf("limit reached", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     error.IndexOf("HTTP 403", StringComparison.OrdinalIgnoreCase) >= 0))
                    onLimitReached?.Invoke();
                else
                    onError?.Invoke(error);
            }));
        }

        /// <summary>
        /// Fetch an Agora RTC token for joining a voice/video channel.
        /// Returns token + appId via onSuccess.
        /// </summary>
        public void FetchAgoraToken(string channelName, string rtcGrant,
            Action<string, string> onSuccess, Action<string> onError)
        {
            FetchAgoraTokenInternal(channelName, rtcGrant, onSuccess, onError, true);
        }

        private void FetchAgoraTokenInternal(string channelName, string rtcGrant,
            Action<string, string> onSuccess, Action<string> onError, bool allowRefresh)
        {
            if (!IsLoggedIn) { onError?.Invoke("Not logged in"); return; }

            var current = CaptureAccountGuard();
            Action<string> Guard(Action<string> callback) => GuardAgoraTokenResponse(current, callback, onError);
            var body = $"{{\"channel_name\":\"{Escape(channelName)}\",\"rtc_grant\":\"{Escape(rtcGrant)}\"}}";
            StartCoroutine(Post($"{BaseUrl}/boards/agora-token/", body, AccessToken,
                Guard(json => CompleteAgoraTokenResponse(json, onSuccess, onError)),
                Guard(error =>
                {
                    if (allowRefresh && IsAuthenticationError(error))
                    {
                        RefreshAccessToken(
                            () => FetchAgoraTokenInternal(channelName, rtcGrant, onSuccess, onError, false),
                            onError);
                        return;
                    }
                    onError?.Invoke(error);
                }), allowTokenRefresh: true));
        }

        private static Action<string> GuardAgoraTokenResponse(Func<bool> current,
            Action<string> callback, Action<string> onError)
        {
            return response =>
            {
                if (!current()) { onError?.Invoke("Account changed. Please connect again."); return; }
                callback(response);
            };
        }

        private static void CompleteAgoraTokenResponse(string json, Action<string, string> onSuccess,
            Action<string> onError)
        {
            AgoraTokenResponse response;
            try { response = JsonUtility.FromJson<AgoraTokenResponse>(json); }
            catch (ArgumentException)
            {
                onError?.Invoke("Invalid token response from server.");
                return;
            }
            if (response == null || string.IsNullOrWhiteSpace(response.token) || string.IsNullOrWhiteSpace(response.app_id))
            {
                onError?.Invoke("Invalid token response from server.");
                return;
            }
            onSuccess?.Invoke(response.token, response.app_id);
        }

        private void RefreshAccessToken(Action onSuccess, Action<string> onError)
        {
            var current = CaptureCredentialGuard();
            if (string.IsNullOrEmpty(RefreshToken))
            {
                // Expired cloud credentials must not switch/lock the local workspace.
                // Explicit sign-out still clears identity and transitions the workspace.
                onError?.Invoke("Session expired. Please sign in again.");
                return;
            }

            var body = $"{{\"refresh\":\"{Escape(RefreshToken)}\"}}";
            StartCoroutine(Post($"{BaseUrl}/auth/token/refresh/", body, null, json =>
            {
                if (!current()) { onError?.Invoke("Account changed. Please connect again."); return; }
                var response = JsonUtility.FromJson<TokenResponse>(json);
                if (response == null || string.IsNullOrEmpty(response.access))
                {
                    onError?.Invoke("Session expired. Please sign in again.");
                    return;
                }

                AccessToken = response.access;
                if (!string.IsNullOrEmpty(response.refresh))
                    RefreshToken = response.refresh;
                SaveToPrefs();
                onSuccess?.Invoke();
            }, error =>
            {
                if (!current()) { onError?.Invoke("Account changed. Please connect again."); return; }
                if (IsAuthenticationError(error))
                {
                    onError?.Invoke("Session expired. Please sign in again.");
                }
                else onError?.Invoke("Session service unavailable. Please try again.");
            }));
        }

        private Func<bool> CaptureCredentialGuard()
        {
            int user = UserId;
            string access = AccessToken, refresh = RefreshToken;
            return () => this != null && UserId == user && AccessToken == access && RefreshToken == refresh;
        }

        private Func<bool> CaptureAccountGuard()
        {
            int user = UserId;
            int epoch = Sandplay.Data.LocalAccountStorage.Epoch;
            return () => this != null && IsLoggedIn && user > 0 && UserId == user &&
                Sandplay.Data.LocalAccountStorage.Epoch == epoch;
        }

        // ── HTTP helpers ──────────────────────────────────────────────────────

        private const int DefaultRequestTimeoutSeconds = 30;
        private const int UploadRequestTimeoutSeconds = 180;
        private const int AiRequestTimeoutSeconds = 200;

        private static void ApplyRequestTimeout(UnityWebRequest request, string url, bool isUpload = false)
        {
            int timeout = isUpload ? UploadRequestTimeoutSeconds : DefaultRequestTimeoutSeconds;

            if (url.EndsWith("/analysis/reflect/", StringComparison.Ordinal))
                timeout = AiRequestTimeoutSeconds;
            else if (url.Contains("/auth/relay/ticket/") || url.Contains("/auth/token/refresh/"))
                timeout = 4;
            else if (url.EndsWith("/auth/access/local-leases/", StringComparison.Ordinal) ||
                     url.Contains("/auth/social/"))
                timeout = 15;

            request.timeout = timeout;
        }

        private IEnumerator Post(string url, string jsonBody, string token,
            Action<string> onSuccess, Action<string> onError, bool allowTokenRefresh = false)
        {
            using var req = new UnityWebRequest(url, "POST");
            bool isLargeUpload = url.EndsWith("/analysis/", StringComparison.Ordinal) ||
                                 url.Contains("/cloud/v1/");
            ApplyRequestTimeout(req, url, isLargeUpload);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", "Bearer " + token);

            var requestGuard = allowTokenRefresh ? CaptureAccountGuard() : CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;

            yield return req.SendWebRequest();

            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success ||
                req.responseCode == 201)
            {
                InvalidateAccess();
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                string msg = RequestFailure(req);
                onError?.Invoke(msg);
            }
        }

        private IEnumerator Get(string url, string token,
            Action<string> onSuccess, Action<string> onError)
        {
            var req = UnityWebRequest.Get(url);
            ApplyRequestTimeout(req, url);
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", "Bearer " + token);

            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;

            yield return req.SendWebRequest();

            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(req.downloadHandler.text);
            else
                onError?.Invoke(RequestFailure(req));
        }

        private IEnumerator GetBytes(string url, string token,
            Action<byte[]> onSuccess, Action<string> onError)
        {
            var req = UnityWebRequest.Get(url);
            ApplyRequestTimeout(req, url);
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", "Bearer " + token);

            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;

            yield return req.SendWebRequest();

            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(req.downloadHandler.data);
            else
                onError?.Invoke(RequestFailure(req));
        }

        private static string RequestFailure(UnityWebRequest request)
        {
            if (request.responseCode > 0) return ExtractError(request.downloadHandler.text, request.responseCode);
            string error = request.error ?? "";
            bool zh = Localization.Current == Language.Chinese;
            if (error.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("TLS", StringComparison.OrdinalIgnoreCase) >= 0)
                return zh ? "无法验证服务器的安全证书，请检查网络或代理连接后重试。"
                    : "The server's security certificate could not be verified. Check your network or proxy connection and retry.";
            if (error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                return zh ? "请求超时，尚未收到服务器响应，请重试。" : "The request timed out before a server response arrived. Please retry.";
            return zh ? "无法连接服务器，请检查网络连接后重试。" : "Could not connect to the server. Check your connection and retry.";
        }

        private static string ExtractError(string body, long code)
        {
            // Try to pull "detail" or "non_field_errors" from DRF JSON error
            if (!string.IsNullOrEmpty(body))
            {
                var detail = ParseField(body, "detail");
                if (!string.IsNullOrEmpty(detail)) return detail;
                var nfe = ParseField(body, "non_field_errors");
                if (!string.IsNullOrEmpty(nfe)) return nfe;
                // DRF field validation errors have neither detail nor non_field_errors.
                // Surface known capacity field messages without echoing the request/receipt.
                try
                {
                    var token = Newtonsoft.Json.Linq.JToken.Parse(body);
                    if (token is Newtonsoft.Json.Linq.JArray rootErrors && rootErrors.Count > 0 &&
                        rootErrors[0].Type == Newtonsoft.Json.Linq.JTokenType.String)
                        return (string)rootErrors[0];
                    if (!(token is Newtonsoft.Json.Linq.JObject fields)) return $"HTTP {code}";
                    foreach (string field in new[] { "action", "device_id", "operation_id", "capability", "lease_id", "receipt", "therapist_code",
                        "id", "client_code", "organization_id", "organization_client_id", "starts_at", "utc_offset_minutes", "nonce", "room" })
                    {
                        var value = fields[field];
                        if (value is Newtonsoft.Json.Linq.JArray errors && errors.Count > 0 &&
                            errors[0].Type == Newtonsoft.Json.Linq.JTokenType.String)
                            return field == "therapist_code" ? (string)errors[0] : field + ": " + (string)errors[0];
                        if (value != null && value.Type == Newtonsoft.Json.Linq.JTokenType.String)
                            return field == "therapist_code" ? (string)value : field + ": " + (string)value;
                    }
                }
                catch (Newtonsoft.Json.JsonException) { }
            }
            return $"HTTP {code}";
        }

        private static bool IsAuthenticationError(string error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            return error.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("authentication credentials",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("HTTP 401", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// Minimal field extractor — avoids a full JSON library dependency.
        private static string ParseField(string json, string field)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field)) return null;

            string key = $"\"{field}\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + key.Length);
            if (colon < 0) return null;
            int start = json.IndexOf('"', colon + 1);
            if (start < 0) return null;
            int end = json.IndexOf('"', start + 1);
            if (end < 0) return null;
            return json.Substring(start + 1, end - start - 1);
        }

        private static string Escape(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// Full JSON string escape (handles newlines and all control chars).
        private static string EscapeForJson(string s) =>
            (s ?? "").Replace("\\", "\\\\")
             .Replace("\"", "\\\"")
             .Replace("\n", "\\n")
             .Replace("\r", "\\r")
             .Replace("\t", "\\t");

        // ── Catalog Object Upload ─────────────────────────────────────────────

        /// <summary>
        /// Upload a file (GLB model or thumbnail image) to the backend.
        /// Returns a coroutine that yields FileUploadResult with the file URL on success.
        /// Backend endpoint: POST /api/catalogs/upload/
        /// Expected response: { "url": "https://..." }
        /// </summary>
        public FileUploadResult UploadFile(byte[] fileData, string fileName, string mimeType)
        {
            return new FileUploadResult(this, fileData, fileName, mimeType);
        }

        /// <summary>
        /// Create a new catalog object in the specified catalog.
        /// Returns a coroutine that yields CatalogObjectResult with the created object on success.
        /// Backend endpoint: POST /api/catalogs/{catalogId}/objects/
        /// </summary>
        public CatalogObjectResult CreateCatalogObject(string catalogId, UI.CatalogObjectCreateRequest objectData)
        {
            return new CatalogObjectResult(this, catalogId, objectData);
        }

        /// <summary>
        /// Fetch all catalogs owned by the current user.
        /// Backend endpoint: GET /api/catalogs/
        /// </summary>
        public UserCatalogsResult FetchUserCatalogs()
        {
            return new UserCatalogsResult(this);
        }

        /// <summary>
        /// Fetch all objects in a specific catalog.
        /// Backend endpoint: GET /api/catalogs/{catalogId}/objects/
        /// </summary>
        public CatalogObjectsResult FetchCatalogObjects(string catalogId)
        {
            return new CatalogObjectsResult(this, catalogId);
        }

        /// <summary>
        /// Delete a catalog object.
        /// Backend endpoint: DELETE /api/catalogs/{catalogId}/objects/{objectId}/
        /// </summary>
        public DeleteObjectResult DeleteCatalogObject(string catalogId, string objectId)
        {
            return new DeleteObjectResult(this, catalogId, objectId);
        }

        /// <summary>
        /// Create a new catalog for the current user.
        /// </summary>
        public CreateCatalogResult CreateCatalog(string catalogName, string visibility = "private")
        {
            return new CreateCatalogResult(this, catalogName, visibility);
        }

        public CatalogUpdateResult UpdateCatalogVisibility(string catalogId, string visibility)
        {
            return new CatalogUpdateResult(this, catalogId, visibility);
        }

        internal IEnumerator UploadFileCoroutine(byte[] fileData, string fileName, string mimeType,
            Action<string, string> onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn)
            {
                onError?.Invoke("Not logged in");
                yield break;
            }

            if (fileData == null || fileData.Length == 0 || string.IsNullOrEmpty(fileName))
            {
                onError?.Invoke("Missing file data");
                yield break;
            }

            if (string.IsNullOrEmpty(mimeType))
                mimeType = "application/octet-stream";

            // Create multipart form data
            var form = new WWWForm();
            form.AddBinaryData("file", fileData, fileName, mimeType);

            string uploadUrl = $"{BaseUrl}/catalogs/upload/";
            var req = UnityWebRequest.Post(uploadUrl, form);
            ApplyRequestTimeout(req, uploadUrl, isUpload: true);
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;

            yield return req.SendWebRequest();

            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 201)
            {
                var response = req.downloadHandler.text;
                var url = ParseField(response, "url");
                var sha256 = ParseField(response, "sha256");
                if (string.IsNullOrEmpty(url))
                {
                    onError?.Invoke("Upload succeeded but no URL returned");
                }
                else
                {
                    onSuccess?.Invoke(url, sha256 ?? "");
                }
            }
            else
            {
                string msg = RequestFailure(req);
                onError?.Invoke(msg);
            }
        }

        internal IEnumerator CreateCatalogObjectCoroutine(string catalogId, UI.CatalogObjectCreateRequest objectData,
            Action<string> onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn)
            {
                onError?.Invoke("Not logged in");
                yield break;
            }

            if (string.IsNullOrEmpty(catalogId) || objectData == null)
            {
                onError?.Invoke("Missing catalog object data");
                yield break;
            }

            // Build JSON payload
            var tags = objectData.tags ?? Array.Empty<string>();
            var tagsJson = string.Join(",", tags.Select(t => $"\"{EscapeForJson(t)}\""));
            var json = $@"{{
                ""display_name"":""{EscapeForJson(objectData.display_name)}"",
                ""category"":""{EscapeForJson(objectData.category)}"",
                ""tags"":[{tagsJson}],
                ""description"":""{EscapeForJson(objectData.description)}"",
                ""model_url"":""{EscapeForJson(objectData.model_url)}"",
                ""thumbnail_url"":""{EscapeForJson(objectData.thumbnail_url)}"",
                ""model_hash"":""{EscapeForJson(objectData.model_hash)}""
            }}";

            string url = $"{BaseUrl}/catalogs/{catalogId}/objects/";
            var req = new UnityWebRequest(url, "POST");
            ApplyRequestTimeout(req, url);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;

            yield return req.SendWebRequest();

            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 201)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                string msg = RequestFailure(req);
                onError?.Invoke(msg);
            }
        }

        internal IEnumerator FetchUserCatalogsCoroutine(Action<string> onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn)
            {
                onError?.Invoke("Not logged in");
                yield break;
            }

            string url = $"{BaseUrl}/catalogs/";
            var req = UnityWebRequest.Get(url);
            ApplyRequestTimeout(req, url);
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;

            yield return req.SendWebRequest();

            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                string msg = RequestFailure(req);
                onError?.Invoke(msg);
            }
        }

        internal IEnumerator FetchCatalogObjectsCoroutine(string catalogId,
            Action<string> onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn)
            {
                onError?.Invoke("Not logged in");
                yield break;
            }

            if (string.IsNullOrEmpty(catalogId))
            {
                onError?.Invoke("Missing catalog ID");
                yield break;
            }

            string url = $"{BaseUrl}/catalogs/{catalogId}/objects/";
            var req = UnityWebRequest.Get(url);
            ApplyRequestTimeout(req, url);
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;

            yield return req.SendWebRequest();

            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                string msg = RequestFailure(req);
                onError?.Invoke(msg);
            }
        }

        internal IEnumerator DeleteCatalogObjectCoroutine(string catalogId, string objectId,
            Action onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn)
            {
                onError?.Invoke("Not logged in");
                yield break;
            }

            if (string.IsNullOrEmpty(catalogId) || string.IsNullOrEmpty(objectId))
            {
                onError?.Invoke("Missing catalog or object ID");
                yield break;
            }

            string url = $"{BaseUrl}/catalogs/{catalogId}/objects/{objectId}/";
            var req = UnityWebRequest.Delete(url);
            ApplyRequestTimeout(req, url);
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;

            yield return req.SendWebRequest();

            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 204)
            {
                onSuccess?.Invoke();
            }
            else
            {
                string msg = RequestFailure(req);
                onError?.Invoke(msg);
            }
        }

        internal IEnumerator CreateCatalogCoroutine(string catalogName, string visibility,
            Action<string> onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn)
            {
                onError?.Invoke("Not logged in");
                yield break;
            }

            // Create JSON payload
            string json = $"{{\"name\":\"{EscapeForJson(catalogName)}\",\"visibility\":\"{EscapeForJson(visibility)}\"}}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

            string url = $"{BaseUrl}/catalogs/";
            var req = UnityWebRequest.Put(url, bodyRaw);
            ApplyRequestTimeout(req, url);
            req.method = "POST";  // Override PUT with POST
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;

            yield return req.SendWebRequest();

            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 201)
            {
                try
                {
                    string response = req.downloadHandler.text;
                    string catalogId = ParseField(response, "id");
                    if (string.IsNullOrEmpty(catalogId))
                    {
                        onError?.Invoke("Catalog created but no ID returned");
                    }
                    else
                    {
                        onSuccess?.Invoke(catalogId);
                    }
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"Parse error: {ex.Message}");
                }
            }
            else
            {
                string msg = RequestFailure(req);
                onError?.Invoke(msg);
            }
        }

        internal IEnumerator UpdateCatalogVisibilityCoroutine(string catalogId, string visibility,
            Action onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn)
            {
                onError?.Invoke("Not logged in");
                yield break;
            }

            var body = Encoding.UTF8.GetBytes(
                $"{{\"visibility\":\"{EscapeForJson(visibility)}\"}}");
            string url = $"{BaseUrl}/catalogs/{catalogId}/";
            var req = new UnityWebRequest(url, "PATCH");
            ApplyRequestTimeout(req, url);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;
            yield return req.SendWebRequest();
            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke();
            else
                onError?.Invoke(RequestFailure(req));
        }

        public IEnumerator MutateCatalogCoroutine(string path, string method, string field, string value,
            Action onSuccess, Action<string> onError)
        {
            if (!IsLoggedIn) { onError?.Invoke("Not logged in"); yield break; }
            string url = $"{BaseUrl}/{path}";
            using (var req = new UnityWebRequest(url, method))
            {
                ApplyRequestTimeout(req, url);
                req.downloadHandler = new DownloadHandlerBuffer();
                if (field != null)
                {
                    string json = "{\"" + EscapeForJson(field) + "\":\"" + EscapeForJson(value) + "\"}";
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.SetRequestHeader("Authorization", "Bearer " + AccessToken);
                var guard = CaptureCredentialGuard();
                int epoch = Sandplay.Data.LocalAccountStorage.Epoch;
                yield return req.SendWebRequest();
                if (epoch != Sandplay.Data.LocalAccountStorage.Epoch || !guard()) yield break;
                if (req.result == UnityWebRequest.Result.Success) onSuccess?.Invoke();
                else onError?.Invoke(RequestFailure(req));
            }
        }

        // ── Response DTOs ─────────────────────────────────────────────────────

        [Serializable] private class TokenResponse { public string access; public string refresh; }
        [Serializable]
        private class MeResponse
        {
            public ProfileResponse profile;
            public int id;
            public string revenuecatAppUserId;
            public string email;
            public string name;
            public string user_type;
            public string access_source;
            public SubscriptionState subscriptionState;
            public string subscriptionExpireDate;  // ISO 8601 datetime string or null
            public bool must_change_password;
            public bool account_type_selected;
            public ManagedTherapistResponse managed_therapist;
        }
        [Serializable] private class ManagedTherapistResponse
        {
            public string organization_id, organization_name;
            public bool can_create_clients, can_create_schedules, can_host_sessions,
                can_create_reports, can_invite_clients, allow_external_contacts;
        }
        [Serializable] private class ProfileResponse { public string avatar_url; }
        [Serializable]
        private class FreeTierConsumeResponse
        {
            public bool allowed;
            public bool is_vip;
            public int used;
            public int limit;
            public int remaining;
        }
        [Serializable]
        private class AiReflectionResponse
        {
            public string reflection;
            public string model;
            public int tokens_used;
        }
        [Serializable] private class AgoraTokenResponse { public string token; public string app_id; public int expires_at; }
    }

    // ── Coroutine Result Classes ──────────────────────────────────────────────

    /// <summary>
    /// Coroutine result for file uploads. Yields until complete, then check Success/Error.
    /// </summary>
    public class FileUploadResult : CustomYieldInstruction
    {
        public bool Success { get; private set; }
        public string FileUrl { get; private set; }
        public string Sha256 { get; private set; }
        public string Error { get; private set; }
        private bool _done;

        public override bool keepWaiting => !_done;

        public FileUploadResult(BackendClient client, byte[] fileData, string fileName, string mimeType)
        {
            client.StartCoroutine(client.UploadFileCoroutine(fileData, fileName, mimeType,
                (url, sha256) =>
                {
                    Success = true;
                    FileUrl = url;
                    Sha256 = sha256;
                    _done = true;
                },
                err =>
                {
                    Success = false;
                    Error = err;
                    _done = true;
                }));
        }
    }

    /// <summary>
    /// Coroutine result for catalog object creation. Yields until complete, then check Success/Error.
    /// </summary>
    public class CatalogObjectResult : CustomYieldInstruction
    {
        public bool Success { get; private set; }
        public string ResponseJson { get; private set; }
        public string Error { get; private set; }
        private bool _done;

        public override bool keepWaiting => !_done;

        public CatalogObjectResult(BackendClient client, string catalogId, UI.CatalogObjectCreateRequest objectData)
        {
            client.StartCoroutine(client.CreateCatalogObjectCoroutine(catalogId, objectData,
                json =>
                {
                    Success = true;
                    ResponseJson = json;
                    _done = true;
                },
                err =>
                {
                    Success = false;
                    Error = err;
                    _done = true;
                }));
        }
    }

    /// <summary>
    /// Coroutine result for fetching user catalogs.
    /// </summary>
    public class UserCatalogsResult : CustomYieldInstruction
    {
        public bool Success { get; private set; }
        public List<UI.UserCatalog> Catalogs { get; private set; } = new List<UI.UserCatalog>();
        public string Error { get; private set; }
        private bool _done;

        public override bool keepWaiting => !_done;

        public UserCatalogsResult(BackendClient client)
        {
            client.StartCoroutine(client.FetchUserCatalogsCoroutine(
                json =>
                {
                    try
                    {
                        UI.UserCatalogsResponse response;
                        if (json.TrimStart().StartsWith("["))
                            response = JsonUtility.FromJson<UI.UserCatalogsResponse>($"{{\"catalogs\":{json}}}");
                        else
                        {
                            var page = JsonUtility.FromJson<UI.PaginatedUserCatalogsResponse>(json);
                            response = new UI.UserCatalogsResponse { catalogs = page?.results };
                        }
                        if (response != null && response.catalogs != null)
                        {
                            Catalogs = new List<UI.UserCatalog>(response.catalogs);
                        }
                        Success = true;
                    }
                    catch (Exception ex)
                    {
                        Success = false;
                        Error = $"Parse error: {ex.Message}";
                    }
                    _done = true;
                },
                err =>
                {
                    Success = false;
                    Error = err;
                    _done = true;
                }));
        }
    }

    /// <summary>
    /// Coroutine result for fetching catalog objects.
    /// </summary>
    public class CatalogObjectsResult : CustomYieldInstruction
    {
        public bool Success { get; private set; }
        public List<UI.CatalogObjectData> Objects { get; private set; } = new List<UI.CatalogObjectData>();
        public string Error { get; private set; }
        private bool _done;

        public override bool keepWaiting => !_done;

        public CatalogObjectsResult(BackendClient client, string catalogId)
        {
            client.StartCoroutine(client.FetchCatalogObjectsCoroutine(catalogId,
                json =>
                {
                    try
                    {
                        UI.CatalogObjectsResponse response;
                        if (json.TrimStart().StartsWith("["))
                            response = JsonUtility.FromJson<UI.CatalogObjectsResponse>($"{{\"objects\":{json}}}");
                        else
                        {
                            var page = JsonUtility.FromJson<UI.PaginatedCatalogObjectsResponse>(json);
                            response = new UI.CatalogObjectsResponse { objects = page?.results };
                        }
                        if (response != null && response.objects != null)
                        {
                            Objects = new List<UI.CatalogObjectData>(response.objects);
                        }
                        Success = true;
                    }
                    catch (Exception ex)
                    {
                        Success = false;
                        Error = $"Parse error: {ex.Message}";
                    }
                    _done = true;
                },
                err =>
                {
                    Success = false;
                    Error = err;
                    _done = true;
                }));
        }
    }

    /// <summary>
    /// Coroutine result for deleting catalog objects.
    /// </summary>
    public class DeleteObjectResult : CustomYieldInstruction
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }
        private bool _done;

        public override bool keepWaiting => !_done;

        public DeleteObjectResult(BackendClient client, string catalogId, string objectId)
        {
            client.StartCoroutine(client.DeleteCatalogObjectCoroutine(catalogId, objectId,
                () =>
                {
                    Success = true;
                    _done = true;
                },
                err =>
                {
                    Success = false;
                    Error = err;
                    _done = true;
                }));
        }
    }

    /// <summary>
    /// Coroutine result for creating a catalog.
    /// </summary>
    public class CreateCatalogResult : CustomYieldInstruction
    {
        public bool Success { get; private set; }
        public string CatalogId { get; private set; }
        public string Error { get; private set; }
        private bool _done;

        public override bool keepWaiting => !_done;

        public CreateCatalogResult(BackendClient client, string catalogName, string visibility)
        {
            client.StartCoroutine(client.CreateCatalogCoroutine(catalogName, visibility,
                catalogId =>
                {
                    Success = true;
                    CatalogId = catalogId;
                    _done = true;
                },
                err =>
                {
                    Success = false;
                    Error = err;
                    _done = true;
                }));
        }
    }

    public class CatalogUpdateResult : CustomYieldInstruction
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }
        private bool _done;
        public override bool keepWaiting => !_done;

        public CatalogUpdateResult(BackendClient client, string catalogId, string visibility)
        {
            client.StartCoroutine(client.UpdateCatalogVisibilityCoroutine(catalogId, visibility,
                () => { Success = true; _done = true; },
                error => { Error = error; Success = false; _done = true; }));
        }
    }
}
