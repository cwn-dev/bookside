# Bookside: project state (end of session 1, 2026-10-24)

## Built
- .NET 10 (SDK 10.0.203 pinned in global.json), `Bookside.slnx`, central package management, warnings-as-errors,
  `latest-recommended` analyzers. EF Core 10.0.13, xUnit v3 4.0.2 on Microsoft.Testing.Platform. Local tool: dotnet-ef 10.0.13.
- `Bookside.Core`: `Book`, `Author`, `BookAuthor` (ordered join), `BookCover` (BLOB, 1:1, own table, 2 MB cap),
  `ReadingSession` (DateOnly start/finish, Status Reading/Finished/Abandoned, Rating 1–5, Notes),
  `VocabularyWord` (optional BookId, Context, PageNumber, DictionaryEntryId), `DictionaryEntry` (definition cache,
  Found/NotFound, Meanings as JSON, RawResponse), `TermNormalizer`.
  Interfaces: `IBookLookupService` + `BookMetadata` (no implementation, roadmap), `IDictionaryLookupService` +
  `DictionaryLookupResult` + `DictionaryLookupException` (no implementation yet).
- `Bookside.Data`: `BooksideDbContext`. Conventions: all DateTimes UTC (Kind restored on read), enums stored as strings.
  SaveChanges stamps CreatedAtUtc/UpdatedAtUtc (TimeProvider), normalizes terms, enforces cover size.
  `DictionaryEntry.Meanings` uses EF 10 `ComplexCollection(...).ToJson()`.
- `Bookside.Data.Sqlite`: `AddBooksideSqlite(path)` registers `IDbContextFactory<BooksideDbContext>` + scoped context;
  `InitializeBooksideDatabaseAsync()` creates the directory, runs `MigrateAsync`, sets WAL. Migration `InitialCreate`.
- `Bookside.Data.Tests`: 16 passing tests (migration applied, no pending model changes, WAL/FKs, graph round-trip,
  JSON meanings, unique normalized words, rating/date check constraints, cover cap, delete behaviour, normalizer).

- `CLAUDE.md` (auto-loaded; imports this file) holds commands, layout, conventions and working preferences.

## Key decisions
- Online-only v1: the server SQLite database is the only copy of the data. No sync, no soft delete. UUIDv7 keys (`Guid.CreateVersion7()`).
- UI: Blazor Web App (Interactive Server), mobile-friendly, with components in a shared Razor class library.
  MAUI Blazor Hybrid comes later and will call a `Bookside.Api` (HTTP implementations of the same service interfaces).
- Deployment target: Docker container, SQLite file at `/data/bookside.db` on a local volume (WAL needs a local
  filesystem, not NFS/SMB). One instance only.
- EF Core used directly (no repository layer); the provider can be swapped at the EF level. Migrations are provider-specific
  and live in `Bookside.Data.Sqlite`; another provider would get a sibling project. Applied at startup.
- Blazor Server components should use `IDbContextFactory`, not a context injected for the whole circuit.
- Cover images are stored in SQLite. Vocabulary words are unique per (language, normalized term), each linked to at most one book.
- Book metadata API (Open Library preferred; Google Books needs a key, returned 429 without one) is on the roadmap but not implemented.
- Dictionary: dictionaryapi.dev (free, no key, Wiktionary-derived, no SLA). Cache NotFound; never cache transient failures.

## Next
1. Application services (`ILibraryService`, `IVocabularyService`, cache-aware definition service) in Core/Data.
2. `Bookside.Integrations`: dictionaryapi.dev client (typed HttpClient + resilience) + tests.
3. `Bookside.UI` RCL + `Bookside.Web` host (Interactive Server, mobile-first layout), cover-image endpoint with ETag.
4. Dockerfile + compose with `/data` volume; also persist ASP.NET Data Protection keys there.
5. Later: Open Library import, `Bookside.Api`, MAUI Blazor Hybrid app.

## Open questions / minor notes
- Title sorting/search is case-sensitive in SQLite by default; decide on NOCASE collation or a normalized column when building search.
- Auth: none yet. Needs a decision before exposing the container beyond the LAN.
