using System;
using System.Collections.Generic;

namespace TacticalDefenseGame.Utils
{
    public class PriorityQueue<T> where T : IComparable<T>
    {
        private List<T> _data = new();

        public int Count => _data.Count;

        public void Enqueue(T item)
        {
            _data.Add(item);
            int childIndex = _data.Count - 1;
            while (childIndex > 0)
            {
                int parentIndex = (childIndex - 1) / 2;
                if (_data[childIndex].CompareTo(_data[parentIndex]) >= 0) break;
                T tmp = _data[childIndex];
                _data[childIndex] = _data[parentIndex];
                _data[parentIndex] = tmp;
                childIndex = parentIndex;
            }
        }

        public T Dequeue()
        {
            int lastIndex = _data.Count - 1;
            T frontItem = _data[0];
            _data[0] = _data[lastIndex];
            _data.RemoveAt(lastIndex);

            --lastIndex;
            int parentIndex = 0;
            while (true)
            {
                int leftChildIndex = parentIndex * 2 + 1;
                if (leftChildIndex > lastIndex) break;
                int rightChildIndex = leftChildIndex + 1;
                if (rightChildIndex <= lastIndex && _data[rightChildIndex].CompareTo(_data[leftChildIndex]) < 0)
                    leftChildIndex = rightChildIndex;
                if (_data[parentIndex].CompareTo(_data[leftChildIndex]) <= 0) break;
                T tmp = _data[parentIndex];
                _data[parentIndex] = _data[leftChildIndex];
                _data[leftChildIndex] = tmp;
                parentIndex = leftChildIndex;
            }
            return frontItem;
        }

        public T Peek() => _data[0];
    }
}
