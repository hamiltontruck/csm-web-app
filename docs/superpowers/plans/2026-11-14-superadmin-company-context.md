# SuperAdmin Company Context Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair remaining legitimate companyless SuperAdmin actions without widening permissions or normal tenant scope.
**Architecture:** A shared company-selection DTO/resolver handles explicit company context and independently granted global authority. Resource-owned actions derive company from their owning record. HR, finance, and compliance changes stay in separate worktrees and integrate after targeted regression tests.
**Tech Stack:** .NET 10, EF Core/Npgsql, Blazor Server, xUnit/bUnit, Playwright.
**Spec:** `docs/superpowers/specs/2026-11-14-superadmin-company-context.md`

## Global Constraints

Use live database roles/grants, not token role/company claims. Do not assign actor.CompanyId, pick an arbitrary company, add grants, broaden existing role policies, create production records or migrate schema. Preserve assigned SuperAdmin own-company invoice/payment list defaults, expense settlement branches, and original staged Vercel files.

## Review Focus

- Ungranted/deleted SuperAdmin membership combined with a granted company role must not enable global scope.
- A selected company conflicting with any resource anchor fails before a record, balance, meter or approval changes.
- Missing/inactive/deleted companies produce actionable selection errors and no writes.
- Empty selections, stale asynchronous option responses and HTTP failures preserve inputs and cannot submit mismatched contexts.
- Read-only/role-excluded callers stay denied; company-only lists do not become unfiltered global reads.

### Task 1: Shared context contract (root)

Files: create Application/Common/Dtos/CompanySelectionOptions.cs, Infrastructure/Security/CompanyContextResolver.cs, Web/Models/Common/CompanySelectionOptions.cs, Web/Components/CompanySelector.razor, Infrastructure.Tests/CompanyContextResolverTests.cs.
Interfaces: `HasGlobalAuthorityAsync(db, actorId, GranularPermission[] required, token)`; `ResolveAsync(db, actorCompanyId, bool allowGlobal, Guid? selectedCompanyId, Func<string,Exception> error, token)`; `ForResource(actorCompanyId, bool allowGlobal, resourceCompanyId, selectedCompanyId, error)`; `OptionsAsync(db, actorCompanyId, bool allowGlobal, error, token)`. CompanySelector consumes Options, Guid? Value/ValueChanged, optional Id. Callers authorize the action before resolving context.

- [ ] Write reflection-based tests that compile before the resolver exists; prove missing selection, valid selection, inactive/deleted/missing company, foreign ordinary-company selection and independent/live SuperAdmin authority.
- [ ] Run targeted test and observe valid RED, implement minimal helper/DTO/component, rerun targeted tests, commit shared contract.

### Task 2: HR (isolated HR worktree)

Files: TimesheetService, LeaveService, AttendanceService, SiteAssignmentService, ShiftService, ContractorService; relevant Application interfaces/create DTOs/controllers; Web HR service/create models/pages; HR company-context regression tests.

- [ ] Reproduce lookup and owned-resource failures before edits.
- [ ] Resolve Leave/Attendance/SiteAssignment from employee with all existing relationship/assignment/geofence/overlap checks; make employee lookups global only for independently granted SuperAdmin authority.
- [ ] Add Shift/Contractor optional CompanyId and policy-equivalent company-options actions/forms. Retain Contractor HRManagement exclusion of pure SuperAdmin.
- [ ] Target-test valid companyless and selected-company success, invalid anchors/companies, revoked/mixed authority and normal tenant boundaries; report commit and validation for integration.

### Task 3: Finance (isolated finance worktree)

Files: ExpenseService, BudgetService, InvoiceService, PaymentService and partial expense settlement guard only if needed; invoice/payment DTOs/interfaces/controllers; Web Finance service and relevant site/company invoice/payment pages; finance company-context tests.

- [ ] Reproduce site-list/site-invoice/options/company-list/core-payment failures before edits.
- [ ] Derive site reads/invoices and anchored payments from resources, rejecting inconsistent context.
- [ ] Add optional company selection to company-only invoice/payment lists, vendor options and unanchored creates; keep omitted assigned-company behavior and all FinanceOfficer/effective-role restrictions.
- [ ] Leave expense-linked settlement logic intact; if CompanyId is supplied, require agreement with expense ownership. Target-test all repaired paths and boundaries; report commit and validation.

### Task 4: Compliance and notifications (root)

Files: DocumentService, DocumentCategoryService, NotificationService; relevant Application/Web DTOs/interfaces/controllers; DocumentCreate/DocumentCategoryCreate and ComplianceService; targeted tests.

- [ ] Reproduce companyless failures before edits.
- [ ] Resolve documents from site/project/employee when anchored, otherwise explicit company; add category company selection and authorized options.
- [ ] Resolve SuperAdmin notification creation from recipient company or explicit validated context when the recipient has no company. Keep recipient-only read/mark privacy; do not broaden normal-company notification visibility.
- [ ] Test company choices, all anchor conflicts, denied authority, confidential/private-field boundaries and safe form errors.

### Task 5: Integration and release (root)

- [ ] Review each domain diff and cherry-pick only its owned files; audit remaining exact-error call sites and classify guarded/role-ineligible paths.
- [ ] Run full PostgreSQL suite, Release builds, no-pending-model check and 390/1280px browser flows using local fixtures only.
- [ ] Compare exact local/remote trees, publish PR referencing #146, wait applicable checks, normal merge, deploy API then Web once per service, verify exact live commits, HTTP assets and filtered startup errors. Record any genuine remaining limitations rather than claiming a blanket bypass.
