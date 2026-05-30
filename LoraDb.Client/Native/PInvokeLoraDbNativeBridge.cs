using System.Runtime.InteropServices;
using System.Text.Json;

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
    private nint _dbHandle;
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
        if (status != 0 || _dbHandle == nint.Zero)
            throw new InvalidOperationException($"lora_db_new failed with status {status}.");
    }

    public string ExecuteJson(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);

        using var doc = JsonDocument.Parse(requestJson);
        var root = doc.RootElement;

        var query = root.TryGetProperty("query", out var qProp) && qProp.ValueKind == JsonValueKind.String
            ? qProp.GetString() ?? throw new ArgumentException("requestJson 'query' field must not be null.")
            : throw new ArgumentException("requestJson must contain a 'query' string field.");

        string? paramsJson = null;
        if (root.TryGetProperty("params", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
            paramsJson = paramsProp.GetRawText();

        nint resultPtr = nint.Zero;
        nint errorPtr = nint.Zero;
        var queryPtr = nint.Zero;
        var paramsPtr = nint.Zero;

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
                var errorMessage = errorPtr != nint.Zero
                    ? Marshal.PtrToStringUTF8(errorPtr) ?? "Unknown error"
                    : $"lora_db_execute_json failed with status {status}";
                throw new InvalidOperationException(errorMessage);
            }

            if (resultPtr == nint.Zero)
                throw new InvalidOperationException("lora_db_execute_json returned a null result pointer.");

            return Marshal.PtrToStringUTF8(resultPtr)
                   ?? throw new InvalidOperationException("Native LoraDB returned an invalid UTF-8 response.");
        }
        finally
        {
            if (queryPtr != nint.Zero)
                Marshal.FreeCoTaskMem(queryPtr);
            if (paramsPtr != nint.Zero)
                Marshal.FreeCoTaskMem(paramsPtr);
            if (resultPtr != nint.Zero)
                _freeString(resultPtr);
            if (errorPtr != nint.Zero)
                _freeString(errorPtr);
        }
    }

    public void Dispose()
    {
        if (_dbHandle != nint.Zero)
        {
            _dbFree(_dbHandle);
            _dbHandle = nint.Zero;
        }

        if (_libraryHandle != nint.Zero)
        {
            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = nint.Zero;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DbNewDelegate(out nint outDb);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DbFreeDelegate(nint db);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DbExecuteJsonDelegate(
        nint db,
        nint query,
        nint paramsJson,
        out nint outResult,
        out nint outError);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeStringDelegate(nint utf8StringPtr);
}
