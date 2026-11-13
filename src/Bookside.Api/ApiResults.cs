using System.Text.Json;
using Bookside.Core.Common;
using Microsoft.AspNetCore.Http;

namespace Bookside.Api;

internal static class ApiResults
{
    /// <summary>
    /// A <see cref="ServiceError"/> as ProblemDetails with its status code and
    /// <c>code</c>; validation errors list their fields in camelCase, like the
    /// request body.
    /// </summary>
    public static IResult Problem(ServiceError error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Type == ErrorType.Validation)
        {
            var errors = error.FieldErrors
                .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.Field))
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());

            return Results.ValidationProblem(
                errors,
                detail: error.Message,
                extensions: extensions);
        }

        return Results.Problem(
            statusCode: error.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status400BadRequest,
            },
            detail: error.Message,
            extensions: extensions);
    }
}
