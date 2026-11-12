# FinanceOfficer assigned-site access

FinanceOfficer is a distinct seeded role. It can manage budgets and budget lines, expenses and approvals, invoices and lines, payments including completion/reversal, and petty cash at its live assigned construction sites. It can view those sites and their financial summaries. Default Site selects a convenience value; it grants no access.

Every action resolves current database role grants before choosing scope. An independently granted corporate read permission does not widen a FinanceOfficer write action. Inactive/deleted users, role links, roles, assignments or sites cannot supply this role's authority. No assignments means no site-owned records or write authority. Independent roles must both hold the action grant and be supported by that service. Embedded invoice payments and paid totals follow the parent site; projecting a response does not mutate tracked child relationships. Old/new record targets, parent lines and related project/invoice/cost-code references are checked before mutations.

Shared company cost codes and vendor references are read-only finance form options. FinanceOfficer does not acquire company master-data writes, user management, payroll/HR, procurement operations or generic report exports. Existing scoped invoice/receipt downloads retain their finance read checks. Existing Accountant company-wide permissions and scope are preserved.

Create User loads actual assignable server roles through its own Create/Edit-authorized endpoint, avoiding phantom role names and an extra Users_View prerequisite. Payment and petty-cash form capabilities come from current action grants, so an unrelated corporate read role cannot enable company-level writes or payroll payments. Mixed-role forms use authorized finance lookups when broad project/vendor capabilities are absent.

Supported FinanceOfficer defaults are seeded when its role is first created; a later administrator revocation is not restored on every restart. Unsupported stale grants are rejected by the runtime role ceiling.

Cost-code Activate/Deactivate controls require both a supported corporate role and Finance_CostCode_Manage, on site and company lists. Rejected changes show a local error and retain the previous status. Total Expenses counts recorded, non-cancelled expense amounts including tax; Total Expenditure counts completed outgoing payments plus petty-cash CashOut. An approved expense can therefore appear in Total Expenses before cash expenditure is recorded. Reports explain this distinction without changing the formulas.

## Verification

- Full .NET/PostgreSQL suite: 2,053 passing, including 11 cost-code UI regressions for readers, authorized managers and rejected changes.
- Real PostgreSQL: seeded role, budget approval, invoice issue, payment complete/reverse with persisted invoice/balance changes, expense approval, petty cash, assignment revocation, and denied cross-site payment/cash mutations without side effects.
- Real Blazor/local fixture browser: Create User live FinanceOfficer option with Users_Create and no Users_View; read-only cost-code controls, assigned-site navigation/forms, budget submit/approve/activate, payment save/complete/reverse, expense submit/approve/paid at 320/390/1280; no eager HR/procurement/project API requests or outer overflow.
- EF model: no changes since the existing migration. No schema migration or migration-flag change is required.

Browser fixtures create no production accounts or business transactions. Production account creation and authenticated persistence still require the user's manual confirmation after release.
