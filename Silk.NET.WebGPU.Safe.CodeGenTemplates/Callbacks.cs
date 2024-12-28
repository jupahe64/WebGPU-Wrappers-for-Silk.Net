using System;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU.Safe.Utils;

namespace Silk.NET.WebGPU.Safe;

#region TEMPLATE FOREACH($Callback : $.Callbacks) 
#region TEMPLATE REPLACE(`CallbackName`, $Callback.Name)
#region TEMPLATE REPLACE(`object obj`, $Callback.SafeParameterString)
public delegate void CallbackName(object obj);
#endregion
public static unsafe class CallbackNameRegistry
{
    public static IntPtr CallbackFuncPointer => (IntPtr)s_callbackPtr;
    public static IntPtr Register(CallbackName callback)
    {
        return (IntPtr)RentalStorage<CallbackName>.SharedInstance.Rent(callback);
    }
    
    #region TEMPLATE REPLACE(`nint, `, $Callback.UnsafeParameterTypeString)
    private static readonly delegate*<nint, void*, void> s_callbackPtr 
        = &Callback;
    #endregion
    
    #region TEMPLATE REPLACE(`nint obj, `, $Callback.UnsafeParameterString)
    private static void Callback(nint obj, void* userdata)
    #endregion
    {
        var callback = RentalStorage<CallbackName>.SharedInstance.GetAndReturn((int)userdata); 
        #region TEMPLATE FOREACH($ConversionString : $Callback.ConversionStrings)
        #region TEMPLATE INSERT($ConversionString)
        object _obj = obj;
        #endregion
        
        #endregion
        #region TEMPLATE REPLACE(`_obj`, $Callback.CallParametersString)
        callback(_obj);
        #endregion
    }
}
#endregion
#endregion

