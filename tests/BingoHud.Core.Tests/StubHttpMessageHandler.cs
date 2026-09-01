using System.Net;

namespace BingoHud.Core.Tests;

/// <summary>
/// Answers HTTP requests from a function, so client tests never touch the network.
///
/// <para>
/// The live endpoint is undocumented, rate-limited, and attached to the user's real quota.
/// Calling it from a test would spend the very thing the app exists to watch, so nothing here
/// ever does.
/// </para>
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        _respond = respond;

    /// <summary>
    /// Every request this handler saw, so tests can assert on what was sent as well as on what
    /// came back.
    /// </summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// A handler that answers every request with the same status and body.
    /// </summary>
    public static StubHttpMessageHandler Returning(HttpStatusCode status, string body = "") =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    /// <summary>
    /// A handler that fails the way an unplugged network does.
    /// </summary>
    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new(_ => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);

        return Task.FromResult(_respond(request));
    }
}
