using CSM.Domain.Constants;
using CSM.Infrastructure.Security;
using CSM.Application.Projects.Equipment;
using CSM.Application.Projects.Equipment.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class ProjectEquipmentService
    : IProjectEquipmentService
{
    private readonly ApplicationDbContext _db;

    public ProjectEquipmentService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<EquipmentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(currentUserId, false, cancellationToken);
        return await _db.ProjectEquipment
            .OrderBy(x => x.EquipmentCode)
            .Select(x => new EquipmentResponse(
                x.Id,
                x.EquipmentCode,
                x.Name,
                x.Category,
                x.Manufacturer,
                x.Model,
                x.SerialNumber,
                x.IsRented,
                x.SupplierName,
                x.PurchaseDate,
                x.NextMaintenanceDate,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<EquipmentResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(currentUserId, false, cancellationToken);
        return await _db.ProjectEquipment
            .Where(x => x.Id == id)
            .Select(x => new EquipmentResponse(
                x.Id,
                x.EquipmentCode,
                x.Name,
                x.Category,
                x.Manufacturer,
                x.Model,
                x.SerialNumber,
                x.IsRented,
                x.SupplierName,
                x.PurchaseDate,
                x.NextMaintenanceDate,
                x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EquipmentResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(currentUserId, true, cancellationToken);
        var equipment = new Equipment
        {
            EquipmentCode = request.EquipmentCode,
            Name = request.Name,
            Category = request.Category,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            IsRented = request.IsRented,
            SupplierName = request.SupplierName,
            PurchaseDate = request.PurchaseDate,
            NextMaintenanceDate = request.NextMaintenanceDate,
            IsActive = true
        };

        _db.ProjectEquipment.Add(equipment);
        await _db.SaveChangesAsync(cancellationToken);

        return new EquipmentResponse(
            equipment.Id,
            equipment.EquipmentCode,
            equipment.Name,
            equipment.Category,
            equipment.Manufacturer,
            equipment.Model,
            equipment.SerialNumber,
            equipment.IsRented,
            equipment.SupplierName,
            equipment.PurchaseDate,
            equipment.NextMaintenanceDate,
            equipment.IsActive);
    }

    public async Task DeleteAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(currentUserId, true, cancellationToken);
        var equipment = await _db.ProjectEquipment
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (equipment is null)
            return;

        _db.ProjectEquipment.Remove(equipment);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAccessAsync(Guid currentUserId, bool write, CancellationToken cancellationToken)
    {
        var allowed = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == currentUserId && u.IsActive && !u.IsDeleted &&
            u.UserRoles.Any(r => !r.IsDeleted && !r.Role.IsDeleted && r.Role.Name == AppRoles.SuperAdmin), cancellationToken);
        var permissions = new PermissionEvaluator(_db);
        if (!allowed || !await permissions.HasPermissionAsync(currentUserId, "Projects_View", cancellationToken) ||
            (write && !await permissions.HasPermissionAsync(currentUserId, "Projects_Manage", cancellationToken)))
            throw new UnauthorizedAccessException("Access denied.");
    }
}
