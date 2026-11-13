# Finance approval and expense settlement

A budget or expense must be approved by another authorized person. The creator and latest material editor cannot approve it, including administrators. Budget line edits update their parent budget's last editor. Saving identical normalized business fields preserves the actual last material editor; a no-op save cannot make that editor eligible to approve their own work. Approved/active/closed budget lines and approved/paid expenses cannot be materially edited. New approval requests with unknown creators fail closed; existing historical approval records remain unchanged.

Expense settlement is based on linked cash movement, not a status-only button:

- An expense's payable amount is **Amount + Tax Amount**.
- A **Completed outgoing Payment** or an approved posted **Petty Cash Cash Out** can settle it. Both sources can be combined, including partial amounts.
- Partial settlement leaves the expense **Approved** and displays its outstanding amount. Full settlement makes it **Paid**.
- A pending payment reserves no amount. Completion rechecks the remaining amount. Failed, cancelled and reversed payments do not count.
- Reversing a completed Payment removes its settlement amount and reopens the remaining expense amount without replacing the original approver.
- Completed expense payments must be reversed before further action; posted petty cash transactions are immutable. No new petty cash reversal action is introduced.

Expense payments match the expense's company, site, project, vendor, currency and exchange rate. They cannot also reference an invoice or payroll record. An expense without a vendor can still be paid. Changing a pending payment's source first validates write authority over the existing payment and its old site, project, invoice and payroll references, then independently checks the new expense. Linked Cash Out requires an active account in the same company and exact site, the expense's cost code, **ETB at exchange rate 1**, and a nonempty reference of at most 100 characters. Posted historical Cash Out still counts if its account later becomes inactive. Amounts must be positive, fit decimal(19,4), and not exceed the remaining payable amount. Replaying a linked reference on the same account is rejected without a second debit.

Expense read permission and the applicable source read/manage permissions are required. Each action's live company/site scope is intersected, so a corporate reader combined with an assigned-site writer cannot widen disbursement access. The option endpoints return minimal expense/account information and never create financial records.

A historical Paid expense without sufficient linked evidence remains **Historical Paid — unverified**. Its outstanding amount is unknown, and the app offers no fresh disbursement against it. The migration creates no guessed links or historical cash movements. Reconciliation of old Paid records requires a separate explicit review. Historical Approved obligations with a recorded approver remain payable, including approvals allowed by the older policy; settlement never replaces the old creator, editor or approver. New approval transitions enforce the independent-checker rule.

Cash Balance and Budget Remaining remain separate: cash balance measures actual cash in minus actual expenditure; budget remaining measures budget minus recorded expenses. An expense and its settlement must not be added together as two cash expenditures.

## Release and verification

Migration `20261113014528_LinkExpenseSettlementEvidence` adds nullable ExpenseId links, restrictive foreign keys and filtered linked-reference uniqueness; unlinked duplicate references and historical records are preserved. Deploy the API with the existing controlled EF startup migration process before deploying the Web app, then restore the startup migration flag to false. Schema downgrade is deliberately rejected: application rollback must retain the additive schema and its settlement links. Do not apply manual production SQL or backfill guessed evidence.

Verification covers independent approval, mixed-role/site boundaries, partial/mixed/full settlement, both payment completion/reversal APIs, PostgreSQL overpayment/overdraft races, duplicate requests and transaction rollback. Browser QA uses local fixture identities and checks maker/checker actions, field preservation, partial Cash Out plus Payment, reversal and mobile/desktop layout. Production verification by the executor is limited to startup and public authentication boundaries; users perform the authenticated two-person workflow with their own accounts.
