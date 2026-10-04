using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;

namespace Sandplay.UI
{
    // A persistent edge marker remains distinguishable from a button's hover tint.
    public class ActiveToolIndicator : MonoBehaviour
    {
        public ToolMode Mode;
        public bool FollowToolMode = true;
        private Image _marker;
        private Outline _outline;

        private void Awake()
        {
            var go = new GameObject("Active tool", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            _marker = go.GetComponent<Image>();
            _marker.color = new Color(.2f, 1f, .85f);
            _marker.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, .15f); rect.anchorMax = new Vector2(0, .85f);
            rect.pivot = new Vector2(0, .5f); rect.sizeDelta = new Vector2(4, 0);
            rect.anchoredPosition = new Vector2(3, 0);
            _outline = GetComponent<Outline>();
            if (_outline == null) _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = _marker.color;
            _outline.effectDistance = new Vector2(1.5f, -1.5f);
            SetActiveState(false);
        }

        private void LateUpdate()
        {
            if (FollowToolMode)
                SetActiveState(GameManager.Instance != null && GameManager.Instance.CurrentTool == Mode);
        }

        public void SetActiveState(bool active)
        {
            if (_marker != null) _marker.enabled = active;
            if (_outline != null) _outline.enabled = active;
        }
    }
}
