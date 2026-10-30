# CSM Blue & Gold workspace theme

The shared `wwwroot/csm-theme.css` is loaded after Bootstrap, app.css and Blazor isolated styles. Workspace rules are scoped to `.csm-workspace` on MainLayout; the login finish uses `.csm-login-shell`. Existing permission checks, service calls, routes and validation remain unchanged.

The theme covers headings, module cards, dashboard metrics, navigation, Bootstrap buttons, controls, forms, filters, tables, pagination, alerts and dialogs. Navy is the primary action color; muted gold marks active navigation, card edges and keyboard focus. Status colors retain their meaning. Mobile inputs use 16px text and controls have 44px touch targets. Bare legacy tables scroll inside their own boundary. Reduced motion and print presentation are supported.

## Verification

- .NET SDK 10.0.403, Release build of CSM.Web: 0 warnings, 0 errors.
- Actual local Blazor app with a disposable mock HTTP API; no production API/database access.
- 25 rendered route/viewport combinations: dashboard, equipment, users table, create-user form and procurement at 320, 390, 768, 992 and 1440px.
- No document horizontal overflow; shared brand stylesheet/token loaded in every case.
- Mobile menu selection/closing and reachable Sign Out checked at 320, 390 and 768px.
- Mobile form input: 16px font, 46px height. No browser page errors.
- Desktop dashboard/users and phone users/form screenshots visually inspected.
- Authenticated Render production smoke remains a deployment check; local fixtures do not prove production API behavior.

## Reproduce

Build `CSM.Web/CSM.Web.csproj` in Release. Install Playwright in your test environment and its Chromium browser, then run:

```sh
node scripts/qa/blue-gold-smoke.cjs
```

The script launches the local web app and mock API on ports 5057 and 5056. It performs a fixture login only; business forms are never submitted. `CSM_DOTNET` selects the dotnet executable, `CSM_CHROMIUM_PATH` optionally selects an installed Chromium executable, and `CSM_SCREENSHOT_DIR` optionally saves screenshots. Playwright must be resolvable by Node (for example via the test environment's NODE_PATH).
