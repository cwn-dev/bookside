using Bookside.Core.Books.Services;

namespace Bookside.UI.Forms;

/// <summary>Mutable form state for <see cref="TagInput"/>; property names match the input.</summary>
public sealed class TagFormModel
{
    public string? Name { get; set; }

    public TagInput ToInput() => new() { Name = Name ?? "" };
}
