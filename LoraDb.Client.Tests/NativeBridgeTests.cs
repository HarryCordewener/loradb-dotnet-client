using System.Runtime.InteropServices;
using LoraDb.Client.Native;
using LoraDb.Client.Native.Loader;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

/// <summary>
/// Unit tests for <see cref="PInvokeLoraDbNativeBridge"/> (Gap 1) and
/// <see cref="LoraDbNativeLoader"/> (Gap 2).
///
/// These tests do NOT require a real native library — they verify the
/// resolution/loading contract without executing Rust code.
/// </summary>
public class NativeBridgeTests
{
    // ── Gap 1: PInvokeLoraDbNativeBridge ────────────────────────────

    [Test]
    public async Task Constructor_ThrowsDllNotFoundException_WhenLibraryNotFound()
    {
        // The module initialiser has run by this point, so LibraryPathResolver is
        // LoraDbNativeLoader.FindLibraryPath. That returns null for names other than
        // "lora_ffi", which means NativeLibrary.Load falls back to the OS search and
        // fails for a non-existent library.
        await Assert.That(() => new PInvokeLoraDbNativeBridge("non_existent_library_xyz_abc"))
            .ThrowsException()
            .And
            .IsTypeOf<DllNotFoundException>();
    }

    [Test]
    [NotInParallel("LibraryPathResolver")]
    public async Task Constructor_InvokesLibraryPathResolver()
    {
        string? capturedName = null;
        var original = PInvokeLoraDbNativeBridge.LibraryPathResolver;
        try
        {
            PInvokeLoraDbNativeBridge.LibraryPathResolver = name =>
            {
                capturedName = name;
                return null; // fall back → OS search will fail → DllNotFoundException
            };

            try { _ = new PInvokeLoraDbNativeBridge("sentinel_lib_xyz"); }
            catch (DllNotFoundException) { /* expected */ }

            await Assert.That(capturedName).IsEqualTo("sentinel_lib_xyz");
        }
        finally
        {
            PInvokeLoraDbNativeBridge.LibraryPathResolver = original;
        }
    }

    [Test]
    public async Task LibraryPathResolver_IsNotNull_AfterModuleInitializer()
    {
        // LoraDbNativeLoader.Initialize() is a [ModuleInitializer] on LoraDb.Client.
        // By the time any test runs the assembly is already loaded, so the resolver
        // must have been set.
        await Assert.That(PInvokeLoraDbNativeBridge.LibraryPathResolver).IsNotNull();
    }

    // ── Gap 2: LoraDbNativeLoader ────────────────────────────────────

    [Test]
    public async Task FindLibraryPath_ReturnsNull_ForUnrelatedLibraryName()
    {
        var result = LoraDbNativeLoader.FindLibraryPath("some_other_library");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindLibraryPath_ReturnsNull_ForEmptyLibraryName()
    {
        var result = LoraDbNativeLoader.FindLibraryPath("");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindLibraryPath_ReturnsNull_WhenNoNativeBinaryOnDisk()
    {
        // In the normal test environment no runtimes/{rid}/native/lora_ffi* file
        // exists, so the resolver should return null and let the OS search proceed.
        EnsureNativeBinaryAbsent();
        var result = LoraDbNativeLoader.FindLibraryPath("lora_ffi");
        await Assert.That(result).IsNull();
    }

    [Test]
    [NotInParallel("NativeFileOnDisk")]
    public async Task FindLibraryPath_ReturnsFullPath_WhenNativeBinaryExists()
    {
        var fakeLib = GetExpectedNativePath(AppContext.BaseDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(fakeLib)!);
        try
        {
            File.WriteAllBytes(fakeLib, []);
            var result = LoraDbNativeLoader.FindLibraryPath("lora_ffi");
            await Assert.That(result).IsNotNull();
            await Assert.That(File.Exists(result!)).IsTrue();
        }
        finally
        {
            if (File.Exists(fakeLib)) File.Delete(fakeLib);
        }
    }

    [Test]
    [NotInParallel("NativeFileOnDisk")]
    public async Task FindLibraryPath_IsCaseInsensitive_ForLibraryName()
    {
        var fakeLib = GetExpectedNativePath(AppContext.BaseDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(fakeLib)!);
        try
        {
            File.WriteAllBytes(fakeLib, []);
            // Both casing variants must resolve to the same path.
            var lower = LoraDbNativeLoader.FindLibraryPath("lora_ffi");
            var upper = LoraDbNativeLoader.FindLibraryPath("LORA_FFI");
            await Assert.That(lower).IsEqualTo(upper);
        }
        finally
        {
            if (File.Exists(fakeLib)) File.Delete(fakeLib);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Ensures no fake native binary exists in either candidate directory so that
    /// the "returns null" tests are not polluted by a leftover file.
    /// </summary>
    private static void EnsureNativeBinaryAbsent()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory,
                     Path.GetDirectoryName(typeof(LoraDbNativeLoader).Assembly.Location)! })
        {
            var path = GetExpectedNativePath(dir);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string GetExpectedNativePath(string baseDir) =>
        Path.Combine(baseDir, "runtimes", GetCurrentRid(), "native", GetNativeFileName());

    private static string GetCurrentRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return RuntimeInformation.RuntimeIdentifier;
    }

    private static string GetNativeFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "lora_ffi.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "liblora_ffi.dylib";
        return "liblora_ffi.so";
    }
}
