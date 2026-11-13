using System.Security.Cryptography;
using System.Text;
using Bookside.Api.Words;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace Bookside.Api;

public static class ApiEndpoints
{
    /// <summary>
    /// Maps the API under <c>/api</c>, every endpoint requiring
    /// <c>Authorization: Bearer {token}</c>. A blank token maps nothing, so
    /// the API is off until one is configured.
    /// </summary>
    public static IEndpointRouteBuilder MapBooksideApi(
        this IEndpointRouteBuilder endpoints,
        string? token)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (string.IsNullOrWhiteSpace(token))
        {
            return endpoints;
        }

        var expected = Encoding.UTF8.GetBytes(token);
        var api = endpoints
            .MapGroup("/api")
            // The host's status code pages would replace empty-body errors
            // (e.g. bad JSON) with its HTML not-found page.
            .WithMetadata(new SkipStatusCodePagesAttribute())
            .AddEndpointFilter((context, next) =>
                HasToken(context.HttpContext.Request, expected)
                    ? next(context)
                    : ValueTask.FromResult<object?>(Unauthorized(context.HttpContext)));

        WordsApi.Map(api);
        return endpoints;
    }

    private static bool HasToken(HttpRequest request, byte[] expected)
    {
        const string Scheme = "Bearer ";
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(header[Scheme.Length..].Trim()),
                expected);
    }

    private static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Missing or invalid API token.");
    }
}
