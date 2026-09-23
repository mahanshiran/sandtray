using System;
using UnityEngine;
using Sandplay.Core;
using Sandplay.Data;

namespace Sandplay.AI
{
    public class AIAnalysisManager : MonoBehaviour
    {
        public static AIAnalysisManager Instance { get; private set; }
        public string LastModelUsed { get; private set; } = "";

        [Header("Analysis Configuration")]
        [SerializeField] private bool _useVisionModel = true;

        private void Awake()
        {
            Instance = this;
        }

        public void RequestAnalysis(AnalysisPayload payload, string screenshotBase64,
            Action<string> onResult, Action<string> onError,
            Action<AccessCapability> onQuotaChecked = null)
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                onError?.Invoke("Please sign in to use AI-assisted reflection.");
                return;
            }

            string image = _useVisionModel ? screenshotBase64 : "";
            string language = Localization.Current == Language.Chinese ? "zh" : "en";
            client.FetchAccessSnapshot(snapshot =>
            {
                var capability = AccessPolicy.Find(snapshot.capabilities, "ai.analyze");
                var decision = AccessPolicy.Evaluate(snapshot, "ai.analyze", 1, checkUsage: true);
                // Surface the authoritative counters even when the request is denied so
                // the error UI can explain a spent allowance instead of showing a dead end.
                onQuotaChecked?.Invoke(capability);
                if (decision != AccessDecision.Allowed)
                {
                    string message = decision == AccessDecision.NotIncluded
                        ? (language == "zh" ? "当前方案不包含 AI 分析。" : "Your current plan does not include AI analyses.")
                        : decision == AccessDecision.LimitReached
                            ? (language == "zh" ? "本月 AI 分析额度已用完。" : "Your AI analysis allowance is used up for this month.")
                            : (language == "zh" ? "暂时无法验证 AI 分析额度，请重试。" : "Your AI analysis allowance could not be verified. Please retry.");
                    onError?.Invoke(message);
                    return;
                }
                client.RequestAiReflection(payload, image, language,
                    (reflection, model) =>
                    {
                        LastModelUsed = model ?? "";
                        onResult?.Invoke(reflection);
                    },
                    onError);
            }, onError, force: true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
