using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Silk.NET.WebGPU.Safe.Utils
{
    public sealed class RentalStorage<T>
    {
        public static RentalStorage<T> SharedInstance { get; } = new RentalStorage<T>();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Rent(T item)
        {
            throw null!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? Get(int key)
        {
            throw null!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetAndReturn(int key)
        {
            throw null!;
        }
    }
}
