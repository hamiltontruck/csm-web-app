using CSM.Application.Common.Exceptions;
using CSM.Application.HRM.Payroll;
using CSM.Application.HRM.Payroll.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace CSM.Infrastructure.Services;

public sealed class PayrollService : IPayrollService
{
    private readonly ApplicationDbContext _dbContext;

    public PayrollService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PayrollResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? employeeId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var query = _dbContext.PayrollRecords
            .AsNoTracking()
            .Include(x => x.Employee)
            .Include(x => x.Adjustments)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == GetActorCompanyId(actor));
        }

        if (employeeId.HasValue)
        {
            query = query.Where(
                x => x.EmployeeId == employeeId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(
                x => x.PeriodEnd >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(
                x => x.PeriodStart <= toDate.Value);
        }

        var records = await query
            .OrderByDescending(x => x.PeriodStart)
            .ThenBy(x => x.Employee.EmployeeNumber)
            .ToListAsync(cancellationToken);

        return records
            .Select(Map)
            .ToArray();
    }

    public async Task<PayrollResponse> GetByIdAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var record = await GetPayrollAsync(
            payrollRecordId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        return Map(record);
    }

    public async Task<PayrollResponse> CreateAsync(
        Guid currentUserId,
        CreatePayrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        if (request.EmployeeId == Guid.Empty)
        {
            throw new PayrollManagementException(
                "Employee is required.");
        }

        if (request.PeriodEnd < request.PeriodStart)
        {
            throw new PayrollManagementException(
                "Payroll period end cannot be before the start date.");
        }



        var employee = await _dbContext.Employees
            .FirstOrDefaultAsync(
                x => x.Id == request.EmployeeId,
                cancellationToken)
            ?? throw new PayrollManagementException(
                "Employee was not found.");

        if (employee.WageType == WageType.Monthly)
        {
            var tempRecord = new PayrollRecord
            {
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd
            };

            EnsureFullCalendarMonth(tempRecord);
        }

       EnsureCompanyAccess(actor, employee.CompanyId);

       if (employee.Status != EmployeeStatus.Active)
       {
           throw new PayrollManagementException(
               "Payroll can only be created for an active employee.");
       }

       var duplicateExists = await _dbContext.PayrollRecords.AnyAsync(
           x =>
               x.EmployeeId == request.EmployeeId &&
               x.PeriodStart == request.PeriodStart &&
               x.PeriodEnd == request.PeriodEnd &&
               x.Status != PayrollStatus.Cancelled,
           cancellationToken);

       if (duplicateExists)
       {
           throw new PayrollManagementException(
               "A payroll record already exists for this employee and period.");
       }

        var record = new PayrollRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = employee.CompanyId,
            EmployeeId = employee.Id,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            CurrencyCode = employee.CurrencyCode,
            Status = PayrollStatus.Draft
        };

        _dbContext.PayrollRecords.Add(record);


        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetResponseAsync(
            record.Id,
            cancellationToken);
    }

    public async Task<PayrollResponse> CalculateAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CalculatePayrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetPayrollAsync(
            payrollRecordId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status is
            PayrollStatus.Approved or
            PayrollStatus.Paid or
            PayrollStatus.Cancelled)
        {
            throw new PayrollManagementException(
                "This payroll record cannot be recalculated in its current status.");
        }

        if (request.OvertimeMultiplier < 0m)
        {
            throw new PayrollManagementException(
                "Overtime multiplier cannot be negative.");
        }

        EnsureNonNegative(
            request.Allowances,
            nameof(request.Allowances));

        EnsureNonNegative(
            request.Bonuses,
            nameof(request.Bonuses));

        EnsureNonNegative(
            request.TaxDeduction,
            nameof(request.TaxDeduction));

        EnsureNonNegative(
            request.PensionDeduction,
            nameof(request.PensionDeduction));

        EnsureNonNegative(
            request.OtherDeductions,
            nameof(request.OtherDeductions));

        var timesheets = await _dbContext.Timesheets
            .AsNoTracking()
            .Where(
                x =>
                    x.EmployeeId == record.EmployeeId &&
                    x.Status == TimesheetStatus.Approved &&
                    x.PeriodStartDate >= record.PeriodStart &&
                    x.PeriodEndDate <= record.PeriodEnd)
            .ToListAsync(cancellationToken);

        if (timesheets.Count == 0)
        {
            throw new PayrollManagementException(
                "No approved timesheets were found for this payroll period.");
        }

        record.RegularHours =
            timesheets.Sum(x => x.RegularHours);

        record.OvertimeHours =
            timesheets.Sum(x => x.OvertimeHours);

        var employee = record.Employee;

        decimal hourlyRate;

        switch (employee.WageType)
        {
            case WageType.Hourly:
                hourlyRate = employee.BaseWage;

                record.BasePay =
                    employee.BaseWage *
                    record.RegularHours;

                break;

            case WageType.Daily:
                var payableDays =
                    timesheets
                        .Select(x => x.PeriodStartDate)
                        .Distinct()
                        .Count();

                record.BasePay =
                    employee.BaseWage *
                    payableDays;

                hourlyRate =
                    employee.BaseWage / 8m;

                break;

            case WageType.Monthly:
                EnsureFullCalendarMonth(record);

                record.BasePay =
                    employee.BaseWage;

                hourlyRate =
                    employee.BaseWage / 173.33m;

                break;

            default:
                throw new PayrollManagementException(
                    "Employee wage type is not supported.");
        }

        record.OvertimePay =
            hourlyRate *
            record.OvertimeHours *
            request.OvertimeMultiplier;

        record.Allowances =
            request.Allowances;

        record.Bonuses =
            request.Bonuses;

        record.TaxDeduction =
            request.TaxDeduction;

        record.PensionDeduction =
            request.PensionDeduction;

        record.OtherDeductions =
            request.OtherDeductions;

        RecalculateTotals(record);

        record.Status =
            PayrollStatus.Calculated;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    public async Task<PayrollResponse> ApproveAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        ApprovePayrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanApprove(actor);

        var record = await GetPayrollAsync(
            payrollRecordId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status != PayrollStatus.Calculated)
        {
            throw new PayrollManagementException(
                "Only calculated payroll can be approved.");
        }

        record.Status =
            PayrollStatus.Approved;

        record.ApprovedBy =
            actor.Id;

        record.ApprovedAtUtc =
            DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    public async Task<PayrollResponse> MarkPaidAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        MarkPayrollPaidRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanApprove(actor);

        var record = await GetPayrollAsync(
            payrollRecordId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status != PayrollStatus.Approved)
        {
            throw new PayrollManagementException(
                "Only approved payroll can be marked as paid.");
        }

        var paymentReference =
            Clean(request.PaymentReference);

        if (paymentReference is null)
        {
            throw new PayrollManagementException(
                "Payment reference is required.");
        }

        record.Status =
            PayrollStatus.Paid;

        record.PaidAtUtc =
            DateTime.UtcNow;

        record.PaymentReference =
            paymentReference;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    public async Task<PayrollResponse> CancelAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CancelPayrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetPayrollAsync(
            payrollRecordId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status == PayrollStatus.Paid)
        {
            throw new PayrollManagementException(
                "Paid payroll cannot be cancelled.");
        }

        if (record.Status == PayrollStatus.Cancelled)
        {
            throw new PayrollManagementException(
                "Payroll is already cancelled.");
        }

        record.Status =
            PayrollStatus.Cancelled;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    public async Task<PayrollResponse> AddAdjustmentAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CreatePayrollAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetPayrollAsync(
            payrollRecordId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status is
            PayrollStatus.Approved or
            PayrollStatus.Paid or
            PayrollStatus.Cancelled)
        {
            throw new PayrollManagementException(
                "Adjustments cannot be added to this payroll record.");
        }

        var type = Clean(request.Type);
        var description = Clean(request.Description);

        if (type is null)
        {
            throw new PayrollManagementException(
                "Adjustment type is required.");
        }

        if (description is null)
        {
            throw new PayrollManagementException(
                "Adjustment description is required.");
        }

        if (request.Amount <= 0m)
        {
            throw new PayrollManagementException(
                "Adjustment amount must be greater than zero.");
        }

        var adjustment = new PayrollAdjustment
        {
            Id = Guid.NewGuid(),
            CompanyId = record.CompanyId,
            PayrollRecordId = record.Id,
            Type = type,
            Description = description,
            Amount = request.Amount,
            IsDeduction = request.IsDeduction
        };

        _dbContext.PayrollAdjustments.Add(adjustment);


        if (record.Status == PayrollStatus.Calculated)
        {
            RecalculateTotals(record);
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    public async Task<byte[]> GeneratePayslipAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var actor = await GetActorAsync(currentUserId, cancellationToken);
        EnsureCanRead(actor);

        var record = await GetPayrollAsync(payrollRecordId, cancellationToken);
        EnsureCompanyAccess(actor, record.CompanyId);

        var employeeName = string.Join(" ",
            new[]
            {
                record.Employee.FirstName,
                record.Employee.MiddleName,
                record.Employee.LastName
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);

                page.Header().Column(column =>
                {
                    column.Item().Text("HBG CONSTRUCTION PLC")
                        .FontSize(22)
                        .Bold();

                    column.Item().Text("Employee Payslip")
                        .FontSize(14)
                        .FontColor(Colors.Grey.Darken2);

                    column.Item().PaddingTop(5).LineHorizontal(1);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Employee: {employeeName}");
                            c.Item().Text($"Employee No: {record.Employee.EmployeeNumber}");
                            c.Item().Text($"Currency: {record.CurrencyCode}");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignRight().Text($"Status: {record.Status}");
                            c.Item().AlignRight().Text(
                                $"Period: {record.PeriodStart:dd/MM/yyyy} - {record.PeriodEnd:dd/MM/yyyy}");
                        });
                    });

                    column.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    column.Item().Text("Earnings")
                        .FontSize(13)
                        .Bold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn();
                        });

                        void Row(string title, decimal value)
                        {
                            table.Cell().PaddingVertical(3).Text(title);
                            table.Cell().PaddingVertical(3).AlignRight()
                                .Text(value.ToString("N2"));
                        }

                        Row("Basic Salary", record.BasePay);
                        Row("Overtime", record.OvertimePay);
                        Row("Allowances", record.Allowances);
                        Row("Bonuses", record.Bonuses);

                        foreach (var item in record.Adjustments.Where(x => !x.IsDeduction))
                            Row(item.Description, item.Amount);

                        table.Cell().PaddingTop(6).Text("GROSS PAY").Bold();
                        table.Cell().PaddingTop(6).AlignRight()
                            .Text(record.GrossPay.ToString("N2"))
                            .Bold();
                    });

                    column.Item().PaddingTop(15);

                    column.Item().Text("Deductions")
                        .FontSize(13)
                        .Bold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn();
                        });

                        void Row(string title, decimal value)
                        {
                            table.Cell().PaddingVertical(3).Text(title);
                            table.Cell().PaddingVertical(3).AlignRight()
                                .Text(value.ToString("N2"));
                        }

                        Row("Income Tax", record.TaxDeduction);
                        Row("Pension", record.PensionDeduction);
                        Row("Other Deductions", record.OtherDeductions);

                        foreach (var item in record.Adjustments.Where(x => x.IsDeduction))
                            Row(item.Description, item.Amount);

                        table.Cell().PaddingTop(6).Text("TOTAL DEDUCTIONS").Bold();
                        table.Cell().PaddingTop(6).AlignRight()
                            .Text(record.TotalDeductions.ToString("N2"))
                            .Bold();
                    });

                    column.Item().PaddingTop(18);

                    column.Item().Background(Colors.Grey.Lighten3).Padding(12).Row(row =>
                    {
                        row.RelativeItem().Text("NET PAY")
                            .FontSize(16)
                            .Bold();

                        row.RelativeItem().AlignRight().Text(
                            $"{record.CurrencyCode} {record.NetPay:N2}")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Green.Darken2);
                    });

                    column.Item().PaddingTop(30);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Prepared By").FontSize(11);
                            c.Item().PaddingTop(20).LineHorizontal(0.5f);
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text("Finance Manager").FontSize(11);
                            c.Item().PaddingTop(20).LineHorizontal(0.5f);
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignRight().Text("Employee").FontSize(11);
                            c.Item().PaddingTop(20).LineHorizontal(0.5f);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated on ");
                    text.Span(DateTime.UtcNow.ToString("dd MMM yyyy HH:mm"));
                });
            });
        });

        return document.GeneratePdf();
    }



    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new PayrollManagementException(
                "Current user was not found.");
    }

    private async Task<PayrollRecord> GetPayrollAsync(
        Guid payrollRecordId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.PayrollRecords
            .Include(x => x.Employee)
            .Include(x => x.Adjustments)
            .FirstOrDefaultAsync(
                x => x.Id == payrollRecordId,
                cancellationToken)
            ?? throw new PayrollManagementException(
                "Payroll record was not found.");
    }

    private async Task<PayrollResponse> GetResponseAsync(
        Guid payrollRecordId,
        CancellationToken cancellationToken)
    {
        var record = await GetPayrollAsync(
            payrollRecordId,
            cancellationToken);

        return Map(record);
    }

    private static void RecalculateTotals(
        PayrollRecord record)
    {
        var additions =
            record.Adjustments
                .Where(x => !x.IsDeduction)
                .Sum(x => x.Amount);

        var adjustmentDeductions =
            record.Adjustments
                .Where(x => x.IsDeduction)
                .Sum(x => x.Amount);

        record.GrossPay =
            record.BasePay +
            record.OvertimePay +
            record.Allowances +
            record.Bonuses +
            additions;

        record.TotalDeductions =
            record.TaxDeduction +
            record.PensionDeduction +
            record.OtherDeductions +
            adjustmentDeductions;

        record.NetPay =
            record.GrossPay -
            record.TotalDeductions;
    }

    private static void EnsureFullCalendarMonth(PayrollRecord record)
    {
        var start = record.PeriodStart;
        var end = record.PeriodEnd;

        // Must start on the 1st
        if (start.Day != 1)
        {
            throw new PayrollManagementException(
                "Monthly payroll must start on the first day of the month.");
        }

        // Must end on the month's last day
        var lastDay = DateTime.DaysInMonth(start.Year, start.Month);

        if (end.Year != start.Year ||
            end.Month != start.Month ||
            end.Day != lastDay)
        {
            throw new PayrollManagementException(
                "Monthly payroll must cover a complete calendar month.");
        }
    }

    private static void EnsureNonNegative(
        decimal value,
        string fieldName)
    {
        if (value < 0m)
        {
            throw new PayrollManagementException(
                $"{fieldName} cannot be negative.");
        }
    }

    private static void EnsureCanRead(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor) ||
            HasRole(actor, AppRoles.Accountant) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new PayrollManagementException(
            "You are not authorized to access payroll.");
    }

    private static void EnsureCanManage(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new PayrollManagementException(
            "You are not authorized to manage payroll.");
    }

    private static void EnsureCanApprove(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin))
        {
            return;
        }

        throw new PayrollManagementException(
            "You are not authorized to approve payroll.");
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new PayrollManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static void EnsureCompanyAccess(
        User actor,
        Guid companyId)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != companyId)
        {
            throw new PayrollManagementException(
                "You cannot access payroll outside your company.");
        }
    }

    private static bool IsSuperAdmin(
        User actor)
    {
        return HasRole(
            actor,
            AppRoles.SuperAdmin);
    }

    private static bool HasRole(
        User actor,
        string roleName)
    {
        return actor.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static PayrollResponse Map(
        PayrollRecord record)
    {
        var employeeName = string.Join(
            " ",
            new[]
            {
                record.Employee.FirstName,
                record.Employee.MiddleName,
                record.Employee.LastName
            }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        var adjustments =
            record.Adjustments
                .OrderBy(x => x.CreatedAtUtc)
                .Select(
                    x => new PayrollAdjustmentResponse(
                        x.Id,
                        x.Type,
                        x.Description,
                        x.Amount,
                        x.IsDeduction))
                .ToArray();

        return new PayrollResponse(
            record.Id,
            record.CompanyId,
            record.EmployeeId,
            record.Employee.EmployeeNumber,
            employeeName,
            record.PeriodStart,
            record.PeriodEnd,
            record.BasePay,
            record.RegularHours,
            record.OvertimeHours,
            record.OvertimePay,
            record.Allowances,
            record.Bonuses,
            record.GrossPay,
            record.TaxDeduction,
            record.PensionDeduction,
            record.OtherDeductions,
            record.TotalDeductions,
            record.NetPay,
            record.CurrencyCode,
            record.Status,
            record.ApprovedBy,
            record.ApprovedAtUtc,
            record.PaidAtUtc,
            record.PaymentReference,
            adjustments,
            record.CreatedAtUtc);
    }

}



