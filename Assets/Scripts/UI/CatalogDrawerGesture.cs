using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sandplay.UI
{
    /// <summary>
    /// Recognizes horizontal swipes on the catalog drawer handle. A left swipe
    /// opens the drawer; a right swipe closes it.
    /// </summary>
    public sealed class CatalogDrawerGesture : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float TriggerDistance = 28f;
        private Action _open;
        private Action _close;
        private Vector2 _start;
        private bool _tracking;
        private bool _triggered;

        public void Initialize(Action open, Action close)
        {
            _open = open;
            _close = close;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _start = eventData.position;
            _tracking = true;
            _triggered = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_tracking || _triggered) return;
            Vector2 delta = eventData.position - _start;
            if (Mathf.Abs(delta.x) < TriggerDistance || Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) return;

            _triggered = true;
            if (delta.x < 0f) _open?.Invoke();
            else _close?.Invoke();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _tracking = false;
            _triggered = false;
        }
    }
}
