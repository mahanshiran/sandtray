using TMPro;
using UnityEngine;
using System;
using Sandplay.Core;

namespace Sandplay.UI
{
    /// <summary>Elapsed time in this live session, independent of game time scale.</summary>
    public sealed class LiveSessionTimer : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private long previousSecond = -1;
        private float offlineSeconds;
        private float idleSeconds;
        private bool focused = true;
        private int recordedMinutes;

        public void Initialize(TMP_FontAsset font)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -10);
            rect.sizeDelta = new Vector2(160, 32);
            label = gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 20;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.color = Color.white;
            label.outlineWidth = .15f;
            label.outlineColor = new Color32(15, 20, 24, 220);
            label.enabled = false;
        }

        private void Update()
        {
            if (label == null) return;
            var network = NetworkBootstrapper.Instance;
            bool visible = network != null && network.IsOnline;
            if (!visible && focused)
            {
                idleSeconds += Time.unscaledDeltaTime;
                // Offline usage is active only while the app is foregrounded and
                // the user has interacted recently. This timer is intentionally
                // visible so the eventual server reconciliation is understandable.
                if (idleSeconds <= 600f)
                {
                    offlineSeconds += Time.unscaledDeltaTime;
                    int minutes=Mathf.FloorToInt(offlineSeconds/60f);
                    while(recordedMinutes<minutes)
                    {
                        recordedMinutes++;
                        BackendClient.Instance?.RecordOfflineMinute(Guid.NewGuid().ToString(),null,err=>Debug.LogWarning("Offline usage was not recorded: "+err));
                    }
                }
            }
            else if (visible) idleSeconds = 0f;
            label.enabled = visible || offlineSeconds > 0f;
            if (!label.enabled) return;
            long seconds = visible ? (long)network.SessionElapsedSeconds : (long)offlineSeconds;
            if (seconds == previousSecond) return;
            previousSecond = seconds;
            label.text = FormatElapsed(seconds);
        }

        private void OnApplicationFocus(bool value) { focused = value; if (!value) idleSeconds = 0f; }
        private void OnApplicationPause(bool value) { focused = !value; if (value) idleSeconds = 0f; }

        public static string FormatElapsed(long seconds)
        {
            seconds = System.Math.Max(0, seconds);
            return $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
        }
    }
}
