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
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        RegisterClient(services);
        return services;
    }

    public static IServiceCollection AddLoraDb(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var parsed = LoraDbClientOptions.FromConnectionString(connectionString);
        services.Configure<LoraDbClientOptions>(o =>
        {
            o.Mode = parsed.Mode;
            o.Endpoint = parsed.Endpoint;
            o.NativeLibraryName = parsed.NativeLibraryName;
        });

        RegisterClient(services);
        return services;
    }

    public static IServiceCollection AddLoraDb(
        this IServiceCollection services,
        LoraDbClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.Configure<LoraDbClientOptions>(o =>
        {
            o.Mode = options.Mode;
            o.Endpoint = options.Endpoint;
            o.NativeLibraryName = options.NativeLibraryName;
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
                    new PInvokeLoraDbNativeBridge(options.NativeLibraryName)),
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
        return LoraDbClient.CreateHttp(options.Endpoint, httpClientFactory);
    }
}
