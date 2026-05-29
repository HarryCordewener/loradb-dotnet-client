using System.Runtime.InteropServices;

namespace LoraDb.Client.Native;

public sealed class PInvokeLoraDbNativeBridge : ILoraDbNativeBridge
{
    /// <summary>
    /// Optional resolver invoked before <see cref="NativeLibrary.Load(string)"/>.
    /// Set by <c>LoraDb.Client.Native</c>'s module initializer to locate
    /// RID-specific binaries shipped inside that NuGet package.
    /// Returns the full path to load, or <see langword="null"/> to fall back
    /// to the OS default search.
    /// </summary>
    public static Func<string, string?>? LibraryPathResolver { get; set; }

    private nint _libraryHandle;
    private readonly ExecuteJsonDelegate _executeJson;
    private readonly FreeStringDelegate _freeString;

    public PInvokeLoraDbNativeBridge(string libraryName = "lora_ffi")
    {
        var resolvedPath = LibraryPathResolver?.Invoke(libraryName) ?? libraryName;
        _libraryHandle = NativeLibrary.Load(resolvedPath);

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
            _libraryHandle = nint.Zero;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ExecuteJsonDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string requestJson);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeStringDelegate(nint utf8StringPtr);
}
