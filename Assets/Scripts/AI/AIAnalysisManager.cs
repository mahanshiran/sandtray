using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Sandplay.Core;
using Sandplay.Data;

namespace Sandplay.AI
{
    public interface IAnalysisProvider
    {
        void Analyze(AnalysisPayload payload, string screenshotBase64, Action<string> onResult, Action<string> onError);
    }

    public class AIAnalysisManager : MonoBehaviour
    {
        public static AIAnalysisManager Instance { get; private set; }

        [Header("Analysis Configuration")]
        [SerializeField] private bool _useVisionModel = true;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Set API key at runtime (useful when user provides it later).
        /// </summary>
        public void SetApiKey(string apiKey)
        {
            if (BailianClient.Instance != null)
            {
                BailianClient.Instance.SetApiKey(apiKey);
            }
        }

        public void RequestAnalysis(AnalysisPayload payload, string screenshotBase64, Action<string> onResult, Action<string> onError)
        {
            RequestBailianAnalysis(payload, screenshotBase64, onResult, onError);
        }

        private void RequestBailianAnalysis(AnalysisPayload payload, string screenshotBase64, Action<string> onResult, Action<string> onError)
        {
            Debug.Log($"[AIAnalysisManager] RequestBailianAnalysis called. Use Vision: {_useVisionModel}, Screenshot: {screenshotBase64?.Length ?? 0} chars");

            if (BailianClient.Instance == null)
            {
                string error = "BailianClient not found. Please ensure BailianClient component exists in scene.";
                Debug.LogError($"[AIAnalysisManager] {error}");
                onError?.Invoke(error);
                return;
            }

            string systemPrompt = GetSystemPrompt();
            string userPrompt = BuildPrompt(JsonUtility.ToJson(payload, true));

            Debug.Log($"[AIAnalysisManager] System prompt length: {systemPrompt.Length}, User prompt length: {userPrompt.Length}");

            if (_useVisionModel && !string.IsNullOrEmpty(screenshotBase64))
            {
                Debug.Log($"[AIAnalysisManager] Using VISION model for analysis");
                // Use vision model for richer analysis
                BailianClient.Instance.SendVisionRequest(systemPrompt, userPrompt, screenshotBase64, onResult, onError);
            }
            else
            {
                Debug.Log($"[AIAnalysisManager] Using TEXT model for analysis");
                // Text-only analysis
                BailianClient.Instance.SendTextRequest(systemPrompt, userPrompt, onResult, onError);
            }
        }



        private string GetSystemPrompt()
        {
            // Determine response language based on user's current language setting
            string languageInstruction = Localization.Current == Language.Chinese
                ? "You MUST respond in CHINESE (中文). Use natural, professional Chinese language throughout your analysis."
                : "You MUST respond in ENGLISH. Use clear, professional English language throughout your analysis.";

            return $@"You are an expert sandplay/Hakoniwa therapy analyst trained in Jungian psychology and Dora Kalff's sandplay therapy method.
{languageInstruction}

Your task: Analyze the sandbox arrangement and provide meaningful therapeutic insights. Focus on psychological interpretation — what the scene reveals about the person's inner world, emotions, conflicts, developmental stage, and potential for growth.

Key analytical frameworks:
- **Jungian Psychology**: Identify archetypes (shadow, anima/animus, self, persona), individuation process, symbolic representations of the unconscious
- **Dora Kalff's Stages**: Consider developmental stages (animal-vegetative, fighting, adaptation to collective)
- **Spatial Symbolism**:
  • Left side = unconscious mind, past, feminine, receptive
  • Right side = conscious mind, future, masculine, active
  • Center = ego, self, core identity
  • Near edge = immediate concerns, accessible feelings
  • Far edge = distant, avoided, or aspirational themes
  • Corners = extreme positions, isolation, or protection
- **Object Relationships**: Notice proximity, orientation, barriers, groups, isolation
- **Sand Terrain**: Mountains = obstacles or achievements, valleys = depression or shelter, flat = stability or emptiness, patterns = organization or chaos

Response format rules:
- Plain text only — NO markdown formatting (no ##, **, ---, bullets, or numbered lists)
- Write in flowing, connected paragraphs with natural transitions
- Do NOT simply describe what objects are present — the user already knows what they placed
- Move directly to psychological meaning and therapeutic insights
- Use gentle, empathic language that invites reflection rather than labeling
- Frame observations as possibilities and themes, not definitive diagnoses
- Include 4-5 concrete therapeutic questions or prompts at the end to deepen exploration
- Aim for 300-500 words of substantive psychological insight

Be warm, curious, and professionally careful in your interpretations.";
        }

        private string BuildPrompt(string payloadJson)
        {
            return $@"Analyze this sandplay session and tell me what it reveals about the player psychologically. Plain text only, no formatting.

Session Data:
{payloadJson}";
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
