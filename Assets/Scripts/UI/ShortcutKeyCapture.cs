using System;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.UI
{
    public sealed class ShortcutKeyCapture : MonoBehaviour
    {
        private bool registered;
        private int startedFrame;
        private Action<ShortcutBinding> captured;
        private Action cancelled;
        public bool IsListening => captured != null;
        private static readonly KeyCode[] Keys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        public void BeginSession()
        {
            if (registered) return;
            registered = true;
            KeyboardShortcuts.EditorCount++;
        }
        public void Listen(Action<ShortcutBinding> onCaptured, Action onCancelled)
        {
            captured = onCaptured; cancelled = onCancelled; startedFrame = Time.frameCount;
        }
        public void StopListening() { captured = null; cancelled = null; }
        private void Update()
        {
            if (captured == null || Time.frameCount <= startedFrame || !Input.anyKeyDown) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { var callback = cancelled; StopListening(); callback?.Invoke(); return; }
            foreach (var key in Keys)
            {
                if ((int)key < (int)KeyCode.Backspace || (int)key >= (int)KeyCode.Mouse0) continue;
                if (key == KeyCode.LeftControl || key == KeyCode.RightControl || key == KeyCode.LeftCommand || key == KeyCode.RightCommand ||
                    key == KeyCode.LeftShift || key == KeyCode.RightShift || key == KeyCode.LeftAlt || key == KeyCode.RightAlt) continue;
                if (Input.GetKeyDown(key)) { captured?.Invoke(KeyboardShortcuts.Modifiers(key)); return; }
            }
        }
        public void EndSession()
        {
            StopListening();
            if (!registered) return;
            registered = false;
            KeyboardShortcuts.EditorCount = Mathf.Max(0, KeyboardShortcuts.EditorCount - 1);
        }
        private void OnDisable() { EndSession(); }
        private void OnDestroy() { EndSession(); }
    }
}
