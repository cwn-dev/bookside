using Bookside.Core.Books.Services;

namespace Bookside.UI.Forms;

/// <summary>Mutable form state for <see cref="BookCommentInput"/>; property names match the input.</summary>
public sealed class CommentFormModel
{
    public string? Text { get; set; }

    public BookCommentInput ToInput() => new() { Text = Text ?? "" };
}
