# CSM Web Application

This repository contains a .NET 10 solution with a Blazor Server web app, ASP.NET Core API, and three supporting projects. The web interface already exists in `CSM.Web`; it talks to `CSM.Api`, which stores data in SQL Server.

## Projects

- `CSM.Web` — Blazor Server web application
- `CSM.Api` — ASP.NET Core API
- `CSM.Application` — use cases and application services
- `CSM.Domain` — domain models and rules
- `CSM.Infrastructure` — SQL Server, authentication, and persistence

## Run locally on Windows

Install the .NET 10 SDK and SQL Server Express. Open `CSM.sln` in Visual Studio, or use PowerShell from this folder.

1. Create the local database connection in `CSM.Api/appsettings.Development.json` if your SQL Server instance differs from `.`\SQLEXPRESS.
2. Set a private local JWT key using User Secrets (do not commit the value):

```powershell
dotnet user-secrets set "Jwt:SecretKey" ([Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))) --project CSM.Api
```

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

## Database migrations and first administrator

The API does not change the database schema automatically at startup. Build and run the EF migration bundle before starting the API against a new or upgraded database. The `CSM CI` workflow builds self-contained Linux and Windows bundles and uploads them as a workflow artifact named `csm-db-migration-bundles-<commit>`. Download the artifact from the successful workflow run, extract it, then run the matching bundle with a SQL Server connection string that has permission to apply schema changes:

```bash
./csm-db-migrate --connection "$CSM_CONNECTION_STRING"
```

On Windows PowerShell:

```powershell
.\csm-db-migrate.exe --connection $env:CSM_CONNECTION_STRING
```

Only run the migration bundle after confirming the target server and database. Keep the connection string private.

On the first API start against an empty database, configure a bootstrap administrator. Supply the values through your secret store or environment variables `BootstrapAdmin__Email` and `BootstrapAdmin__Password`. The password must have at least 12 characters and include uppercase, lowercase, numeric, and special characters. Existing databases with a SuperAdmin role assignment are not reset by these settings.

For local development, set User Secrets in the API project:

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" --project CSM.Api
dotnet user-secrets set "BootstrapAdmin:Password" "<your-private-password>" --project CSM.Api
```

The bootstrap account is created only if no SuperAdmin assignment exists. Choose a private password and change it after first sign-in if your deployment process requires rotation.

## Hosting configuration

Host the API and web app as separate .NET 10 services. Use a reachable SQL Server database for the API. Set these environment variables in the hosting platform's secret/configuration panel:

### API

- `ConnectionStrings__DefaultConnection` — production SQL Server connection string
- `BootstrapAdmin__Email` and `BootstrapAdmin__Password` — required only for the first API start when the database has no SuperAdmin; store them as secrets and remove them after the account is created
- `Jwt__SecretKey` — random secret with at least 32 characters
- `Jwt__Issuer` — JWT issuer, matching the configured client validation
- `Jwt__Audience` — JWT audience, matching the configured client validation
- `Cors__AllowedOrigins__0` — exact public web app origin, such as `https://csm.example.com`
- `ASPNETCORE_URLS` — listener address required by the hosting platform, commonly `http://0.0.0.0:8080` behind its TLS proxy
- `ASPNETCORE_ENVIRONMENT=Production`

### Web app

- `ApiSettings__BaseUrl` — absolute API URL ending in `/api/`, such as `https://api.example.com/api/`
- `ApiSettings__PublicBaseUrl` — browser-reachable API URL used by offline synchronization; normally the same public API URL
- `ASPNETCORE_URLS` — listener address required by the hosting platform
- `ASPNETCORE_ENVIRONMENT=Production`

Keep the API and web app on HTTPS in production. Configure the API CORS origin to match the browser origin exactly. The development SQL Server address and local API URL are stored only in development settings.

## Run with Docker

Copy `.env.example` to `.env`, replace both secret values, and run:

```powershell
docker compose up --build
```

Open `http://localhost:8080`. The public API is available at `http://localhost:5065/api/`. The SQL Server data is kept in the `csm_sql_data` volume.
