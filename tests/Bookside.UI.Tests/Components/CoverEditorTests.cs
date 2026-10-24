using Bookside.Core.Books;
using Bookside.UI.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bookside.UI.Tests.Components;

public sealed class CoverEditorTests : ComponentTestBase
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    [Fact]
    public async Task Uploading_an_image_sets_the_cover_and_notifies()
    {
        var book = await AddBookAsync("Dune");
        var changed = false;
        var editor = Render<CoverEditor>(p => p
            .Add(x => x.BookId, book.Id)
            .Add(x => x.OnChanged, () => changed = true));

        editor.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(Png, "cover.png", contentType: "image/png"));

        editor.WaitForAssertion(() => Assert.True(changed));
        var cover = Success(await Library.GetCoverAsync(book.Id, Ct));
        Assert.Equal("image/png", cover.ContentType);
        Assert.Equal(Png, cover.Content.ToArray());
    }

    [Fact]
    public async Task Unsupported_file_type_is_rejected_without_saving()
    {
        var book = await AddBookAsync("Dune");
        var editor = Render<CoverEditor>(p => p.Add(x => x.BookId, book.Id));

        editor.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("<svg/>", "cover.svg", contentType: "image/svg+xml"));

        editor.WaitForAssertion(() => Assert.Contains("JPEG, PNG, WebP or GIF", editor.Find(".alert-error").TextContent));
        Assert.False((await Library.GetCoverAsync(book.Id, Ct)).IsSuccess);
    }

    [Fact]
    public async Task Oversized_file_is_rejected_without_saving()
    {
        var book = await AddBookAsync("Dune");
        var editor = Render<CoverEditor>(p => p.Add(x => x.BookId, book.Id));

        var big = new byte[BookCover.MaxSizeBytes + 1];
        editor.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(big, "big.jpg", contentType: "image/jpeg"));

        editor.WaitForAssertion(() => Assert.Contains("the limit is 2 MB", editor.Find(".alert-error").TextContent));
        Assert.False((await Library.GetCoverAsync(book.Id, Ct)).IsSuccess);
    }

    [Fact]
    public async Task Remove_deletes_the_cover_after_confirming()
    {
        var book = await AddBookAsync("Dune");
        Assert.True((await Library.SetCoverAsync(book.Id, new() { Content = Png, ContentType = "image/png" }, Ct)).IsSuccess);
        var changed = false;
        var editor = Render<CoverEditor>(p => p
            .Add(x => x.BookId, book.Id)
            .Add(x => x.HasCover, true)
            .Add(x => x.OnChanged, () => changed = true));

        editor.Find("button.btn-danger").Click();
        editor.Find(".confirm .btn-solid").Click();

        editor.WaitForAssertion(() => Assert.True(changed));
        Assert.False((await Library.GetCoverAsync(book.Id, Ct)).IsSuccess);
    }
}
