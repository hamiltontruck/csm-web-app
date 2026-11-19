using CSM.Application.Common.Exceptions;
using CSM.Application.HRM.Employees.Dtos;
using CSM.Application.Procurement.Equipment.Dtos;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;

namespace CSM.Infrastructure.Tests;

/// <summary>
/// Isolated lifecycle regression for companyless SuperAdmin actions.
/// Uses the existing in-memory test fixture; never contacts Neon or Render.
/// </summary>
public sealed class SuperAdminLifecycleRegressionTests
{
    [Fact]
    public async Task Companyless_superadmin_creates_edits_and_reads_equipment_in_selected_company()
    {
        using var f = new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin", false);
        var service = new EquipmentService(f.Db);
        var created = await service.CreateAsync(f.Actor.Id, new CreateEquipmentRequest
        {
            CompanyId = f.Beta.Id,
            EquipmentCode = "LIFE-EQ",
            Name = "Original roller",
            MeterUnit = "Hours"
        });

        var updated = await service.UpdateAsync(f.Actor.Id, created.Id, new UpdateEquipmentRequest
        {
            EquipmentCode = "LIFE-EQ",
            Name = "Updated roller",
            CurrentMeterReading = 25,
            MeterUnit = "Hours",
            Status = EquipmentStatus.Available,
            IsActive = true
        });
        var read = await service.GetByIdAsync(f.Actor.Id, created.Id);

        Assert.Equal("Updated roller", updated.Name);
        Assert.Equal("Updated roller", read.Name);
        Assert.Equal(25m, read.CurrentMeterReading);
        Assert.Equal(f.Beta.Id, read.CompanyId);
        Assert.Equal(f.Beta.Id, f.Db.Equipment.Single().CompanyId);
        Assert.Null(f.Actor.CompanyId);
    }

    [Fact]
    public async Task Company_actor_cannot_read_or_edit_equipment_in_foreign_company()
    {
        using var f = new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin", false);
        var service = new EquipmentService(f.Db);
        var created = await service.CreateAsync(f.Actor.Id, new CreateEquipmentRequest
        {
            CompanyId = f.Beta.Id, EquipmentCode = "FOREIGN-EQ", Name = "Protected"
        });
        f.Actor.CompanyId = f.Alpha.Id;
        f.Actor.UserRoles.Single().Role.Name = "CompanyAdmin";
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<EquipmentManagementException>(
            () => service.GetByIdAsync(f.Actor.Id, created.Id));
        await Assert.ThrowsAsync<EquipmentManagementException>(
            () => service.UpdateAsync(f.Actor.Id, created.Id, new UpdateEquipmentRequest
            {
                EquipmentCode = "FOREIGN-EQ", Name = "Unauthorized edit",
                MeterUnit = "Hours", Status = EquipmentStatus.Available, IsActive = true
            }));
        Assert.Equal("Protected", f.Db.Equipment.Single().Name);
    }

    [Fact]
    public async Task Companyless_superadmin_employee_update_termination_and_read_preserve_ownership()
    {
        using var f = new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin", false);
        var service = new EmployeeService(f.Db);
        var created = await service.CreateAsync(f.Actor.Id,
            NewEmployee(f.Beta.Id));

        var changed = await service.UpdateAsync(f.Actor.Id, created.Id,
            new UpdateEmployeeRequest(
                "Updated", null, "Employee", null, null, null, null, null,
                new DateOnly(2026, 11, 1), "Operator", "Construction",
                EmployeeType.Permanent, EmployeeScope.Company, WageType.Monthly,
                1500m, "ETB", null, null, null, null));
        Assert.Equal("Updated", changed.FirstName);
        Assert.Equal(f.Beta.Id, changed.CompanyId);

        var terminated = await service.ChangeStatusAsync(f.Actor.Id, created.Id,
            new ChangeEmployeeStatusRequest(EmployeeStatus.Terminated, new DateOnly(2026, 11, 20)));
        var read = await service.GetByIdAsync(f.Actor.Id, created.Id);

        Assert.Equal(EmployeeStatus.Terminated, terminated.Status);
        Assert.Equal(EmployeeStatus.Terminated, read.Status);
        Assert.Equal("Updated", read.FirstName);
        Assert.Equal(f.Beta.Id, read.CompanyId);
        Assert.Null(f.Actor.CompanyId);
        await Assert.ThrowsAsync<EmployeeManagementException>(() =>
            service.UpdateAsync(f.Actor.Id, created.Id,
                new UpdateEmployeeRequest(
                    "Forbidden", null, "Employee", null, null, null, null, null,
                    new DateOnly(2026, 11, 1), "Operator", "Construction",
                    EmployeeType.Permanent, EmployeeScope.Company, WageType.Monthly,
                    1500m, "ETB", null, null, null, null)));
        Assert.Equal("Updated", f.Db.Employees.Single().FirstName);
    }

    [Fact]
    public async Task Ordinary_company_actor_cannot_read_or_edit_foreign_employee()
    {
        using var f = new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin", false);
        var service = new EmployeeService(f.Db);
        var created = await service.CreateAsync(f.Actor.Id, NewEmployee(f.Beta.Id));
        f.Actor.CompanyId = f.Alpha.Id;
        f.Actor.UserRoles.Single().Role.Name = "CompanyAdmin";
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<EmployeeManagementException>(
            () => service.GetByIdAsync(f.Actor.Id, created.Id));
        await Assert.ThrowsAsync<EmployeeManagementException>(() =>
            service.UpdateAsync(f.Actor.Id, created.Id,
                new UpdateEmployeeRequest(
                    "Forbidden", null, "Employee", null, null, null, null, null,
                    new DateOnly(2026, 11, 1), "Operator", "Construction",
                    EmployeeType.Permanent, EmployeeScope.Company, WageType.Monthly,
                    1500m, "ETB", null, null, null, null)));
        Assert.Equal("Original", f.Db.Employees.Single().FirstName);
    }

    private static CreateEmployeeRequest NewEmployee(Guid companyId) =>
        new("LIFE-EMP", "Original", null, "Employee", null, null, null,
            null, null, new DateOnly(2026, 11, 1), "Operator", "Construction",
            EmployeeType.Permanent, EmployeeScope.Company, WageType.Monthly,
            1000m, "ETB", null, null, null, null)
        { CompanyId = companyId };
}
