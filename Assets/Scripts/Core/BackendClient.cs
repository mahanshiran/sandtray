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
    /// Persists the JWT access token in PlayerPrefs.
    /// </summary>
    public class BackendClient : MonoBehaviour
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

        private const string BaseUrl = "https://api.sandtraypro.com/api";

        private const string PrefKeyToken = "backend_access_token";
        private const string PrefKeyRefreshToken = "backend_refresh_token";
        private const string PrefKeyName = "backend_user_name";
        private const string PrefKeyEmail = "backend_user_email";
        private const string PrefKeyUserId = "backend_user_id";
        private const string PrefKeyRevenueCatUserId = "backend_revenuecat_user_id";
        private const string PrefKeyUserType = "backend_user_type";
        private const string PrefKeySubscriptionExpires = "backend_subscription_expires";

        public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);
        public string AccessToken { get; private set; }
        private string RefreshToken { get; set; }
        public int UserId { get; private set; }
        public string UserName { get; private set; }
        public string UserEmail { get; private set; }
        public string UserType { get; private set; }  // "normal" | "psychologist" | "admin"
        public DateTime? SubscriptionExpiresAt { get; private set; }
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
            AccessToken = PlayerPrefs.GetString(PrefKeyToken, "");
            RefreshToken = PlayerPrefs.GetString(PrefKeyRefreshToken, "");
            UserId = PlayerPrefs.GetInt(PrefKeyUserId, 0);
            RevenueCatUserId = PlayerPrefs.GetString(PrefKeyRevenueCatUserId, "");
            UserName = PlayerPrefs.GetString(PrefKeyName, "");
            UserEmail = PlayerPrefs.GetString(PrefKeyEmail, "");
            UserType = PlayerPrefs.GetString(PrefKeyUserType, "");

            var expiresStr = PlayerPrefs.GetString(PrefKeySubscriptionExpires, "");
            if (!string.IsNullOrEmpty(expiresStr) && DateTime.TryParse(expiresStr, out DateTime expires))
                SubscriptionExpiresAt = expires;
            else
                SubscriptionExpiresAt = null;
        }

        private void SaveToPrefs()
        {
            PlayerPrefs.SetString(PrefKeyToken, AccessToken ?? "");
            PlayerPrefs.SetString(PrefKeyRefreshToken, RefreshToken ?? "");
            PlayerPrefs.SetInt(PrefKeyUserId, UserId);
            PlayerPrefs.SetString(PrefKeyRevenueCatUserId, RevenueCatUserId ?? "");
            PlayerPrefs.SetString(PrefKeyName, UserName ?? "");
            PlayerPrefs.SetString(PrefKeyEmail, UserEmail ?? "");
            PlayerPrefs.SetString(PrefKeyUserType, UserType ?? "");
            PlayerPrefs.SetString(PrefKeySubscriptionExpires,
                SubscriptionExpiresAt?.ToString("o") ?? "");
            PlayerPrefs.Save();
        }

        public void SignOut()
        {
            RevenueCatManager.Instance?.ClearUserIdentity();
            AccessToken = RefreshToken = UserName = UserEmail = UserType = "";
            UserId = 0;
            RevenueCatUserId = "";
            SubscriptionExpiresAt = null;
            SaveToPrefs();
        }

        // ── Public API calls ──────────────────────────────────────────────────

        public void Login(string email, string password,
            Action<string> onSuccess, Action<string> onError)
        {
            var body = $"{{\"email\":\"{Escape(email)}\",\"password\":\"{Escape(password)}\"}}";
            StartCoroutine(Post($"{BaseUrl}/auth/login/", body, null, json =>
            {
                var resp = JsonUtility.FromJson<TokenResponse>(json);
                if (string.IsNullOrEmpty(resp.access))
                {
                    onError?.Invoke("Invalid response from server.");
                    return;
                }
                AccessToken = resp.access;
                RefreshToken = resp.refresh ?? "";
                FetchMe(onSuccess, onError);
            }, onError));
        }

        public void Register(string email, string name, string password, string userType,
            Action<string> onSuccess, Action<string> onError)
        {
            var body = $"{{\"email\":\"{Escape(email)}\",\"name\":\"{Escape(name)}\"," +
                       $"\"password\":\"{Escape(password)}\",\"user_type\":\"{Escape(userType)}\"}}";
            StartCoroutine(Post($"{BaseUrl}/auth/register/", body, null, json =>
            {
                // After register, auto-login
                Login(email, password, onSuccess, onError);
            }, onError));
        }

        private void FetchMe(Action<string> onSuccess, Action<string> onError)
        {
            FetchMeInternal(onSuccess, onError, true);
        }

        private void FetchMeInternal(Action<string> onSuccess, Action<string> onError,
            bool allowRefresh)
        {
            StartCoroutine(Get($"{BaseUrl}/auth/me/", AccessToken, json =>
            {
                var me = JsonUtility.FromJson<MeResponse>(json);
                UserId = me.id;
                RevenueCatUserId = me.revenuecatAppUserId;
                UserName = me.name;
                UserEmail = me.email;
                UserType = me.user_type;

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

            StartCoroutine(Post($"{BaseUrl}/analysis/reflect/", body, AccessToken, json =>
            {
                var response = JsonUtility.FromJson<AiReflectionResponse>(json);
                if (response == null || string.IsNullOrEmpty(response.reflection))
                {
                    onError?.Invoke("The reflection service returned an empty response.");
                    return;
                }
                onSuccess?.Invoke(response.reflection, response.model);
            }, onError));
        }

        /// <summary>
        /// Save an AI-assisted reflection record to the cloud. Returns the new record UUID via onSuccess.
        /// </summary>
        public void SaveAnalysisRecord(string resultText, string screenshotB64, string modelUsed,
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
        /// Consume one monthly free-tier use. VIP users bypass the counter.
        /// </summary>
        public void ConsumeFreeFeature(string feature, Action<int> onAllowed,
            Action onLimitReached, Action<string> onError)
        {
            if (IsSubscribed)
            {
                onAllowed?.Invoke(-1);
                return;
            }
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
        public void FetchAgoraToken(string channelName, uint uid,
            Action<string, string> onSuccess, Action<string> onError)
        {
            FetchAgoraTokenInternal(channelName, uid, onSuccess, onError, true);
        }

        private void FetchAgoraTokenInternal(string channelName, uint uid,
            Action<string, string> onSuccess, Action<string> onError, bool allowRefresh)
        {
            if (!IsLoggedIn) { onError?.Invoke("Not logged in"); return; }

            var body = $"{{\"channel_name\":\"{Escape(channelName)}\",\"uid\":{uid},\"role\":1}}";
            StartCoroutine(Post($"{BaseUrl}/boards/agora-token/", body, AccessToken, json =>
            {
                var resp = JsonUtility.FromJson<AgoraTokenResponse>(json);
                if (string.IsNullOrEmpty(resp.token) || string.IsNullOrEmpty(resp.app_id))
                {
                    onError?.Invoke("Invalid token response from server.");
                    return;
                }
                onSuccess?.Invoke(resp.token, resp.app_id);
            }, error =>
            {
                if (allowRefresh && IsAuthenticationError(error))
                {
                    RefreshAccessToken(
                        () => FetchAgoraTokenInternal(
                            channelName, uid, onSuccess, onError, false),
                        onError);
                    return;
                }
                onError?.Invoke(error);
            }));
        }

        private void RefreshAccessToken(Action onSuccess, Action<string> onError)
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                SignOut();
                onError?.Invoke("Session expired. Please sign in again.");
                return;
            }

            var body = $"{{\"refresh\":\"{Escape(RefreshToken)}\"}}";
            StartCoroutine(Post($"{BaseUrl}/auth/token/refresh/", body, null, json =>
            {
                var response = JsonUtility.FromJson<TokenResponse>(json);
                if (response == null || string.IsNullOrEmpty(response.access))
                {
                    SignOut();
                    onError?.Invoke("Session expired. Please sign in again.");
                    return;
                }

                AccessToken = response.access;
                if (!string.IsNullOrEmpty(response.refresh))
                    RefreshToken = response.refresh;
                SaveToPrefs();
                onSuccess?.Invoke();
            }, _ =>
            {
                SignOut();
                onError?.Invoke("Session expired. Please sign in again.");
            }));
        }

        // ── HTTP helpers ──────────────────────────────────────────────────────

        private IEnumerator Post(string url, string jsonBody, string token,
            Action<string> onSuccess, Action<string> onError)
        {
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", "Bearer " + token);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success ||
                req.responseCode == 201)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                string msg = ExtractError(req.downloadHandler.text, req.responseCode);
                onError?.Invoke(msg);
            }
        }

        private IEnumerator Get(string url, string token,
            Action<string> onSuccess, Action<string> onError)
        {
            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", "Bearer " + token);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(req.downloadHandler.text);
            else
                onError?.Invoke(ExtractError(req.downloadHandler.text, req.responseCode));
        }

        private IEnumerator GetBytes(string url, string token,
            Action<byte[]> onSuccess, Action<string> onError)
        {
            var req = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", "Bearer " + token);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(req.downloadHandler.data);
            else
                onError?.Invoke(ExtractError(req.downloadHandler.text, req.responseCode));
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

            var req = UnityWebRequest.Post($"{BaseUrl}/catalogs/upload/", form);
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            yield return req.SendWebRequest();

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
                string msg = ExtractError(req.downloadHandler.text, req.responseCode);
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

            var req = new UnityWebRequest($"{BaseUrl}/catalogs/{catalogId}/objects/", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 201)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                string msg = ExtractError(req.downloadHandler.text, req.responseCode);
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

            var req = UnityWebRequest.Get($"{BaseUrl}/catalogs/");
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                string msg = ExtractError(req.downloadHandler.text, req.responseCode);
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

            var req = UnityWebRequest.Get($"{BaseUrl}/catalogs/{catalogId}/objects/");
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                string msg = ExtractError(req.downloadHandler.text, req.responseCode);
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

            var req = UnityWebRequest.Delete($"{BaseUrl}/catalogs/{catalogId}/objects/{objectId}/");
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 204)
            {
                onSuccess?.Invoke();
            }
            else
            {
                string msg = ExtractError(req.downloadHandler.text, req.responseCode);
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

            var req = UnityWebRequest.Put($"{BaseUrl}/catalogs/", bodyRaw);
            req.method = "POST";  // Override PUT with POST
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            yield return req.SendWebRequest();

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
                string msg = ExtractError(req.downloadHandler.text, req.responseCode);
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
            var req = new UnityWebRequest($"{BaseUrl}/catalogs/{catalogId}/", "PATCH");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke();
            else
                onError?.Invoke(ExtractError(req.downloadHandler.text, req.responseCode));
        }

        // ── Response DTOs ─────────────────────────────────────────────────────

        [Serializable] private class TokenResponse { public string access; public string refresh; }
        [Serializable]
        private class MeResponse
        {
            public int id;
            public string revenuecatAppUserId;
            public string email;
            public string name;
            public string user_type;
            public string subscriptionExpireDate;  // ISO 8601 datetime string or null
        }
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
