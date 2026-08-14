using UnityEngine;

namespace Sandplay.UI
{
    /// <summary>
    /// Adjusts a RectTransform to stay within Screen.safeArea.
    /// Attach to a full-screen panel that parents all UI content.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _lastSafeArea;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea)
                ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            var safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // On iOS landscape, the top edge has no safe area inset but sits flush
            // against the bezel. Mirror the bottom inset to the top so all sides match.
#if UNITY_IOS
            float bottomInset = anchorMin.y;
            float topInset = 1f - anchorMax.y;
            if (topInset < bottomInset)
                anchorMax.y = 1f - bottomInset;
#endif

            _rt.anchorMin = anchorMin;
            _rt.anchorMax = anchorMax;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
