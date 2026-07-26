using LoraDb.Client.Native;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoraDb.Client.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoraDb(
        this IServiceCollection services,
        Action<LoraDbClientOptions> configure)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        services.Configure(configure);
        RegisterClient(services);
        return services;
    }

    public static IServiceCollection AddLoraDb(
        this IServiceCollection services,
        string connectionString)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(connectionString));

        var parsed = LoraDbClientOptions.FromConnectionString(connectionString);
        services.Configure<LoraDbClientOptions>(o =>
        {
            o.Mode = parsed.Mode;
            o.Endpoint = parsed.Endpoint;
            o.NativeLibraryName = parsed.NativeLibraryName;
            o.EmbeddedDatabaseName = parsed.EmbeddedDatabaseName;
            o.EmbeddedDatabaseDirectory = parsed.EmbeddedDatabaseDirectory;
            o.EmbeddedWalDirectory = parsed.EmbeddedWalDirectory;
            o.SerializerOptions = parsed.SerializerOptions;
        });

        RegisterClient(services);
        return services;
    }

    public static IServiceCollection AddLoraDb(
        this IServiceCollection services,
        LoraDbClientOptions options)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        services.Configure<LoraDbClientOptions>(o =>
        {
            o.Mode = options.Mode;
            o.Endpoint = options.Endpoint;
            o.NativeLibraryName = options.NativeLibraryName;
            o.EmbeddedDatabaseName = options.EmbeddedDatabaseName;
            o.EmbeddedDatabaseDirectory = options.EmbeddedDatabaseDirectory;
            o.EmbeddedWalDirectory = options.EmbeddedWalDirectory;
            o.SerializerOptions = options.SerializerOptions;
        });

        RegisterClient(services);
        return services;
    }

    private static void RegisterClient(IServiceCollection services)
    {
        services.AddHttpClient();

        services.TryAddSingleton<ILoraDbClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LoraDbClientOptions>>().Value;

            return options.Mode switch
            {
                LoraDbClientMode.Embedded => LoraDbClient.CreateEmbedded(
                    new PInvokeLoraDbNativeBridge(new LoraDbEmbeddedOpenOptions
                    {
                        NativeLibraryName = options.NativeLibraryName,
                        DatabaseName = options.EmbeddedDatabaseName,
                        DatabaseDirectory = options.EmbeddedDatabaseDirectory,
                        WalDirectory = options.EmbeddedWalDirectory,
                    }),
                    options.SerializerOptions),
                _ => CreateHttpClient(sp, options),
            };
        });
    }

    private static LoraDbClient CreateHttpClient(IServiceProvider sp, LoraDbClientOptions options)
    {
        if (options.Endpoint is null)
        {
            throw new InvalidOperationException(
                "LoraDB HTTP mode requires an endpoint. " +
                "Set LoraDbClientOptions.Endpoint or use a connection string with Server=<url>.");
        }

        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        return LoraDbClient.CreateHttp(options.Endpoint, httpClientFactory, serializerOptions: options.SerializerOptions);
    }
}
