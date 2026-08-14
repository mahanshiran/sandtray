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

        public Vector2 Direction => _input;

        public void Initialize(RectTransform outerRing, RectTransform knob)
        {
            _outerRing = outerRing;
            _knob = knob;
            _radius = outerRing.sizeDelta.x * 0.5f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _outerRing, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                Vector2 clamped = Vector2.ClampMagnitude(localPoint, _radius);
                _knob.anchoredPosition = clamped;
                _input = clamped / _radius;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _input = Vector2.zero;
            _knob.anchoredPosition = Vector2.zero;
        }
    }
}
