using System.Runtime.InteropServices;
using System.Text.Json;
using LoraDb.Client.Models;

namespace LoraDb.Client.Native;

public sealed class PInvokeLoraDbNativeBridge : ILoraDbNativeBridge
{
#if NETSTANDARD2_1
    public PInvokeLoraDbNativeBridge(string libraryName = "lora_ffi")
    {
        throw new PlatformNotSupportedException("Embedded mode is not supported on netstandard2.1.");
    }

    public PInvokeLoraDbNativeBridge(LoraDbEmbeddedOpenOptions openOptions)
    {
        throw new PlatformNotSupportedException("Embedded mode is not supported on netstandard2.1.");
    }

    public string ExecuteJson(string requestJson) => throw new PlatformNotSupportedException("Embedded mode is not supported on netstandard2.1.");

    public string ExplainJson(string requestJson) => throw new PlatformNotSupportedException("Embedded mode is not supported on netstandard2.1.");

    public string ProfileJson(string requestJson) => throw new PlatformNotSupportedException("Embedded mode is not supported on netstandard2.1.");

    public LoraDbSnapshotMeta SaveSnapshot(string path) => throw new PlatformNotSupportedException("Embedded mode is not supported on netstandard2.1.");

    public LoraDbSnapshotMeta LoadSnapshot(string path) => throw new PlatformNotSupportedException("Embedded mode is not supported on netstandard2.1.");

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
    private readonly object _sync = new();
    private readonly DbNewDelegate _dbNew;
    private readonly DbNewNamedDelegate _dbNewNamed;
    private readonly DbNewWithWalDelegate _dbNewWithWal;
    private readonly DbFreeDelegate _dbFree;
    private readonly DbExecuteJsonDelegate _dbExecuteJson;
    private readonly DbExecuteJsonDelegate _dbExplainJson;
    private readonly DbExecuteJsonDelegate _dbProfileJson;
    private readonly DbSnapshotDelegate _dbSaveSnapshot;
    private readonly DbSnapshotDelegate _dbLoadSnapshot;
    private readonly FreeStringDelegate _freeString;

    public PInvokeLoraDbNativeBridge(string libraryName = "lora_ffi")
        : this(new LoraDbEmbeddedOpenOptions { NativeLibraryName = libraryName })
    {
    }

    public PInvokeLoraDbNativeBridge(LoraDbEmbeddedOpenOptions openOptions)
    {
        if (openOptions is null)
            throw new ArgumentNullException(nameof(openOptions));
        if (string.IsNullOrWhiteSpace(openOptions.NativeLibraryName))
            throw new ArgumentException("NativeLibraryName cannot be null or whitespace.", nameof(openOptions));
        if (!string.IsNullOrWhiteSpace(openOptions.DatabaseName) && !string.IsNullOrWhiteSpace(openOptions.WalDirectory))
            throw new ArgumentException("DatabaseName and WalDirectory are mutually exclusive in embedded mode.", nameof(openOptions));

        var resolvedPath = LibraryPathResolver?.Invoke(openOptions.NativeLibraryName) ?? openOptions.NativeLibraryName;
        _libraryHandle = NativeLibrary.Load(resolvedPath);

        _dbNew = Marshal.GetDelegateForFunctionPointer<DbNewDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_new"));
        _dbNewNamed = Marshal.GetDelegateForFunctionPointer<DbNewNamedDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_new_named"));
        _dbNewWithWal = Marshal.GetDelegateForFunctionPointer<DbNewWithWalDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_new_with_wal"));
        _dbFree = Marshal.GetDelegateForFunctionPointer<DbFreeDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_free"));
        _dbExecuteJson = Marshal.GetDelegateForFunctionPointer<DbExecuteJsonDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_execute_json"));
        _dbExplainJson = Marshal.GetDelegateForFunctionPointer<DbExecuteJsonDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_explain_json"));
        _dbProfileJson = Marshal.GetDelegateForFunctionPointer<DbExecuteJsonDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_profile_json"));
        _dbSaveSnapshot = Marshal.GetDelegateForFunctionPointer<DbSnapshotDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_save_snapshot"));
        _dbLoadSnapshot = Marshal.GetDelegateForFunctionPointer<DbSnapshotDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_db_load_snapshot"));
        _freeString = Marshal.GetDelegateForFunctionPointer<FreeStringDelegate>(
            NativeLibrary.GetExport(_libraryHandle, "lora_string_free"));

        _dbHandle = OpenDatabase(openOptions);
    }

    public string ExecuteJson(string requestJson) => ExecuteRequestJson(requestJson, _dbExecuteJson, "lora_db_execute_json");

    public string ExplainJson(string requestJson) => ExecuteRequestJson(requestJson, _dbExplainJson, "lora_db_explain_json");

    public string ProfileJson(string requestJson) => ExecuteRequestJson(requestJson, _dbProfileJson, "lora_db_profile_json");

    public LoraDbSnapshotMeta SaveSnapshot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        return ExecuteSnapshot(path, _dbSaveSnapshot, "lora_db_save_snapshot");
    }

    public LoraDbSnapshotMeta LoadSnapshot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        return ExecuteSnapshot(path, _dbLoadSnapshot, "lora_db_load_snapshot");
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

    private IntPtr OpenDatabase(LoraDbEmbeddedOpenOptions openOptions)
    {
        if (!string.IsNullOrWhiteSpace(openOptions.DatabaseName))
            return OpenNamedDatabase(openOptions.DatabaseName!, openOptions.DatabaseDirectory);

        if (!string.IsNullOrWhiteSpace(openOptions.WalDirectory))
            return OpenWalDatabase(openOptions.WalDirectory!);

        var status = _dbNew(out var handle);
        if (status != 0 || handle == IntPtr.Zero)
            throw new LoraDbNativeException(status, $"lora_db_new failed with status {status}.");

        return handle;
    }

    private IntPtr OpenNamedDatabase(string databaseName, string? databaseDirectory)
    {
        IntPtr namePtr = IntPtr.Zero;
        IntPtr directoryPtr = IntPtr.Zero;
        IntPtr errorPtr = IntPtr.Zero;

        try
        {
            namePtr = Marshal.StringToCoTaskMemUTF8(databaseName);
            if (!string.IsNullOrWhiteSpace(databaseDirectory))
                directoryPtr = Marshal.StringToCoTaskMemUTF8(databaseDirectory);

            var status = _dbNewNamed(out var handle, namePtr, directoryPtr, out errorPtr);
            if (status != 0 || handle == IntPtr.Zero)
                throw CreateNativeException(status, errorPtr, "lora_db_new_named");

            return handle;
        }
        finally
        {
            if (namePtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(namePtr);
            if (directoryPtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(directoryPtr);
            if (errorPtr != IntPtr.Zero)
                _freeString(errorPtr);
        }
    }

    private IntPtr OpenWalDatabase(string walDirectory)
    {
        IntPtr walPtr = IntPtr.Zero;
        IntPtr errorPtr = IntPtr.Zero;

        try
        {
            walPtr = Marshal.StringToCoTaskMemUTF8(walDirectory);
            var status = _dbNewWithWal(out var handle, walPtr, out errorPtr);
            if (status != 0 || handle == IntPtr.Zero)
                throw CreateNativeException(status, errorPtr, "lora_db_new_with_wal");

            return handle;
        }
        finally
        {
            if (walPtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(walPtr);
            if (errorPtr != IntPtr.Zero)
                _freeString(errorPtr);
        }
    }

    private string ExecuteRequestJson(
        string requestJson,
        DbExecuteJsonDelegate operation,
        string operationName)
    {
        if (requestJson is null)
            throw new ArgumentNullException(nameof(requestJson));

        var (query, paramsJson) = ParseRequestJson(requestJson);

        IntPtr resultPtr = IntPtr.Zero;
        IntPtr errorPtr = IntPtr.Zero;
        IntPtr queryPtr = IntPtr.Zero;
        IntPtr paramsPtr = IntPtr.Zero;

        try
        {
            queryPtr = Marshal.StringToCoTaskMemUTF8(query);
            if (paramsJson is not null)
                paramsPtr = Marshal.StringToCoTaskMemUTF8(paramsJson);

            int status;
            lock (_sync)
            {
                status = operation(
                    _dbHandle,
                    queryPtr,
                    paramsPtr,
                    out resultPtr,
                    out errorPtr);
            }

            if (status != 0)
                throw CreateNativeException(status, errorPtr, operationName);

            if (resultPtr == IntPtr.Zero)
                throw new LoraDbNativeException(status, $"{operationName} returned a null result pointer.");

            return Marshal.PtrToStringUTF8(resultPtr)
                   ?? throw new LoraDbNativeException(status, "Native LoraDB returned an invalid UTF-8 response.");
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

    private LoraDbSnapshotMeta ExecuteSnapshot(
        string path,
        DbSnapshotDelegate operation,
        string operationName)
    {
        IntPtr pathPtr = IntPtr.Zero;
        IntPtr errorPtr = IntPtr.Zero;

        try
        {
            pathPtr = Marshal.StringToCoTaskMemUTF8(path);
            var nativeMeta = new NativeSnapshotMeta();
            int status;
            lock (_sync)
            {
                status = operation(_dbHandle, pathPtr, out nativeMeta, out errorPtr);
            }

            if (status != 0)
                throw CreateNativeException(status, errorPtr, operationName);

            return new LoraDbSnapshotMeta
            {
                FormatVersion = checked((int)nativeMeta.FormatVersion),
                NodeCount = checked((long)nativeMeta.NodeCount),
                RelationshipCount = checked((long)nativeMeta.RelationshipCount),
                WalLsn = nativeMeta.WalLsnSet == 0 ? null : checked((long)nativeMeta.WalLsn),
                Path = path
            };
        }
        finally
        {
            if (pathPtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(pathPtr);
            if (errorPtr != IntPtr.Zero)
                _freeString(errorPtr);
        }
    }

    private static (string Query, string? ParamsJson) ParseRequestJson(string requestJson)
    {
        using var doc = JsonDocument.Parse(requestJson);
        var root = doc.RootElement;

        var query = root.TryGetProperty("query", out var qProp) && qProp.ValueKind == JsonValueKind.String
            ? qProp.GetString() ?? throw new ArgumentException("requestJson 'query' field must not be null.", nameof(requestJson))
            : throw new ArgumentException("requestJson must contain a 'query' string field.", nameof(requestJson));

        string? paramsJson = null;
        if (root.TryGetProperty("params", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
            paramsJson = paramsProp.GetRawText();

        return (query, paramsJson);
    }

    private static (string? ErrorCode, string Message) ParseError(string raw)
    {
        var delimiterIndex = raw.IndexOf(':');
        if (delimiterIndex <= 0)
            return (null, raw);

        var code = raw[..delimiterIndex].Trim();
        var message = raw[(delimiterIndex + 1)..].TrimStart();
        return (code, string.IsNullOrEmpty(message) ? raw : message);
    }

    private LoraDbNativeException CreateNativeException(int status, IntPtr errorPtr, string operationName)
    {
        var raw = errorPtr != IntPtr.Zero
            ? Marshal.PtrToStringUTF8(errorPtr) ?? $"{operationName} failed with status {status}"
            : $"{operationName} failed with status {status}";

        var (code, message) = ParseError(raw);
        return new LoraDbNativeException(status, message, code);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSnapshotMeta
    {
        public uint FormatVersion;
        public uint WalLsnSet;
        public ulong NodeCount;
        public ulong RelationshipCount;
        public ulong WalLsn;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DbNewDelegate(out IntPtr outDb);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DbNewNamedDelegate(
        out IntPtr outDb,
        IntPtr databaseName,
        IntPtr databaseDir,
        out IntPtr outError);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DbNewWithWalDelegate(
        out IntPtr outDb,
        IntPtr walDir,
        out IntPtr outError);

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
    private delegate int DbSnapshotDelegate(
        IntPtr db,
        IntPtr path,
        out NativeSnapshotMeta outMeta,
        out IntPtr outError);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeStringDelegate(IntPtr utf8StringPtr);
#endif
}
