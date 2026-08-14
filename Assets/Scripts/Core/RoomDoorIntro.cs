using System.Collections;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Hinged therapy-room door used by the board-enter intro.
    /// </summary>
    public class RoomDoorIntro : MonoBehaviour
    {
        public Transform Hinge;
        public Vector3 DoorwayCenter;
        public Vector3 OutwardNormal = Vector3.back; // front wall: outside is -Z (matches overview cam)
        public float OpenAngle = 92f;
        public float EyeHeight = 1.6f;

        private float _currentAngle;
        private Coroutine _anim;

        public Vector3 OutsidePoint =>
            DoorwayCenter + OutwardNormal * Mathf.Max(4f, Mathf.Abs(EyeHeight) * 0.4f + 3f)
            + Vector3.up * (EyeHeight - DoorwayCenter.y + 0.35f);

        public Vector3 ThresholdPoint =>
            DoorwayCenter + Vector3.up * (EyeHeight - DoorwayCenter.y + 0.2f);

        public void SnapClosed()
        {
            SetAngle(0f);
        }

        public IEnumerator Open(float duration)
        {
            yield return AnimateAngle(_currentAngle, OpenAngle, duration);
        }

        public IEnumerator Close(float duration)
        {
            yield return AnimateAngle(_currentAngle, 0f, duration);
        }

        private IEnumerator AnimateAngle(float from, float to, float duration)
        {
            if (_anim != null) StopCoroutine(_anim);
            duration = Mathf.Max(0.05f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Quintic smoothstep for softer door motion
                float ease = t * t * t * (t * (t * 6f - 15f) + 10f);
                SetAngle(Mathf.Lerp(from, to, ease));
                yield return null;
            }
            SetAngle(to);
        }

        private void SetAngle(float angle)
        {
            _currentAngle = angle;
            if (Hinge != null)
                Hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
