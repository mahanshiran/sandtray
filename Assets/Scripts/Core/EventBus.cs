using System;
using System.Collections.Generic;

namespace Sandplay.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _listeners = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_listeners.ContainsKey(type))
                _listeners[type] = new List<Delegate>();
            _listeners[type].Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_listeners.ContainsKey(type))
                _listeners[type].Remove(handler);
        }

        public static void Publish<T>(T evt)
        {
            var type = typeof(T);
            if (!_listeners.ContainsKey(type)) return;
            foreach (var handler in _listeners[type].ToArray())
            {
                try
                {
                    ((Action<T>)handler)(evt);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        public static void Clear()
        {
            _listeners.Clear();
        }
    }
}
