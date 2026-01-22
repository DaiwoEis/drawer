using System.Collections.Generic;
using UnityEngine;

namespace Game.Architecture.Events
{
    /// <summary>
    /// A ScriptableObject-based event channel that allows decoupled communication between systems.
    /// Listeners subscribe to this event, and broadcasters raise it.
    /// This prevents direct dependencies between sender and receiver.
    /// </summary>
    [CreateAssetMenu(menuName = "Events/Void Event Channel")]
    public class VoidEventChannelSO : ScriptableObject
    {
        private readonly List<System.Action> _listeners = new List<System.Action>();

        public void RaiseEvent()
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i]?.Invoke();
            }
        }

        public void RegisterListener(System.Action action)
        {
            if (!_listeners.Contains(action))
                _listeners.Add(action);
        }

        public void UnregisterListener(System.Action action)
        {
            if (_listeners.Contains(action))
                _listeners.Remove(action);
        }
    }
}
