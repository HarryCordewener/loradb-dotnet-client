using System.Text.Json;

namespace LoraDb.Client.Extensions;

public enum LoraDbClientMode
{
    Http,
    Embedded
}

public sealed class LoraDbClientOptions
{
    public LoraDbClientMode Mode { get; set; } = LoraDbClientMode.Http;

    public Uri? Endpoint { get; set; }

    public string NativeLibraryName { get; set; } = "lora_ffi";

    public string? EmbeddedDatabaseName { get; set; }

    public string? EmbeddedDatabaseDirectory { get; set; }

    public string? EmbeddedWalDirectory { get; set; }

    public JsonSerializerOptions? SerializerOptions { get; set; }

    public static LoraDbClientOptions FromConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(connectionString));

        var options = new LoraDbClientOptions();

        foreach (var segmentRaw in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = segmentRaw.Trim();
            var eq = segment.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
            {
                continue;
            }

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();

            if (key.Equals("Server", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                    throw new ArgumentException(
                        $"The '{key}' value '{value}' is not a valid absolute URI.",
                        nameof(connectionString));
                options.Endpoint = uri;
                options.Mode = LoraDbClientMode.Http;
            }
            else if (key.Equals("Mode", StringComparison.OrdinalIgnoreCase))
            {
                options.Mode = value.Equals("Embedded", StringComparison.OrdinalIgnoreCase)
                    ? LoraDbClientMode.Embedded
                    : LoraDbClientMode.Http;
            }
            else if (key.Equals("NativeLibrary", StringComparison.OrdinalIgnoreCase))
            {
                options.NativeLibraryName = value;
            }
            else if (key.Equals("DatabaseName", StringComparison.OrdinalIgnoreCase))
            {
                options.EmbeddedDatabaseName = value;
                options.Mode = LoraDbClientMode.Embedded;
            }
            else if (key.Equals("DatabaseDirectory", StringComparison.OrdinalIgnoreCase))
            {
                options.EmbeddedDatabaseDirectory = value;
                options.Mode = LoraDbClientMode.Embedded;
            }
            else if (key.Equals("WalDirectory", StringComparison.OrdinalIgnoreCase))
            {
                options.EmbeddedWalDirectory = value;
                options.Mode = LoraDbClientMode.Embedded;
            }
        }

        return options;
    }
}
