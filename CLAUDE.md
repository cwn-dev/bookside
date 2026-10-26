# Bookside

Personal reading companion (library / reading log + vocabulary builder). .NET 10, C#, EF Core + SQLite.
Built in phases; current state, decisions and next steps are in PROJECT_STATE.md:

@PROJECT_STATE.md

## Keep these files current (required every session)

- **PROJECT_STATE.md**: before ending any session that changed code or decisions, update it: what's built,
  key decisions (with the reasoning), what's next, open questions. Keep it a concise snapshot, not a changelog.
- **CLAUDE.md** (this file): update it when commands, project layout, conventions or working preferences change.
- Mention both updates in the session's final summary.

## Working with the user

- Experienced C# developer: be concise, skip C# basics.
- Flag decisions with a real trade-off and let the user choose; don't decide silently.
- Verify anything version-specific (.NET 10, EF Core 10, package versions, templates) instead of assuming older patterns.
- Propose architecture changes before implementing them.

## Commands

```bash
dotnet tool restore                                                  # dotnet-ef (local tool)
dotnet build                                                         # warnings are errors
dotnet test                                                          # xUnit v3 on Microsoft.Testing.Platform
dotnet run --project src/Bookside.Web                                # http://localhost:5233, DB in src/Bookside.Web/App_Data
                                                                     # (.claude/launch.json "web" = same, for the Claude preview pane;
                                                                     #  "web-scratch" uses App_Data/scratch.db, use it for UI checks)
# UI screenshots: no prerendering, so `firefox --screenshot` is blank. Drive headless Firefox over Marionette instead
# (firefox --headless --marionette -remote-allow-system-access --no-remote --profile <tmp>; TCP 2828, length-prefixed
#  JSON; chrome-context pref layout.css.prefers-color-scheme.content-override = 0 for dark mode).
dotnet user-secrets set Bookside:GoogleBooks:ApiKey <key> --project src/Bookside.Web   # optional Google Books fallback
dotnet ef migrations add <Name> --project src/Bookside.Data.Sqlite   # after any model change
dotnet ef migrations script --project src/Bookside.Data.Sqlite       # review generated SQL
docker compose up -d --build                                         # on the Docker host (no Docker on the dev Mac); see README "Deploy"
```

## Layout and dependency direction

- `src/Bookside.Core`: entities, domain rules, `Common/` (`Result`, `ServiceError`, paging), service interfaces + DTOs
  in `<Area>/Services/`. **No EF Core or HTTP dependencies.**
- `src/Bookside.Data`: provider-agnostic EF Core model (`BooksideDbContext`, `Configurations/`) and internal service
  implementations in `Services/`, registered by `AddBooksideServices()`. No provider packages.
- `src/Bookside.Data.Sqlite`: SQLite wiring, `Migrations/`, design-time factory. References Data.
- `src/Bookside.Integrations`: external API clients implementing Core interfaces (typed HttpClient + standard
  resilience handler), registered by `Add<Provider>Client()` extensions that return the `IHttpClientBuilder`.
  Dictionary providers: several `IDictionaryLookupService` registrations, tried in registration order by
  `DefinitionService`; new ones go through `AddDictionaryClient<TClient, TOptions>` (own client name) and reuse
  `DictionaryHttp` + `MeaningListBuilder`. Book providers (`Books/`): several `IBookLookupService` registrations
  tried in order by `BookMetadataService`; same `AddLookupClient` helper, shared `BookHttp`, covers through the
  shared `BookCoverDownloader` (`book-covers` client). Both kinds share `LookupHttp` for GETs.
- `tests/Bookside.Integrations.Tests`: client tests through the real registration, with `StubHttpMessageHandler` as the
  primary handler (retry delay zeroed via the `{clientName}-standard` resilience options; covers use the
  `BookCoverClientName` client).
- `tests/Bookside.Data.Tests`: tests against real temp SQLite files (`SqliteTestDatabase`); service tests derive
  from `Services/ServiceTestBase` (production registrations + `FakeDictionaryLookupService`; pass fallback
  sources to the base constructor for multi-provider tests; override `ConfigureServices` to register e.g. a
  `TestClock` as the `TimeProvider`).
- `src/Bookside.UI`: Razor class library with pages (`Pages/`), layout, shared `Components/`, form models (`Forms/`) and
  `wwwroot/bookside.css`; display logic with no markup as plain classes (e.g. `BookTimeline`, unit-tested without
  bUnit). References only Core + `Microsoft.AspNetCore.Components.Web` (no ASP.NET Core framework), so
  MAUI can reuse it. Components inject Core service interfaces.
- `src/Bookside.Web`: Blazor Web App host (Interactive Server, no prerendering): `Program.cs`, `App.razor`, `Routes.razor`,
  error page, minimal API endpoints (`CoverEndpoint`). Config: `Bookside:DatabasePath`, `Bookside:DataProtectionKeysPath`.
- `Dockerfile` / `docker-compose.yaml` / `.dockerignore` at the root: the image copies only the root build files
  (`global.json`, `Directory.*.props`, `.editorconfig`) and `src/`; add any new root-level build file to the `COPY`.
- `tests/Bookside.UI.Tests`: bUnit tests deriving from `ComponentTestBase` (production registrations on temp SQLite,
  controllable `FakeDictionaryLookup` via `Dictionary`, `FakeBookLookup` via `BookLookup`) and
  `WebApplicationFactory<Program>` endpoint tests (`Endpoints/WebHostFactory`).
- Planned: `Bookside.Api` + `Bookside.App` (MAUI Blazor Hybrid).

## Conventions

- Match the existing code style and keep it consistent across the project. Before writing code, look at nearby files
  and copy their formatting and patterns, e.g. XML doc layout (if `<summary>` text sits on its own line, keep doing
  that), member ordering, naming, comment style, file structure. Don't introduce a new style alongside an existing one.

- Format to standard C# conventions (.NET runtime / Microsoft style), and use the rules below where they're more
  specific. Where older code breaks these rules, follow the rules, not the older code.
- Line length: aim for ~80 characters, 100 max where reasonable (`max_line_length = 100` in `.editorconfig`).
  Break long argument lists, conditions, LINQ chains and Razor attribute lists one item per line rather than
  letting them run on. Older code has many longer lines: fix them only in code you're already changing.
- Expression bodies: when the parameter list wraps (one parameter per line), keep `=>` at the end of the last
  parameter line and indent the body one level deeper than the parameters, so it doesn't line up with them.
  When wrapped generic arguments are followed by wrapped call arguments, indent the call arguments one level
  deeper than the type arguments.
- Comments (XML docs, `//`, Razor `@* *@`): wrap at ~80 columns including indentation, even when the code
  around them is longer. Keep each `<see cref="…"/>` whole on one line.
- XML docs: `<summary>` and `</summary>` on their own lines with the text between, even for one sentence:
  ```csharp
  /// <summary>
  /// Text.
  /// </summary>
  ```
  Short `<param>`/`<returns>` may stay on one line. Don't copy the older one-line summaries.

- Package versions live only in `Directory.Packages.props`; common build settings in `Directory.Build.props`.
- Entities derive from `Entity` (UUIDv7 `Id`, `CreatedAtUtc`/`UpdatedAtUtc` stamped in `SaveChanges`). All DateTimes are UTC.
- Enums are stored as strings. Constraints belong in entity configurations (check constraints, indexes, delete behaviour).
- Every model change needs a migration; a test fails if the model has changes with no migration.
- Migrations are provider-specific and live only in `Bookside.Data.Sqlite`; they are generated code (analyzers off).
- Use EF Core directly in services (no repository layer). Blazor components inject the service interfaces, never a DbContext.
- Services return DTOs (never entities) and report expected failures as `Result`/`Result<T>` with a `ServiceError`
  (add codes to `ErrorCodes`); exceptions are for bugs. Validate with `InputValidator` and return all field errors.
  Each service method creates its own context from the factory.
- String length limits are constants on the entity (e.g. `Book.TitleMaxLength`), shared by configuration and validation.
- External APIs sit behind Core interfaces; implementations go in `Bookside.Integrations`.
- UI must work equally well on desktop and mobile; neither is the priority (not "mobile-first"). Check both widths
  (and dark mode). Plain CSS: tokens in `bookside.css`, component styles in scoped `.razor.css`; no component library.
- Shared page/list styles (toolbar, chips, empty, more, back, section-header) live in `bookside.css`; page-specific ones stay scoped.
- Component/page code: no `ConfigureAwait(false)` (CA2007 is off in UI/Web/UI.Tests). Pages are `*Page.razor` to avoid
  clashing with service/entity names. Forms validate via the services and `ServiceErrorValidator`, not DataAnnotations.
- bUnit 2: set `[SupplyParameterFromQuery]` values by navigating `BunitNavigationManager` before `Render`; qualify
  `Xunit.TestContext` (bUnit also has a `TestContext`).
- Tests: underscore sentence names, pass `TestContext.Current.CancellationToken`.
