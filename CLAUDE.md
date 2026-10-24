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
dotnet ef migrations add <Name> --project src/Bookside.Data.Sqlite   # after any model change
dotnet ef migrations script --project src/Bookside.Data.Sqlite       # review generated SQL
```

## Layout and dependency direction

- `src/Bookside.Core`: entities, domain rules, service interfaces + DTOs. **No EF Core or HTTP dependencies.**
- `src/Bookside.Data`: provider-agnostic EF Core model (`BooksideDbContext`, `Configurations/`). No provider packages.
- `src/Bookside.Data.Sqlite`: SQLite wiring, `Migrations/`, design-time factory. References Data.
- `tests/Bookside.Data.Tests`: tests against real temp SQLite files (`SqliteTestDatabase`).
- Planned: `Bookside.Integrations`, `Bookside.UI` (RCL), `Bookside.Web` (Blazor Interactive Server, Docker),
  later `Bookside.Api` + `Bookside.App` (MAUI Blazor Hybrid).

## Conventions

- Package versions live only in `Directory.Packages.props`; common build settings in `Directory.Build.props`.
- Entities derive from `Entity` (UUIDv7 `Id`, `CreatedAtUtc`/`UpdatedAtUtc` stamped in `SaveChanges`). All DateTimes are UTC.
- Enums are stored as strings. Constraints belong in entity configurations (check constraints, indexes, delete behaviour).
- Every model change needs a migration; a test fails if the model has changes with no migration.
- Migrations are provider-specific and live only in `Bookside.Data.Sqlite`; they are generated code (analyzers off).
- Use EF Core directly in services (no repository layer). In Blazor components use `IDbContextFactory<BooksideDbContext>`.
- External APIs sit behind Core interfaces; implementations go in `Bookside.Integrations`.
- Tests: underscore sentence names, pass `TestContext.Current.CancellationToken`.
