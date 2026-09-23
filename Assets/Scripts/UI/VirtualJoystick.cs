using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Sandplay.UI
{
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _outerRing;
        private RectTransform _knob;
        private Vector2 _input;
        private float _radius;
        private int? _pointer;

        public Vector2 Direction => _input;

        public void Initialize(RectTransform outerRing, RectTransform knob)
        {
            _outerRing = outerRing;
            _knob = knob;
            _radius = outerRing.sizeDelta.x * 0.32f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointer.HasValue) return;
            _pointer = eventData.pointerId;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_pointer != eventData.pointerId) return;
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _outerRing, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                Vector2 clamped = Vector2.ClampMagnitude(localPoint - _outerRing.rect.center, _radius);
                _knob.anchoredPosition = clamped;
                _input = clamped.magnitude < _radius * .12f ? Vector2.zero : clamped / _radius;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_pointer != eventData.pointerId) return;
            OnDisable();
        }

        private void OnDisable()
        {
            _pointer = null;
            _input = Vector2.zero;
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        }
    }
}
