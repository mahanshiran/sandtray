using System;
using System.Collections;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Sandplay.AI
{
    public class BailianClient : MonoBehaviour
    {
        public static BailianClient Instance { get; private set; }

        [Header("Aliyun Bailian Configuration")]
        [SerializeField] private string _apiKey = "";
        [Tooltip("e.g., qwen-vl-plus for vision, qwen-plus for text")]
        [SerializeField] private string _model = "qwen-plus";

        private const string DASHSCOPE_ENDPOINT = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";

        private static HttpClient _httpClient;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // Force TLS 1.2
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;

                // Create HttpClient with SSL bypass handler
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true;
                _httpClient = new HttpClient(handler);
                _httpClient.Timeout = TimeSpan.FromSeconds(60);

                Debug.Log("[BailianClient] Instance created, HttpClient ready");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey ?? "";
            Debug.Log($"[BailianClient] API key set ({_apiKey.Length} chars)");
        }

        public void SendTextRequest(string systemPrompt, string userPrompt, Action<string> onSuccess, Action<string> onError)
        {
            Debug.Log($"[BailianClient] SendTextRequest called. Model: {_model}, Key length: {_apiKey?.Length ?? 0}");
            if (string.IsNullOrEmpty(_apiKey))
            {
                onError?.Invoke("Bailian API key not configured.");
                return;
            }
            StartCoroutine(DoRequest(systemPrompt, userPrompt, onSuccess, onError));
        }

        public void SendVisionRequest(string systemPrompt, string userPrompt, string imageBase64, Action<string> onSuccess, Action<string> onError)
        {
            Debug.Log($"[BailianClient] SendVisionRequest called. Image: {imageBase64?.Length ?? 0} chars");
            if (string.IsNullOrEmpty(_apiKey))
            {
                onError?.Invoke("Bailian API key not configured.");
                return;
            }
            if (string.IsNullOrEmpty(imageBase64))
            {
                Debug.Log("[BailianClient] No image provided, falling back to text");
                SendTextRequest(systemPrompt, userPrompt, onSuccess, onError);
                return;
            }
            StartCoroutine(DoVisionRequest(systemPrompt, userPrompt, imageBase64, onSuccess, onError));
        }

        private IEnumerator DoRequest(string systemPrompt, string userPrompt, Action<string> onSuccess, Action<string> onError)
        {
            string json = BuildJson(systemPrompt, userPrompt);
            Debug.Log($"[BailianClient] JSON built ({json.Length} chars)");
            Debug.Log($"[BailianClient] POST to {DASHSCOPE_ENDPOINT}");

            // Use System.Net.Http.HttpClient instead of UnityWebRequest for proper SSL
            Task<string> task = null;
            string errorMsg = null;

            try
            {
                task = SendHttpRequest(json);
            }
            catch (Exception e)
            {
                errorMsg = $"Failed to start request: {e.Message}";
                Debug.LogError($"[BailianClient] {errorMsg}");
                onError?.Invoke(errorMsg);
                yield break;
            }

            // Wait for the async task to complete
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                errorMsg = $"Request failed: {task.Exception?.InnerException?.Message ?? task.Exception?.Message}";
                Debug.LogError($"[BailianClient] {errorMsg}");
                onError?.Invoke(errorMsg);
                yield break;
            }

            string responseBody = task.Result;
            Debug.Log($"[BailianClient] Response ({responseBody.Length} chars)");
            if (responseBody.Length > 0)
                Debug.Log($"[BailianClient] Body: {responseBody.Substring(0, Mathf.Min(500, responseBody.Length))}");

            // Check for API error responses
            if (responseBody.Contains("\"error\"") || responseBody.Contains("\"code\""))
            {
                // Could be an error — log it but still try to parse
                Debug.LogWarning($"[BailianClient] Response may contain error: {responseBody.Substring(0, Mathf.Min(300, responseBody.Length))}");
            }

            string content = ExtractContent(responseBody);
            if (!string.IsNullOrEmpty(content))
            {
                Debug.Log($"[BailianClient] SUCCESS! Content: {content.Length} chars");
                onSuccess?.Invoke(content);
            }
            else
            {
                errorMsg = $"Could not parse API response:\n{responseBody}";
                Debug.LogError($"[BailianClient] {errorMsg}");
                onError?.Invoke(errorMsg);
            }
        }

        private async Task<string> SendHttpRequest(string jsonBody)
        {
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, DASHSCOPE_ENDPOINT);
            requestMsg.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            Debug.Log("[BailianClient] Sending via HttpClient...");
            var response = await _httpClient.SendAsync(requestMsg);
            string body = await response.Content.ReadAsStringAsync();

            Debug.Log($"[BailianClient] HTTP {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"HTTP {(int)response.StatusCode}: {body}");
            }

            return body;
        }

        private IEnumerator DoVisionRequest(string systemPrompt, string userPrompt, string imageBase64, Action<string> onSuccess, Action<string> onError)
        {
            string json = BuildVisionJson(systemPrompt, userPrompt, imageBase64);
            Debug.Log($"[BailianClient] Vision JSON built ({json.Length} chars), using qwen-vl-plus");

            Task<string> task = null;
            string errorMsg = null;

            try
            {
                task = SendHttpRequest(json);
            }
            catch (Exception e)
            {
                errorMsg = $"Failed to start vision request: {e.Message}";
                Debug.LogError($"[BailianClient] {errorMsg}");
                onError?.Invoke(errorMsg);
                yield break;
            }

            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
            {
                errorMsg = $"Vision request failed: {task.Exception?.InnerException?.Message ?? task.Exception?.Message}";
                Debug.LogError($"[BailianClient] {errorMsg}");
                onError?.Invoke(errorMsg);
                yield break;
            }

            string responseBody = task.Result;
            Debug.Log($"[BailianClient] Vision response ({responseBody.Length} chars)");

            string content = ExtractContent(responseBody);
            if (!string.IsNullOrEmpty(content))
            {
                Debug.Log($"[BailianClient] VISION SUCCESS! Content: {content.Length} chars");
                onSuccess?.Invoke(content);
            }
            else
            {
                errorMsg = $"Could not parse vision response:\n{responseBody}";
                Debug.LogError($"[BailianClient] {errorMsg}");
                onError?.Invoke(errorMsg);
            }
        }

        private string BuildJson(string systemPrompt, string userPrompt)
        {
            string sysEsc = EscapeJson(systemPrompt);
            string usrEsc = EscapeJson(userPrompt);
            string modelEsc = EscapeJson(_model);

            // OpenAI-compatible format for /compatible-mode/v1/chat/completions
            return "{" +
                "\"model\":\"" + modelEsc + "\"," +
                "\"messages\":[" +
                    "{\"role\":\"system\",\"content\":\"" + sysEsc + "\"}," +
                    "{\"role\":\"user\",\"content\":\"" + usrEsc + "\"}" +
                "]," +
                "\"max_tokens\":2000," +
                "\"temperature\":0.7," +
                "\"top_p\":0.8" +
            "}";
        }

        private string BuildVisionJson(string systemPrompt, string userPrompt, string imageBase64)
        {
            string sysEsc = EscapeJson(systemPrompt);
            string usrEsc = EscapeJson(userPrompt);

            // OpenAI-compatible vision format with qwen-vl-plus
            return "{" +
                "\"model\":\"qwen-vl-plus\"," +
                "\"messages\":[" +
                    "{\"role\":\"system\",\"content\":\"" + sysEsc + "\"}," +
                    "{\"role\":\"user\",\"content\":[" +
                        "{\"type\":\"text\",\"text\":\"" + usrEsc + "\"}," +
                        "{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/png;base64," + imageBase64 + "\"}}" +
                    "]}" +
                "]," +
                "\"max_tokens\":2000" +
            "}";
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private string ExtractContent(string json)
        {
            // OpenAI-compatible format: { "choices": [{ "message": { "content": "..." } }] }
            // Also handles DashScope format: { "output": { "choices": [...] } }

            // Try JsonUtility with OpenAI format first
            try
            {
                var resp = JsonUtility.FromJson<OpenAIResponse>(json);
                if (resp?.choices != null && resp.choices.Length > 0
                    && resp.choices[0].message != null
                    && !string.IsNullOrEmpty(resp.choices[0].message.content))
                {
                    return resp.choices[0].message.content;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BailianClient] OpenAI format parse failed: {e.Message}");
            }

            // Try DashScope native format
            try
            {
                var resp = JsonUtility.FromJson<DashScopeResponse>(json);
                if (resp?.output?.choices != null && resp.output.choices.Length > 0
                    && resp.output.choices[0].message != null
                    && !string.IsNullOrEmpty(resp.output.choices[0].message.content))
                {
                    return resp.output.choices[0].message.content;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BailianClient] DashScope format parse failed: {e.Message}");
            }

            // Fallback: manual string extraction for "content":"..."
            Debug.Log("[BailianClient] Using manual JSON extraction...");
            int choicesIdx = json.IndexOf("\"choices\"", StringComparison.Ordinal);
            if (choicesIdx < 0) return null;

            int contentIdx = json.IndexOf("\"content\"", choicesIdx, StringComparison.Ordinal);
            if (contentIdx < 0) return null;

            int colonIdx = json.IndexOf(':', contentIdx + 9);
            if (colonIdx < 0) return null;

            int startQuote = -1;
            for (int i = colonIdx + 1; i < json.Length; i++)
            {
                if (json[i] == '"') { startQuote = i; break; }
                if (json[i] != ' ' && json[i] != '\t' && json[i] != '\n' && json[i] != '\r') return null;
            }
            if (startQuote < 0) return null;

            var result = new StringBuilder();
            for (int i = startQuote + 1; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': result.Append('"'); i++; break;
                        case '\\': result.Append('\\'); i++; break;
                        case 'n': result.Append('\n'); i++; break;
                        case 'r': result.Append('\r'); i++; break;
                        case 't': result.Append('\t'); i++; break;
                        default: result.Append('\\'); result.Append(next); i++; break;
                    }
                }
                else if (json[i] == '"') return result.ToString();
                else result.Append(json[i]);
            }
            return null;
        }

        // Response structures
        // OpenAI-compatible format: { "choices": [{ "message": { "content": "..." } }] }
        [Serializable] private class OpenAIResponse { public ChoiceItem[] choices; public string id; }
        [Serializable] private class ChoiceItem { public string finish_reason; public MsgItem message; }
        [Serializable] private class MsgItem { public string role; public string content; }

        // DashScope native format: { "output": { "choices": [...] } }
        [Serializable] private class DashScopeResponse { public DashScopeOutput output; public string request_id; }
        [Serializable] private class DashScopeOutput { public ChoiceItem[] choices; }
    }
}
