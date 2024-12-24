// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NO_INDEX_RANGE
using System.Runtime.CompilerServices;
namespace System
{
    /// <summary>
    /// This is just a dummy class do NOT use at runtime
    /// </summary>
    internal readonly struct Range : IEquatable<Range>
    {
        /// <summary>Represent the inclusive start index of the Range.</summary>
        public Index Start => throw null!;

        /// <summary>Represent the exclusive end index of the Range.</summary>
        public Index End => throw null!;

        /// <summary>Construct a Range object using the start and end indexes.</summary>
        /// <param name="start">Represent the inclusive start index of the range.</param>
        /// <param name="end">Represent the exclusive end index of the range.</param>
        public Range(Index start, Index end)
        {
            throw null!;
        }

        /// <summary>Indicates whether the current Range object is equal to another object of the same type.</summary>
        /// <param name="value">An object to compare with this object</param>
        public override bool Equals(object? value) =>
            throw null!;

        /// <summary>Indicates whether the current Range object is equal to another Range object.</summary>
        /// <param name="other">An object to compare with this object</param>
        public bool Equals(Range other) =>  throw null!;

        /// <summary>Converts the value of the current Range object to its equivalent string representation.</summary>
        public override string ToString()
        {
            throw null!;
        }

        /// <summary>Create a Range object starting from start index to the end of the collection.</summary>
        public static Range StartAt(Index start) =>  throw null!;

        /// <summary>Create a Range object starting from first element in the collection to the end Index.</summary>
        public static Range EndAt(Index end) =>  throw null!;

        /// <summary>Create a Range object starting from first element to the end.</summary>
        public static Range All =>  throw null!;

        /// <summary>Calculate the start offset and length of range object using a collection length.</summary>
        /// <param name="length">The length of the collection that the range will be used with. length has to be a positive value.</param>
        /// <remarks>
        /// For performance reason, we don't validate the input length parameter against negative values.
        /// It is expected Range will be used with collections which always have non negative length/count.
        /// We validate the range is inside the length scope though.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            throw null!;
        }

        public override int GetHashCode()
        {
            throw null!;
        }

        public static bool operator ==(Range left, Range right)
        {
            throw null!;
        }

        public static bool operator !=(Range left, Range right)
        {
            throw null!;
        }
    }
}
#endif
