# Independent finance approval and evidence-backed expense settlement

## Intent and user decisions

The user requested two priorities: prevent people approving their own finance records, then link Expense Paid to actual disbursements. They explicitly selected support for both Completed Payments and Petty Cash Cash Out, rather than Payments only. This applies to Budget/Expense approval and expense settlement, retaining FinanceOfficer assigned-site scope and existing Accountant corporate authority. It is not a new payroll, invoice-reconciliation or multi-currency treasury project.

Current production main is 4b1e80f76b22283a65ee5fabec1d831fe3c7ea0c (PR133). Local parent4f51a8e has the identical b7589a92 tree. Existing report definitions remain: recorded cash receipts minus actual expenditure; budget minus recorded expenses; opening petty cash balances stay separate.

## Current implementation and concrete gaps

- BudgetService and ExpenseService stamp ApprovedBy but do not stamp CreatedBy on creation or UpdatedBy on material edits. The DbContext preserves these fields but does not infer an actor.
- Budget/Expense approval currently allows the creator to approve. BudgetLineService permits edits even after approval/activation, which could bypass a meaningful approval.
- Expense.ChangeStatus(Paid) changes only a status; it creates no Payment or cash movement.
- Payment supports invoice/vendor/payroll/direct sources, but no ExpenseId. Payment has both generic status and dedicated complete/reverse paths.
- PettyCashTransaction supports immutable posted CashIn/CashOut rows and immediately updates account balance; it has no ExpenseId or reversal endpoint. Accounts have no currency property.
- Existing role, live-grant, company, site and parent-reference checks must remain in force.

## Approaches considered

1. Completed Payment evidence only: simpler linkage, but petty cash expenses would still have no supported settlement evidence.
2. Completed Payment plus posted Petty Cash Cash Out: one expense can be settled in parts using either source; stronger reconciliation without counting one source twice. The user selected this approach.

Builder choices below implement that selection. No raw reference-string matching, guessed creator IDs, automatic historical cash movements or silent legacy reassignment.

## Approval rules

1. New budgets and expenses get CreatedBy=current authenticated database actor and UpdatedBy=current actor on creation. Real header edits stamp UpdatedBy; status changes do not overwrite the identity of the last material editor.
2. Budget line create/update/delete also stamps its parent budget's UpdatedBy. Budget lines can change only in Draft or PendingApproval, never Approved, Active, Closed or Cancelled.
3. Approval requires an active actor with the existing live approval/action grant, correct company/site scope, a known creator, and an actor different from both CreatedBy and the last material editor UpdatedBy. No role, including SuperAdmin, bypasses the same-person restriction. A different FinanceOfficer with the grant and the same assigned site can approve; CompanyAdmin/Accountant can approve within their existing authority.
4. Existing pending records with unknown creator cannot receive a new approval under these controls. Explain the missing provenance and require a newly authored record rather than guessing ownership. Existing approved/active/paid records are not retroactively changed or silently invalidated.
5. Approval and edits use a shared row lock/version check so an edit cannot race past approval. Preserve approved actor/time. No changes to the meaning or IDs of existing permission enums.
6. UI hides/disables self-approval with an explanatory reason; backend independently enforces the rule even through direct HTTP calls. Approved records are not editable through budget lines or header shortcuts.

## Expense settlement rules

### Evidence and amounts

- Add optional ExpenseId references to Payment and PettyCashTransaction, with restrictive foreign keys and query indexes. One disbursement references at most one expense.
- An expense-linked Payment is outgoing, matches expense company/site/project/currency/exchange rate, and cannot also reference InvoiceId or PayrollRecordId. Vendor must match the expense when present. Existing non-expense payment modes retain their rules.
- An expense-linked petty transaction must be CashOut, use an active account in the same company and exact site (including company-level/null matching), and match the expense cost code. A linked CashIn is invalid.
- Because current petty accounts have no currency, linked cash-out settlement is supported for ETB expenses at exchange rate1 only. Non-ETB expenses use same-currency Payments. Existing unlinked petty transactions are unchanged; adding currency-aware accounts is a separate project.
- The payable expense amount is Amount+TaxAmount in its currency. Use decimal precision19,4, reject linked amounts with more than four decimal places, require positive amounts, and never settle beyond the remaining payable amount.
- SettledAmount = non-deleted Completed outgoing expense-linked Payments + non-deleted approved posted expense-linked CashOut amounts. Recorded/Pending, Failed, Cancelled and Reversed Payments do not count.
- Multiple partial settlements and mixed Payment/CashOut are allowed. A partial expense stays Approved with a visible partial-settlement indicator; no new ExpenseStatus enum member is required. Paid applies only when SettledAmount equals the full payable amount. OutstandingAmount is the exact unpaid remainder.

### State changes

- Settle only expenses already Approved by an eligible other person, or historically approved legacy expenses that have not yet been marked Paid. Draft/Submitted/Rejected/Cancelled cannot be paid.
- Replace the status-only Mark as Paid button with Record Payment and Pay from Petty Cash. Creating a Payment records Pending evidence; completing it changes cash totals and synchronizes the expense. A posted CashOut immediately synchronizes it.
- Guard the existing Expense.ChangeStatus(Paid) endpoint: it must verify full linked completed evidence and cannot manufacture evidence or move cash. It may act as an idempotent synchronization operation, never as an override.
- Both generic Payment.ChangeStatus and dedicated Complete/Reverse paths invoke the same settlement synchronization. Reversing one payment excludes it from the sum and returns a previously Paid expense to Approved if a positive remainder reopens. Original expense approval identity/time remains unchanged.
- Completed linked payments are immutable and cannot be deleted; reverse them first. Pending linked payments can be edited/deleted under normal checks, with old/new expense references validated. Prevent switching a completed record's expense, amount or site.
- Expenses with settlements cannot be edited to change amounts, tax, currency, site, project or cost code, or cancelled underneath posted evidence. Do not create a new petty-cash reversal feature: existing posted cash transactions remain immutable.

### Permissions and information boundaries

- Payment actions retain Payment_View/Manage and additionally require current Expense_View for linking. Scope comes from the action's effective actor; a corporate read grant cannot widen a site-only write.
- Linked CashOut retains PettyCash_View/Manage and additionally requires current Expense_View. The account and expense must each independently pass their own scope checks.
- ApprovedBy is a prerequisite, not an implicit permission to post payments. The creator may pay an expense only after someone else has approved it and the creator has the required disbursement grant.
- Minimal expense options and settlement history expose only authorized same-scope IDs/numbers, status, amounts and currency. No raw Users/HR/payroll/procurement lookup grants are added. Links to full payment pages still require the normal payment view permission.

## Atomicity, concurrency and duplicate requests

- For PostgreSQL use transactions and explicit ordered row locks. Global order: expense IDs in deterministic order, then existing payment row if applicable, then petty account. Read IDs before locking, then re-read and validate after locking; reject/retry if a pending payment changed parent concurrently.
- Completing/reversing a linked Payment and changing Expense.Status commit together. Posting linked CashOut, deducting account.CurrentBalance and updating Expense.Status commit together. Failures roll back all monetary/status changes.
- Lock the expense before checking the aggregate remaining amount so simultaneous payments/cash outs cannot overpay it. Lock the account before reading/modifying CurrentBalance for all account transaction paths so a linked and unlinked cash-out cannot race into an overdraft.
- Require a nonempty reference on linked petty cash disbursements. Add a filtered unique index on (PettyCashAccountId, ReferenceNumber) for ExpenseId IS NOT NULL; it does not reject historical unlinked duplicates. Repeated requests reject an already posted linked reference without another debit. Existing company/payment-number uniqueness protects Payment retries.
- Pending Payments do not reserve cash or expense balance. Recheck remaining amount at completion. Overpayment errors preserve Pending status and all balances.

## Historical records

- Additive nullable links do not infer associations from shared vendor, reference, amount or date.
- Historical Paid expenses without linked evidence keep their recorded status and are labelled Legacy Paid / no linked settlement evidence. Do not show a verified settled amount or offer a fresh disbursement against them, avoiding accidental duplicate payment. A separate user-authorized reconciliation can link historical evidence later.
- Historical Approved expenses can receive explicit new settlement evidence under existing approval provenance, without relabelling who created/approved them. Unknown-creator pending records fail closed for new approvals.
- Existing manual TEST records remain unchanged. New testing uses fresh records and two distinct users; do not reapprove or pay the previous TEST-FO-EXP-001 Paid50 automatically.

## UI and contracts

- Budget and expense details expose server-calculated CanApprove plus a reason; creator/editor metadata is server-owned and never accepted from request fields.
- Expense response adds settlement verification/state, settled and outstanding amounts and a minimal list of evidence. Keep existing constructor/wire fields compatible where possible.
- Expense details show approval provenance, verified settlement totals/history and Record Payment / Pay from Petty Cash actions when appropriate. Approved partial state shows its unpaid remainder.
- Payment Create/Edit/Details supports Expense as a distinct source, preserves expense/site context and hides incompatible invoice/payroll sources. Success returns to scoped details.
- Petty transaction create accepts a preselected authorized ExpenseId, CashOut, cost code and remaining amount; preserve the user's entered partial amount, show account balance and expense remainder, and prevent duplicate submission. From a petty account, offer only approved eligible expenses in that account's scope.
- Errors remain local, preserve input and allow safe retry. Page/record switches discard stale context. Do not expose implementation or migration terms in product UI.

## Schema and release

- One additive EF PostgreSQL migration adds nullable Payment.ExpenseId and PettyCashTransaction.ExpenseId, restrictive relationships/indexes and the filtered linked-reference unique index. No destructive down migration or business-data backfill.
- Validate fresh database and upgrade from current model with sample legacy rows. Verify EF model has no pending changes after the migration.
- Release only after tests, review and exact-head CI pass. Apply reviewed migration through the normal application EF startup mechanism in a controlled API deploy, then restore the existing migration flag to false; no ad-hoc production SQL or account/business mutations.
- API with additive schema and old request compatibility goes live before Web. Old clients' bare Mark as Paid attempts fail with an actionable error. Verify public boundaries; user performs authenticated production test with new records and a different approver.

## Verification

- RED-to-GREEN tests for creator/editor self-approval, different approver, unknown creator, revoked/inactive/wrong-site actors, header and budget-line approval bypasses, and approval/edit races.
- Settlement lifecycle: pending -> complete -> reverse; full/partial/mixed Payment+CashOut; correct amount+tax; paid status cannot be forged; overpayment and insufficient cash rejected without side effects.
- Cross-company/site, wrong currency/rate/cost code/vendor, CashIn, invoice/payroll+expense conflicts, deleted/invalid parents, paid-legacy protection, and repeated requests.
- Real PostgreSQL concurrent completion/CashOut, concurrent account withdrawals, atomic rollback, restrictive relations, index retries and migration upgrades. No InMemory-only substitute for transactional proof.
- UI tests and real Blazor/local API browser at mobile/desktop widths with two users, denied self-approval, preserved fields on errors, partial/full settlement and reversal, assigned-site boundaries, existing Accountant/invoice/payroll flows and legacy display.
- Full project suite, Release builds and existing required workflows. Document behavior and any material remaining limits.

## Builder-owned details

Exact helper/service organization, DTO backward-compatible extension style, UI arrangement and database-lock abstraction are implementation choices. Keep helpers explicit, minimize new permissions/dependencies, and cover both existing payment action paths. Any departure from the financial or legacy rules above must be brought back to the user.
