using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WgpuWrappersSilk.Net.Utils
{
    internal sealed class RentalStorage<T>
    {
        private readonly List<T?> _buffer = new();
        private readonly Stack<int> _freeList = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Rent(T item)
        {
            if (_freeList.TryPop(out int idx))
            {
                _buffer[idx] = item;
            }
            else
            {
                idx = _freeList.Count;
                _buffer.Add(item);
            }

            return idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? Get(int key)
        {
            return _buffer[key];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? GetAndReturn(int key)
        {
            T? value = _buffer[key];
            _freeList.Push(key);
            _buffer[key] = default;
            return value;
        }
    }
}
