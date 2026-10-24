using System.Net;
using System.Text;

namespace Bookside.Integrations.Tests;

/// <summary>Replies from a queue (the last reply repeats) and records every request.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _replies = new();
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? _last;

    public List<Uri> Requests { get; } = [];

    public StubHttpMessageHandler Reply(HttpStatusCode status, string body, string mediaType = "application/json") =>
        Reply((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        }));

    public StubHttpMessageHandler Throw(Exception exception) => Reply((_, _) => Task.FromException<HttpResponseMessage>(exception));

    public StubHttpMessageHandler Hang() => Reply(async (_, ct) =>
    {
        await Task.Delay(Timeout.Infinite, ct);
        throw new InvalidOperationException("Unreachable.");
    });

    public StubHttpMessageHandler Reply(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply)
    {
        _replies.Enqueue(reply);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        if (_replies.TryDequeue(out var reply))
        {
            _last = reply;
        }

        return (_last ?? throw new InvalidOperationException("No reply configured."))(request, cancellationToken);
    }
}
