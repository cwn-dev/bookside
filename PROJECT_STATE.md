# Bookside: project state (end of session 3, 2026-10-24)

## Built
- .NET 10 (SDK 10.0.203 pinned in global.json), `Bookside.slnx`, central package management, warnings-as-errors,
  `latest-recommended` analyzers. EF Core 10.0.13, xUnit v3 4.0.2 on Microsoft.Testing.Platform. Local tool: dotnet-ef 10.0.13.
- `Bookside.Core`: `Book`, `Author`, `BookAuthor` (ordered join), `BookCover` (BLOB, 1:1, own table, 2 MB cap, allowed
  content types), `ReadingSession` (DateOnly start/finish, Status Reading/Finished/Abandoned, Rating 1–5, Notes),
  `VocabularyWord` (optional BookId, Context, PageNumber, DictionaryEntryId), `DictionaryEntry` (definition cache,
  Found/NotFound, Meanings as JSON, RawResponse), `TermNormalizer`, `Isbn` (clean, checksum, 10→13).
  Max-length constants live on the entities and are used by both configurations and validation.
  - `Common/`: `Result` / `Result<T>` + `ServiceError` (Validation/NotFound/Conflict/Unavailable, stable `ErrorCodes`,
    per-field `FieldErrors`), `PagedResult<T>`, `Paging.Clamp` (default 50, max 200).
  - Service contracts + DTO records: `ILibraryService` (books, authors, covers, reading sessions),
    `IVocabularyService` (words), `IDefinitionService` (cache-aware definitions).
  - External: `IBookLookupService` + `BookMetadata`, `IDictionaryLookupService` + `DictionaryLookupResult` +
    `DictionaryLookupException` (no implementations yet).
- `Bookside.Data`: `BooksideDbContext` (UTC DateTimes, enums as strings, SaveChanges stamps timestamps, normalizes
  terms, enforces cover size; `DictionaryEntry.Meanings` via EF 10 `ComplexCollection(...).ToJson()`).
  `Services/`: `LibraryService`, `VocabularyService`, `DefinitionService` (internal), registered by
  `AddBooksideServices()` (scoped; adds `TimeProvider.System` if missing).
- `Bookside.Data.Sqlite`: `AddBooksideSqlite(path)` registers `IDbContextFactory<BooksideDbContext>`;
  `InitializeBooksideDatabaseAsync()` creates the directory, runs `MigrateAsync`, sets WAL. Migration `InitialCreate` (unchanged).
- `Bookside.Integrations` (session 3): `AddDictionaryApiClient()` registers `DictionaryApiClient` (internal) as
  `IDictionaryLookupService`: typed HttpClient + `Microsoft.Extensions.Http.Resilience` 10.11.0 standard handler
  (3 s/attempt, 2 retries, 300 ms base delay, 8 s total), 1 MB response cap. Returns the `IHttpClientBuilder`.
  Not wired into a host yet (no host exists).
- `Bookside.Integrations.Tests`: 14 tests through the real registration with a stubbed primary handler.
- `Bookside.Data.Tests`: 54 passing tests. Model/migration tests, plus service tests (`Services/ServiceTestBase`: real
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
- Blazor Server components should use `IDbContextFactory`, not a context injected for the whole circuit.
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

## Next
1. `Bookside.UI` RCL + `Bookside.Web` host (Interactive Server, responsive layout that works equally well on desktop and mobile), cover-image endpoint with ETag
   (`BookSummary/BookDetails.CoverSha256`; consider a hash-only query so a 304 doesn't load the BLOB).
   Host wiring: `AddBooksideSqlite(path)` + `AddBooksideServices()` + `AddDictionaryApiClient()`, then `InitializeBooksideDatabaseAsync()`.
2. Dockerfile + compose with `/data` volume; also persist ASP.NET Data Protection keys there.
3. Later: Open Library import (maps `BookMetadata` → `BookInput` + `SetCoverAsync`), `Bookside.Api`, MAUI Blazor Hybrid app.

## Open questions / minor notes
- Non-ASCII search/sort (é vs É) is case-sensitive with LIKE. If it matters, revisit: normalized search columns (+ migration) or NOCASE/ICU.
- Auth: none yet. Needs a decision before exposing the container beyond the LAN.
- No optimistic concurrency (last write wins); fine for a single user, revisit if the API/app allow concurrent edits.
