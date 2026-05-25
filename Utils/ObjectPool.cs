using System;
using System.Collections.Generic;

namespace TacticalDefenseGame.Utils
{
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool = new();
        private readonly Func<T> _factory;

        public ObjectPool(Func<T> factory, int initialCapacity = 10)
        {
            _factory = factory;
            for (int i = 0; i < initialCapacity; i++)
            {
                _pool.Push(_factory());
            }
        }

        public T Get()
        {
            return _pool.Count > 0 ? _pool.Pop() : _factory();
        }

        public void Return(T obj)
        {
            _pool.Push(obj);
        }
    }
}
