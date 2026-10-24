using System.Diagnostics.CodeAnalysis;

namespace Bookside.Core.Common;

/// <summary>Outcome of an operation that can fail in an expected way. Return a <see cref="ServiceError"/> to fail.</summary>
public class Result
{
    private static readonly Result SuccessResult = new(null);

    protected Result(ServiceError? error)
    {
        Error = error;
    }

    public ServiceError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => SuccessResult;

    public static Result<T> Success<T>(T value) => new(value);

    public static Result Failure(ServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static Result<T> Failure<T>(ServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    public static implicit operator Result(ServiceError error) => Failure(error);
}

/// <summary>A <see cref="Result"/> carrying a value on success. Values and errors convert implicitly.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T value)
        : base(null)
    {
        _value = value;
    }

    internal Result(ServiceError error)
        : base(error)
    {
    }

    /// <summary>Throws if the result is a failure; check <see cref="Result.IsSuccess"/> first.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({Error.Code}); there is no value.");

#pragma warning disable CA1000 // Named alternates for the implicit operators (CA2225); Result.Success/Failure are the usual entry points.
    public static Result<T> FromValue(T value) => new(value);

    public static Result<T> FromError(ServiceError error) => Failure<T>(error);
#pragma warning restore CA1000

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(ServiceError error) => Failure<T>(error);
}
