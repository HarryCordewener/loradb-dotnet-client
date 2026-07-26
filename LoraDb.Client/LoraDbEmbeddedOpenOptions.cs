namespace LoraDb.Client;

/// <summary>
/// Controls how the embedded native database handle is opened.
/// </summary>
public sealed class LoraDbEmbeddedOpenOptions
{
    /// <summary>
    /// Native library name or absolute path passed to <c>NativeLibrary.Load</c>.
    /// </summary>
    public string NativeLibraryName { get; set; } = "lora_ffi";

    /// <summary>
    /// Optional named-database archive name. When set, embedded mode is persistent.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Optional base directory for <see cref="DatabaseName"/>.
    /// </summary>
    public string? DatabaseDirectory { get; set; }

    /// <summary>
    /// Optional WAL directory. When set, opens a WAL-backed persistent database.
    /// </summary>
    public string? WalDirectory { get; set; }
}
