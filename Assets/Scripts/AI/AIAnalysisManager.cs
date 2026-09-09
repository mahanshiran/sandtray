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
            Action<string> onResult, Action<string> onError)
        {
            var client = BackendClient.Instance;
            if (client == null || !client.IsLoggedIn)
            {
                onError?.Invoke("Please sign in to use AI-assisted reflection.");
                return;
            }

            string image = _useVisionModel ? screenshotBase64 : "";
            string language = Localization.Current == Language.Chinese ? "zh" : "en";
            client.RequestAiReflection(payload, image, language,
                (reflection, model) =>
                {
                    LastModelUsed = model ?? "";
                    onResult?.Invoke(reflection);
                },
                onError);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
