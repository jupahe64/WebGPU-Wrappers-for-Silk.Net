using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Silk.NET.WebGPU.Safe.Utils
{
    internal sealed class RentalStorage<T>
    {
        private readonly List<T?> _buffer = new();
        private readonly Stack<int> _freeList = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Rent(T item)
        {
            int idx;
            if (_freeList.Count > 0)
            {
                idx = _freeList.Pop();
                _buffer[idx] = item;
            }
            else
            {
                idx = _buffer.Count;
                _buffer.Add(item);
            }

            return idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? Get(int key)
        {
            T? value = _buffer[key];

            if (value == null)
                throw new KeyNotFoundException($"There is no entry for key {key}");

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetAndReturn(int key)
        {
            T? value = _buffer[key];
            _freeList.Push(key);
            _buffer[key] = default;

            if (value == null)
                throw new KeyNotFoundException($"There is no entry for key {key}");

            return value;
        }
    }
}
