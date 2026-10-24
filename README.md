# Bookside

A personal reading companion: library / reading log and a vocabulary builder.

## Layout

| Project | Role |
|---|---|
| `src/Bookside.Core` | Entities, domain rules, service contracts + DTOs, `Result` types. No EF/HTTP dependencies. |
| `src/Bookside.Data` | Provider-agnostic EF Core model (`BooksideDbContext`, configurations, save rules) and service implementations (`AddBooksideServices`). |
| `src/Bookside.Data.Sqlite` | SQLite wiring (`AddBooksideSqlite`, `InitializeBooksideDatabaseAsync`), migrations, design-time factory. |
| `tests/Bookside.Data.Tests` | xUnit v3 tests against real temp SQLite files. |

Planned: `Bookside.UI` (Razor class library), `Bookside.Web` (Blazor Web App, Interactive Server, Docker),
`Bookside.Integrations` (dictionary + book lookup clients), `Bookside.Api` + `Bookside.App` (MAUI Blazor Hybrid).

## Commands

```bash
dotnet tool restore
dotnet build
dotnet test

# Add a migration after changing the model
dotnet ef migrations add <Name> --project src/Bookside.Data.Sqlite

# Generate the SQL for review
dotnet ef migrations script --project src/Bookside.Data.Sqlite
```

Hosts apply migrations at startup via `services.InitializeBooksideDatabaseAsync()`.
In Docker the database path will be `/data/bookside.db` on a mounted volume.
