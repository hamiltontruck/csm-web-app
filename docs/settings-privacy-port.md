# Settings & Privacy

The eight Settings categories adapt the user-provided reference repository `hamileto2021-glitch/HBG-ConstructionSiteManagement` at `e8192220375c4fb3f5759ff4c3be8fa954b6842c`. Current authentication, permissions, navigation branding and business workflows remain the integration boundary.

Preferences preserve all existing fields and support Save, Discard, Restore defaults, theme, compact spacing, language on supported screens, remembered navigation groups, and date/time formatting in Settings history. Failed loads and saves do not imply successful persistence. Notification preferences are stored; delivery/category filtering and optional analytics collection are not enabled by these controls.

Self-account endpoints under `api/auth` provide profile edits, assigned organization sites, new login history, active sessions and refresh-session revocation. Profile edits only accept first name, last name, phone and job title. Current-session verification is required before revoking another session. Existing access tokens can remain valid until expiry. Password changes retain the existing 12-character complexity policy and return the browser to sign-in.

Personal JSON download contains only the caller's safe account and preference data. Company privacy requests are submissions for review, including deletion requests; they do not delete accounts. Administrative audit remains behind the existing user-management authorization and permission checks.

## Database release order

`20261111142950_AddAccountLoginHistory` is an additive migration creating `LoginHistoryEntries` with a restrictive user foreign key and `(UserId, OccurredAtUtc)` index. It does not backfill history, alter existing business tables, or delete records. The generated idempotent script is in [migrations/20261111142950_AddAccountLoginHistory.sql](migrations/20261111142950_AddAccountLoginHistory.sql).

1. Confirm production's existing EF migration history and absence/presence of this migration.
2. Review and apply only this additive script through the authorized migration path.
3. Deploy the API and verify readiness before deploying Web.

Startup migration configuration remains unchanged. Do not deploy the history-writing API before the table exists. Local PostgreSQL tests exercise migration up/down/up while preserving a pre-existing user and fresh-context successful/failed login history. The down migration is for isolated testing; production release does not execute it.

## Verification

Run the full infrastructure suite with `CSM_TEST_POSTGRES`, EF pending-model check and Compose validation. `scripts/qa/settings-preferences.test.cjs` tests the browser runtime. `scripts/qa/settings-privacy.cjs` runs the real Web app with a local fake API at widths 320, 390 and 1280, including Save/reopen, discard/defaults, failures, theme, language, lazy account panels, protected current session and personal JSON download. It uses no production credentials or business records. Production preference save/reopen remains separate manual user proof.
