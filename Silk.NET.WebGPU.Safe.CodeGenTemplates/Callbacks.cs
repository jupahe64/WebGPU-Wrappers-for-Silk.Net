﻿using System;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU.Safe.Utils;

namespace Silk.NET.WebGPU.Safe;

#region TEMPLATE FOREACH($Callback : $Callbacks) 
#region TEMPLATE REPLACE(`([Cc])allbackName`, $Callback.Name)
#region TEMPLATE REPLACE(`object obj`, $Callback.SafeParameterString)
public delegate void CallbackName(object obj);
#endregion
public static unsafe class CallbackNameRegistry
{
    public static IntPtr CallbackFuncPointer => (IntPtr)s_callbackPtr;
    public static IntPtr Register(CallbackName callbackName)
    {
        return (IntPtr)RentalStorage<CallbackName>.SharedInstance.Rent(callbackName);
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
        #region TEMPLATE REPLACE(`object _obj = obj;`, $ConversionString, REMOVE_IF_NULL)
        object _obj = obj;
        #endregion
        #endregion
        
        callback(_obj);
    }
}
#endregion
#endregion
