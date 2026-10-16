# CSM Web Application

CSM is a .NET 10 solution with a Blazor Server web app and ASP.NET Core API. The API stores application data in PostgreSQL and can connect to the Neon project `csm-db`.

## Projects

- `CSM.Web` — Blazor Server web application
- `CSM.Api` — ASP.NET Core API
- `CSM.Application` — use cases and application services
- `CSM.Domain` — domain models and rules
- `CSM.Infrastructure` — PostgreSQL persistence, authentication, and services

## Run locally on Windows

Install the .NET 10 SDK. In Visual Studio, open `CSM.sln`, or run these commands in PowerShell from this folder.

1. Store the Neon connection string as a local secret. Paste the `.NET` connection string copied from Neon into your PC terminal when prompted; do not put it in a source file or send it in chat:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<paste-your-Neon-connection-string>" --project CSM.Api
```

2. Create a private JWT signing key and bootstrap administrator credentials:

```powershell
dotnet user-secrets set "Jwt:SecretKey" ([Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))) --project CSM.Api
dotnet user-secrets set "BootstrapAdmin:Email" "<your-email>" --project CSM.Api
dotnet user-secrets set "BootstrapAdmin:Password" "<a-private-strong-password>" --project CSM.Api
```

The first API start applies the PostgreSQL migration and creates the database tables. The bootstrap password must have at least 12 characters and include uppercase, lowercase, numeric, and special characters. Keep all credentials private.

3. Trust the local HTTPS development certificate:

```powershell
dotnet dev-certs https --trust
```

4. Start the API and web app in separate terminals:

```powershell
dotnet run --project CSM.Api --launch-profile https
```

```powershell
dotnet run --project CSM.Web --launch-profile https
```

Open `https://localhost:7055`. The web app calls the API at `https://localhost:7051/api/`.

## PostgreSQL migrations

The API applies pending migrations at startup by default. Set `Database__ApplyMigrationsOnStartup=false` only when you plan to apply migrations through a separate release process. The CI workflow checks that the migration snapshot matches the current model.

The migration is a fresh PostgreSQL baseline for a new Neon database. The previous SQL Server migration source is retained in `SqlServerMigrationsArchive` for reference and is not used by the PostgreSQL app. It does not migrate data from SQL Server.

## Hosting configuration

Host the API and web app as separate .NET 10 services. Configure these values in the hosting platform's secret/configuration panel.

### API

- `ConnectionStrings__DefaultConnection` — Neon PostgreSQL connection string
- `BootstrapAdmin__Email` and `BootstrapAdmin__Password` — required only for first startup before a SuperAdmin exists; store as secrets
- `Jwt__SecretKey` — random secret with at least 32 characters
- `Jwt__Issuer` — JWT issuer, matching the configured client validation
- `Jwt__Audience` — JWT audience, matching the configured client validation
- `Cors__AllowedOrigins__0` — exact public web app origin, such as `https://csm.example.com`
- `Database__ApplyMigrationsOnStartup` — defaults to true; the API applies pending migrations at startup
- `ASPNETCORE_URLS` — listener address required by the hosting platform, commonly `http://0.0.0.0:8080` behind its TLS proxy
- `ASPNETCORE_ENVIRONMENT=Production`

### Web app

- `ApiSettings__BaseUrl` — absolute API URL ending in `/api/`, such as `https://api.example.com/api/`
- `ApiSettings__PublicBaseUrl` — browser-reachable API URL used by offline synchronization; normally the same public API URL
- `ASPNETCORE_URLS` — listener address required by the hosting platform
- `ASPNETCORE_ENVIRONMENT=Production`

Keep the API and web app on HTTPS in production. Configure the API CORS origin to match the browser origin exactly.

## Run with Docker

Copy `.env.example` to `.env`, set the Neon connection string, private JWT key, and bootstrap admin credentials, then run:

```powershell
docker compose up --build
```

The first API start creates the schema in Neon. Open `http://localhost:8080`; the API is available at `http://localhost:5065/api/`.

## Backup and restore

The API backup endpoints use PostgreSQL's `pg_dump` and `pg_restore` client tools. Install those tools on the API host and set `Database__PostgresToolsPath` if they are not on `PATH`. Backups are written to the API host's `Backups` directory; configure durable storage before relying on local files for production recovery.
