using System.Collections.Generic;
using UnityEngine;

namespace Game.Architecture.Events
{
    /// <summary>
    /// A generic ScriptableObject-based event channel.
    /// Use this for events that need to pass data (e.g., float for health, int for score).
    /// </summary>
    /// <typeparam name="T">The type of data to pass.</typeparam>
    public abstract class GenericEventChannelSO<T> : ScriptableObject
    {
        private readonly List<System.Action<T>> _listeners = new List<System.Action<T>>();

        public void RaiseEvent(T value)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i]?.Invoke(value);
            }
        }

        public void RegisterListener(System.Action<T> action)
        {
            if (!_listeners.Contains(action))
                _listeners.Add(action);
        }

        public void UnregisterListener(System.Action<T> action)
        {
            if (_listeners.Contains(action))
                _listeners.Remove(action);
        }
    }
}
