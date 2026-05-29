using System.Net;
using System.Text;
using System.Text.Json;

namespace LoraDb.Client.Tests;

public class HttpLoraDbTransportTests
{
    [Fact]
    public async Task ExecuteAsync_PostsQueryPayloadToHttpEndpoint()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"rows\":[{\"name\":\"Alice\"}]}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:4747/")
        };

        await using var client = LoraDbClient.CreateHttp(new Uri("http://localhost:4747/"), httpClient);
        using var result = await client.ExecuteAsync(
            "MATCH (u:User) WHERE u.name = $name RETURN u.name AS name",
            new Dictionary<string, object?> { ["name"] = "Alice" });

        Assert.Equal("Alice", result.Root.GetProperty("rows")[0].GetProperty("name").GetString());
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/query", handler.LastRequest.RequestUri!.AbsolutePath);

        var payload = await handler.LastRequest.Content!.ReadAsStringAsync();
        using var payloadJson = JsonDocument.Parse(payload);
        Assert.Equal("Alice", payloadJson.RootElement.GetProperty("params").GetProperty("name").GetString());
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory = responseFactory;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
