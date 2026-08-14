using UnityEngine;
using Sandplay.Camera;

namespace Sandplay.UI
{
    public class WalkJoystickFeeder : MonoBehaviour
    {
        private VirtualJoystick _moveJoystick;

        public void Initialize(VirtualJoystick move)
        {
            _moveJoystick = move;
        }

        private void Update()
        {
            if (WalkModeController.Instance == null || !WalkModeController.Instance.IsActive) return;
            if (!WalkModeController.IsTouchDevice) return;

            if (_moveJoystick != null)
                WalkModeController.Instance.SetMoveInput(_moveJoystick.Direction);
        }
    }
}
