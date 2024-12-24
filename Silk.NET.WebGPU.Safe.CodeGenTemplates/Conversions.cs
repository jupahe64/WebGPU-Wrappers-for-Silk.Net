using Silk.NET.WebGPU.Safe.Utils;

namespace Silk.NET.WebGPU.Safe;

public class Conversions
{
    public static class WGPU
    {
        public record struct PackableStruct();
        public record struct UnpackableStruct();
    }
    public struct PackableStruct
    {
        internal readonly void CalculatePayloadSize(ref PayloadSizeCalculator payloadSize)
        {
            throw null!;
        }

        internal readonly void PackInto(ref WGPU.PackableStruct baseStruct, ref PayloadWriter payload)
        {
            throw null!;
        }
    }
    
    public struct UnpackableStruct
    {
        internal static unsafe UnpackableStruct UnpackFrom(WGPU.UnpackableStruct* baseStruct)
        {
            throw null!;
        }
    }
    
    public static unsafe void PackStructs(PackableStruct packableStruct)
    {
        #region TEMPLATE DEFINE("PackStructs")
        var payloadSizeCalculator = new PayloadSizeCalculator();

        #region TEMPLATE FOREACH($Struct : $PackableStructs) REPLACE(`([Pp])ackableStruct`, $Struct.Name)
        packableStruct.CalculatePayloadSize(ref payloadSizeCalculator);
        #endregion
        
        payloadSizeCalculator.GetSize(out int size, out int stringPoolOffset);
            
        byte* ptr = stackalloc byte[size];
        var payloadWriter = new PayloadWriter(size, ptr, ptr + stringPoolOffset);
        
        #region TEMPLATE FOREACH($Struct : $PackableStructs) REPLACE(`([Pp])ackableStruct`, $Struct.Name)
        WGPU.PackableStruct _packableStruct = default;
        packableStruct.PackInto(ref _packableStruct, ref payloadWriter);
        #endregion

        #endregion
    }

    public static unsafe void UnpackStructs(WGPU.UnpackableStruct unpackableStruct)
    {
        #region TEMPLATE DEFINE("UnpackStructs")
        #region TEMPLATE FOREACH($Struct : $PackableStructs) REPLACE(`([Uu])npackableStruct`, $Struct.Name)
        var _unpackableStruct = UnpackableStruct.UnpackFrom(&unpackableStruct);
        #endregion
        #endregion
    }
}