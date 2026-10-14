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

## Hosting configuration

Host the API and web app as separate .NET 10 services. Use a reachable SQL Server database for the API. Set these environment variables in the hosting platform's secret/configuration panel:

### API

- `ConnectionStrings__DefaultConnection` — production SQL Server connection string
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
