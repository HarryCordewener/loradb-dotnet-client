using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
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
    private PInvokeLoraDbNativeBridge? _sharedBridge;

    public Task InitializeAsync()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return Task.CompletedTask;

        var ffiLibraryPath = IntegrationTestEnvironment.FfiLibraryPath;
        if (string.IsNullOrWhiteSpace(ffiLibraryPath))
            Skip.Test("Set LORADB_FFI_LIBRARY_PATH when LORADB_RUN_INTEGRATION_TESTS is enabled.");
        var libraryInfo = new FileInfo(ffiLibraryPath);
        if (!libraryInfo.Exists || libraryInfo.Length == 0)
            Skip.Test($"Native library not found or not populated: {ffiLibraryPath}");

        _sharedBridge = new PInvokeLoraDbNativeBridge(ffiLibraryPath);
        return Task.CompletedTask;
    }

    public LoraDbClient CreateClient()
    {
        if (_sharedBridge is null)
            throw new InvalidOperationException("Embedded fixture has not been initialized.");

        return LoraDbClient.CreateEmbedded(new NonOwningNativeBridge(_sharedBridge));
    }

    public ValueTask DisposeAsync()
    {
        _sharedBridge?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Wraps a shared <see cref="ILoraDbNativeBridge"/> without taking ownership.
    /// <see cref="Dispose"/> is a no-op so that individual clients can be disposed
    /// independently while the underlying database handle remains alive.
    /// </summary>
    private sealed class NonOwningNativeBridge : ILoraDbNativeBridge
    {
        private readonly ILoraDbNativeBridge _inner;

        public NonOwningNativeBridge(ILoraDbNativeBridge inner) => _inner = inner;

        public string ExecuteJson(string requestJson) => _inner.ExecuteJson(requestJson);

        public string ExplainJson(string requestJson) => _inner.ExplainJson(requestJson);

        public string ProfileJson(string requestJson) => _inner.ProfileJson(requestJson);

        public LoraDb.Client.Models.LoraDbSnapshotMeta SaveSnapshot(string path) => _inner.SaveSnapshot(path);

        public LoraDb.Client.Models.LoraDbSnapshotMeta LoadSnapshot(string path) => _inner.LoadSnapshot(path);

        public void Dispose() { }
    }
}

public sealed class HttpClientFixture : ILoraDbClientFixture
{
    private IFutureDockerImage? _builtImage;
    private IContainer? _container;
    private Uri? _endpoint;

    public async Task InitializeAsync()
    {
        if (!IntegrationTestEnvironment.IsEnabled())
            return;

        var imageName = IntegrationTestEnvironment.HttpImage;

        ContainerBuilder containerBuilder;
        if (imageName is not null)
        {
            containerBuilder = new ContainerBuilder(imageName);
        }
        else
        {
            var futureImage = new ImageFromDockerfileBuilder()
                .WithName("loradb-server-integration")
                .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), Path.Combine("docker", "lora-server"))
                .WithDockerfile("Dockerfile")
                .Build();
            await futureImage.CreateAsync();
            _builtImage = futureImage;
            containerBuilder = new ContainerBuilder(futureImage);
        }

        _container = containerBuilder
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
        if (_builtImage is not null)
            await _builtImage.DisposeAsync();
    }
}
