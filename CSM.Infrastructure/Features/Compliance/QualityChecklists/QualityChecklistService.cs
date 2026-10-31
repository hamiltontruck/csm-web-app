using CSM.Infrastructure.Features.Compliance;
using System.Linq.Expressions;
using CSM.Application.Features.Compliance.QualityChecklists;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Features.Compliance.QualityChecklists;

public class QualityChecklistService
{
    private readonly ApplicationDbContext _db;

    public QualityChecklistService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<QualityChecklistResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var companyId = await ComplianceRecordAccess.CompanyAsync(_db, currentUserId, false, ct);

        return await _db.QualityChecklists.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted && x.ConstructionSite.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .Select(Map())
            .ToListAsync(ct);
    }

    public async Task<QualityChecklistResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken ct = default)
    {
        var companyId = await ComplianceRecordAccess.CompanyAsync(_db, currentUserId, false, ct);

        return await _db.QualityChecklists.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == id && !x.IsDeleted && x.ConstructionSite.CompanyId == companyId)
            .Select(Map())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<QualityChecklistResponse> CreateAsync(
        Guid userId,
        CreateQualityChecklistRequest request,
        CancellationToken ct = default)
    {
        var companyId = await ComplianceRecordAccess.CompanyAsync(_db, userId, true, ct);
        await ComplianceRecordAccess.ParentsAsync(_db, companyId, request.ConstructionSiteId, null, null, ct);

        var entity = new QualityChecklist
        {

            CompanyId = companyId,

            ConstructionSiteId = request.ConstructionSiteId,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Description = request.Description?.Trim(),
            IsActive = true
        };

        foreach (var item in request.Items)
        {
            entity.Items.Add(new QualityChecklistItem
            {
                Sequence = item.Sequence,
                Requirement = item.Requirement.Trim(),
                IsMandatory = item.IsMandatory
            });
        }

        _db.QualityChecklists.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(userId, entity.Id, ct))!;
    }

    public async Task<QualityChecklistResponse> UpdateAsync(
        Guid currentUserId,
        Guid id,
        UpdateQualityChecklistRequest request,
        CancellationToken ct = default)
    {
        var companyId = await ComplianceRecordAccess.CompanyAsync(_db, currentUserId, true, ct);

        var entity = await _db.QualityChecklists.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.Id == id && !x.IsDeleted,
                ct)
            ?? throw new KeyNotFoundException("Checklist not found.");

        await ComplianceRecordAccess.ParentsAsync(_db, companyId, request.ConstructionSiteId, null, null, ct);

        entity.ConstructionSiteId = request.ConstructionSiteId;
        entity.Name = request.Name.Trim();
        entity.Category = request.Category.Trim();
        entity.Description = request.Description?.Trim();
        entity.IsActive = request.IsActive;

        // Save parent first
        await _db.SaveChangesAsync(ct);

        // Soft delete old items
        await _db.QualityChecklistItems
            .Where(x => x.QualityChecklistId == id && !x.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.DeletedAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow),
                ct);

        // Insert new items
        var newItems = request.Items
            .OrderBy(x => x.Sequence)
            .Select(x => new QualityChecklistItem
            {
                QualityChecklistId = id,
                Sequence = x.Sequence,
                Requirement = x.Requirement.Trim(),
                IsMandatory = x.IsMandatory
            });

        await _db.QualityChecklistItems.AddRangeAsync(newItems, ct);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(currentUserId, id, ct))!;
    }

    private static Expression<Func<QualityChecklist, QualityChecklistResponse>> Map()
    {
        return x => new QualityChecklistResponse
        {
            Id = x.Id,
            ConstructionSiteId = x.ConstructionSiteId,
            ConstructionSiteName = x.ConstructionSite.Name,
            CompanyId = x.CompanyId,
            Name = x.Name,
            Category = x.Category,
            Description = x.Description,
            IsActive = x.IsActive,
Items = x.Items
    .Where(i => !i.IsDeleted)
    .OrderBy(i => i.Sequence)
    .Select(i => new QualityChecklistItemDto
    {
        Id = i.Id,
        Sequence = i.Sequence,
        Requirement = i.Requirement,
        IsMandatory = i.IsMandatory
    })
    .ToList()
        };
    }
}