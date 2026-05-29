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

    public static LoraDbClientOptions FromConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = new LoraDbClientOptions();

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
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
                options.Endpoint = new Uri(value);
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
        }

        return options;
    }
}
