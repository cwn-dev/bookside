using System.Net;
using Polly;

namespace Bookside.Integrations;

/// <summary>
/// Shared GETs for lookup clients: transport failures become the provider's own
/// exception type.
/// </summary>
internal static class LookupHttp
{
    public static Task<(HttpStatusCode Status, string Body)> GetStringAsync(
        HttpClient http,
        Uri path,
        Func<Exception, Exception> failure,
        CancellationToken cancellationToken) =>
            GetAsync(
                http,
                path,
                async (response, ct) => (
                    response.StatusCode,
                    await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)),
                failure,
                cancellationToken);

    public static Task<(HttpStatusCode Status, byte[] Body, string? ContentType)> GetBytesAsync(
        HttpClient http,
        Uri path,
        Func<Exception, Exception> failure,
        CancellationToken cancellationToken) =>
            GetAsync(
                http,
                path,
                async (response, ct) => (
                    response.StatusCode,
                    await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false),
                    response.Content.Headers.ContentType?.MediaType),
                failure,
                cancellationToken);

    private static async Task<T> GetAsync<T>(
        HttpClient http,
        Uri path,
        Func<HttpResponseMessage, CancellationToken, Task<T>> read,
        Func<Exception, Exception> failure,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http
                .GetAsync(path, cancellationToken)
                .ConfigureAwait(false);

            return await read(response, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
            or OperationCanceledException
            or ExecutionRejectedException)
        {
            // Network errors, response too large, timeouts (attempt/total),
            // open circuit.
            throw failure(ex);
        }
    }
}
