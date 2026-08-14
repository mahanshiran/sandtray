using UnityEngine;
using UnityEngine.EventSystems;

namespace Sandplay.Core
{
    /// <summary>
    /// Attach to any UI panel to let the user drag it around within its parent
    /// canvas. Originally lived inside AgoraManager.cs alongside the video panel
    /// for which it was written; promoted to its own file so other panels can
    /// reuse it without depending on the Agora module.
    /// </summary>
    public class AgoraDragPanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform _rt;
        private Vector2 _dragOffset;

        private void Awake() => _rt = GetComponent<RectTransform>();

        public void OnBeginDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt.parent as RectTransform, e.position, e.pressEventCamera, out var lp);
            _dragOffset = _rt.anchoredPosition - lp;
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt.parent as RectTransform, e.position, e.pressEventCamera, out var lp);
            _rt.anchoredPosition = lp + _dragOffset;
        }
    }
}
