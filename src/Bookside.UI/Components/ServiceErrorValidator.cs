using Bookside.Core.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bookside.UI.Components;

/// <summary>
/// Puts a service's <see cref="ServiceError.FieldErrors"/> onto the surrounding <see cref="EditForm"/>, matching
/// <see cref="FieldError.Field"/> to the form model's property of the same name. A field's error clears when it's edited.
/// </summary>
public sealed class ServiceErrorValidator : ComponentBase, IDisposable
{
    private ValidationMessageStore? _store;
    private EditContext? _subscribedContext;

    [CascadingParameter]
    private EditContext? EditContext { get; set; }

    protected override void OnParametersSet()
    {
        if (EditContext is null)
        {
            throw new InvalidOperationException($"{nameof(ServiceErrorValidator)} must be placed inside an EditForm.");
        }

        if (!ReferenceEquals(EditContext, _subscribedContext))
        {
            Unsubscribe();
            _store = new ValidationMessageStore(EditContext);
            EditContext.OnFieldChanged += HandleFieldChanged;
            EditContext.OnValidationRequested += HandleValidationRequested;
            _subscribedContext = EditContext;
        }
    }

    /// <summary>
    /// Shows <paramref name="error"/>'s field errors. Returns the messages that don't belong to a model property
    /// (and the error's own message when it has no field errors), for the caller to show separately.
    /// </summary>
    public IReadOnlyList<string> Show(ServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var context = EditContext ?? throw new InvalidOperationException("Not attached to an EditForm.");
        _store!.Clear();

        var unmatched = new List<string>();
        foreach (var fieldError in error.FieldErrors)
        {
            if (context.Model.GetType().GetProperty(fieldError.Field) is not null)
            {
                _store.Add(context.Field(fieldError.Field), fieldError.Message);
            }
            else
            {
                unmatched.Add($"{fieldError.Field}: {fieldError.Message}");
            }
        }

        if (error.FieldErrors.Count == 0)
        {
            unmatched.Add(error.Message);
        }

        context.NotifyValidationStateChanged();
        return unmatched;
    }

    public void Clear()
    {
        _store?.Clear();
        EditContext?.NotifyValidationStateChanged();
    }

    public void Dispose() => Unsubscribe();

    private void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        _store?.Clear(e.FieldIdentifier);
        EditContext?.NotifyValidationStateChanged();
    }

    // Keep service errors from a previous submit out of the next one; the service re-validates everything.
    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e) => _store?.Clear();

    private void Unsubscribe()
    {
        if (_subscribedContext is not null)
        {
            _subscribedContext.OnFieldChanged -= HandleFieldChanged;
            _subscribedContext.OnValidationRequested -= HandleValidationRequested;
            _subscribedContext = null;
        }
    }
}
