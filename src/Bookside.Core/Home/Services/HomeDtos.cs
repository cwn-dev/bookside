using Bookside.Core.Books.Services;
using Bookside.Core.Vocabulary.Services;

namespace Bookside.Core.Home.Services;

public sealed record HomeSummary
{
    /// <summary>Books with a session in progress, most recently started first.</summary>
    public required IReadOnlyList<BookRead> Reading { get; init; }

    /// <summary>The latest finished session of each of the most recently finished books, newest first.</summary>
    public required IReadOnlyList<BookRead> RecentlyFinished { get; init; }

    /// <summary>Most recently added words first.</summary>
    public required IReadOnlyList<WordSummary> RecentWords { get; init; }

    /// <summary>A defined word picked by <see cref="HomeStats.Today"/>, so it stays the same all day; null when no word has a definition.</summary>
    public WordSummary? WordOfTheDay { get; init; }

    public required HomeStats Stats { get; init; }
}

/// <summary>A book with one of its reading sessions.</summary>
public sealed record BookRead
{
    public required Guid BookId { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<string> Authors { get; init; }

    public string? CoverSha256 { get; init; }

    public required ReadingSessionDto Session { get; init; }
}

public sealed record HomeStats
{
    /// <summary>The server's local date the summary was built for (set TZ in the container).</summary>
    public DateOnly Today { get; init; }

    public int BookCount { get; init; }

    public int WordCount { get; init; }

    /// <summary>Distinct books with a session finished in <see cref="Today"/>'s year.</summary>
    public int BooksFinishedThisYear { get; init; }

    /// <summary>Words added since the start of <see cref="Today"/>'s month (local time).</summary>
    public int WordsAddedThisMonth { get; init; }
}
