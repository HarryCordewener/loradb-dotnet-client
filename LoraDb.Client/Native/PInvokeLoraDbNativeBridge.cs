using System.Runtime.InteropServices;
using System.Text.Json;

namespace LoraDb.Client.Native;

public sealed class PInvokeLoraDbNativeBridge : ILoraDbNativeBridge
{
#if NETSTANDARD2_1
    public PInvokeLoraDbNativeBridge(string libraryName = "lora_ffi")
    {
        throw new PlatformNotSupportedException("Embedded mode is not supported on netstandard2.1.");
    }

    public string ExecuteJson(string requestJson) => throw new PlatformNotSupportedException("Embedded mode is not supported on netstandard2.1.");

    public void Dispose()
    {
    }
#else
    /// <summary>
    /// Optional resolver invoked before <see cref="NativeLibrary.Load(string)"/>.
    /// Set by <c>LoraDb.Client.Native</c>'s module initializer to locate
    /// RID-specific binaries shipped inside that NuGet package.
    /// Returns the full path to load, or <see langword="null"/> to fall back
    /// to the OS default search.
    /// </summary>
    public static Func<string, string?>? LibraryPathResolver { get; set; }

    private IntPtr _libraryHandle;
    private IntPtr _dbHandle;
    private readonly DbNewDelegate _dbNew;
    private readonly DbFreeDelegate _dbFree;
    private readonly DbExecuteJsonDelegate _dbExecuteJson;
    private readonly FreeStringDelegate _freeString;

    public PInvokeLoraDbNativeBridge(string libraryName = "lora_ffi")
    {
        var resolvedPath = LibraryPathResolver?.Invoke(libraryName) ?? libraryName;
        _libraryHandle = NativeLibrary.Load(resolvedPath);

        _dbNew = Marshal.GetDelegateForFunctionPointer<DbNewDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_new"));

        _dbFree = Marshal.GetDelegateForFunctionPointer<DbFreeDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_free"));

        _dbExecuteJson = Marshal.GetDelegateForFunctionPointer<DbExecuteJsonDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_execute_json"));

        _freeString = Marshal.GetDelegateForFunctionPointer<FreeStringDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_string_free"));

        var status = _dbNew(out _dbHandle);
        if (status != 0 || _dbHandle == IntPtr.Zero)
            throw new InvalidOperationException($"lora_db_new failed with status {status}.");
    }

    public string ExecuteJson(string requestJson)
    {
        if (requestJson is null)
            throw new ArgumentNullException(nameof(requestJson));

        using var doc = JsonDocument.Parse(requestJson);
        var root = doc.RootElement;

        var query = root.TryGetProperty("query", out var qProp) && qProp.ValueKind == JsonValueKind.String
            ? qProp.GetString() ?? throw new ArgumentException("requestJson 'query' field must not be null.")
            : throw new ArgumentException("requestJson must contain a 'query' string field.");

        string? paramsJson = null;
        if (root.TryGetProperty("params", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
            paramsJson = paramsProp.GetRawText();

        IntPtr resultPtr = IntPtr.Zero;
        IntPtr errorPtr = IntPtr.Zero;
        IntPtr queryPtr = IntPtr.Zero;
        IntPtr paramsPtr = IntPtr.Zero;

        try
        {
            queryPtr = Marshal.StringToCoTaskMemUTF8(query);
            if (paramsJson is not null)
                paramsPtr = Marshal.StringToCoTaskMemUTF8(paramsJson);

            var status = _dbExecuteJson(
                _dbHandle,
                queryPtr,
                paramsPtr,
                out resultPtr,
                out errorPtr);

            if (status != 0)
            {
                var errorMessage = errorPtr != IntPtr.Zero
                    ? Marshal.PtrToStringUTF8(errorPtr) ?? "Unknown error"
                    : $"lora_db_execute_json failed with status {status}";
                throw new InvalidOperationException(errorMessage);
            }

            if (resultPtr == IntPtr.Zero)
                throw new InvalidOperationException("lora_db_execute_json returned a null result pointer.");

            return Marshal.PtrToStringUTF8(resultPtr)
                   ?? throw new InvalidOperationException("Native LoraDB returned an invalid UTF-8 response.");
        }
        finally
        {
            if (queryPtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(queryPtr);
            if (paramsPtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(paramsPtr);
            if (resultPtr != IntPtr.Zero)
                _freeString(resultPtr);
            if (errorPtr != IntPtr.Zero)
                _freeString(errorPtr);
        }
    }

    public void Dispose()
    {
        if (_dbHandle != IntPtr.Zero)
        {
            _dbFree(_dbHandle);
            _dbHandle = IntPtr.Zero;
        }

        if (_libraryHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DbNewDelegate(out IntPtr outDb);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DbFreeDelegate(IntPtr db);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DbExecuteJsonDelegate(
        IntPtr db,
        IntPtr query,
        IntPtr paramsJson,
        out IntPtr outResult,
        out IntPtr outError);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeStringDelegate(IntPtr utf8StringPtr);
#endif
}
