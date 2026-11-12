# FinanceOfficer: working role with assigned-site finance authority

## Approved intent

The user cannot create a FinanceOfficer: Create User shows the checkbox but the API returns `One or more selected roles are invalid.` The user explicitly wants FinanceOfficer to exist, wants site-only access, confirmed **full finance work at the selected site including budget/invoice/payment/petty-cash creation, editing, approval and reversal**, and additionally requested **Sites_View**. Their target account is to be assigned Hamilton Test Site only. Accountant's existing company-wide behavior must remain unchanged. The supplied Accountant permission screenshot is from the separate reference/local application; it is context, not permission to copy unsupported permission identifiers or unrelated HR/procurement/admin grants.

This is a permission integration project, not a one-line role seed. Current `AppRoles.All`/SeedRoles does not seed FinanceOfficer; Create User hardcodes unsupported names (Edit already reads the roles API); granular defaults, service role checks, finance scope and Web navigation do not implement a usable site-only FinanceOfficer.

## Approaches

Only seeding the name fixes creation but leaves finance unusable or potentially unscoped. Reusing/aliasing Accountant contradicts the user's site-only requirement. Implement a distinct FinanceOfficer using the existing granular permission system plus a shared live action-specific site boundary. This is the one approach consistent with the user's approved role and scope; existing .NET10/EF/PostgreSQL/Blazor architecture and authorization rules remain in place.

Implementation details below are the builder's choices within that authorization. Do not re-ask the already answered role/site/full-finance questions or present another merge/deploy menu.

## Role and grants

- Idempotently seed canonical FinanceOfficer and its supported granular defaults; preserve enum numbers and existing roles/grants, especially Accountant.
- Default/ceiling: Sites_View; finance budget view/manage, expense view/submit/approve, invoice view (current model also authorizes its management), payment view/manage including current status/reverse operations, petty-cash view/manage, cost-code view; Reports_View and Reports_FinancialSummary_View.
- Company-shared cost-code/vendor definitions are read-only lookups for permitted finance actions. No shared cost-code/vendor writes: those affect other sites and have no site owner.
- No new Users, company administration, HR/payroll management, procurement approvals, inventory management, compliance, or generic export authority. Preserve invoice/receipt PDF reads through the same scoped finance record checks. No new enum values merely to mimic the reference UI. Existing finance state machines, balances and validation still apply.
- Optional notification/generic export grants are not introduced by this request; any old unsupported FinanceOfficer grants are ineffective through per-role ceilings. Account/profile/preferences remain self-service as before.

## Live action scope

FinanceOfficer-only authority requires an active, non-deleted user with a company and active, non-deleted assignments to active, non-deleted sites in that same company. No assignments, only inactive/deleted assignments, foreign-company assignments or malformed site/company associations give **zero finance/site access**, never company-wide fallback. DefaultSiteId is not authorization.

Resolve the specific granular action against live user-role links, role state and grants. A separate role may retain its own authority only when it independently grants that same action and the existing service supports it. A globally named but ungranted role must not widen a FinanceOfficer action. An effective untracked actor containing only the independently authorized roles can reuse existing scope machinery, following ProcurementOfficerScope's established pattern. Do not mutate tracked role collections or weaken non-FinanceOfficer policies.

Filter finance lists, direct IDs, company endpoints, aggregates, lookups, PDFs and all writes. Reject company-level/null-site finance records for FinanceOfficer-only actions. Parent budget/invoice/account scope must govern lines and transactions. Check old and requested scope before updates/moves and before status or reverse side effects. Validate relevant project/invoice/site/company lineage; forged IDs cannot smuggle an unassigned site or company through a permitted parent. Shared cost codes/vendors must belong to the company and remain read-only. Payroll records have no site owner: do not introduce payroll lookup or payroll-linked write authority through FinanceOfficer-only payment access.

## Surfaces to integrate

1. Seeder, role ceiling/evaluator, any legacy/handler policy gates.
2. Create User role options from an authenticated server endpoint, retaining the already database-backed Edit role flow returning actual supported roles. Access requires active user-management actor and Users_Create or Users_Edit, not an additional Users_View grant. Preserve existing SuperAdmin assignment restrictions. Failed role-option load is visible and prevents unsafe submission; do not fall back to hardcoded phantom roles.
3. Budget and budget lines; invoices/lines/vendor options and PDF; expenses/status; payments/status/reverse/receipt; petty-cash accounts/transactions and approval; read-only cost codes.
4. Assigned sites/site details and finance project/invoice options; reporting/financial summary/dashboard scopes. A FinanceOfficer may read assigned-site financial aggregates, never company totals or another site's data. Suppress unsupported HR/payroll/procurement lookups and company entry points in Web.
5. Role-based finance controls/WorkspaceAccess/Finance hub, keeping UI consistent with API permissions while retaining backend enforcement.

## Verification and release

Behavioral RED→GREEN tests must reproduce missing role/options and denied own-site access, and cover own/other/foreign/null sites, removed/inactive/malformed assignments, inactive/deleted actors, missing/stale grants and mixed-role action scope. Exercise direct IDs, parent/child and old→new reference moves, status/reversal atomicity, totals/options/PDF paths, and unchanged Accountant/company-admin behavior. Add real PostgreSQL translation/persistence tests and bUnit/browser checks for role options and usable site finance navigation. Existing full suite baseline is 1,913 tests.

No production credentials, account creation, password changes or business transactions by the agent. User will retry the failed Create User form after release. No schema migration should be needed: roles/grants use existing tables. Full exact-head CI, a fresh independent whole-branch review, reviewed fixes and the normal authorized merge/API-before-Web Render release are required. Preserve the original `/workspace/csm-web-app` user's five staged Vercel changes. Worktree `/workspace/finance-officer-site-fix` starts at production/main `f82b3b2cdca090b160d95e212483160c8ff229af`.
