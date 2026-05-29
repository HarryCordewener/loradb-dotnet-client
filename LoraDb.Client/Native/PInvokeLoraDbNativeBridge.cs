using System.Runtime.InteropServices;

namespace LoraDb.Client.Native;

public sealed class PInvokeLoraDbNativeBridge : ILoraDbNativeBridge
{
    private readonly nint _libraryHandle;
    private readonly ExecuteJsonDelegate _executeJson;
    private readonly FreeStringDelegate _freeString;

    public PInvokeLoraDbNativeBridge(string libraryName = "lora_ffi")
    {
        _libraryHandle = NativeLibrary.Load(libraryName);

        _executeJson = Marshal.GetDelegateForFunctionPointer<ExecuteJsonDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_execute_json"));

        _freeString = Marshal.GetDelegateForFunctionPointer<FreeStringDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_string_free"));
    }

    public string ExecuteJson(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);

        var responsePtr = _executeJson(requestJson);
        if (responsePtr == nint.Zero)
        {
            throw new InvalidOperationException("Native LoraDB returned a null response pointer.");
        }

        try
        {
            return Marshal.PtrToStringUTF8(responsePtr)
                   ?? throw new InvalidOperationException("Native LoraDB returned an invalid UTF-8 response.");
        }
        finally
        {
            _freeString(responsePtr);
        }
    }

    public void Dispose()
    {
        if (_libraryHandle != nint.Zero)
        {
            NativeLibrary.Free(_libraryHandle);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ExecuteJsonDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string requestJson);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeStringDelegate(nint utf8StringPtr);
}
