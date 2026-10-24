# Bookside: project state (end of session 6, 2026-10-24)

## Built
- .NET 10 (SDK 10.0.203 pinned in global.json), `Bookside.slnx`, central package management, warnings-as-errors,
  `latest-recommended` analyzers. EF Core 10.0.13, xUnit v3 4.0.2 on Microsoft.Testing.Platform. Local tool: dotnet-ef 10.0.13.
  ASP.NET Core packages 10.0.13 (`Microsoft.AspNetCore.Components.Web`, `Mvc.Testing`), bUnit 2.12.5.
- `Bookside.Core`: `Book`, `Author`, `BookAuthor` (ordered join), `BookCover` (BLOB, 1:1, own table, 2 MB cap, allowed
  content types), `ReadingSession` (DateOnly start/finish, Status Reading/Finished/Abandoned, Rating 1–5, Notes),
  `BookComment` (session 5: BookId FK cascade, plain `Text` ≤ 10 000, index (BookId, CreatedAtUtc)),
  `VocabularyWord` (optional BookId, Context, PageNumber, DictionaryEntryId), `DictionaryEntry` (definition cache,
  Found/NotFound, Meanings as JSON, RawResponse), `TermNormalizer`, `Isbn` (clean, checksum, 10→13).
  Max-length constants live on the entities and are used by both configurations and validation.
  - `Common/`: `Result` / `Result<T>` + `ServiceError` (Validation/NotFound/Conflict/Unavailable, stable `ErrorCodes`,
    per-field `FieldErrors`), `PagedResult<T>`, `Paging.Clamp` (default 50, max 200).
  - Service contracts + DTO records: `ILibraryService` (books, authors, covers, reading sessions, comments:
    `Add/Update/DeleteCommentAsync`, `BookDetails.Comments` newest first, `BookCommentDto.IsEdited`),
    `IVocabularyService` (words; session 6 added `FindWordAsync(term, language)` → `WordSummary?` by normalized term),
    `IDefinitionService` (cache-aware definitions).
  - External: `IBookLookupService` + `BookMetadata`, `IDictionaryLookupService` + `DictionaryLookupResult` +
    `DictionaryLookupException` (no implementations yet).
- `Bookside.Data`: `BooksideDbContext` (UTC DateTimes, enums as strings, SaveChanges stamps timestamps, normalizes
  terms, enforces cover size; `DictionaryEntry.Meanings` via EF 10 `ComplexCollection(...).ToJson()`).
  `Services/`: `LibraryService`, `VocabularyService`, `DefinitionService` (internal), registered by
  `AddBooksideServices()` (scoped; adds `TimeProvider.System` if missing).
- `Bookside.Data.Sqlite`: `AddBooksideSqlite(path)` registers `IDbContextFactory<BooksideDbContext>`;
  `InitializeBooksideDatabaseAsync()` creates the directory, runs `MigrateAsync`, sets WAL. Migrations `InitialCreate`,
  `AddBookComments` (session 5: new table only).
- `Bookside.Integrations` (session 3): `AddDictionaryApiClient()` registers `DictionaryApiClient` (internal) as
  `IDictionaryLookupService`: typed HttpClient + `Microsoft.Extensions.Http.Resilience` 10.11.0 standard handler
  (3 s/attempt, 2 retries, 300 ms base delay, 8 s total), 1 MB response cap. Returns the `IHttpClientBuilder`.
  Wired into `Bookside.Web`.
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
  `ReconnectModal` from the template, restyled. `.claude/launch.json` has a `web` config (port 5233) for the preview pane, and `web-scratch` (same port,
  `App_Data/scratch.db`) for UI checks without touching the dev database.
- `Bookside.UI.Tests`: 52 tests (session 6: `VocabularyPageTests`, `WordDetailsPageTests`, book page add-word;
  `ComponentTestBase` now has a controllable `FakeDictionaryLookup` (`Define`, `Unavailable`) and `AddWordAsync`). Earlier: 37 tests (incl. `BookTimelineTests`, pure ordering tests). bUnit component tests (`ComponentTestBase`: `BunitContext` + production
  registrations on a temp SQLite file) and `WebApplicationFactory<Program>` endpoint tests (`Endpoints/WebHostFactory`
  sets the DB path with `UseSetting`).
- `Bookside.Integrations.Tests`: 14 tests through the real registration with a stubbed primary handler.
- `Bookside.Data.Tests`: 59 passing tests. Model/migration tests, plus service tests (`Services/ServiceTestBase`: real
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
- Book metadata API (Open Library preferred; Google Books needs a key, returned 429 without one) is on the roadmap but not implemented.
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

## Next
1. Dockerfile + compose with `/data` volume; also persist ASP.NET Data Protection keys there; set `TZ`.
   Decide HTTPS/reverse proxy (the host currently has no HTTPS redirect/HSTS).
2. Later: Open Library import (maps `BookMetadata` → `BookInput` + `SetCoverAsync`), `Bookside.Api`, MAUI Blazor Hybrid app.

## Open questions / minor notes
- A word links to one book. If re-meeting words across books matters, consider a word↔book occurrences table
  (context/page per occurrence) instead of the single `BookId`.
- dictionaryapi.dev was still returning Cloudflare 530 for uncached words at the end of session 6 (edge-cached
  words like "petrichor" worked). If outages keep recurring, add a second provider behind `IDictionaryLookupService`.
- Non-ASCII search/sort (é vs É) is case-sensitive with LIKE. If it matters, revisit: normalized search columns (+ migration) or NOCASE/ICU.
- Auth: none yet. Needs a decision before exposing the container beyond the LAN.
- Local runtime is ASP.NET Core 10.0.7 while packages are 10.0.13; works, but consider updating the SDK/runtime.
- Typed `HttpClient` captured by circuit-scoped services lives as long as the circuit (no handler rotation). Fine for
  one user; revisit if long-lived circuits ever see DNS issues.
- No optimistic concurrency (last write wins); fine for a single user, revisit if the API/app allow concurrent edits.
