using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>
    /// Shared feedback for short, user-initiated operations that must temporarily
    /// prevent duplicate taps. Every operation has a deadline and late callbacks
    /// are rejected through <see cref="Ticket.TryComplete"/>.
    /// </summary>
    public sealed class BlockingOperationFeedback : MonoBehaviour
    {
        public sealed class Ticket
        {
            private BlockingOperationFeedback _owner;
            private readonly int _token;

            internal Ticket(BlockingOperationFeedback owner, int token)
            {
                _owner = owner;
                _token = token;
            }

            /// <summary>
            /// Removes the blocker and returns true only while this operation is
            /// still current. Call this before applying an asynchronous result.
            /// </summary>
            public bool TryComplete()
            {
                var owner = _owner;
                _owner = null;
                return owner != null && owner.Complete(_token);
            }

            public void Cancel()
            {
                var owner = _owner;
                _owner = null;
                owner?.Cancel(_token);
            }
        }

        private TMP_FontAsset _font;
        private Color _backdropColor;
        private Color _cardColor;
        private Color _borderColor;
        private Color _accentColor;
        private Color _textColor;
        private Action<Image> _cardStyler;
        private GameObject _overlay;
        private Coroutine _timeoutRoutine;
        private int _token;

        public void Configure(
            TMP_FontAsset font,
            Color backdropColor,
            Color cardColor,
            Color borderColor,
            Color accentColor,
            Color textColor,
            Action<Image> cardStyler = null)
        {
            _font = font;
            _backdropColor = backdropColor;
            _cardColor = cardColor;
            _borderColor = borderColor;
            _accentColor = accentColor;
            _textColor = textColor;
            _cardStyler = cardStyler;
        }

        public Ticket Begin(string message, float timeoutSeconds, Action onTimeout = null)
        {
            ClearCurrent();
            int operationToken = ++_token;
            BuildOverlay(string.IsNullOrWhiteSpace(message) ? "Loading…" : message);
            _timeoutRoutine = StartCoroutine(TimeoutAfter(
                operationToken,
                Mathf.Max(1f, timeoutSeconds),
                onTimeout));
            return new Ticket(this, operationToken);
        }

        private bool Complete(int operationToken)
        {
            if (operationToken != _token || _overlay == null) return false;
            ClearCurrent();
            return true;
        }

        private void Cancel(int operationToken)
        {
            if (operationToken == _token) ClearCurrent();
        }

        private IEnumerator TimeoutAfter(int operationToken, float seconds, Action onTimeout)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (operationToken != _token || _overlay == null) yield break;
            _timeoutRoutine = null;
            ClearCurrent();
            onTimeout?.Invoke();
        }

        private void BuildOverlay(string message)
        {
            var overlay = CreateRect(transform, "BlockingOperation", 0f, 0f, 1f, 1f);
            _overlay = overlay.gameObject;
            var blocker = overlay.gameObject.AddComponent<Image>();
            blocker.color = _backdropColor;
            blocker.raycastTarget = true;

            var card = CreateRect(overlay, "ProgressCard", .5f, .5f, .5f, .5f);
            card.sizeDelta = new Vector2(220f, 64f);
            card.anchoredPosition = Vector2.zero;
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = _cardColor;
            _cardStyler?.Invoke(cardImage);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = _borderColor;
            outline.effectDistance = new Vector2(1f, -1f);

            var spinner = CreateRect(card, "Spinner", .07f, .18f, .23f, .82f);
            var spinnerLabel = spinner.gameObject.AddComponent<TextMeshProUGUI>();
            spinnerLabel.font = _font;
            // Use an ASCII glyph so the indicator works with every bundled font.
            spinnerLabel.text = "+";
            spinnerLabel.fontSize = 25f;
            spinnerLabel.alignment = TextAlignmentOptions.Center;
            spinnerLabel.color = _accentColor;
            spinnerLabel.raycastTarget = false;
            spinner.gameObject.AddComponent<LoadingSpinner>();

            var labelRect = CreateRect(card, "Message", .29f, .12f, .93f, .88f);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = message;
            label.fontSize = 15f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 15f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = _textColor;
            label.raycastTarget = false;

            overlay.SetAsLastSibling();
        }

        private void ClearCurrent()
        {
            if (_timeoutRoutine != null)
            {
                StopCoroutine(_timeoutRoutine);
                _timeoutRoutine = null;
            }
            if (_overlay == null) return;
            var oldOverlay = _overlay;
            _overlay = null;
            oldOverlay.SetActive(false);
            if (Application.isPlaying) Destroy(oldOverlay);
            else DestroyImmediate(oldOverlay);
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void OnDisable() => ClearCurrent();
        private void OnDestroy() => ClearCurrent();
    }
}
