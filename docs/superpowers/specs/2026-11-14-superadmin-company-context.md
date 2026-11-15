# Companyless SuperAdmin action context

The user asks to fix `{"message":"Current user is not assigned to a company."}` for SuperAdmin, without identifying a page after clarification. Audit the remaining reproducible legitimate actions on current main. Already-fixed Equipment, Employee, Maintenance, Assignment, Hour Meter and Daily Usage flows remain intact.

- An active SuperAdmin with the required live action grants may act globally without assigning a company to the account. Timesheet employee options retain their existing role-only authorization contract, with live user/membership/role checks.
- Derive ownership from the selected authorized employee/site/project/invoice/payroll/resource; validate all related records against that one company.
- For unanchored company-owned records and company-only lists, expose an explicit, validated company selection. Preserve assigned users' omitted-selection behavior and existing own-company list contracts.
- Retain normal users' company/site restrictions, action grants, existing role eligibility and independent-role authority. A raw/ungranted SuperAdmin role does not expand another role's scope.
- Never use an arbitrary first company, Guid.Empty/global fallback, mutate actor.CompanyId, broaden role policies, or touch production business records.
- Existing controller policies that exclude pure SuperAdmin (for example Contractors HRManagement) remain unchanged; only already-eligible mixed-role flows are repaired.
- No migrations are expected. Verify real PostgreSQL ownership/boundaries, source-aware Web forms at 390/1280px, full suite and production assets. Publish a focused reviewed PR; release API before Web and verify exact commits/live health. Preserve original staged Vercel files.
