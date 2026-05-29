using System.Net;
using System.Text;

namespace LoraDb.Client.Tests.Helpers;

internal sealed class RecordingHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestJson { get; private set; }
    public int CallCount { get; private set; }

    public RecordingHttpHandler(
        Func<HttpRequestMessage, HttpResponseMessage>? responseFactory = null)
    {
        _responseFactory = responseFactory ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"rows":[]}""", Encoding.UTF8, "application/json")
        });
    }

    public static RecordingHttpHandler WithJson(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    public static RecordingHttpHandler WithStatus(HttpStatusCode status, string? json = null) =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = json is null
                ? null
                : new StringContent(json, Encoding.UTF8, "application/json")
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            LastRequestJson = await request.Content.ReadAsStringAsync(cancellationToken);
        CallCount++;
        return _responseFactory(request);
    }

    internal HttpClient BuildClient(Uri baseAddress)
    {
        return new HttpClient(this) { BaseAddress = baseAddress };
    }
}
