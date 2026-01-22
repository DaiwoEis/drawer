using System.Collections.Generic;
using UnityEngine;

namespace Game.Architecture.Pooling
{
    /// <summary>
    /// A generic Object Pool class for MonoBehaviours.
    /// Can be used for particles, projectiles, enemies, etc.
    /// </summary>
    /// <typeparam name="T">The component type to pool.</typeparam>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Queue<T> _pool = new Queue<T>();

        public ObjectPool(T prefab, int initialSize = 10, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;

            for (int i = 0; i < initialSize; i++)
            {
                T instance = Object.Instantiate(_prefab, _parent);
                instance.gameObject.SetActive(false);
                _pool.Enqueue(instance);
            }
        }

        public T Get()
        {
            if (_pool.Count == 0)
            {
                T instance = Object.Instantiate(_prefab, _parent);
                return instance;
            }
            
            T pooledInstance = _pool.Dequeue();
            pooledInstance.gameObject.SetActive(true);
            return pooledInstance;
        }

        public void Return(T instance)
        {
            instance.gameObject.SetActive(false);
            _pool.Enqueue(instance);
        }
    }
}
