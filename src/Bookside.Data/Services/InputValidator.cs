using Bookside.Core.Common;

namespace Bookside.Data.Services;

/// <summary>Collects field errors so a form gets all of them at once.</summary>
internal sealed class InputValidator
{
    private readonly List<FieldError> _errors = [];

    public bool IsValid => _errors.Count == 0;

    public ServiceError ToError() => ServiceError.Validation(_errors.ToArray());

    /// <summary>Trims; returns empty string (and records an error) when missing or blank.</summary>
    public string Required(string field, string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            Add(field, "Required.");
            return string.Empty;
        }

        MaxLength(field, trimmed, maxLength);
        return trimmed;
    }

    /// <summary>Trims; blank becomes null.</summary>
    public string? Optional(string field, string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        MaxLength(field, trimmed, maxLength);
        return trimmed;
    }

    public void Check(bool condition, string field, string message)
    {
        if (!condition)
        {
            Add(field, message);
        }
    }

    public void Add(string field, string message) => _errors.Add(new FieldError(field, message));

    private void MaxLength(string field, string value, int maxLength) =>
        Check(value.Length <= maxLength, field, $"Must be at most {maxLength} characters.");
}
