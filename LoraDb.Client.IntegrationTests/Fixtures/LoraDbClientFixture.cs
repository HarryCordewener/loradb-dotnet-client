using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using LoraDb.Client.Native;
using TUnit.Core;
using TUnit.Core.Interfaces;

namespace LoraDb.Client.IntegrationTests.Fixtures;

public interface ILoraDbClientFixture : IAsyncInitializer, IAsyncDisposable
{
    LoraDbClient CreateClient();
}

public sealed class EmbeddedClientFixture : ILoraDbClientFixture
{
    private string? _ffiLibraryPath;

    public Task InitializeAsync()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return Task.CompletedTask;

        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");
        if (!File.Exists(ffiLibraryPath) || new FileInfo(ffiLibraryPath).Length == 0)
            Skip.Test($"Native library not found or not populated: {ffiLibraryPath}");

        _ffiLibraryPath = ffiLibraryPath;
        return Task.CompletedTask;
    }

    public LoraDbClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_ffiLibraryPath))
            throw new InvalidOperationException("Embedded fixture has not been initialized.");

        return LoraDbClient.CreateEmbedded(new PInvokeLoraDbNativeBridge(_ffiLibraryPath));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class HttpClientFixture : ILoraDbClientFixture
{
    private IContainer? _container;
    private Uri? _endpoint;

    public async Task InitializeAsync()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;

        var image = IntegrationTestEnvironment.HttpImage;
        if (string.IsNullOrWhiteSpace(image))
            Skip.Test("Set LORADB_HTTP_IMAGE when LORADB_RUN_INTEGRATION_TESTS is enabled.");

        _container = new ContainerBuilder(image)
            .WithPortBinding(4747, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(4747))
            .Build();

        await _container.StartAsync();

        var port = _container.GetMappedPublicPort(4747);
        _endpoint = new Uri($"http://127.0.0.1:{port}/");
    }

    public LoraDbClient CreateClient()
    {
        if (_endpoint is null)
            throw new InvalidOperationException("HTTP fixture has not been initialized.");

        return LoraDbClient.CreateHttp(_endpoint);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
