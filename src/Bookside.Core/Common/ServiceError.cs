namespace Bookside.Core.Common;

public enum ErrorType
{
    /// <summary>
    /// Input failed validation; see <see cref="ServiceError.FieldErrors"/>.
    /// HTTP 400.
    /// </summary>
    Validation = 0,

    /// <summary>
    /// HTTP 404.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// Clashes with existing state (duplicate, rule violation).
    /// HTTP 409.
    /// </summary>
    Conflict = 2,

    /// <summary>
    /// An external dependency failed transiently; retrying later may succeed.
    /// HTTP 503.
    /// </summary>
    Unavailable = 3,
}

/// <summary>
/// One invalid input field. <see cref="Field"/> is the input property name,
/// e.g. "Title".
/// </summary>
public sealed record FieldError(string Field, string Message);

/// <summary>
/// Expected, user-facing failure returned in a <see cref="Result"/>.
/// Bugs still throw.
/// </summary>
public sealed record ServiceError(ErrorType Type, string Code, string Message)
{
    public IReadOnlyList<FieldError> FieldErrors { get; init; } = [];

    public static ServiceError Validation(IReadOnlyList<FieldError> fieldErrors) =>
        new(ErrorType.Validation,ErrorCodes.Validation, "One or more fields are invalid.")
        {
            FieldErrors = fieldErrors
        };

    public static ServiceError Validation(string field, string message) =>
        Validation([new FieldError(field, message)]);

    public static ServiceError NotFound(string code, string message) =>
        new(ErrorType.NotFound, code, message);

    public static ServiceError Conflict(string code, string message) =>
        new(ErrorType.Conflict, code, message);

    public static ServiceError Unavailable(string code, string message) =>
        new(ErrorType.Unavailable, code, message);
}

/// <summary>
/// Stable codes the UI and a future API can switch on.
/// </summary>
public static class ErrorCodes
{
    public const string Validation = "validation";
    public const string BookNotFound = "book.not_found";
    public const string ReadingSessionNotFound = "reading_session.not_found";
    public const string AlreadyReading = "reading_session.already_reading";
    public const string CoverNotFound = "cover.not_found";
    public const string CommentNotFound = "comment.not_found";
    public const string WordNotFound = "word.not_found";
    public const string DuplicateWord = "word.duplicate";
    public const string DictionaryUnavailable = "dictionary.unavailable";
}
