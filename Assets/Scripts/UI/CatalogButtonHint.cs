using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;

namespace Sandplay.UI
{
    /// <summary>
    /// A quiet first-use cue for the sandbox catalog button. It runs only for
    /// editors and dismisses permanently as soon as the button is used.
    /// </summary>
    public sealed class CatalogButtonHint : MonoBehaviour
    {
        private Image _target;
        private Color _baseColor;
        private Vector3 _baseScale;
        private bool _dismissed;
        private bool _running;

        public void Initialize(Image target)
        {
            _target = target;
            _baseColor = target != null ? target.color : Color.white;
            _baseScale = transform.localScale;
            _dismissed = false;
            _running = IsEditor();
        }

        public void Dismiss()
        {
            if (_dismissed) return;
            _dismissed = true;
            _running = false;
            Restore();
        }

        private void Update()
        {
            if (!_running || _dismissed) return;
            if (!IsEditor())
            {
                Dismiss();
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * .5f;
            if (_target != null)
            {
                // Highlight the handle without changing its intended opacity.
                var highlight = new Color(1f, 1f, 1f, _baseColor.a);
                _target.color = Color.Lerp(_baseColor, highlight, .06f + pulse * .10f);
            }
            transform.localScale = _baseScale * (1f + pulse * .018f);
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (_target != null) _target.color = _baseColor;
            transform.localScale = _baseScale;
        }

        private static bool IsEditor()
        {
            return GameManager.Instance == null || !GameManager.Instance.IsSpectator;
        }
    }
}
