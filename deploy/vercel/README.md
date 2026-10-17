# Vercel deployment

Import `hamiltontruck/csm-web-app`, branch `main`, using root `./` and preset
`Other`. Leave build/output commands unset. Root `vercel.json` defines two
container services using the repository root as the Docker build context.
The web service reaches the API through a deployment-specific service binding;
no localhost URL or hard-coded production hostname is required.

Before deploying, add these encrypted Vercel environment variables for Production:

- `ConnectionStrings__DefaultConnection`: the existing Neon Npgsql connection
  string, including SSL settings. Copy it directly from your local secret store
  to Vercel; never commit it or paste it into a chat.
- `Jwt__SecretKey`: a persistent random secret of at least 32 characters.
- `Database__ApplyMigrationsOnStartup`: `false` for the existing migrated database.
  Apply schema changes as a separate controlled release step.

The existing database already contains the SuperAdmin, so bootstrap account
credentials are not needed for it. For a new empty database, initialize its
schema and supply `BootstrapAdmin__Email` and `BootstrapAdmin__Password` privately.

Deploy, then verify `/login`, sign-in, company selection, and `/dashboard`.
Inspect both services' runtime logs if startup or sign-in fails. A successful
build alone does not verify the database connection or interactive Blazor flow.

Vercel containers and service bindings are beta. Blazor Server keeps circuits
in memory and ProtectedSessionStorage uses server data-protection keys. Validate
reconnects and scale-out before relying on this deployment for normal work;
container replacement can require sign-in again without a shared key ring.
