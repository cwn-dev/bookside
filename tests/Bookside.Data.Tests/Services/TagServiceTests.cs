using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Common;

namespace Bookside.Data.Tests.Services;

public sealed class TagServiceTests : ServiceTestBase
{
    [Fact]
    public async Task Set_book_tags_creates_then_reuses_tags_case_insensitively()
    {
        var mort = await AddBookAsync("Mort");
        var dune = await AddBookAsync("Dune");

        var mortTags = Success(await Tags.SetBookTagsAsync(mort.Id, [" Own ", "have e-book", "", "OWN"], Ct));
        var duneTags = Success(await Tags.SetBookTagsAsync(dune.Id, ["own", "Have  E-book", "Lent out"], Ct));

        Assert.Equal(["have e-book", "Own"], mortTags);
        Assert.Equal(["have e-book", "Lent out", "Own"], duneTags); // existing spellings win
        Assert.Equal(
            [("have e-book", 2), ("Lent out", 1), ("Own", 2)],
            (await Tags.ListTagsAsync(Ct)).Select(t => (t.Name, t.BookCount)));
        Assert.Equal(["have e-book", "Own"], Success(await Library.GetBookAsync(mort.Id, Ct)).Tags);
    }

    [Fact]
    public async Task Removing_a_tag_from_every_book_keeps_the_tag()
    {
        var mort = await AddBookAsync("Mort");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["Own", "Signed"], Ct));

        var remaining = Success(await Tags.SetBookTagsAsync(mort.Id, ["Signed"], Ct));

        Assert.Equal(["Signed"], remaining);
        Assert.Equal([("Own", 0), ("Signed", 1)], (await Tags.ListTagsAsync(Ct)).Select(t => (t.Name, t.BookCount)));
    }

    [Fact]
    public async Task Set_book_tags_validates_names_and_book()
    {
        var mort = await AddBookAsync("Mort");

        var tooLong = Failure(await Tags.SetBookTagsAsync(mort.Id, [new string('x', Tag.NameMaxLength + 1)], Ct), ErrorType.Validation);
        var missing = Failure(await Tags.SetBookTagsAsync(Guid.CreateVersion7(), ["Own"], Ct), ErrorType.NotFound);

        Assert.Equal(["Tags"], tooLong.FieldErrors.Select(e => e.Field));
        Assert.Equal(ErrorCodes.BookNotFound, missing.Code);
        Assert.Empty(await Tags.ListTagsAsync(Ct));
    }

    [Fact]
    public async Task Rename_allows_a_case_change_but_not_another_tags_name()
    {
        var mort = await AddBookAsync("Mort");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["own", "Signed"], Ct));
        var tags = await Tags.ListTagsAsync(Ct);
        var own = tags.Single(t => t.Name == "own");

        var renamed = Success(await Tags.RenameTagAsync(own.Id, new TagInput { Name = " Own " }, Ct));
        var duplicate = Failure(await Tags.RenameTagAsync(own.Id, new TagInput { Name = "SIGNED" }, Ct), ErrorType.Conflict);
        var blank = Failure(await Tags.RenameTagAsync(own.Id, new TagInput { Name = " " }, Ct), ErrorType.Validation);
        var missing = Failure(await Tags.RenameTagAsync(Guid.CreateVersion7(), new TagInput { Name = "X" }, Ct), ErrorType.NotFound);

        Assert.Equal(("Own", 1), (renamed.Name, renamed.BookCount));
        Assert.Equal(ErrorCodes.DuplicateTag, duplicate.Code);
        Assert.Equal(["Name"], blank.FieldErrors.Select(e => e.Field));
        Assert.Equal(ErrorCodes.TagNotFound, missing.Code);
        Assert.Equal(["Own", "Signed"], Success(await Library.GetBookAsync(mort.Id, Ct)).Tags);
    }

    [Fact]
    public async Task Delete_removes_the_tag_from_its_books()
    {
        var mort = await AddBookAsync("Mort");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["Own", "Signed"], Ct));
        var own = (await Tags.ListTagsAsync(Ct)).Single(t => t.Name == "Own");

        Assert.True((await Tags.DeleteTagAsync(own.Id, Ct)).IsSuccess);
        Failure(await Tags.DeleteTagAsync(own.Id, Ct), ErrorType.NotFound);

        Assert.Equal(["Signed"], Success(await Library.GetBookAsync(mort.Id, Ct)).Tags);
        Assert.Equal(["Signed"], (await Tags.ListTagsAsync(Ct)).Select(t => t.Name));
    }

    [Fact]
    public async Task Deleting_a_book_keeps_its_tags()
    {
        var mort = await AddBookAsync("Mort");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["Own"], Ct));

        Assert.True((await Library.DeleteBookAsync(mort.Id, Ct)).IsSuccess);

        Assert.Equal([("Own", 0)], (await Tags.ListTagsAsync(Ct)).Select(t => (t.Name, t.BookCount)));
    }

    [Fact]
    public async Task Library_filters_by_tag_combined_with_status_and_lists_tags()
    {
        var mort = await AddBookAsync("Mort");
        var dune = await AddBookAsync("Dune");
        await AddBookAsync("Emma");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["Own", "Signed"], Ct));
        Success(await Tags.SetBookTagsAsync(dune.Id, ["Own"], Ct));
        Success(await Library.AddReadingSessionAsync(dune.Id, new ReadingSessionInput(), Ct));

        var owned = await Library.ListBooksAsync(new BookQuery { Tag = "OWN" }, Ct);
        var ownedReading = await Library.ListBooksAsync(new BookQuery { Tag = "own", Filter = BookFilter.Reading }, Ct);
        var unknown = await Library.ListBooksAsync(new BookQuery { Tag = "Nope" }, Ct);

        Assert.Equal(["Dune", "Mort"], owned.Items.Select(b => b.Title));
        Assert.Equal(2, owned.TotalCount);
        Assert.Equal(["Own", "Signed"], owned.Items.Single(b => b.Title == "Mort").Tags);
        Assert.Equal(["Dune"], ownedReading.Items.Select(b => b.Title));
        Assert.Empty(unknown.Items);
    }
}
