# FinanceOfficer Assigned-Site Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans inline, task-by-task, with one fresh whole-branch reviewer at the end. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Make FinanceOfficer a valid, usable full-finance role restricted to live assigned sites, with Sites_View and unchanged Accountant behavior.

**Architecture:** Seed an explicit role/permission ceiling and use a live action-specific effective-actor resolver following the established ProcurementOfficerScope pattern. Reuse existing site finance/financial read machinery, add missing parent/reference checks, and align API role options and Web routes/lookups with the backend.

**Tech Stack:** .NET10, EF10/Npgsql/PostgreSQL16, Blazor, xUnit/bUnit, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-11-finance-officer-site-design.md`

## Global Constraints
- Base f82b3b2cdca090b160d95e212483160c8ff229af; isolated worktree `/workspace/finance-officer-site-fix`.
- Full finance incl approve/reverse in assigned sites, plus Sites_View, explicitly approved. Keep Accountant company-wide behavior.
- No assignment means zero FinanceOfficer authority; no inactive/deleted/foreign/malformed assignment may widen access. DefaultSiteId is not a grant.
- Same-action independently granted roles retain their original authority; unrelated/ungranted global roles cannot widen a FinanceOfficer action.
- Shared cost-code/vendor definitions read-only; no HR/payroll, admin, procurement write, generic export, notification or unrelated grants introduced. No enum renumbering or reference-only permission identifiers.
- No schema changes expected. No root production account/password/business transaction mutations; user retries account creation after release.

## Review Focus
- Mixed-role grants differ by action: read authority must not widen write authority (Tasks1–4).
- Parent/child or old/new references carry different site/company attribution (Task3).
- Finance company endpoints, PDF and dashboard/report totals expose aggregates outside assigned sites (Tasks3–4).
- Empty, deleted or foreign assignment sets and removed grants after a token was issued (Tasks1–4).
- UI pages eagerly load HR/procurement or broad options despite site finance being authorized (Task5).

### Task1: Seeded role, ceiling, and live scope resolver
**Files:** Domain/Security/FinanceOfficerPermissionRules.cs; RolePermissionRules.cs; Infrastructure/Persistence/DatabaseInitializer.cs; Infrastructure/Security/FinanceOfficerScope.cs; Services/SiteFinanceScope.cs; tests FinanceOfficerAuthorityTests.cs.
**Interfaces:** `FinanceOfficerScope.ResolveActorAsync(ApplicationDbContext db, Guid actorId, GranularPermission action, CancellationToken ct, Func<string,Exception> error): Task<User?>`; null means no FinanceOfficer identity and no alteration of legacy behavior, otherwise return an untracked actor narrowed to independently granted roles or the valid FinanceOfficer role. `FinanceOfficerScope.IsOfficer(User):bool` identifies the effective site-only actor. Domain `FinanceOfficerPermissionRules.Allows(string? role,string permission):bool` is shared by evaluator and default seeding.
- [ ] Write behavioral tests: initialized DB contains canonical FinanceOfficer and exactly supported defaults, no Accountant grant changes; real UserService can create the test role after initialization; unseeded baseline fails. Runtime role ceiling rejects Users/HR/cost-code-manage/export/stale grants. Resolver respects same-action mixed roles, inactive/deleted user/role/link and missing company/grant.
- [ ] Run focused tests and observe valid RED (not compiler/setup errors).
- [ ] Seed only requested role, explicit finance/Sites/report defaults; add role ceiling and resolver; expand SiteFinanceScope for the effective FinanceOfficer with active valid sites and zero-assignment deny.
- [ ] Run focused/full tests, record receipts and commit.

### Task2: Real assignable-role options
**Files:** new Application/Users/IUserRoleOptionsService.cs; Infrastructure/Services/UserRoleOptionsService.cs; Api/Controllers/UserRoleOptionsController.cs; DI; Web UserWebService/Create/Edit User; tests FinanceOfficerRoleOptionsTests.cs/UI.
**Interfaces:** `GetAssignableRolesAsync(Guid actorId, CancellationToken ct):Task<IReadOnlyCollection<string>>`; GET `api/users/role-options`. Active SuperAdmin or CompanyAdmin, with live Users_Create OR Users_Edit, receives actual non-deleted assignable names excluding SuperAdmin as the current form does. No new Users_View prerequisite. Failure blocks unsafe UI submission and offers retry.
- [ ] RED for actual roles only, FinanceOfficer present after seed, phantom names absent, create-only admin permitted, reader/inactive rejected, Create/Edit UI loads supported names and retains fields on load/save error.
- [ ] Implement server/service/client and replace hardcoded lists in both forms without changing identity/password/site validation.
- [ ] Run focused/full tests, record receipts and commit.

### Task3: Finance reads/writes, parents and side effects
**Files:** BudgetService/BudgetLineService, InvoiceService/InvoiceLineService, ExpenseService, PaymentService, PettyCashAccountService/PettyCashTransactionService, CostCodeService, PDFs as needed; tests FinanceOfficerFinanceTests.cs and reference/transaction cases.
**Interfaces:** Each GetActorAsync consumes Task1 resolver with its real action permission; do not use mutable per-service scope state. Use view for reads/options/PDF, manage/submit/approve for writes. Existing typed exceptions/DTOs/state machines stay intact.
- [ ] RED matrix: own assigned site lists/direct IDs and normal create/edit/status/reverse/lines/transactions succeed; other/foreign/null-site/empty/inactive assignments deny without mutation. Both old/new scope and parent budget/invoice/account ownership are checked; invalid project/invoice/line/account references cannot cross boundaries. No payroll-linked FinanceOfficer-only writes or shared catalog mutations. Verify balances/status/parent totals remain unchanged on denied attempts, including fresh contexts.
- [ ] Integrate resolver, necessary FinanceOfficer role gates, filters/guards and parent/reference checks. Preserve independent-role and non-FinanceOfficer flows, including Accountant/company admins.
- [ ] Run focused/full tests and commit.

### Task4: Sites, financial aggregates and scoped lookup endpoints
**Files:** SiteService, financial project/invoice options, FinancialReadScope, FinancialSummaryService, ReportingService/dashboard controllers/services as needed; tests FinanceOfficerReportingTests.cs and PostgreSQL proof.
**Interfaces:** Task1 resolver for Sites_View, Reports_View and Reports_FinancialSummary_View independently; FinancialReadScope consumes the effective actor. Existing endpoint shapes remain; no corporate totals for FinanceOfficer-only actions.
- [ ] RED for assigned-site list/details only; foreign/direct site and selectedCompanyId forgery denied; company-level rows excluded from each finance aggregate, permitted-site summaries/PDFs/options only; same-action corporate role grant works without widening unrelated actions.
- [ ] Apply scoped effective actor before company resolution/role checks, active FinanceOfficer site predicates, and existing scoped finance projections.
- [ ] Add real PostgreSQL translation/persistence checks (including create/approve/reverse and denied cross-site atomicity). EF pending-model must remain empty.
- [ ] Run focused/full tests and commit.

### Task5: Usable Web finance workspace
**Files:** WorkspaceAccess.cs, finance hub/pages/actions/forms, Dashboard/Reports where needed, bUnit and browser fixture.
**Interfaces:** Sites_View and actual finance permissions/roles determine navigation; FinanceOfficer-only uses site entry points, no company master writes/payroll/procurement lookups. Backend remains authoritative for mixed roles and record scope.
- [ ] RED for FinanceOfficer assigned-site finance cards/actions/routes, no company routes or unsupported eager API calls; read-only company lookup options work from finance endpoints; Accountant existing UI unchanged. API errors stay local to the form.
- [ ] Implement gates/labels/scoped links and lazy authorized options; preserve form typing, save payloads, confirmations and status validation.
- [ ] Browser uses local fake API: Create User role options, site finance navigation and ordinary form interaction at320/390/1280; no production credentials or transactions.
- [ ] Run focused/full tests and commit.

### Task6: Review and release
- [ ] Full solution/PostgreSQL suite, browser and model/Compose/container/Web smoke green on exact head; source tree matches reviewed commits.
- [ ] Dispatch one fresh most-capable whole-branch reviewer per executing-plans; regrade by user effect and fix blocking findings in one RED→GREEN pass plus whole suite. No per-task reviewer/implementer delegation.
- [ ] Merge tested/reviewed tree using existing authorized workflow. API startup seeds requested role/grants in existing tables; no migration flag/schema change and no manual account/business mutation. API ready before Web. Verify exact commits, bounded runtime errors, public auth boundaries and compiled assets.
- [ ] Report working scope/limits concisely in Oromo and ask user to retry the existing FinanceOfficer creation form with selected Hamilton Test Site. Do not claim authenticated production site/persistence proof before manual user confirmation.
