using Bookside.Core.Books.Lookup;
using Bookside.Core.Books.Services;

namespace Bookside.UI.Forms;

/// <summary>Mutable form state for <see cref="BookInput"/>. Property names match the input so service field errors map 1:1.</summary>
public sealed class BookFormModel
{
    public string Title { get; set; } = "";

    public string? Subtitle { get; set; }

    public string? Series { get; set; }

    public double? SeriesNumber { get; set; }

    /// <summary>One entry per input box; blanks are dropped by the service.</summary>
    public List<AuthorEntry> Authors { get; } = [new()];

    public int? PageCount { get; set; }

    public string? Isbn13 { get; set; }

    public string? Isbn10 { get; set; }

    public string? Publisher { get; set; }

    public int? PublishedYear { get; set; }

    public string? Description { get; set; }

    // Not editable in the form, but UpdateBookAsync replaces every field, so they're carried through.
    public string? MetadataSource { get; set; }

    public string? MetadataSourceId { get; set; }

    public static BookFormModel From(BookDetails book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var model = new BookFormModel
        {
            Title = book.Title,
            Subtitle = book.Subtitle,
            Series = book.Series,
            SeriesNumber = book.SeriesNumber,
            PageCount = book.PageCount,
            Isbn13 = book.Isbn13,
            Isbn10 = book.Isbn10,
            Publisher = book.Publisher,
            PublishedYear = book.PublishedYear,
            Description = book.Description,
            MetadataSource = book.MetadataSource,
            MetadataSourceId = book.MetadataSourceId,
        };

        if (book.Authors.Count > 0)
        {
            model.Authors.Clear();
            model.Authors.AddRange(book.Authors.Select(name => new AuthorEntry { Name = name }));
        }

        return model;
    }

    /// <summary>
    /// Copies looked-up values into the fields that are still empty; anything
    /// already typed wins.
    /// </summary>
    public void FillEmpty(BookMetadata book)
    {
        ArgumentNullException.ThrowIfNull(book);
        Title = Blank(Title) ? book.Title : Title;
        Subtitle = Blank(Subtitle) ? book.Subtitle : Subtitle;
        PageCount ??= book.PageCount;
        Isbn13 = Blank(Isbn13) ? book.Isbn13 : Isbn13;
        Isbn10 = Blank(Isbn10) ? book.Isbn10 : Isbn10;
        Publisher = Blank(Publisher) ? book.Publisher : Publisher;
        PublishedYear ??= book.PublishedYear;
        Description = Blank(Description) ? book.Description : Description;
        if (Blank(MetadataSource))
        {
            MetadataSource = book.Source;
            MetadataSourceId = book.SourceId;
        }

        if (Authors.All(a => Blank(a.Name)) && book.Authors.Count > 0)
        {
            Authors.Clear();
            Authors.AddRange(book.Authors.Select(name => new AuthorEntry { Name = name }));
        }

        static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
    }

    public BookInput ToInput() => new()
    {
        Title = Title,
        Subtitle = Subtitle,
        Series = Series,
        SeriesNumber = SeriesNumber,
        Authors = Authors.Select(a => a.Name ?? "").ToList(),
        PageCount = PageCount,
        Isbn13 = Isbn13,
        Isbn10 = Isbn10,
        Publisher = Publisher,
        PublishedYear = PublishedYear,
        Description = Description,
        MetadataSource = MetadataSource,
        MetadataSourceId = MetadataSourceId,
    };
}

/// <summary>
/// A reference type per row so inputs keep their identity when rows are added
/// or removed.
/// </summary>
public sealed class AuthorEntry
{
    public string? Name { get; set; }
}
