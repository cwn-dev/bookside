# Bookside

A personal reading companion: library / reading log and a vocabulary builder.

## Layout

| Project | Role |
|---|---|
| `src/Bookside.Core` | Entities, domain rules, service contracts + DTOs, `Result` types. No EF/HTTP dependencies. |
| `src/Bookside.Data` | Provider-agnostic EF Core model (`BooksideDbContext`, configurations, save rules) and service implementations (`AddBooksideServices`). |
| `src/Bookside.Data.Sqlite` | SQLite wiring (`AddBooksideSqlite`, `InitializeBooksideDatabaseAsync`), migrations, design-time factory. |
| `tests/Bookside.Data.Tests` | xUnit v3 tests against real temp SQLite files. |

Also `src/Bookside.Integrations` (dictionary clients), `src/Bookside.UI` (Razor class library), `src/Bookside.Web`
(Blazor Web App host) and their test projects; see CLAUDE.md. Planned: `Bookside.Api` + `Bookside.App` (MAUI Blazor Hybrid).

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

## Deploy (Docker)

One container, SQLite in `./data` on the Docker host, plain HTTP behind a reverse proxy that terminates TLS.

```bash
git clone <repo> bookside && cd bookside
mkdir -p data && sudo chown 1654:1654 data   # the image runs as the non-root `app` user (UID 1654)
echo "TZ=Europe/London" > .env               # server time zone: "today" for reading dates, reading-log times
docker compose up -d --build
```

- **Data:** `data/bookside.db` (+ `-wal`/`-shm`) and `data/keys/` (ASP.NET Core Data Protection keys, stored
  unencrypted: keep the folder private). `data/` must be on a local filesystem (SQLite WAL doesn't work on NFS/SMB).
- **Backup:** stop the container and copy `data/`, or while it runs: `sqlite3 data/bookside.db ".backup backup.db"`
  (copying only `bookside.db` while it runs can miss changes still in the WAL file).
- **Port:** published on `127.0.0.1:8080` for a proxy on the same host. `.env` settings: `BOOKSIDE_PORT=…`,
  `BOOKSIDE_BIND=0.0.0.0` when the proxy runs elsewhere (then keep the port reachable only by the proxy).
- **Reverse proxy:** must pass WebSockets (Blazor Server), e.g. nginx needs `proxy_http_version 1.1` plus the
  `Upgrade`/`Connection` headers; Caddy and Traefik do it by default. `X-Forwarded-For`/`-Proto` are trusted.
- **No authentication yet:** don't expose it beyond your LAN/VPN.
- **Update:** `git pull && docker compose up -d --build` (migrations run at startup).
