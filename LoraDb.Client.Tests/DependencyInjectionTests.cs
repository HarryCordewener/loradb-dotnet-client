using LoraDb.Client.Extensions;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Extensions;

namespace LoraDb.Client.Tests;

public class DependencyInjectionTests
{
    [Test]
    public async Task AddLoraDb_Action_RegistersILoraDbClient()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddLoraDb(o =>
        {
            o.Mode = LoraDbClientMode.Http;
            o.Endpoint = new Uri("http://localhost:4747/");
        });

        var sp = services.BuildServiceProvider();
        var client = sp.GetService<ILoraDbClient>();

        await Assert.That(client).IsNotNull();
    }

    [Test]
    public async Task AddLoraDb_Action_ReturnsSameSingletonInstance()
    {
        var services = new ServiceCollection();
        services.AddLoraDb(o =>
        {
            o.Mode = LoraDbClientMode.Http;
            o.Endpoint = new Uri("http://localhost:4747/");
        });

        var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<ILoraDbClient>();
        var b = sp.GetRequiredService<ILoraDbClient>();

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task AddLoraDb_ConnectionString_ParsesEndpoint()
    {
        var services = new ServiceCollection();
        services.AddLoraDb("Server=http://localhost:4747/;Mode=http");

        var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<ILoraDbClient>();

        await Assert.That(client).IsNotNull();
    }

    [Test]
    public async Task AddLoraDb_ConnectionString_ParsesMode()
    {
        var services = new ServiceCollection();
        services.AddLoraDb("Server=http://localhost:4747/;Mode=Http");
        var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<ILoraDbClient>();
        await Assert.That(client).IsNotNull();
    }

    [Test]
    public async Task AddLoraDb_OptionsObject_RegistersILoraDbClient()
    {
        var services = new ServiceCollection();
        services.AddLoraDb(new LoraDbClientOptions
        {
            Mode = LoraDbClientMode.Http,
            Endpoint = new Uri("http://127.0.0.1:4747/")
        });

        var sp = services.BuildServiceProvider();
        var client = sp.GetService<ILoraDbClient>();

        await Assert.That(client).IsNotNull();
    }

    [Test]
    public async Task AddLoraDb_NullAction_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        await Assert.That(() =>
        {
            services.AddLoraDb((Action<LoraDbClientOptions>)null!);
            return Task.CompletedTask;
        }).ThrowsException().And.IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task AddLoraDb_NullConnectionString_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        await Assert.That(() =>
        {
            services.AddLoraDb((string)null!);
            return Task.CompletedTask;
        }).ThrowsException().And.IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task AddLoraDb_NullServicesAction_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
        {
            ((IServiceCollection)null!).AddLoraDb(o => o.Endpoint = new Uri("http://localhost/"));
            return Task.CompletedTask;
        }).ThrowsException().And.IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task AddLoraDb_HttpModeWithoutEndpoint_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddLoraDb(o => o.Mode = LoraDbClientMode.Http);
        var sp = services.BuildServiceProvider();

        await Assert.That(() =>
        {
            sp.GetRequiredService<ILoraDbClient>();
            return Task.CompletedTask;
        })
            .ThrowsException()
            .And
            .IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task ConnectionString_Parse_ServerSetsEndpointAndHttpMode()
    {
        var opts = LoraDbClientOptions.FromConnectionString("Server=http://example.com/;Mode=Http");

        await Assert.That(opts.Endpoint).IsEqualTo(new Uri("http://example.com/"));
        await Assert.That(opts.Mode).IsEqualTo(LoraDbClientMode.Http);
    }

    [Test]
    public async Task ConnectionString_Parse_EmbeddedMode()
    {
        var opts = LoraDbClientOptions.FromConnectionString("Mode=Embedded;NativeLibrary=my_lora_ffi");

        await Assert.That(opts.Mode).IsEqualTo(LoraDbClientMode.Embedded);
        await Assert.That(opts.NativeLibraryName).IsEqualTo("my_lora_ffi");
    }

    [Test]
    public async Task ConnectionString_Parse_DefaultNativeLibraryName()
    {
        var opts = LoraDbClientOptions.FromConnectionString("Mode=Embedded");

        await Assert.That(opts.NativeLibraryName).IsEqualTo("lora_ffi");
    }

    [Test]
    public async Task ConnectionString_EmptyString_ThrowsArgumentException()
    {
        await Assert.That(() =>
        {
            LoraDbClientOptions.FromConnectionString("");
            return Task.CompletedTask;
        })
            .ThrowsException()
            .And
            .IsTypeOf<ArgumentException>();
    }
}
