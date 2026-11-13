# Finance Approval and Expense Settlement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce independent budget/expense approval and make Expense Paid depend on linked completed Payments or posted CashOut.

**Architecture:** Preserve existing grants/site resolvers and add a shared approval policy, PostgreSQL mutation lock/transaction helper and expense settlement service. Extend nullable evidence links and existing DTOs compatibly. Integrate every payment lifecycle path and petty posting, then expose scoped options and honest UI states.

**Tech Stack:** Existing .NET10/EF Core10/PostgreSQL16/Blazor Server/bunit/Playwright; no new runtime dependency.

**Spec:** `docs/superpowers/specs/2026-11-13-finance-approval-settlement-design.md` (approved user: “Gorsa kee yoo tahe itti fufi”, 2026-11-13).

## Global Constraints

- Prevent creator/last-material-editor self approval for all roles; unknown-creator new approval fails closed. Preserve legacy approved/paid records and existing role/grant/site boundaries.
- Support both Completed outgoing Payments and approved CashOut; partial/mixed settlement is valid, excess and precision beyond19,4 are invalid. Expense target=Amount+TaxAmount.
- ETB/rate1 only for linked petty CashOut; Payments must match expense currency/rate. No new petty reversal, payroll features, automatic legacy linking or business-data rewrite.
- A legacy Paid expense without evidence stays historically Paid/unverified and cannot receive fresh disbursement. A legacy Approved expense can receive explicit settlement.
- Atomic PostgreSQL mutation order: budget separately; expense IDs sorted, then payment, then petty account. Every petty posting, including unlinked posting, locks its account.
- Additive EF migration, restrictive Expense FKs, filtered linked-reference uniqueness; API before Web and restore migration startup flagfalse. No production account/password/transaction mutations by executor.
- Preserve original five staged user files in `/workspace/csm-web-app`; work only in isolated `/workspace/finance-officer-site-fix`, branch`feature/finance-approval-settlement`.
- Native execution selected under user's delegated “your recommendation, continue”; no further execution menu. Final fresh whole-branch review per executing-plans.

## Review Focus

1. A status-only or alternate complete/reverse API must not bypass settlement; exercise both Payment action families (Task3).
2. Concurrent partial payment, cash-out and account withdrawals must not overpay/overdraw or leave a changed status on failure (Tasks3–4 real PostgreSQL).
3. A corporate read role combined with site-only disbursement authority must not widen writes or option lookups (Tasks2–5).
4. Approval racing with a header/line edit, or edits after approval, must not allow authors to certify their own content (Task1).
5. Legacy unknown authors/Paid rows, stale page context and repeated partial CashOut requests must preserve history and avoid duplicate disbursement (Tasks1,4–5).

## File map / contracts

- New `CSM.Infrastructure/Services/FinanceMutationTransaction.cs`: owns transactional row-lock acquisition/commit; `ExecuteAsync<T>(ApplicationDbContext db, FinanceMutationTargets targets, Func<Task<T>> action, CancellationToken ct)` wraps the whole operation in the provider execution strategy and owned transaction; targets contain optional BudgetId/ExpenseIds/PaymentId/AccountId. Nonrelational test execution is explicit; transactional guarantees require PostgreSQL tests.
- New `CSM.Infrastructure/Services/FinanceApprovalPolicy.cs`: provenance checks and current-action approval capability. Services stamp existing CreatedBy/UpdatedBy; no owner request fields.
- New `CSM.Application/Finance/Expenses/IExpenseSettlementService.cs` and `Dtos/ExpenseSettlement*.cs`; new `CSM.Infrastructure/Services/ExpenseSettlementService.cs`, DI scoped registration. Public `GetOptionsAsync(Guid userId,string purpose,Guid? siteId,Guid? accountId,CancellationToken ct)` and `GetForExpenseAsync(Guid userId,Guid expenseId,string purpose,CancellationToken ct)` supply minimal options. Internal `LoadForDisbursementAsync(Guid userId,Guid expenseId,string purpose,CancellationToken ct)`, `GetSummaryAsync(Expense expense,ct)`, `ValidateNewSettlementAsync(Expense expense,decimal amount,string currency,decimal rate,bool petty,ct)` and `SynchronizeAsync(Expense expense,ct)` centralize rules; Synchronize changes tracked state but caller saves/commits.
- `ExpenseSettlementSummaryResponse`: TotalDue, SettledAmount, OutstandingAmount, IsVerified, IsLegacyPaid, State and minimal Evidence. Evidence carries source/id/number/status/amount/currency only. `ExpenseSettlementOption`: authorized expense IDs/numbers/context/currency/rate/outstanding/payee display. `ExpenseSettlementOptionsResponse` adds eligible same-scope petty accounts when purpose=petty-cash.
- Existing Domain `Payment`/`PettyCashTransaction`: nullable ExpenseId/Expense navigation. EF configs/indexes + one generated migration/snapshot. Existing positional Payment request/response records gain optional init-body properties (avoid breaking existing constructors).
- Existing Budget/Expense response records gain `CanApprove`, `ApprovalBlockReason`; detail responses calculate current grant/action scope and provenance. Expense response additionally gains Settlement. Existing Web models mirror additive properties.
- Existing services: BudgetService, BudgetLineService, ExpenseService, PaymentService, PettyCashTransactionService; existing HTTP controllers gain minimal options endpoints, retaining required live policies. Existing finance Web service/API models and BudgetDetails/SiteExpenseDetails/PaymentCreate/Edit/Details/PettyCashTransactionCreate are consumers.

## Task1: Independent approval and provenance

**Files:** new approval/transaction helpers; modify BudgetService, BudgetLineService, ExpenseService, Budget/Expense DTO detail capability mapping; test`FinanceIndependentApprovalTests.cs` and PostgreSQL race cases.
**Interfaces:** consumes live actor resolvers/current permissions; produces guard/capability and retry-aware mutation contract above for Tasks2–4.

- [x] Write actual-service RED tests: new CreatedBy/UpdatedBy==maker; maker and latest editor cannot approve even as SuperAdmin; distinct eligible same-site checker succeeds; unknown creator fails; revoked/inactive/wrong-site checker fails. Header/line edits update provenance; Approved/Active budget lines cannot change.
- [x] Run filtered tests with explicit PostgreSQL env; verify feature failures, not unavailable database/compile errors.
- [x] Implement guards and audit stamps. Acquire parent row locks before re-reading/editing/approving. Keep material UpdatedBy through status changes. Detail capability reads must not combine corporate view with unrelated site-only management.
- [x] Add real PostgreSQL approval/edit race assertion: result is a valid serial order, no stale self-approval or postapproval edit succeeds.
- [x] Run related approval/service scope tests GREEN and commit.

## Task2: Evidence schema, shared settlement rules and options

**Files:** new settlement interface/DTO/service, Domain Payment/PettyCashTransaction, EF configs/migration/snapshot, DI and scoped options controller routes; test`ExpenseSettlementRulesTests.cs`,`ExpenseSettlementMigrationTests.cs`.
**Interfaces:** consumes Task1 locking/effective actor; produces DTOs and public/internal methods in file map.

- [x] RED actual-service tests: approved target50+tax10, no evidence outstanding60; only completed outgoing matching evidence counts; partial/mixed sums correct; legacy Paid remains unverified/blocked; bad currency/rate/context/role rejected; absent permission options fail closed.
- [x] Implement nullable ExpenseId links, decimal19,4 checks, aggregate summaries, minimal current-purpose options and approval eligibility. GET options must not create evidence or cash movement.
- [x] Add filtered unique `(PettyCashAccountId,ReferenceNumber)` index for ExpenseId IS NOT NULL and restrictive FKs; generate additive EF migration, no data backfill.
- [x] Verify current-schema-to-new-schema upgrade with legacy Paid/duplicate unlinked references unchanged, fresh schema and EF no-pending-model; GREEN and commit.

## Task3: Payment-backed settlement on every action path

**Files:** PaymentService, Create/Update/Response DTO optionalExpenseId, API payment expense-options endpoint; ExpenseService Paid guard; tests`ExpensePaymentSettlementTests.cs` and real PostgreSQL lifecycle/concurrency.
**Interfaces:** consumes settlement methods; produces existing Payment APIs supporting Expense source without changing invoice/payroll/direct contracts.

- [x] RED create Pending expense payment leaves expense Approved; generic status Completed and dedicated Complete both settle; full60 ->Paid; partial20 ->Approved/outstanding40; reverse reopens unpaid amount preserving approver. Bare Expense Paid fails without evidence.
- [x] Implement exclusive Expense vs Invoice/Payroll source, outgoing/site/project/vendor/currency inheritance and validation. Do not require a vendor for a valid expense with no vendor. Require Expense_View plus current Payment action grant.
- [x] Under ordered transaction/locks recheck parents/remaining at completion, synchronize expense and commit atomically. Both reverse routes use the same rule. Pending old/new parent changes validated; completed/legacy Paid conflicts denied.
- [x] Real PostgreSQL tests: simultaneous full/partial completion cannot overpay; injected synchronization failure rolls back payment/status; duplicate number no second evidence. Missing scope leaves rows untouched; all preexisting invoice/payroll payment tests remain valid.
- [x] GREEN related suite, update historical tests that assumed self-approval only by creating a real distinct checker (never weaken assertions), commit.

## Task4: Petty-cash-backed settlement and account safety

**Files:** PettyCashTransactionService/interface/request/response, EF petty reference config fromTask2, account expense-options endpoint; tests`ExpensePettySettlementTests.cs` and PostgreSQL cross-source/account races.
**Interfaces:** consumes settlement methods/locks, produces optionalExpenseId CashOut and minimal same-account expense options.

- [x] RED ETB approved60 CashOut20 ->accountbalance80/expenseoutstanding40; Payment40 completion ->Paid; reversed Payment40 ->Approved/outstanding40. Linked CashIn, unknown type, wrong cost/site/currency, inactive account and overdraft fail without side effects.
- [x] Implement canonical strict CashIn/CashOut validation and account row locks for every posting. Linked CashOut requires Expense_View, authorized approved expense, matching account/site/cost and positive <=remaining amount. New writes stamp actor and preserve entered partial amount.
- [x] In one transaction save CashOut/account then expense synchronization. Require unique linked reference and turn duplicate request into an actionable local/domain error without debit. Keep posted rows immutable; no new reversal endpoint.
- [x] Real PostgreSQL tests: mixed Payment/CashOut concurrency, linked/unlinked withdrawals competing for the same account, duplicate partial reference, invalid reference precision, and rollback. GREEN and commit.

## Task5: Honest workflow UI, regression verification and release

**Files:** Web finance models/service/interfaces; BudgetDetails(site/company), SiteExpenseDetails, PaymentCreate/Edit/Details, PettyCashTransactionCreate/Transactions; new UI tests and local browser QA; behavior docs.
**Interfaces:** consumes server capabilities/Settlement/options, optionalExpenseId existing create/update payloads.

- [x] RED bunit: self-approval unavailable with reason but other checker enabled; no status-only Mark as Paid; Approved expense actions preselect valid Expense source/account context; partial/legacy display is truthful; submit errors preserve fields; route/record switches discard stale targets.
- [x] Implement minimal actions and evidence history. Do not call broad Users/HR/payroll/vendor/project endpoints. Add Expense source to Payment options; account cash-out form offers only its authorized approved expense options. Keep ordinary invoice/payroll/unlinked forms working.
- [x] Extend real Blazor/local API browser with maker/checker identities at320/390/1280: submit, independent approve, partial/full Payment+CashOut, reversal, duplicate denial, field-preserving failure and correct report values. Assert no circuit errors/outer overflow.
- [ ] Run full .NET/PostgreSQL suite, Release Web/API builds, migration/model check and all required exact-head CI. Update behavior docs with policy/legacy/ETB constraints and commit.
- [x] Final fresh whole-branch reviewer, one fix pass per execution skill; resolve all Critical/Important findings and record rulings/minors.
- [ ] Publish reviewable PR, merge tested head after green checks; controlled additive migration via normal EF startup/API deploy, restore migration flagfalse, then Web deploy. Verify exact live commits, startup/public boundaries. User performs new two-user production test; executor creates no production business/account records.

## Commands / receipts

Use `DOTNET_ROOT=/workspace/settings-dotnet DOTNET_CLI_HOME=/workspace/settings-cli NUGET_PACKAGES=/workspace/settings-nuget`, PATH includes SDK, `CSM_TEST_POSTGRES='Host=localhost;Port=55433;Username=postgres;Password=settings-test-only'`. Isolated PostgreSQL container`finance-officer-test-postgres` only. Test with `/workspace/settings-dotnet/dotnet test CSM.Infrastructure.Tests/CSM.Infrastructure.Tests.csproj --no-restore --filter ...` then full suite. Network permission is needed for test sockets. Browser uses NODE_PATH`/workspace/settings-port-browser/node_modules`, CSM_CHROMIUM`/usr/bin/chromium`; fixtures never use production credentials. Preserve original CRLF and run `git -c core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol diff --check`.

## Self-review

Spec sections map to Tasks1(approval/audit),2(schema/evidence/legacy/options),3(payment lifecycle),4(petty/idempotency/concurrency),5(UI/verification/release). Each Review Focus line has owner tests above. Task interfaces match file map. No new permission enum or petty reversal; the approved two-source/partial and ETB/legacy rules are explicit. API before Web and migration-flag restoration are release invariants, not optional follow-ups.
