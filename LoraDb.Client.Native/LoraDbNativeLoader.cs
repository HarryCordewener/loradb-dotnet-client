using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using LoraDb.Client.Native;

namespace LoraDb.Client.Native.Loader;

/// <summary>
/// Registers a <see cref="NativeLibrary.SetDllImportResolver"/> hook on the
/// <c>LoraDb.Client</c> assembly so that the <c>lora_ffi</c> native library is
/// resolved from the RID-specific folder shipped inside this NuGet package
/// (i.e. <c>runtimes/{rid}/native/</c> next to the assembly).
/// </summary>
/// <remarks>
/// The <see cref="ModuleInitializerAttribute"/> ensures this runs automatically
/// the first time any type in this assembly is touched — no explicit call needed.
/// </remarks>
public static class LoraDbNativeLoader
{
    private const string NativeLibraryBaseName = "lora_ffi";

    [SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute is only intended to be used in application code or advanced source generator scenarios", Justification = "The resolver must be registered automatically when the assembly loads.")]
    [ModuleInitializer]
    internal static void Initialize()
    {
        PInvokeLoraDbNativeBridge.LibraryPathResolver = FindLibraryPath;
    }

    /// <summary>
    /// Searches for the platform-specific <c>lora_ffi</c> binary shipped in the
    /// <c>runtimes/{rid}/native/</c> sub-folder relative to this assembly's
    /// location or <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    /// <param name="libraryName">The bare library name as passed to
    /// <see cref="NativeLibrary.Load(string)"/>.</param>
    /// <returns>
    /// Full path of the native binary, or <see langword="null"/> if no
    /// RID-specific file is found (falling back to OS default search).
    /// </returns>
    public static string? FindLibraryPath(string libraryName)
    {
        if (!libraryName.Equals(NativeLibraryBaseName, StringComparison.OrdinalIgnoreCase))
            return null;

        var rid = GetCurrentRid();
        var fileName = GetNativeFileName();

        var nativeAssemblyDir = Path.GetDirectoryName(typeof(LoraDbNativeLoader).Assembly.Location);
        if (nativeAssemblyDir is not null)
        {
            var candidate = Path.Combine(nativeAssemblyDir, "runtimes", rid, "native", fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        var baseCandidate = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
        if (File.Exists(baseCandidate))
            return baseCandidate;

        return null;
    }

    /// <summary>Returns the simplified RID for the current process.</summary>
    private static string GetCurrentRid()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";

        return rid;
    }

    /// <summary>Returns the OS-appropriate file name for <c>lora_ffi</c>.</summary>
    private static string GetNativeFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "lora_ffi.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "liblora_ffi.dylib";
        return "liblora_ffi.so";
    }
}
