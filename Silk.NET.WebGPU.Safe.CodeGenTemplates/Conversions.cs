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
    
    public static unsafe void PackStructs(PackableStruct obj)
    {
        #region TEMPLATE DEFINE("PackStructs")
        var payloadSizeCalculator = new PayloadSizeCalculator();

        #region TEMPLATE FOREACH($Struct : $.PackableStructs)
        #region TEMPLATE REPLACE(`PackableStruct`, $Struct.Name)
        #region TEMPLATE REPLACE(`obj`, $Struct.VariableName)
        obj.CalculatePayloadSize(ref payloadSizeCalculator);
        #endregion
        #endregion
        #endregion
        
        payloadSizeCalculator.GetSize(out int size, out int stringPoolOffset);
            
        byte* ptr = stackalloc byte[size];
        var payloadWriter = new PayloadWriter(size, ptr, ptr + stringPoolOffset);
        
        #region TEMPLATE FOREACH($Struct : $.PackableStructs)
        #region TEMPLATE REPLACE(`PackableStruct`, $Struct.Name)
        #region TEMPLATE REPLACE(`obj`, $Struct.VariableName)
        WGPU.PackableStruct _packableStruct = default;
        obj.PackInto(ref _packableStruct, ref payloadWriter);
        #endregion
        #endregion
        #endregion

        #endregion
    }

    public static unsafe void UnpackStructs(WGPU.UnpackableStruct obj)
    {
        #region TEMPLATE DEFINE("UnpackStructs")
        #region TEMPLATE FOREACH($Struct : $.PackableStructs)
        #region TEMPLATE REPLACE(`UnpackableStruct`, $Struct.Name) 
        #region TEMPLATE REPLACE(`obj`, $Struct.VariableName)
        var _obj = UnpackableStruct.UnpackFrom(&obj);
        #endregion
        #endregion
        #endregion
        #endregion
    }
}