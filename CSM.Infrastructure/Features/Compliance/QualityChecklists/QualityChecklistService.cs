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
        Guid companyId,
        CancellationToken ct = default)
    {
        return await _db.QualityChecklists
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .Select(Map())
            .ToListAsync(ct);
    }

    public async Task<QualityChecklistResponse?> GetByIdAsync(
        Guid companyId,
        Guid id,
        CancellationToken ct = default)
    {
        return await _db.QualityChecklists
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == id)
            .Select(Map())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<QualityChecklistResponse> CreateAsync(
        Guid userId,
        CreateQualityChecklistRequest request,
        CancellationToken ct = default)
    {
        var user = await GetUser(userId, ct);

        var entity = new QualityChecklist
        {

            CompanyId = user.CompanyId
                ?? throw new InvalidOperationException("User has no company."),

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

        return (await GetByIdAsync(entity.CompanyId, entity.Id, ct))!;
    }

    public async Task<QualityChecklistResponse> UpdateAsync(
        Guid companyId,
        Guid id,
        UpdateQualityChecklistRequest request,
        CancellationToken ct = default)
    {
        // Load ONLY the checklist (no Include)
        var entity = await _db.QualityChecklists
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.Id == id,
                ct)
            ?? throw new KeyNotFoundException("Checklist not found.");

        var siteExists = await _db.ConstructionSites.AnyAsync(
            s => s.CompanyId == companyId &&
                 s.Id == request.ConstructionSiteId,
            ct);

        if (!siteExists)
            throw new InvalidOperationException(
                $"Construction site not found: {request.ConstructionSiteId}");

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

        return (await GetByIdAsync(companyId, id, ct))!;
    }

    private async Task<User> GetUser(Guid id, CancellationToken ct)
    {
        return await _db.Users.FirstAsync(x => x.Id == id, ct);
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