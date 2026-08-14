using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Tiny helper that lets background-thread code (e.g. SDK callbacks) marshal
    /// work back onto the Unity main thread. Originally bundled inside
    /// AgoraManager.cs because Agora's <c>IRtcEngineEventHandler</c> needed it;
    /// promoted to its own file so the rest of the codebase (notably
    /// NetworkBootstrapper's relay loop) can use it too instead of every caller
    /// rolling its own queue.
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _queue = new Queue<Action>();
        private static UnityMainThreadDispatcher _inst;

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            if (_inst == null)
            {
                var go = new GameObject("UnityMainThreadDispatcher");
                _inst = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            lock (_queue) _queue.Enqueue(action);
        }

        private void Update()
        {
            while (true)
            {
                Action act;
                lock (_queue)
                {
                    if (_queue.Count == 0) break;
                    act = _queue.Dequeue();
                }

                try
                {
                    act?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
