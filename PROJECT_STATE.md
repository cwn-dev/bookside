# Bookside: project state (end of session 13, 2026-10-27)

## Built
- .NET 10 (SDK 10.0.203 pinned in global.json), `Bookside.slnx`, central package management, warnings-as-errors,
  `latest-recommended` analyzers. EF Core 10.0.13, xUnit v3 4.0.2 on Microsoft.Testing.Platform. Local tool: dotnet-ef 10.0.13.
  ASP.NET Core packages 10.0.13 (`Microsoft.AspNetCore.Components.Web`, `Mvc.Testing`), bUnit 2.12.5.
- `Bookside.Core`: `Book`, `Author`, `BookAuthor` (ordered join), `BookCover` (BLOB, 1:1, own table, 2 MB cap, allowed
  content types), `ReadingSession` (DateOnly start/finish, Status Reading/Finished/Abandoned, Rating 1–5, Notes),
  `BookComment` (session 5: BookId FK cascade, plain `Text` ≤ 10 000, index (BookId, CreatedAtUtc)),
  `Series` (session 10: name ≤ 300, like `Author`; `Book.SeriesId` FK set-null + `Book.SeriesNumber` double 0–9999),
  `Tag` + `BookTag` (session 11: name ≤ 50, unique `NormalizedName` via `TermNormalizer`, join cascades from both sides),
  `VocabularyWord` (optional BookId, Context, PageNumber, DictionaryEntryId), `DictionaryEntry` (definition cache,
  Found/NotFound, Meanings as JSON, RawResponse), `TermNormalizer`, `Isbn` (clean, checksum, 10→13).
  Max-length constants live on the entities and are used by both configurations and validation.
  - `Common/`: `Result` / `Result<T>` + `ServiceError` (Validation/NotFound/Conflict/Unavailable, stable `ErrorCodes`,
    per-field `FieldErrors`), `PagedResult<T>`, `Paging.Clamp` (default 50, max 200).
  - Service contracts + DTO records: `ILibraryService` (books, authors, covers, reading sessions, comments:
    `Add/Update/DeleteCommentAsync`, `BookDetails.Comments` newest first, `BookCommentDto.IsEdited`),
    `IVocabularyService` (words; session 6 added `FindWordAsync(term, language)` → `WordSummary?` by normalized term),
    `IDefinitionService` (cache-aware definitions). Session 9: `IVocabularyService.GetRandomWordAsync(excludeWordId)`
    (random word with a Found definition) and `Home/Services/IHomeService.GetHomeAsync()` → `HomeSummary` (reading and
    recently finished as `BookRead` = book + session DTO, recent words, word of the day, `HomeStats` incl. `Today`).
    Session 11: `ITagService` (`ListTagsAsync` with book counts, `SetBookTagsAsync(bookId, names)` replaces the set and
    creates new names, `RenameTagAsync` (Conflict `tag.duplicate`), `DeleteTagAsync`); `BookSummary`/`BookDetails.Tags`
    (alphabetical names), `BookQuery.Tag`.
  - External: `IBookLookupService` (per provider) + `BookMetadata` + `BookLookupException`,
    `IDictionaryLookupService` + `DictionaryLookupResult` + `DictionaryLookupException`.
- `Bookside.Data`: `BooksideDbContext` (UTC DateTimes, enums as strings, SaveChanges stamps timestamps, normalizes
  terms, enforces cover size; `DictionaryEntry.Meanings` via EF 10 `ComplexCollection(...).ToJson()`).
  `Services/`: `LibraryService`, `VocabularyService`, `DefinitionService`, `HomeService` (session 9), `TagService` (session 11) (internal), registered by
  `AddBooksideServices()` (scoped; adds `TimeProvider.System` if missing).
- `Bookside.Data.Sqlite`: `AddBooksideSqlite(path)` registers `IDbContextFactory<BooksideDbContext>`;
  `InitializeBooksideDatabaseAsync()` creates the directory, runs `MigrateAsync`, sets WAL. Migrations `InitialCreate`,
  `AddBookComments` (session 5: new table only), `AddBookSeries` (session 10), `AddBookTags` (session 11: new tables only).
- `Bookside.Integrations` (session 3): `AddDictionaryApiClient()` registers `DictionaryApiClient` (internal) as
  `IDictionaryLookupService`: typed HttpClient + `Microsoft.Extensions.Http.Resilience` 10.11.0 standard handler
  (3 s/attempt, 2 retries, 300 ms base delay, 8 s total), 1 MB response cap. Returns the `IHttpClientBuilder`.
  Session 7: `AddFreeDictionaryApiClient()` registers `FreeDictionaryApiClient` (freedictionaryapi.com) the same way.
  Both go through private `AddDictionaryClient<TClient, TOptions>(name, …)`: each provider has its own named
  HttpClient (base address, `{name}-standard` resilience options); shared `DictionaryHttp` (GET + transport-failure
  mapping) and `MeaningListBuilder` (merge by part of speech, de-dup, phonetic length). `Program.cs` registers
  freedictionaryapi.com, then dictionaryapi.dev.
- Book metadata import (session 12): `IBookMetadataService` (`Data/Services/BookMetadataService`) runs the
  `IBookLookupService` providers in registration order. `SearchAsync(query)`: a valid ISBN-10/13 → `FindByIsbnAsync`
  (ISBN-13), else text search (10 results); `DownloadCoverAsync(metadata)` goes through the provider that found it.
  Results are cleaned (over-long values, bad ISBNs, pages ≤ 0 dropped; no title → result dropped). Providers in
  `Integrations/Books`: `OpenLibraryClient` (ISBN: `api/books?jscmd=data` = edition with ISBNs/publisher; text:
  `search.json` = works, no ISBN/publisher/description) and `GoogleBooksClient` (`volumes?q=`, key in the
  `X-Goog-Api-Key` header, thumbnails rewritten to https). Registration shares `AddLookupClient<TService, TClient,
  TOptions>` with the dictionaries; HTTP via shared `LookupHttp` (`DictionaryHttp` now delegates). Covers download
  through one shared `BookCoverDownloader` (`book-covers` client: 15 s/attempt, 1 retry, 30 s; 2 MB cap; https +
  the provider's own image hosts only). `Program.cs`: Open Library, then Google Books only if
  `Bookside:GoogleBooks:ApiKey` is set (compose: `GOOGLE_BOOKS_API_KEY` in `.env`; dev: user secrets, Web has a
  `UserSecretsId`). UI: `BookLookup` card above the book form (add and edit; not a `<form>`, Enter or "Look up"),
  result list (thumb, title, authors, year, "Results from …"); picking calls `BookFormModel.FillEmpty` and, if the
  book has no cover, the cover is downloaded after the book saves (failure = no cover, silently).
- Flash cards (session 13): `WordReview` (own table `WordReviews`, keyed by `WordId`, cascade from the word;
  `DueOn`/`FirstReviewedOn`/`LastReviewedOn` local `DateOnly`s, `IntervalDays`, `Ease` ≥ 1.3, `Repetitions`,
  `Lapses`; migration `AddWordReviews`, new table only). No row = new card. `Core/Vocabulary/ReviewScheduler`
  (pure SM-2, `NewWordsPerDay` = 10). `IReviewService` (`Data/Services/ReviewService`): `GetStatusAsync` →
  `ReviewStatus` (due, new available today, new waiting, next due date), `GetQueueAsync` → `ReviewCard`s (due
  oldest first, then new oldest added first up to the day's limit; max 100), `RecordReviewAsync(wordId, knew)`.
  Only words with a Found definition are cards. `HomeSummary.Review` reuses the status query.
  UI: `ReviewPage` (`/words/review`): term + phonetic + audio, "Show answer", then meanings (`DefinitionView`,
  shared with the word page), book/page/sentence, "Forgot" / "Knew it" with next-interval hints
  (`ReviewLabels`); keys Space, 1, 2. `/words` header "Review (N)" and a home "Flash cards" card (side column,
  second on phones) when anything is due.
- Book series (session 10): `BookInput`/`BookSummary`/`BookDetails` carry `Series` + `SeriesNumber`;
  `ILibraryService.ListSeriesNamesAsync()`. Edit form: "Series" text input with a native `<datalist>` of existing names
  + "Number in series". Library cards show `SeriesLabel.Format` ("Discworld #3") under the authors; the book page
  shows it under "by …" as a link to `books?q={series}`. Library search matches the series name.
- Book tags (session 11): book page shows tags (links to `books?tag=`) + a dashed "+ Add tags" pill (no tags) / "Edit tags" link opening `TagEditor`
  (all tags as toggle chips saved per click, "New tag" box, Done, "Manage tags"). Library: tag chip row under the
  toolbar (`?tag=`, ANDed with search and status filter; clicking the selected chip clears it; hidden with no tags)
  + "Manage tags"; cards show outlined `.tag` pills after the status badges (not links: the card is one `<a>`).
  `TagsPage` (`/tags`, no nav link): name, book count linking to the filtered library, inline rename, two-step delete
  ("Remove it from N books and delete it?").
- Home page reading cards (session 11): "Add comment" opens an inline `CommentForm` (now has `AutoFocus`); saving
  closes it with "Comment added. View" (book link) until another panel opens. One panel per card still. With 2+
  books the list is two-up when the section is ≥ 640px wide (container query; i.e. desktop main column, not
  ≤ ~1000px viewports); actions sit under cover + text when a card is < 400px wide (card container query).
  Side-by-side cards share a height (actions at the bottom; title 1.1rem), except while one has a form open.
- Home page (session 9): `HomePage` (`/`, replaces the redirect to `/books`; the brand links to it, no nav link).
  Currently reading cards (cover, authors, "Started … · day N", inline "Finished" form = `ReadingSessionForm`, inline
  "Add word" = `WordForm` that stays open), word of the day + "Another", quick-add word, recent words (5), recently
  finished (4, rating + date), stats (finished this year, words this month, book and word counts). Empty library →
  welcome card (add a book, or add a word). Phones: one column in that order (column wrappers are `display: contents`
  and sections use `order`); ≥900px: 2fr main column (reading, finished) + 1fr side column, stats full width.
- `Bookside.UI` (session 4, RCL; references only Core + `Microsoft.AspNetCore.Components.Web`, so MAUI can reuse it):
  `Layout/MainLayout` (sticky header nav, `ErrorBoundary`), pages `LibraryPage` (`/books`: search, filter chips,
  sort, cover grid, "Show more" paging of 48), `BookDetailsPage` (`/books/{id}`: details, cover editor, reading
  history with start / finish / stop / log past read / edit / delete), `BookEditPage` (`/books/new`,
  `/books/{id}/edit`), `Home` (`/` → `/books`), `NotFound`. Components: `CoverImage` (image or coloured placeholder),
  `CoverEditor` (InputFile upload, client-side size/type check), `ReadingSessionForm`, `RatingInput`/`RatingStars`,
  `ConfirmButton` (two-step delete), `ErrorAlert`, `ServiceErrorValidator`, `CommentForm` (add/edit, session 5).
  `BookTimeline` (session 5) merges sessions + comments into the book page's "Reading log" (see decisions).
  Vocabulary (session 6): `VocabularyPage` (`/words`: quick-add, search, `?book=` filter chip, sort recent/A–Z,
  "Show more" paging of 50), `WordDetailsPage` (`/words/{id}`: phonetic, `<audio preload="none">`, meanings by part
  of speech with examples, retry / check again / refresh definition, where found (book link, page, sentence), notes,
  edit, delete). Components: `WordForm` (compact quick-add or full edit; `AutoFocus`), `BookPicker` (type-ahead,
  `InputBase<Guid?>`), `WordAddedNotice`. "Vocabulary" nav link; book page has "Add word" and links "Words saved"
  to `/words?book={id}`. Shared list styles (toolbar, chips, empty, more, back, section-header) moved to `bookside.css`.
  `Forms/` holds mutable form models whose
  property names match the service inputs. `CoverUrls` holds the cover route (used by components and host).
  Styling: `wwwroot/bookside.css` (tokens, light/dark via `prefers-color-scheme`, buttons/forms/cards) + scoped `.razor.css`.
- `Bookside.Web` (session 4): host. `Program.cs` reads `Bookside:DatabasePath` (relative paths resolve against the
  content root; `appsettings.json` = `/data/bookside.db`, Development = `App_Data/bookside.db`, git-ignored), wires
  SQLite + services + dictionary client, runs `InitializeBooksideDatabaseAsync()` before serving. `App.razor` sets
  Interactive Server without prerendering globally (static only for `[ExcludeFromInteractiveRouting]` pages, i.e. `Error`).
  `Routes.razor` routes the UI assembly (+ host). `CoverEndpoint`: `GET /covers/{bookId}?v={sha256}`.
  `ReconnectModal` from the template, restyled. Session 8: Data Protection keys persisted to
  `Bookside:DataProtectionKeysPath` (`/data/keys`; Development `App_Data/keys`; app name "Bookside"). `.claude/launch.json` has a `web` config (port 5233) for the preview pane, and `web-scratch` (same port,
  `App_Data/scratch.db`) for UI checks without touching the dev database.
- Docker (session 8): root `Dockerfile` (SDK `10.0.203-noble` on `$BUILDPLATFORM`, `publish -a $TARGETARCH`, NuGet
  cache mount; runtime `aspnet:10.0-noble-chiseled-extra`, UID 1654, port 8080), `.dockerignore`, `docker-compose.yaml`
  (`./data:/data`, `TZ`, forwarded headers on, port bound to `127.0.0.1` by default). README "Deploy" covers setup,
  backup, proxy WebSockets.
- CI (GitHub `cwn-dev/bookside`): `build-and-test.yml` (push/PR to main; SDK from `global.json`) and
  `docker-publish.yml` (release → Docker Hub `cwndev/bookside`). README has shields.io badges for both.
- `Bookside.UI.Tests`: 99 tests (session 13: `ReviewPageTests`, `ReviewLabelsTests`); 82 in session 12 (edit page lookup); 80 in session 11 (session 11: tags on library/book/tags pages, home add comment); 70 in session 10 (session 10: series on library/details/edit pages, `SeriesLabelTests`); session 9 had 61 (`HomePageTests`); session 8 had 54 (`DataProtectionTests`); session 6 had 52 (session 6: `VocabularyPageTests`, `WordDetailsPageTests`, book page add-word;
  `ComponentTestBase` now has a controllable `FakeDictionaryLookup` (`Define`, `Unavailable`) and `AddWordAsync`). Earlier: 37 tests (incl. `BookTimelineTests`, pure ordering tests). bUnit component tests (`ComponentTestBase`: `BunitContext` + production
  registrations on a temp SQLite file) and `WebApplicationFactory<Program>` endpoint tests (`Endpoints/WebHostFactory`
  sets the DB path with `UseSetting`).
- `Bookside.Integrations.Tests`: 36 tests (session 12: `BookLookupClientTests`, both providers + covers);
  26 before through the real registration with a stubbed primary handler
  (session 7: `FreeDictionaryApiClientTests`, `DictionaryRegistrationTests`).
- `Bookside.Data.Tests`: 109 tests (session 13: `ReviewSchedulerTests`, `ReviewServiceTests`; `ServiceTestBase.Reviews`); 96 in session 12 (`BookMetadataServiceTests` with `FakeBookLookupService`;
  `ServiceTestBase.Metadata` needs a provider registered in `ConfigureServices`); 90 in session 11 (session 11: `TagServiceTests`; session 10: series tests in `LibraryServiceTests`; session 9: `HomeServiceTests`, random-word tests; `ServiceTestBase` has a
  virtual `ConfigureServices` hook that runs before the production registrations, and `TestClock` is a settable
  `TimeProvider` that advances 1 ms per read). Session 7: `DefinitionFallbackTests`; `ServiceTestBase(params string[] fallbackSources)`
  registers extra `FakeDictionaryLookupService(source)` providers after `Dictionary`). Model/migration tests, plus service tests (`Services/ServiceTestBase`: real
  SQLite + production registrations + `FakeDictionaryLookupService`), `Isbn` and `Result` tests.

## Key decisions
- Online-only v1: the server SQLite database is the only copy of the data. No sync, no soft delete. UUIDv7 keys (`Guid.CreateVersion7()`).
- UI: Blazor Web App (Interactive Server), with components in a shared Razor class library. Responsive design that
  works equally well on desktop and mobile; neither is the priority (user's requirement). Design and test both widths.
  MAUI Blazor Hybrid comes later and will call a `Bookside.Api` (HTTP implementations of the same service interfaces).
- Deployment target: Docker container, SQLite file at `/data/bookside.db` on a local volume (WAL needs a local
  filesystem, not NFS/SMB). One instance only.
- EF Core used directly (no repository layer); the provider can be swapped at the EF level. Migrations are provider-specific
  and live in `Bookside.Data.Sqlite`; another provider would get a sibling project. Applied at startup.
- Blazor components inject the service interfaces (`ILibraryService` etc.), never a DbContext; each service method
  makes its own context from `IDbContextFactory`, so circuit-scoped services are safe.
- Cover images are stored in SQLite. Vocabulary words are unique per (language, normalized term), each linked to at most one book.
- Dictionary: dictionaryapi.dev (free, no key, Wiktionary-derived, no SLA). Cache NotFound; never cache transient failures.
- **Services (session 2):**
  - Contracts and DTOs in Core, implementations in `Bookside.Data/Services`. They return DTOs, never entities, so a
    future `Bookside.Api` client can implement the same interfaces. Each method makes its own short-lived context from the factory.
  - Expected failures are `Result`/`Result<T>` with a `ServiceError` (user's choice over exceptions). Bugs still throw.
    Validation collects every field error so forms can show them all at once. `ErrorType` maps to HTTP 400/404/409/503 later.
  - Search is `EF.Functions.Like` with escaped wildcards (user's choice: no schema change). Case-insensitive for ASCII
    only; title sorting stays case-sensitive (SQLite BINARY). Word search/sort uses `NormalizedTerm`, so it is effectively case-insensitive.
  - Authors are matched to existing ones by name (LIKE, so ASCII case-insensitive); authors left with no books are deleted on book update/delete.
  - A book has at most one session with status Reading (Conflict otherwise). FinishedOn is not allowed while Reading.
  - Only ISBN-10 given → ISBN-13 derived. Both are checksum-validated.
  - Adding a word saves it first, then tries the definition inline (cache → API). A transient failure leaves it
    undefined; `DefineWordAsync` retries. Cached NotFound never expires (user's choice); `DefineWordAsync(refresh: true)`
    is the manual re-check, and a failed refresh keeps the cached entry.
  - Unique-index races (words, dictionary entries) are handled provider-agnostically: catch `DbUpdateException`, re-query, map to Conflict / reuse.
  - Services are scoped because the dictionary client will be a typed HttpClient (transient).
- **Dictionary client (session 3):**
  - Only a 404 whose body is JSON (the API's "No Definitions Found" object) becomes a cacheable NotFound. An HTML/proxy
    404, any other non-success status, network error, timeout, oversized or malformed response throws
    `DictionaryLookupException` (never cached). Caller cancellation is rethrown unwrapped.
  - A 200 with no usable definitions is NotFound.
  - The API returns one entry per etymology ("bank" has 4). They're merged into one meaning list grouped by part of
    speech (case-insensitive, first-seen order), duplicate/blank definitions dropped. Phonetic = first non-blank
    `phonetic` then `phonetics[].text`; audio = first valid absolute http(s) URL (protocol-relative fixed up).
    Values longer than the entity limits are skipped. `RawResponse` keeps the full payload, so this can be re-parsed.
  - `DictionaryEntry` now has `SourceMaxLength`/`PhoneticMaxLength`/`AudioUrlMaxLength` constants (same values, no migration).
  - Provider reliability: on 2026-10-24 the origin was down; edge-cached words returned 200, every other lookup
    returned Cloudflare 530 (error 1033). Handled as transient, but expect "dictionary unavailable" for new words
    during such outages. If it recurs, consider a second provider (e.g. Wiktionary REST) behind the same interface.

- **UI (session 4):**
  - Plain CSS design system, no component library (user's choice): no dependency, full control of both widths, MAUI-ready.
  - Prerendering off (user's choice): data loads once, nothing clickable before it works, same behaviour as MAUI Hybrid.
    Cost: blank page until the circuit connects (fast on a LAN).
  - Library list state (search, filter, sort) lives in the query string (`?q=&filter=&sort=`), so Back from a book
    restores the list. Search is debounced 300 ms and uses `replace: true` navigation.
  - Forms validate only through the services: `ServiceErrorValidator` maps `FieldErrors` onto the `EditForm` by
    property name (no DataAnnotations duplication). Unmatched field errors and non-validation errors show in `ErrorAlert`.
  - Cover URLs carry the hash (`?v=`): matching `v` → `private, max-age=1y, immutable`; missing/stale `v` →
    `private, no-cache`. ETag = hash, so `Results.File` answers `If-None-Match` with 304. No hash-only query: with
    immutable URLs, revalidation is rare, so loading the ≤2 MB BLOB for a 304 isn't worth an interface change.
  - "Today" (start/finish dates) uses `TimeProvider.GetLocalNow()`, i.e. the server's time zone: set `TZ` in the container.
  - UI covers the library only this session (user's choice); vocabulary pages next.
  - Session 8: `FocusOnNavigate Selector="h1"` stays (screen readers announce the new page); `h1:focus { outline: none; }`
    in `bookside.css` hides the outline on that programmatic focus (the heading isn't tabbable, so keyboard users lose nothing).

- **Book comments (session 5, all user's choices):**
  - Book-level only (no link to a reading session; `ReadingSession.Notes` covers one read-through). Plain text with
    line breaks (`white-space: pre-wrap`), no Markdown. Editable inline; shows "· edited" (edit time on hover) when
    `UpdatedAtUtc > CreatedAtUtc`. Saving unchanged text isn't an edit (EF leaves the entity unmodified).
  - No separate list call: comments come with `GetBookAsync` (like sessions). Fine for a personal library; add a paged
    method if a book ever gets hundreds.
  - Comments and sessions form one newest-first "Reading log" (replaces "Reading history"); add box at its top.
    A session's card sits at its finish date, with a "Started reading" marker at its start date when that differs,
    so comments written during a read fall between the two. No finish → card at start date; no dates → creation time.
  - Session dates have no time: if the session was created (start) / updated (finish) on that same local date, that
    timestamp is used, otherwise end of day. Merge is UI-side (`BookTimeline.Build`, server time zone via
    `TimeProvider.LocalTimeZone`); ties go to the later UUIDv7.

- **Vocabulary UI (session 6, all user's choices):**
  - Inline quick-add, not a separate form page: term only on `/words`; term + page + sentence when it comes from a book
    (book page "Add word", or `/words?book=` where new words go to the filtered book). The form stays open and
    refocuses after each add (not on first load, so phones don't pop the keyboard); the book page keeps the page number.
    Notes and changing the book are on the word page's full edit form.
  - Book picker is a type-ahead over `ListBooksAsync(Search)` (8 results); Enter picks the first. Its input uses
    `form=` pointing at no form so Enter doesn't submit the surrounding `EditForm` (no JS).
  - Duplicates: on `DuplicateWord` the form calls `FindWordAsync` and links to the existing word ("already on your
    list (from Book). Open it"). Words still belong to one book; meeting a word again in another book doesn't relink it.
  - Book page shows only the linked count + "Add word" (no words section), to keep it about the reading log.
  - Definition states on the word page: none (dictionary was down) → "Retry definition"; NotFound → "Check again"
    (refresh); Found → "Refresh" link. Unavailable shows a friendly retry-later message.

- **Docker (session 8, user's choices: Dockerfile, chiseled-extra, existing reverse proxy, bind mount):**
  - Multi-stage Dockerfile built on the Docker host (`docker compose up -d --build`) or by the GitHub workflow
    (`.github/workflows/docker-publish.yml`, on release, linux/amd64 + linux/arm64 → Docker Hub). A multi-platform
    `buildx` build succeeded on 2026-10-25; the image hasn't been run yet. The NuGet cache mount id is per
    `$TARGETARCH`: with one shared id, the two parallel publishes raced on the same package files and restore failed.
  - SDK image pinned to the global.json version (same analyzers, warnings-as-errors build is reproducible); runtime
    image floats on `10.0` for security patches.
  - Chiseled-extra: no shell/package manager (no in-container sqlite3/curl, no compose healthcheck); "extra" has
    ICU + tzdata so `TZ` works. No `LANG` needed: date formats are explicit patterns (English month names).
  - TLS by the user's existing proxy; compose enables `ASPNETCORE_FORWARDEDHEADERS_ENABLED` (trusts any proxy, so
    the port is published on 127.0.0.1 by default). No HTTPS redirect/HSTS in the app; the proxy owns that.
  - `./data` bind mount owned by UID 1654. Data Protection keys there unencrypted at rest (startup warning
    "No XML encryptor configured" is expected); they only protect antiforgery tokens and circuit state.

- **Book metadata import (session 12, user's choices: one box + result list, fill empty fields only, cover only
  if the book has none, Google only with a key, separate cover client):**
  - Fallback chain like the dictionary one: a failure or no results → next provider; first provider with results
    wins (no merging across providers); Unavailable only if one failed and none had results. No caching.
  - An ISBN search always shows the list too (usually one row), so one UI covers ISBN, title and author searches.
  - Fill rules: only empty fields; authors only when every author box is blank; `MetadataSource(Id)` only if
    unset. Open Library ids are the key path (`/books/OL…M` from ISBN, `/works/OL…W` from text search).
  - Open Library covers redirect via archive.org and take 4–8 s, hence the own `book-covers` client; lookups keep
    the 3 s / 8 s budget. Saving waits for the download. Search thumbnails load straight from the provider in the browser.
  - Google key verified only by stubbed tests (no key available; keyless = 429). Header auth is documented by Google
    for API keys; if it is rejected, switch to `key=` in the query.
- **Flash cards (session 13, user's choices: self-graded cards, word → meaning only, SM-2, two buttons, entry from
  `/words` + home, defined words only, 10 new a day):**
  - Two-grade SM-2: "Knew it" = quality 4 (1 day, 6 days, then interval × ease, at least +1 day, capped at 3650);
    "Forgot" = back to 1 day, ease −0.2 (Anki's penalty, not SM-2's −0.54), lapse counted if it had been learnt.
    With two buttons the ease never rises. For a new card both buttons say "tomorrow".
  - Only the first grade of a word per day is recorded; a forgotten card comes back at the end of the session
    (UI only, no hints shown). A lost race creating the review row returns the existing schedule.
  - Own table rather than columns on `VocabularyWord`: no change to the word table, and new = no row. No review
    log yet: add one (per-review rows) if stats, streaks or FSRS are wanted later.
  - Daily new limit counts rows with `FirstReviewedOn` = today; "today" is the server's local date, like sessions.
  - Keyboard: focus moves to "Show answer", then "Knew it", so Space/Enter work natively; the card handles 1 / 2
    (`tabindex="-1"` keeps focus inside after a click). No JS. Key hint hidden on `hover: none` devices.
- **Book series (session 10, user's choices: own table, optional number):**
  - `Series` table rather than a text column, so a rename could later be one row. Matched by name like authors
    (LIKE, ASCII case-insensitive; an existing spelling wins); series left with no books are deleted on book update/delete.
  - Number is `double?` (allows 2.5), not `decimal`: EF SQLite stores decimal as TEXT, which breaks the check
    constraint and ordering. Shown with `"0.##"`. A number without a series is a validation error.
  - No series page, filter or sort (keep it simple); the book page's series link reuses library search.
  - Picker is a plain `<datalist>`: no JS, works on desktop and mobile; typing a new name creates the series.

- **Book tags (session 11, user's choices: book-page toggles, `/tags` page, no colour, one tag ANDed):**
  - Separate `ITagService` rather than growing `ILibraryService`; tags are not part of `BookInput`, so the edit form
    doesn't touch them and toggles save immediately.
  - Unlike authors/series, a tag with no books is kept (standing labels like "Own"); only `/tags` deletes it.
  - Uniqueness on `NormalizedName` (NFKC, collapsed spaces, lower-case invariant; same as vocabulary terms), so
    case-insensitive for all scripts, unlike LIKE matching. Existing spelling wins when reused; rename may change case.
  - Alphabetical order by `NormalizedName`. No starter tags. A unique-index race in `SetBookTagsAsync` retries once.
- **Home page (session 9, user's choices from a proposed layout):**
  - One `IHomeService.GetHomeAsync()` call (one context, ~8 queries) instead of several existing calls:
    `BookSummary` has no start date or rating, and stats needed counts; one round trip suits the future API client.
  - Word of the day: a Found word picked by the local date (SplitMix64 of `DayNumber` mod count, id order), so it
    stays put all day unless words are added/defined; "Another" calls `GetRandomWordAsync` and survives reloads
    after adding a word. Hidden when no word has a definition.
  - Brand links to `/`; nav stays Library + Vocabulary (no "Home" link).
  - Recently finished = each book's latest finish (over-fetch 20 sessions, de-duplicate in memory). "Finished this
    year" counts distinct books. "Words this month" starts at local midnight (server zone; DST-skipped midnight →
    first valid time).
  - "Finished" on a reading card opens the same `ReadingSessionForm` inline (status Finished, today), not a link to the book page.

- **Dictionary fallback chain (session 7, all user's choices):**
  - Each provider is an `IDictionaryLookupService`; `DefinitionService` takes `IEnumerable<IDictionaryLookupService>`
    and tries them in registration order (no config). Chosen over a composite provider: fallback sits next to the
    caching rules it depends on. Adding a provider = one client class + one `Add…Client()` method + one line in `Program.cs`.
  - Order: freedictionaryapi.com first (reliable, more senses), then dictionaryapi.dev (has audio, flaky).
  - A failure **or** NotFound moves on to the next provider. NotFound is cached only when every provider says
    NotFound (entry keeps the first one's source and raw reply). NotFound + a failure → Unavailable, nothing cached.
    `DictionaryEntry.Source` records the provider that answered. Refresh re-runs the chain from the first provider.
  - freedictionaryapi.com mapping: any non-200 (even 404) or a body without `entries` is a failure; `entries: []`
    or no definitions → NotFound. Senses → definitions, subsenses (any depth) right after their parent. Example =
    first example, else first quote text ≤ 300 chars (no reference). Phonetic = first untagged IPA, else first
    IPA. No audio. Definition text keeps Wiktionary's "(countable)" style prefixes; `tags` are ignored.
  - Latency: each provider keeps 3 s/attempt, 2 retries, 8 s total. A 530 fails over in ~1 s; worst case (both
    hanging) is 16 s.

## Next
1. **Flash cards stay visible when nothing is due (user's request, do this first).** Today the `/words` "Review"
   button and the home "Flash cards" card only show when `ToReviewCount > 0`, so after the day's 10 new words
   the feature looks gone. User chose:
   - Always show the Review entry when any word is reviewable: e.g. "All done · next review tomorrow" on the
     home card and a plain "Review" button on `/words` (still "Review (N)" when due). Hide only with no defined words.
   - "Practice anyway" on the done/nothing-due screen: review words without changing their schedule (no
     `RecordReviewAsync` call). Decide which words (random, most-forgotten by `Lapses`, recently added, or one
     book's) and how many per round; the existing card UI can be reused with grading not recorded.
2. **List view on the library page (user's request).** A list alternative to the cover grid on `/books`.
   Decisions to make: columns/fields per row (small cover, title, authors, status, rating, last read/finished
   date?); how it collapses on narrow widths (rows, not a wide table); toggle placement in the toolbar and whether
   the choice lives in the query string (`?view=list`, like search/filter/sort) or is remembered per device; whether
   list view adds sortable columns or reuses the existing sort control; same "Show more" paging.
3. Flash card follow-ups (not chosen yet, from the session 13 ideas): reverse cards (meaning → word), cloze
   cards from the saved sentence, multiple choice / spelling / matching quizzes, per-book recap, review stats and
   streak (needs a review log), word of the day preferring due words, Anki CSV export.
4. Later: `Bookside.Api`, MAUI Blazor Hybrid app.

## Open questions / minor notes
- Book import: subjects → tags, series from metadata, descriptions for Open Library text results (needs a works
  call) and merging providers were skipped. Open Library text results have no ISBN; an ISBN search fills it.
- Deployment is the user's: they will test the image on the Docker host and set up a GitHub build that pushes it to
  Docker Hub. Fix image problems if they report any; don't plan deploy work otherwise.
- A word links to one book. If re-meeting words across books matters, consider a word↔book occurrences table
  (context/page per occurrence) instead of the single `BookId`.
- dictionaryapi.dev was still returning Cloudflare 530 for uncached words in session 7; freedictionaryapi.com
  now answers first, so new words get no audio unless they fall through to dictionaryapi.dev.
- Both providers serve Wiktionary content (CC BY-SA 4.0). The word page shows only the provider name; add a
  Wiktionary attribution/link if the app is ever shared beyond personal use.
- Quote-derived examples can include Wiktionary's editorial brackets ("[…]"); fine for now.
- Non-ASCII search/sort (é vs É) is case-sensitive with LIKE. If it matters, revisit: normalized search columns (+ migration) or NOCASE/ICU.
- Possible hardening once the image runs: `read_only: true` + tmpfs `/tmp` in compose, a `/healthz` endpoint for
  the proxy (no compose healthcheck: chiseled has no curl).
- Auth: none yet. Needs a decision before exposing the container beyond the LAN.
- Local runtime is ASP.NET Core 10.0.7 while packages are 10.0.13; works, but consider updating the SDK/runtime.
- Typed `HttpClient` captured by circuit-scoped services lives as long as the circuit (no handler rotation). Fine for
  one user; revisit if long-lived circuits ever see DNS issues.
- No optimistic concurrency (last write wins); fine for a single user, revisit if the API/app allow concurrent edits.
