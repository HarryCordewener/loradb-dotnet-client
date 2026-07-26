namespace LoraDb.Client;

/// <summary>
/// Describes runtime capabilities for a specific client instance.
/// </summary>
public sealed class LoraDbClientCapabilities
{
    public IReadOnlyList<string> SupportedResultFormats { get; set; } = [];

    public bool SupportsExplain { get; set; }

    public bool SupportsProfile { get; set; }

    public bool SupportsSnapshots { get; set; }

    public bool SupportsCheckpoint { get; set; }

    public bool SupportsWalStatus { get; set; }

    public bool SupportsWalTruncate { get; set; }
}
